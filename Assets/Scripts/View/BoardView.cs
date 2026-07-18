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

    private string _renderedMoves;         // moves string of the last state shown (null = none yet)
    private Coroutine _activeTween;         // in-flight slide, if any
    private readonly List<GameObject> _spawned = new List<GameObject>();   // current on-board piece objects

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
        BoardState newBoard = BoardState.FromMoves(moves);

        // Finish any in-flight moves instantly before handling the next update
        if (_activeTween != null)
        {
            StopCoroutine(_activeTween);
            _activeTween = null;
            Render(_currentBoard);   // snap the interrupted move to its destination
        }

        // Animate only when this state is exactly one move past the one we last showed
        if (IsSingleNewMove(_renderedMoves, moves,
                out int fromFile, out int fromRank, out int toFile, out int toRank))
            AnimateThenRender(fromFile, fromRank, toFile, toRank, newBoard);
        else
            Render(newBoard);

        _renderedMoves = moves;
    }

    // True if 'moves' is 'prev' plus exactly one more token (prev being a prefix)
    // Outputs the from/to squares of that one new move.
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
        if (move.Length < 4)
            return false;

        fromFile = move[0] - 'a';
        fromRank = move[1] - '1';
        toFile = move[2] - 'a';
        toRank = move[3] - '1';
        return fromFile >= 0 && fromFile < 8 && fromRank >= 0 && fromRank < 8
            && toFile >= 0 && toFile < 8 && toRank >= 0 && toRank < 8;
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
}