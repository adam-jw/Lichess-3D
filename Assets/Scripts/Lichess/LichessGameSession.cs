using UnityEngine;

// Owns ONE GAME: its id, which color we are, and the board stream that carries it
// Acts as the stable object the view and input layers talk to

public class LichessGameSession : MonoBehaviour
{
    [SerializeField] private LichessAuthManager _authManager;
    [SerializeField] private LichessClient _client;
    [SerializeField] private LichessEventStream _eventStream;

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

        Debug.Log("Session: game " + CurrentGameId + " begun, playing " + MyColor);
        OnGameStarted?.Invoke(game);
    }


    // ---------- During ----------

    private void HandleGameState(GameStateEvent state)
    {
        SideToMove = BoardState.SideToMove(CountMoves(state.moves));

        OnGameStateReceived?.Invoke(state);
        OnMovesReceived?.Invoke(state.moves);

        if (GameStatus.IsTerminal(state.status))
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

        GameEndReason gameEnd = _sawTerminalStatus
            ? GameEndReason.Finished
            : GameEndReason.ConnectionLost;

        EndGame(gameEnd);
    }

    // Lichess's out-of-band confirmation on the account event stream 
    // Usually redundant; proof of real finish rather than a dropped socket
    private void HandleGameFinish(GameEventInfo game)
    {
        if (!IsGameActive || game.gameId != CurrentGameId)
            return;

        _sawTerminalStatus = true;

        if (_boardStream != null && _boardStream.IsStreaming)
            _boardStream.StopStream();   // -> OnStreamEnded -> HandleStreamEnded
    }

    private void EndGame(GameEndReason reason)
    {
        if (!IsGameActive)
            return;

        IsGameActive = false;

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