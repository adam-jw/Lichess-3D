using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] private float squareSize = 1f;
    [SerializeField] private float pieceScale = 1f;
    [SerializeField] private LichessGameSession _session;

    private BoardState _currentBoard;

    public BoardState CurrentBoard => _currentBoard;

    // Have Unity serialize an array of a [Serializable] struct; edit mapping
    // in the Inspector and build the real lookup dictionary at runtime
    [System.Serializable]
    private struct PiecePrefab
    {
        public PieceType type;
        public PieceColor color;
        public GameObject prefab;
    }

    [SerializeField] private PiecePrefab[] piecePrefabs;   // 12 entries: 6 types x 2 colors

    private Dictionary<(PieceType, PieceColor), GameObject> _lookup;
    private readonly List<GameObject> _spawned = new List<GameObject>();

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
                go.transform.localScale *= pieceScale;                   
                _spawned.Add(go);

                // Logging this piece's attributes in PieceRef
                PieceRef pieceRef = go.AddComponent<PieceRef>();
                pieceRef.File = file;
                pieceRef.Rank = rank;
                pieceRef.Type = piece.Type;
                pieceRef.Color = piece.Color;

                _spawned.Add(go);
            }
    }

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
        Render(BoardState.FromMoves(moves));
    }
}