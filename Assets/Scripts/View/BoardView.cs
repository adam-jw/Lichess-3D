using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    // ----- Dependencies -----
    [Header("Dependencies")]
    [SerializeField] private LichessGameSession _session;

    // ----- Board geometry -----
    [Header("Board")]
    [SerializeField] private float squareSize = 1f;
    [SerializeField] private float pieceScale = 1f;   // global default; overridden per-type below

    // ----- Animation -----
    [Header("Animation")]
    [SerializeField] private float _moveDuration = 0.14f;
    [SerializeField] private float _hopHeight = 0f;   // 0 = flat slide; global default, overridden per-type below

    // ----- Per-piece-type overrides -----
    // List ONLY the types that differ from the globals
    [System.Serializable]
    private struct PieceScale { public PieceType type; public float scale; }
    [System.Serializable]
    private struct PieceHop { public PieceType type; public float hopHeight; }

    [Header("Per-piece overrides")]
    [SerializeField] private PieceScale[] pieceScaleOverrides;
    [SerializeField] private PieceHop[] pieceHopOverrides;

    // hydrated in Awake
    private Dictionary<PieceType, float> _scaleLookup;
    private Dictionary<PieceType, float> _hopLookup;     

    // ----- Piece prefab mapping -----
    [System.Serializable]
    private struct PiecePrefab
    {
        public PieceType type;
        public PieceColor color;
        public GameObject prefab;
    }

    [Header("Piece prefabs")]
    [SerializeField] private PiecePrefab[] piecePrefabs;   // 12 entries: 6 types x 2 colors

    private Dictionary<(PieceType, PieceColor), GameObject> _lookup;   // hydrated in Awake

    // ----- Runtime state -----
    private BoardState _currentBoard;      // logical truth; updated the instant a move arrives
    public BoardState CurrentBoard => _currentBoard;

    private Coroutine _activeTween;         // in-flight slide, if any
    private readonly List<GameObject> _spawned = new List<GameObject>();   // current on-board piece objects


    // ----- History / navigation -----
    [Header("History")]
    [SerializeField] private bool _snapToLiveOnNewMove = true;   // jump to present if a move arrives while reviewing

    private string _liveMoves;      // latest full moves string from the stream (null = none yet)
    private int _viewedMoveCount;   // how many moves are displayed (cursor); == live count when at present

    // Announces the move token that produced the displayed position (highlighter listens)
    public event System.Action<string> OnViewedMoveChanged;

    public int LiveMoveCount => Tokenize(_liveMoves).Length;
    public bool IsAtLive => _viewedMoveCount == LiveMoveCount;

    private void Awake()
    {
        // Hydrate the fast lookup from the serialized array. The duplicate check
        // is the fail-loud sanity net for the struct-array's one weakness.
        _lookup = new Dictionary<(PieceType, PieceColor), GameObject>();
        foreach (PiecePrefab entry in piecePrefabs)
        {
            var key = (entry.type, entry.color);
            if (_lookup.ContainsKey(key))
                Debug.LogError($"Duplicate prefab mapping for {entry.color} {entry.type}", this);
            _lookup[key] = entry.prefab;
        }

        _scaleLookup = new Dictionary<PieceType, float>();

        foreach (PieceScale entry in pieceScaleOverrides)
        {
            if (_scaleLookup.ContainsKey(entry.type))
                Debug.LogError($"Duplicate scale override for {entry.type}", this);
            _scaleLookup[entry.type] = entry.scale;
        }

        _hopLookup = new Dictionary<PieceType, float>();

        foreach (PieceHop entry in pieceHopOverrides)
        {
            if (_hopLookup.ContainsKey(entry.type))
                Debug.LogError($"Duplicate hop override for {entry.type}", this);
            _hopLookup[entry.type] = entry.hopHeight;
        }
    }

    private void Start()
    {
        Render(BoardState.FromMoves(""));   // empty string -> starting position
    }

    // Destroys last update's pieces, rebuilds from given state
    public void Render(BoardState board)
    {
        _currentBoard = board;

        foreach (GameObject go in _spawned)
            Destroy(go);
        _spawned.Clear();

        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Piece piece = board.At(file, rank);
                if (piece.IsEmpty)
                    continue;

                if (!_lookup.TryGetValue((piece.Type, piece.Color), out GameObject prefab) || prefab == null)
                {
                    Debug.LogError($"No prefab assigned for {piece.Color} {piece.Type}", this);
                    continue;
                }

                GameObject go = Instantiate(prefab, transform);          // parent under the board root
                go.transform.localPosition = SquareToLocal(file, rank);
                go.transform.localScale *= ScaleFor(piece.Type);

                // Logging this piece's attributes in PieceRef
                PieceRef pieceRef = go.AddComponent<PieceRef>();
                pieceRef.File = file;
                pieceRef.Rank = rank;
                pieceRef.Type = piece.Type;
                pieceRef.Color = piece.Color;

                _spawned.Add(go);
            }
    }

    // Per-type value with global fallback
    private float ScaleFor(PieceType type) =>
        _scaleLookup.TryGetValue(type, out float s) ? s : pieceScale;

    private float HopHeightFor(PieceType type) =>
        _hopLookup.TryGetValue(type, out float h) ? h : _hopHeight;

    public Vector3 SquareToLocal(int file, int rank)
    {
        float x = (file - 3.5f) * squareSize;
        float z = (rank - 3.5f) * squareSize;
        return new Vector3(x, 0f, z);
    }

    // LOCAL-space point on the board -> the square containing it
    // Returns false if the point is outside the 8x8
    public bool LocalToSquare(Vector3 local, out int file, out int rank)
    {
        file = Mathf.RoundToInt(local.x / squareSize + 3.5f);
        rank = Mathf.RoundToInt(local.z / squareSize + 3.5f);
        return file >= 0 && file < 8 && rank >= 0 && rank < 8;
    }

    // TEMP board square rendering
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Vector3 world = transform.TransformPoint(SquareToLocal(file, rank));
                Gizmos.DrawWireCube(world, new Vector3(squareSize, 0.01f, squareSize) * 0.9f);
            }
    }

    private void OnEnable()
    {
        if (_session != null)
            _session.OnMovesReceived += HandleMovesReceived;
    }

    private void OnDisable()
    {
        if (_session != null)
            _session.OnMovesReceived -= HandleMovesReceived;
    }

    private void HandleMovesReceived(string moves)
    {
        string previousLive = _liveMoves;
        bool wasAtLive = IsAtLive;   // showing the present before this update?

        _liveMoves = moves;
        StopActiveTween();           // settle any in-flight slide before deciding

        if (wasAtLive)
        {
            // Go forward with the game; animate a clean single move.
            _viewedMoveCount = LiveMoveCount;
            BoardState newBoard = BoardState.FromMoves(moves);

            if (IsSingleNewMove(previousLive, moves, out int ff, out int fr, out int tf, out int tr))
                AnimateThenRender(ff, fr, tf, tr, newBoard);
            else
                Render(newBoard);

            RaiseViewedMoveChanged();
        }
        else if (_snapToLiveOnNewMove || !IsForwardExtension(previousLive, moves))
        {
            // Viewing old move, but the toggle says snap, or timeline diverged -> snap to present
            _viewedMoveCount = LiveMoveCount;
            RefreshViewedPosition();
        }
        // else: reviewing an extension with snap off -> stay put; live advances underneath
    }

    // "e2e4" / "e7e8q" -> squares. False if malformed or off-board
    private static bool TryParseMove(string move, out int fromFile, out int fromRank,
                                                  out int toFile, out int toRank)
    {
        fromFile = fromRank = toFile = toRank = -1;
        if (move == null || move.Length < 4) return false;

        fromFile = move[0] - 'a'; fromRank = move[1] - '1';
        toFile = move[2] - 'a'; toRank = move[3] - '1';
        return fromFile >= 0 && fromFile < 8 && fromRank >= 0 && fromRank < 8
            && toFile >= 0 && toFile < 8 && toRank >= 0 && toRank < 8;
    }

    // True if 'moves' is 'prev' plus exactly one more token
    // Outputs the from/to squares of that one new move
    private static bool IsSingleNewMove(string prev, string moves,
        out int fromFile, out int fromRank, out int toFile, out int toRank)
    {
        fromFile = fromRank = toFile = toRank = -1;

        if (prev == null)   // first state of a game, no animation
            return false;

        string[] prevTokens = Tokenize(prev);
        string[] newTokens = Tokenize(moves);
        if (newTokens.Length != prevTokens.Length + 1)
            return false;
        for (int i = 0; i < prevTokens.Length; i++)
            if (prevTokens[i] != newTokens[i])
                return false;   // histories diverged -> snap

        string move = newTokens[newTokens.Length - 1]; 

        return TryParseMove(move, out fromFile, out fromRank, out toFile, out toRank);
    }

    private static string[] Tokenize(string moves) =>
        string.IsNullOrWhiteSpace(moves)
            ? System.Array.Empty<string>()
            : moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

    // ---------- Animation ----------
    private void AnimateThenRender(int fromFile, int fromRank, int toFile, int toRank, BoardState finalBoard)
    {
        GameObject mover = FindPieceObject(fromFile, fromRank);
        if (mover == null)   // nothing to animate -> just snap pieces
        {
            Render(finalBoard);
            return;
        }

        _currentBoard = finalBoard;   // logical truth updates now; visual catches up over the slide

        // Clear a piece captured on the destination so the mover doesn't glide over it
        GameObject captured = FindPieceObject(toFile, toRank);
        if (captured != null)
        {
            _spawned.Remove(captured);
            Destroy(captured);
        }

        PieceRef moverRef = mover.GetComponent<PieceRef>();
        float hop = HopHeightFor(moverRef != null ? moverRef.Type : PieceType.None);

        _activeTween = StartCoroutine(SlidePiece(
            mover, SquareToLocal(fromFile, fromRank), SquareToLocal(toFile, toRank), finalBoard, hop));
    }

    private IEnumerator SlidePiece(GameObject mover, Vector3 fromLocal, Vector3 toLocal, BoardState finalBoard, float hopHeight)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(_moveDuration, 0.0001f);
            float clamped = Mathf.Clamp01(t);

            Vector3 pos = Vector3.Lerp(fromLocal, toLocal, Mathf.SmoothStep(0f, 1f, clamped));
            pos.y += hopHeight * Mathf.Sin(clamped * Mathf.PI);   // arc; 0 hop = flat slide

            if (mover != null)
                mover.transform.localPosition = pos;
            yield return null;
        }

        _activeTween = null;
        Render(finalBoard);   // rebuild to truth; the slider ended exactly on its square
    }

    private GameObject FindPieceObject(int file, int rank)
    {
        foreach (GameObject go in _spawned)
        {
            if (go == null) continue;
            PieceRef pr = go.GetComponent<PieceRef>();
            if (pr != null && pr.File == file && pr.Rank == rank)
                return go;
        }
        return null;
    }


    // ----- History/Navigation -----
    public void StepBack(bool animate = true)
    {
        if (_viewedMoveCount <= 0) return;
        StopActiveTween();

        string undone = ViewedLastMove();   // the move that produced the current position
        _viewedMoveCount--;
        BoardState target = BoardState.FromMoves(PrefixMoves(_viewedMoveCount));

        // Reversed: move the piece from its destination back to its origin.
        if (animate && TryParseMove(undone, out int ff, out int fr, out int tf, out int tr))
            AnimateThenRender(tf, tr, ff, fr, target);
        else
            Render(target);

        RaiseViewedMoveChanged();
    }

    public void StepForward(bool animate = true)
    {
        if (_viewedMoveCount >= LiveMoveCount) return;
        StopActiveTween();

        _viewedMoveCount++;
        string move = ViewedLastMove();     // the move we just stepped onto
        BoardState target = BoardState.FromMoves(PrefixMoves(_viewedMoveCount));

        if (animate && TryParseMove(move, out int ff, out int fr, out int tf, out int tr))
            AnimateThenRender(ff, fr, tf, tr, target);
        else
            Render(target);

        RaiseViewedMoveChanged();
    }

    public void JumpToStart() { StopActiveTween(); _viewedMoveCount = 0; RefreshViewedPosition(); }
    public void JumpToLive() { StopActiveTween(); _viewedMoveCount = LiveMoveCount; RefreshViewedPosition(); }

    private void StopActiveTween()
    {
        if (_activeTween == null) return;
        StopCoroutine(_activeTween);
        _activeTween = null;
        Render(_currentBoard);   // snap the interrupted slide to its committed destination
    }

    // Snap-render whatever the cursor points at (manual nav is instant, not tweened)
    private void RefreshViewedPosition()
    {
        Render(BoardState.FromMoves(PrefixMoves(_viewedMoveCount)));
        RaiseViewedMoveChanged();
    }

    private void RaiseViewedMoveChanged() => OnViewedMoveChanged?.Invoke(ViewedLastMove());

    private string ViewedLastMove()   // move token that produced the displayed position (null at start)
    {
        if (_viewedMoveCount <= 0) return null;
        string[] tokens = Tokenize(_liveMoves);
        return _viewedMoveCount <= tokens.Length ? tokens[_viewedMoveCount - 1] : null;
    }

    private string PrefixMoves(int count)   // first 'count' tokens rejoined ("" -> starting position)
    {
        string[] tokens = Tokenize(_liveMoves);
        if (count >= tokens.Length) return _liveMoves ?? "";
        return string.Join(" ", tokens, 0, count);
    }

    private static bool IsForwardExtension(string prev, string moves)   // prev is a token-wise prefix of moves
    {
        string[] p = Tokenize(prev);
        string[] m = Tokenize(moves);
        if (m.Length < p.Length) return false;
        for (int i = 0; i < p.Length; i++)
            if (p[i] != m[i]) return false;
        return true;
    }
}