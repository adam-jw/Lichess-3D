using UnityEngine;

// Owns ONE GAME: its id, which color we are, and the board stream that carries it
// Acts as the stable object the view and input layers talk to

public class LichessGameSession : MonoBehaviour
{
    [SerializeField] private LichessAuthManager _authManager;
    [SerializeField] private LichessClient _client;
    [SerializeField] private LichessEventStream _eventStream;
    [SerializeField] private float _finishGraceSeconds = 3f;   // wait this long for the board stream to close itself
    private Coroutine _finishGrace;


    [Header("Reconnect")]
    [SerializeField] private float _reconnectBaseDelay = 1f;
    [SerializeField] private float _reconnectMaxDelay = 8f;
    [SerializeField] private int _reconnectMaxAttempts = 10;   // give up after this many; 0 = never give up

    private StreamReconnector _reconnector;


    // Created per game, destroyed at game end
    private LichessBoardStream _boardStream;

    public string CurrentGameId { get; private set; }
    public PieceColor? MyColor { get; private set; }    // parsed from wire "white"/"black"; null between games
    public PieceColor? SideToMove { get; private set; }  // whose move it is now; null between games
    public bool IsGameActive { get; private set; }

    public bool IsMyTurn =>
        IsGameActive && MyColor.HasValue && SideToMove == MyColor;

    // True only for a piece of our color during an active game
    public bool IsMyPiece(PieceColor pieceColor) =>
        IsGameActive && MyColor == pieceColor;

    private bool _sawTerminalStatus; // Did the board stream tell us in-band the game is over?
    private string _finalStatus;

    public event System.Action<GameEventInfo> OnGameStarted;
    public event System.Action<string> OnMovesReceived;              // for BoardView
    public event System.Action<GameStateEvent> OnGameStateReceived;  // clocks, result, etc.
    public event System.Action<GameEndReason, string> OnGameEnded;   // reason, final status
    public event System.Action OnMyTurnBegan;

    private void OnEnable()
    {
        _eventStream.OnGameStart += HandleGameStart;
        _eventStream.OnGameFinish += HandleGameFinish;
    }

    private void OnDisable()
    {
        _eventStream.OnGameStart -= HandleGameStart;
        _eventStream.OnGameFinish -= HandleGameFinish;
    }


    // ---------- Start ----------

    private void Awake()
    {
        _reconnector = new StreamReconnector(
            this, ReconnectBoardStream,
            _reconnectBaseDelay, _reconnectMaxDelay,
            _reconnectMaxAttempts, HandleReconnectExhausted);
    }

    private void HandleGameStart(GameEventInfo game)
    {
        // One game at a time; will need to be replaced later if multiple boards are desired
        if (IsGameActive)
        {
            
            Debug.LogWarning("Game " + game.gameId + " started while " + CurrentGameId +
                             " is still active. Ignoring.");
            return;
        }

        CurrentGameId = game.gameId;
        MyColor = ParseColor(game.color);
        SideToMove = PieceColor.White;
        IsGameActive = true;
        _sawTerminalStatus = false;
        _finalStatus = null;

        // Fresh object, therefore no stale state to reset
        _boardStream = gameObject.AddComponent<LichessBoardStream>();
        _boardStream.OnGameStateReceived += HandleGameState;
        _boardStream.OnStreamEnded += HandleStreamEnded;
        _boardStream.Initialize(_authManager, _client, game.gameId);
        _boardStream.StartStream();

        _reconnector.NotifyConnected();

        Debug.Log("Session: game " + CurrentGameId + " begun, playing " + MyColor);
        OnGameStarted?.Invoke(game);
    }


    // ---------- During ----------

    private void HandleGameState(GameStateEvent state)
    {
        if (_reconnector.Attempt != 0)
        {
            Debug.Log("Board stream reconnected.");
            _reconnector.NotifyConnected();
        }

        bool wasMyTurn = IsMyTurn;          // sampled BEFORE we advance side-to-move
        SideToMove = BoardState.SideToMove(CountMoves(state.moves));

        bool terminal = GameStatus.IsTerminal(state.status);

        OnGameStateReceived?.Invoke(state);
        OnMovesReceived?.Invoke(state.moves);

        // Rising edge only, & never on the game-ending state
        if (!terminal && !wasMyTurn && IsMyTurn)
            OnMyTurnBegan?.Invoke();

        if (terminal)
        {
            _sawTerminalStatus = true;
            _finalStatus = state.status;
            Debug.Log("Session: game over by '" + state.status + "'" +
                      (string.IsNullOrEmpty(state.winner) ? "" : ", winner " + state.winner));
        }
    }

    // Called by BoardInput 
    public void SendMove(string uciMove)
    {
        if (!IsGameActive || _boardStream == null)
        {
            Debug.LogWarning("SendMove ignored - no active game: " + uciMove);
            return;
        }

        if (!IsMyTurn)
        {
            // TO-DO: Enable premoves here; for now refuse move
            Debug.Log("SendMove refused - not your turn: " + uciMove);
            return;
        }

        _boardStream.SendMove(uciMove);
    }

    private static PieceColor? ParseColor(string wire) => wire switch
    {
        "white" => PieceColor.White,
        "black" => PieceColor.Black,
        _ => null,
    };

    private static int CountMoves(string moves) =>
        string.IsNullOrWhiteSpace(moves)
            ? 0
            : moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;

    // ---------- End ----------

    // The board stream's connection closed, cleanly or by connection drop
    private void HandleStreamEnded(StreamEndReason streamEnd)
    {
        if (!IsGameActive)
            return;

        // Genuine game end (saw a terminal status): finish, don't reconnect
        if (_sawTerminalStatus)
        {
            _reconnector.Cancel();
            EndGame(GameEndReason.Finished);
            return;
        }

        switch (streamEnd)
        {
            case StreamEndReason.StoppedByUs:
                break;   

            case StreamEndReason.AuthFailed:
                _reconnector.Cancel();   // can't reconnect without re-auth
                EndGame(GameEndReason.ConnectionLost);
                break;

            case StreamEndReason.ClosedByServer:
            case StreamEndReason.Error:
                // Transient mid-game drop: keep the stream object, retry with backoff
                Debug.LogWarning("Board stream dropped mid-game; reconnecting (attempt " +
                                 (_reconnector.Attempt + 1) + ")");
                _reconnector.Schedule();
                break;
        }
    }

    // Retry action: restart the SAME board stream
    private void ReconnectBoardStream()
    {
        if (!IsGameActive || _boardStream == null) return;
        _boardStream.StartStream();   // a fresh gameFull will re-sync the board on success
    }

    // Budget spent: drop is not recovering, so end the game
    private void HandleReconnectExhausted()
    {
        if (!IsGameActive) return;
        Debug.LogWarning("Board stream reconnect gave up; ending game.");
        EndGame(_sawTerminalStatus ? GameEndReason.Finished : GameEndReason.ConnectionLost);
    }


    // Lichess's out-of-band confirmation on the account event stream
    // ADVISORY ONLY: board stream still owes the FINAL gameState (mating
    // move + terminal status); Lichess closes that stream itself once game ends
    // Aborting here would discard bytes not yet read
    private void HandleGameFinish(GameEventInfo game)
    {
        if (!IsGameActive || game.gameId != CurrentGameId)
            return;

        _sawTerminalStatus = true;

        // Stream healthy: let it deliver the final state, then close 
        // Mid-reconnect: won't deliver anything, end now
        if (_boardStream != null && _boardStream.IsStreaming)
        {
            if (_finishGrace == null)
                _finishGrace = StartCoroutine(CloseBoardStreamAfterGrace());
        }
        else
        {
            _reconnector.Cancel();
            EndGame(GameEndReason.Finished);
        }
    }

    private System.Collections.IEnumerator CloseBoardStreamAfterGrace()
    {
        float deadline = Time.time + _finishGraceSeconds;

        // Normal path: server closes, HandleStreamEnded runs, IsGameActive goes false, loop exits
        while (Time.time < deadline && IsGameActive &&
               _boardStream != null && _boardStream.IsStreaming)
            yield return null;

        _finishGrace = null;

        if (IsGameActive && _boardStream != null && _boardStream.IsStreaming)
        {
            Debug.LogWarning("Session: board stream still open " + _finishGraceSeconds +
                             "s after gameFinish; forcing close.");
            _boardStream.StopStream();
        }
    }

    private void EndGame(GameEndReason reason)
    {
        if (!IsGameActive)
            return;

        IsGameActive = false;
        _reconnector.Cancel();

        if (_finishGrace != null)
        {
            StopCoroutine(_finishGrace);
            _finishGrace = null;
        }

        if (_boardStream != null)
        {
            _boardStream.OnGameStateReceived -= HandleGameState;
            _boardStream.OnStreamEnded -= HandleStreamEnded;
            _boardStream.StopStream();

            // Defer destroy to end of frame, so it's safe to call 
            // from inside the stream's own Update callback
            Destroy(_boardStream);
            _boardStream = null;
        }

        Debug.Log("Session: game " + CurrentGameId + " ended (" + reason + ")" +
                  (_finalStatus == null ? "" : " status=" + _finalStatus));

        CurrentGameId = null;
        MyColor = null;
        SideToMove = null;

        OnGameEnded?.Invoke(reason, _finalStatus);
    }
}