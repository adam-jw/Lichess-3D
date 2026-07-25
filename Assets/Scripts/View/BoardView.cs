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

    // Square -> the live GameObject on it
    private readonly Dictionary<Square, GameObject> _registry = new Dictionary<Square, GameObject>();


    // ----- History / navigation -----
    [Header("History")]
    [SerializeField] private bool _snapToLiveOnNewMove = true;   // jump to present if a move arrives while reviewing

    private string _liveMoves;      // latest full moves string from the stream (null = none yet)
    private int _viewedMoveCount;   // how many moves are displayed (cursor); == live count when at present

    // Announces the move token that produced the displayed position (highlighter listens)
    public event System.Action<string> OnViewedMoveChanged;

    // ----- Diff Diagnostics -----
    [Header("Diff diagnostics")]
    [SerializeField] private bool _verifyRegistryAfterDiff = true;   // drift detector; turn off once trusted

    // The registry must match the board square-for-square after a diff: same occupied
    // squares, and each object's PieceRef matching the piece there
    private void VerifyRegistryMatches(BoardState board)
    {
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                var sq = new Square(file, rank);
                Piece p = board.At(file, rank);
                bool boardHas = !p.IsEmpty;
                bool regHas = _registry.TryGetValue(sq, out GameObject go);

                if (boardHas != regHas)
                {
                    Debug.LogError($"Drift at {sq}: board={(boardHas ? p.Color + " " + p.Type : "empty")}, registry={(regHas ? "object" : "empty")}", this);
                    continue;
                }
                if (boardHas)
                {
                    PieceRef pr = go.GetComponent<PieceRef>();
                    if (pr == null || pr.Type != p.Type || pr.Color != p.Color)
                        Debug.LogError($"Mismatch at {sq}: board={p.Color} {p.Type}, object={(pr == null ? "no PieceRef" : pr.Color + " " + pr.Type)}", this);
                }
            }
    }

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

    public void Render(BoardState board)
    {
        _currentBoard = board;

        foreach (GameObject go in _registry.Values)
            Destroy(go);
        _registry.Clear();

        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Piece piece = board.At(file, rank);
                if (!piece.IsEmpty)
                    SpawnPiece(new Square(file, rank), piece);
            }
    }

    // Edit existing pieces toward a new position
    private void ApplyEdits(IReadOnlyList<PieceEdit> edits)
    {
        foreach (PieceEdit e in edits)
        {
            switch (e.Kind)
            {
                case PieceEditKind.Remove: RemovePiece(e.From); break;
                case PieceEditKind.Move: MovePiece(e.From, e.To); break;
                case PieceEditKind.Spawn: SpawnPiece(e.To, e.Piece); break;
            }
        }
    }

    private void RemovePiece(Square sq)
    {
        if (!_registry.TryGetValue(sq, out GameObject go))
            throw new System.InvalidOperationException($"Remove: nothing on {sq} (registry drift)");
        _registry.Remove(sq);
        Destroy(go);
    }

    private void MovePiece(Square from, Square to)
    {
        if (!_registry.TryGetValue(from, out GameObject go))
            throw new System.InvalidOperationException($"Move: nothing on {from} (registry drift)");
        if (_registry.ContainsKey(to))
            throw new System.InvalidOperationException($"Move: {to} already occupied (edit ordering / drift)");

        _registry.Remove(from);
        _registry[to] = go;

        go.transform.localPosition = SquareToLocal(to.File, to.Rank);

        PieceRef pr = go.GetComponent<PieceRef>();
        if (pr != null) { pr.File = to.File; pr.Rank = to.Rank; }
    }

    private void SpawnPiece(Square at, Piece piece)
    {
        if (!_lookup.TryGetValue((piece.Type, piece.Color), out GameObject prefab) || prefab == null)
        {
            Debug.LogError($"No prefab assigned for {piece.Color} {piece.Type}", this);
            return;
        }

        GameObject go = Instantiate(prefab, transform);
        go.transform.localPosition = SquareToLocal(at.File, at.Rank);
        go.transform.localScale *= ScaleFor(piece.Type);

        PieceRef pr = go.AddComponent<PieceRef>();
        pr.File = at.File; pr.Rank = at.Rank; pr.Type = piece.Type; pr.Color = piece.Color;

        _registry[at] = go;
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
        Gizmos.color = Color.white;
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
        bool wasAtLive = IsAtLive;

        _liveMoves = moves;
        StopActiveTween();

        if (wasAtLive)
        {
            _viewedMoveCount = LiveMoveCount;
            BoardState newBoard = BoardState.FromMoves(moves);

            if (IsSingleNewMove(previousLive, moves, out string moveToken))
                ApplyPlyToView(BoardState.FromMoves(previousLive), moveToken, newBoard, animate: true);
            else
                Render(newBoard);

            RaiseViewedMoveChanged();
        }
        else if (_snapToLiveOnNewMove || !IsForwardExtension(previousLive, moves))
        {
            _viewedMoveCount = LiveMoveCount;
            RefreshViewedPosition();
        }
    }

    // Single forward ply: edit existing pieces toward newBoard via model's edit set
    private void ApplyMoveToView(string previousMoves, string moveToken, BoardState newBoard)
    {
        try
        {
            BoardState previousBoard = BoardState.FromMoves(previousMoves);
            Move move = Move.FromUci(moveToken);
            IReadOnlyList<PieceEdit> edits = previousBoard.DescribeMove(move);

            _currentBoard = newBoard;   // commit logical truth; edits bring registry/PieceRef/transforms in line
            ApplyEdits(edits);

            if (_verifyRegistryAfterDiff)
                VerifyRegistryMatches(newBoard);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Diff failed for '{moveToken}' ({ex.Message}); rebuilding from scratch.", this);
            Render(newBoard);
        }
    }

    // One forward ply - commit the identity-preserving diff, then optionally
    // slide the moved pieces into place. Failure rebuilds from scratch
    private void ApplyPlyToView(BoardState before, string moveToken, BoardState target, bool animate)
    {
        try
        {
            Move move = Move.FromUci(moveToken);
            IReadOnlyList<PieceEdit> edits = before.DescribeMove(move);

            _currentBoard = target;      // commit logical truth
            ApplyEdits(edits);           // registry, PieceRef, transforms -> committed

            if (_verifyRegistryAfterDiff)
                VerifyRegistryMatches(target);

            if (animate)
                AnimateMoveEdits(edits);   // rewind + slide as a visual overlay
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Diff failed for '{moveToken}' ({ex.Message}); rebuilding.", this);
            Render(target);
        }
    }

    // True if 'moves' is 'prev' plus exactly one more token; outputs that token 
    private static bool IsSingleNewMove(string prev, string moves, out string moveToken)
    {
        moveToken = null;
        if (prev == null) return false;

        string[] prevTokens = Tokenize(prev);
        string[] newTokens = Tokenize(moves);
        if (newTokens.Length != prevTokens.Length + 1) return false;
        for (int i = 0; i < prevTokens.Length; i++)
            if (prevTokens[i] != newTokens[i]) return false;

        moveToken = newTokens[newTokens.Length - 1];
        return true;
    }

    private static string[] Tokenize(string moves) =>
        string.IsNullOrWhiteSpace(moves)
            ? System.Array.Empty<string>()
            : moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

    // ---------- Animation ----------
    private struct MoverAnim { public GameObject go; public Vector3 from; public Vector3 to; public float hop; }

    // Rewind each Move-edit piece to its visual origin and slide it to the square it
    // already occupies. Forward: piece sits on To, slides From->To. Reversed (history
    // step-back): piece sits on From, slides To->From. Remove/Spawn edits don't slide;
    // Captured pieces vanish and promoted pieces appear on commit
    private void AnimateMoveEdits(IReadOnlyList<PieceEdit> edits, bool reversed = false)
    {
        var movers = new List<MoverAnim>();
        foreach (PieceEdit e in edits)
        {
            if (e.Kind != PieceEditKind.Move) continue;

            Square landing = reversed ? e.From : e.To;    // where the piece is committed now
            Square origin = reversed ? e.To : e.From;   // where its slide should start
            if (!_registry.TryGetValue(landing, out GameObject go)) continue;

            Vector3 originLocal = SquareToLocal(origin.File, origin.Rank);
            Vector3 landingLocal = SquareToLocal(landing.File, landing.Rank);
            PieceRef pr = go.GetComponent<PieceRef>();
            float hop = HopHeightFor(pr != null ? pr.Type : PieceType.None);

            go.transform.localPosition = originLocal;   // rewind the visual; state stays committed
            movers.Add(new MoverAnim { go = go, from = originLocal, to = landingLocal, hop = hop });
        }

        if (movers.Count > 0)
            _activeTween = StartCoroutine(SlideMovers(movers));
    }

    private IEnumerator SlideMovers(List<MoverAnim> movers)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(_moveDuration, 0.0001f);
            float c = Mathf.Clamp01(t);
            float eased = Mathf.SmoothStep(0f, 1f, c);

            foreach (MoverAnim m in movers)
            {
                if (m.go == null) continue;
                Vector3 pos = Vector3.Lerp(m.from, m.to, eased);
                pos.y += m.hop * Mathf.Sin(c * Mathf.PI);   // arc; 0 hop = flat slide
                m.go.transform.localPosition = pos;
            }
            yield return null;
        }

        foreach (MoverAnim m in movers)          // settle exactly on the committed square
            if (m.go != null) m.go.transform.localPosition = m.to;

        _activeTween = null;
    }

    // ----- History/Navigation -----
    public void StepBack(bool animate = true)
    {
        if (_viewedMoveCount <= 0) return;
        StopActiveTween();

        string undone = ViewedLastMove();          // the move being taken back
        _viewedMoveCount--;
        BoardState target = BoardState.FromMoves(PrefixMoves(_viewedMoveCount));   // pre-move position

        Render(target);   // rebuild truth; captured/un-promoted pieces reappear here

        // Reverse-animate the forward edits: DescribeMove(undone) on the pre-move board
        // gives the same Move edits, run To->From. Castle now retreats king AND rook.
        if (animate && TryGetMove(undone, out Move move))
            AnimateMoveEdits(target.DescribeMove(move), reversed: true);

        RaiseViewedMoveChanged();
    }

    private static bool TryGetMove(string uci, out Move move)
    {
        try { move = Move.FromUci(uci); return true; }
        catch { move = default; return false; }
    }

    public void StepForward(bool animate = true)
    {
        if (_viewedMoveCount >= LiveMoveCount) return;
        StopActiveTween();

        BoardState before = BoardState.FromMoves(PrefixMoves(_viewedMoveCount));
        _viewedMoveCount++;
        string move = ViewedLastMove();
        BoardState target = BoardState.FromMoves(PrefixMoves(_viewedMoveCount));

        ApplyPlyToView(before, move, target, animate);
        RaiseViewedMoveChanged();
    }

    public void JumpToStart() { StopActiveTween(); _viewedMoveCount = 0; RefreshViewedPosition(); }
    public void JumpToLive() { StopActiveTween(); _viewedMoveCount = LiveMoveCount; RefreshViewedPosition(); }

    private void StopActiveTween()
    {
        if (_activeTween == null) return;
        StopCoroutine(_activeTween);
        _activeTween = null;
        SnapAllToRegistry();   // reconcile any mid-slide transforms to committed truth
    }

    // Fixes visuals after interrupted slide - every piece sits exactly on its registry square
    private void SnapAllToRegistry()
    {
        foreach (KeyValuePair<Square, GameObject> kv in _registry)
            if (kv.Value != null)
                kv.Value.transform.localPosition = SquareToLocal(kv.Key.File, kv.Key.Rank);
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