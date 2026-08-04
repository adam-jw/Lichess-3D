using UnityEngine;

// Tracks both players' remaining time
//
// Not simulated; Every gameState carries authoritative wtime/btime,
// and this only ever anchors on those and counts down from the anchor
//
// Deliberately holds its values after the game ends, and clears only on OnGameStarted
public class GameClockModel : MonoBehaviour
{
    [SerializeField] private LichessGameSession _session;

    // Snapshot of the last gameState.
    private bool _hasClock;
    private int _whiteMsAtAnchor;
    private int _blackMsAtAnchor;
    private float _anchorRealtime;
    private PieceColor _sideToMove;
    private int _plyCount;
    private bool _gameOver;

    // Clock settings from gameFull
    private bool _hasSettings;
    private int _initialMs;
    private int _incrementMs;

    public bool HasClock => _hasClock;
    public bool HasSettings => _hasSettings;
    public int InitialMs => _initialMs;
    public int IncrementMs => _incrementMs;

    // Neither clock runs until both players have moved once
    public bool IsRunning => _hasClock && !_gameOver && _plyCount >= 2;

    private void OnEnable()
    {
        if (_session == null)
        {
            Debug.LogError("GameClockModel: no session assigned; disabling.", this);
            enabled = false;
            return;
        }

        _session.OnGameStarted += HandleGameStarted;
        _session.OnGameFullReceived += HandleGameFull;
        _session.OnGameStateReceived += HandleGameState;
        _session.OnGameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        if (_session == null) return;

        _session.OnGameStarted -= HandleGameStarted;
        _session.OnGameFullReceived -= HandleGameFull;
        _session.OnGameStateReceived -= HandleGameState;
        _session.OnGameEnded -= HandleGameEnded;
    }

    // The only place state is cleared
    private void HandleGameStarted(GameEventInfo game)
    {
        _hasClock = false;
        _hasSettings = false;
        _gameOver = false;
        _plyCount = 0;
    }

    private void HandleGameEnded(GameEndReason reason, string status)
    {
        if (_gameOver || !_hasClock) return;

        // Freeze at the value currently on screen
        _whiteMsAtAnchor = GetRemainingMs(PieceColor.White);
        _blackMsAtAnchor = GetRemainingMs(PieceColor.Black);
        _gameOver = true;
    }

    private void HandleGameFull(GameFullEvent full)
    {
        // Correspondence games have no clock object (days per turn instead)
        if (full.clock == null)
        {
            _hasSettings = false;
            return;
        }

        _hasSettings = true;
        _initialMs = full.clock.initial;
        _incrementMs = full.clock.increment;
    }

    // gameFull nests a gameState, so this also fires for the opening position and on reconnect
    private void HandleGameState(GameStateEvent state)
    {
        _whiteMsAtAnchor = state.wtime;
        _blackMsAtAnchor = state.btime;

        _anchorRealtime = Time.realtimeSinceStartup;

        _plyCount = CountPlies(state.moves);
        _sideToMove = (_plyCount % 2 == 0) ? PieceColor.White : PieceColor.Black;
        _gameOver = GameStatus.IsTerminal(state.status);
        _hasClock = true;
    }

    // Remaining milliseconds for one side, as of now
    public int GetRemainingMs(PieceColor color)
    {
        int anchored = color == PieceColor.White ? _whiteMsAtAnchor : _blackMsAtAnchor;

        if (!IsRunning || color != _sideToMove)
            return Mathf.Max(0, anchored);

        float elapsedSeconds = Time.realtimeSinceStartup - _anchorRealtime;
        return Mathf.Max(0, anchored - Mathf.RoundToInt(elapsedSeconds * 1000f));
    }

    // True for the side whose clock is actively counting down
    public bool IsTicking(PieceColor color) => IsRunning && color == _sideToMove;

    private static int CountPlies(string moves)
    {
        if (string.IsNullOrEmpty(moves)) return 0;

        return moves.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length;
    }
}