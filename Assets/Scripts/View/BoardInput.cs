using UnityEngine;

// Turns a mouse click into a board square. Two cases: hit a piece's collider
// (square checked via PieceRef), or fall through to the board plane (square itself)
public class BoardInput : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private Camera _camera;   // leave empty to use Camera.main
    [SerializeField] private LichessBoardStream _boardStream;

    private bool _hasSelection;
    private int _selectedFile, _selectedRank;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleMouseDown();
        else if (Input.GetMouseButtonUp(0))
            HandleMouseUp();
    }

    private void HandleMouseDown()
    {
        if (!TryGetClickedSquare(out int file, out int rank))
        {
            _hasSelection = false;   // clicked off the board -> cancel
            return;
        }

        BoardState board = _boardView.CurrentBoard;

        if (_hasSelection)
        {
            Piece selected = board.At(_selectedFile, _selectedRank);
            Piece clicked = board.At(file, rank);

            // Click same-color piece = user changed their mind; Change selection
            if (!clicked.IsEmpty && clicked.Color == selected.Color)
            {
                _selectedFile = file;
                _selectedRank = rank;
                Debug.Log($"Re-selected {SquareName(file, rank)}");
                return;
            }

            // Empty square or enemy piece = destination named
            CompleteMove(file, rank);
            return;
        }

        // Otherwise start a selection, but only if a piece is there
        if (board == null || board.At(file, rank).IsEmpty)
            return;

        _hasSelection = true;
        _selectedFile = file;
        _selectedRank = rank;
        Debug.Log($"Selected {SquareName(file, rank)}");
    }

    private void HandleMouseUp()
    {
        if (!_hasSelection)
            return;

        if (!TryGetClickedSquare(out int file, out int rank))
            return;   // released off the board; keep the selection, ignore

        // Mouse released on the origin square = CLICK
        // stay selected and wait for second click to name the destination
        if (file == _selectedFile && rank == _selectedRank)
            return;

        // Released somewhere else = DRAG -> make move now
        CompleteMove(file, rank);
    }

    private void CompleteMove(int destFile, int destRank)
    {
        string uci = SquareName(_selectedFile, _selectedRank) + SquareName(destFile, destRank);

        // Temp handle for sending promotions to lichess: auto queen pawn on back rank
        BoardState board = _boardView.CurrentBoard;
        Piece moving = board.At(_selectedFile, _selectedRank);
        if (moving.Type == PieceType.Pawn && (destRank == 0 || destRank == 7))
            uci += "q";

        _hasSelection = false;

        Debug.Log($"Sending move: {uci}");
        _boardStream.SendMove(uci);
    }

    private bool TryGetClickedSquare(out int file, out int rank)
    {
        file = rank = -1;
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        // Case 1: did we hit a piece? Get square from PieceRef
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            PieceRef pieceRef = hit.collider.GetComponentInParent<PieceRef>();
            if (pieceRef != null)
            {
                file = pieceRef.File;
                rank = pieceRef.Rank;
                return true;
            }
        }

        // Case 2: no piece, intersect the board's plane and invert-map
        Transform boardTransform = _boardView.transform;
        Plane boardPlane = new Plane(boardTransform.up, boardTransform.position);

        if (!boardPlane.Raycast(ray, out float distance))
            return false;   // ray parallel to the plane, or pointing away

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 localPoint = boardTransform.InverseTransformPoint(worldPoint);
        return _boardView.LocalToSquare(localPoint, out file, out rank);
    }

    // Turn array pos. to chessboard notation for readability
    private static string SquareName(int file, int rank) =>
        $"{(char)('a' + file)}{(char)('1' + rank)}";
}