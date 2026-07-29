using UnityEngine;
using System.Collections.Generic;

// Turns a mouse click into a board square. Two cases: hit a piece's collider
// (square checked via PieceRef), or fall through to the board plane (square itself)
public class BoardInput : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private Camera _camera;   // leave empty to use Camera.main
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private BoardHighlighter _highlighter;

    private bool _hasSelection;
    private int _selectedFile, _selectedRank;

    private bool _pressWasOnSelected;

    private int _dotsForFile = -1, _dotsForRank = -1;   // which square the cached dots belong to
    private readonly List<Square> _legalDots = new List<Square>();
    private readonly List<Square> _legalCaptures = new List<Square>();

    private bool _hasPremove;
    private string _premoveUci;
    private int _preFromFile, _preFromRank, _preToFile, _preToRank;

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

        UpdateHoverHighlight();
        UpdateSelectionHighlight();
        UpdateLegalMoveHighlight();
        UpdatePremoveHighlight();
    }

    private void OnEnable()
    {
        if (_session == null) return;
        _session.OnMyTurnBegan += HandleMyTurnBegan;
        _session.OnGameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        if (_session == null) return;
        _session.OnMyTurnBegan -= HandleMyTurnBegan;
        _session.OnGameEnded -= HandleGameEnded;
    }

    private void HandleMyTurnBegan()
    {
        if (!_hasPremove) return;

        // Validate premove
        var premove = new Move(
            new Square(_preFromFile, _preFromRank),
            new Square(_preToFile, _preToRank));

        BoardState board = _boardView.CurrentBoard;
        bool legal = IsPremoveLegal(board, premove);

        string uci = _premoveUci;
        ClearPremove();   // consumed either way: fired, or dropped as invalid

        if (!legal)
        {
            Debug.Log($"Premove {uci} dropped: not legal in the current position.");
            return;
        }

        Debug.Log("Firing premove: " + uci);
        _session.SendMove(uci);
    }

    private static bool IsPremoveLegal(BoardState board, Move premove)
    {
        Piece mover = board.At(premove.From);
        if (mover.IsEmpty) return false;   // our piece was captured -> premove is dead

        foreach (Move legal in board.LegalFrom(premove.From))
            if (legal.To == premove.To)    // from is already fixed by LegalFrom(premove.From)
                return true;
        return false;
    }

    private void HandleGameEnded(GameEndReason reason, string status) => ClearPremove();

    private void ClearPremove()
    {
        _hasPremove = false;
        _premoveUci = null;
    }

    private void HandleMouseDown()
    {
        if (!_boardView.IsAtLive)
        {
            _boardView.JumpToLive();
            _hasSelection = false;   // don't carry a stale selection into the live view
            return;
        }

        if (_hasPremove)
            ClearPremove();   // any click cancels a queued premove

        if (!TryGetClickedSquare(out int file, out int rank))
        {
            _hasSelection = false;   // clicked off the board -> cancel
            return;
        }

        // Needed for deselect logic: Was this piece already selected? 
        _pressWasOnSelected = _hasSelection && file == _selectedFile && rank == _selectedRank;

        BoardState board = _boardView.CurrentBoard;

        if (_hasSelection)
        {
            if (_pressWasOnSelected)
                return;   // pressing a selected piece: mouse-up decides deselect or drag

            Piece selected = board.At(_selectedFile, _selectedRank);
            Piece clicked = board.At(file, rank);

            // Click same-color piece = user changed their mind; change selection
            if (!clicked.IsEmpty && clicked.Color == selected.Color)
            {
                // unless it's the king onto its own corner rook: that's a castle (if legal)
                if (TryCastleTarget(board, _selectedFile, _selectedRank, file, rank, out _))
                {
                    CompleteMove(file, rank);
                    return;
                }

                _selectedFile = file;
                _selectedRank = rank;
                Debug.Log($"Re-selected {SquareName(file, rank)}");
                return;
            }

            // Empty square or enemy piece = destination named
            CompleteMove(file, rank);
            return;
        }

        // Otherwise start a selection: a piece must be there AND it must be ours
        if (board == null)
            return;

        Piece piece = board.At(file, rank);
        if (piece.IsEmpty || !_session.IsMyPiece(piece.Color))
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

        // Released on the origin square = click, not drag
        if (file == _selectedFile && rank == _selectedRank)
        {
            if (_pressWasOnSelected)
                _hasSelection = false;   // re-click on selected piece -> DESELECT
            // else: this click created the selection -> stay selected
            return;
        }

        // Released somewhere else = DRAG -> make move now
        CompleteMove(file, rank);
    }

    private void CompleteMove(int destFile, int destRank)
    {
        BoardState board = _boardView.CurrentBoard;

        // Hanbdle castle; Rewrite rook square to king-move notation (e1h1 -> e1g1)
        if (TryCastleTarget(board, _selectedFile, _selectedRank, destFile, destRank, out int kingDestFile))
            destFile = kingDestFile;

        string uci = SquareName(_selectedFile, _selectedRank) + SquareName(destFile, destRank);

        Piece moving = board.At(_selectedFile, _selectedRank);
        if (moving.Type == PieceType.Pawn && (destRank == 0 || destRank == 7))
            uci += "q";

        int fromFile = _selectedFile, fromRank = _selectedRank;
        _hasSelection = false;

        if (_session.IsMyTurn)
        {
            Debug.Log($"Sending move: {uci}");
            _session.SendMove(uci);
            return;
        }

        // Opponent's turn -> queue move
        _hasPremove = true;
        _premoveUci = uci;
        _preFromFile = fromFile; 
        _preFromRank = fromRank;
        _preToFile = destFile; 
        _preToRank = destRank;
        Debug.Log("Premove queued: " + uci);
    }

    private bool TryGetClickedSquare(out int file, out int rank)
    {
        file = rank = -1;

        Vector3 mouse = Input.mousePosition;
        if (!float.IsFinite(mouse.x) || !float.IsFinite(mouse.y))
            return false;

        Ray ray = _camera.ScreenPointToRay(mouse);

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

    private void UpdateHoverHighlight()
    {
        if (_highlighter == null) return;

        if (TryGetClickedSquare(out int file, out int rank))
            _highlighter.SetHover(file, rank, IsSelectable(file, rank));
        else
            _highlighter.ClearHover();
    }

    // Selection highlight derived from state every frame 
    private void UpdateSelectionHighlight()
    {
        if (_highlighter == null) return;

        if (_hasSelection)
            _highlighter.SetSelection(_selectedFile, _selectedRank);
        else
            _highlighter.ClearSelection();
    }

    private void UpdateLegalMoveHighlight()
    {
        if (_highlighter == null) return;

        if (!_hasSelection || !_boardView.IsAtLive)
        {
            if (_dotsForFile != -1)
            {
                _highlighter.ClearLegalMoves();
                _highlighter.ClearLegalCaptures();
                _dotsForFile = _dotsForRank = -1;
            }
            return;
        }

        // Regenerate only when the selected square actually changed
        if (_selectedFile != _dotsForFile || _selectedRank != _dotsForRank)
        {
            _dotsForFile = _selectedFile;
            _dotsForRank = _selectedRank;

            _legalDots.Clear();
            _legalCaptures.Clear();

            BoardState board = _boardView.CurrentBoard;
            bool selectedIsKing = board.At(_selectedFile, _selectedRank).Type == PieceType.King;

            foreach (Move m in board.LegalFrom(new Square(_selectedFile, _selectedRank)))
            {
                // Occupied target -> capture (Corners); empty -> regular move (Dot)
                List<Square> bucket = board.At(m.To).IsEmpty ? _legalDots : _legalCaptures;
                if (!bucket.Contains(m.To))   // promotions share a destination
                    bucket.Add(m.To);

                // Castling should show as two-square hop OR as a 'capture' indicator on the rook
                if (selectedIsKing && Mathf.Abs(m.To.File - _selectedFile) == 2)
                {
                    var rookSquare = new Square(m.To.File > _selectedFile ? 7 : 0, _selectedRank);
                    if (!_legalCaptures.Contains(rookSquare))
                        _legalCaptures.Add(rookSquare);
                }
            }

            _highlighter.SetLegalMoves(_legalDots);
            _highlighter.SetLegalCaptures(_legalCaptures);
        }
    }

    private void UpdatePremoveHighlight()
    {
        if (_highlighter == null) return;

        if (_hasPremove)
            _highlighter.SetPremove(_preFromFile, _preFromRank, _preToFile, _preToRank);
        else
            _highlighter.ClearPremove();
    }

    private bool IsSelectable(int file, int rank)
    {
        if (!_boardView.IsAtLive) return false;   // reviewing history; nothing is selectable

        BoardState board = _boardView.CurrentBoard;
        if (board == null) return false;

        Piece piece = board.At(file, rank);
        return !piece.IsEmpty && _session.IsMyPiece(piece.Color);
    }

    // Click king -> click own rook = castle intent, ONLY if that castle is currently legal
    // Otherwise, simply reselect rook
    private bool TryCastleTarget(BoardState board, int fromFile, int fromRank,
                                 int toFile, int toRank, out int kingDestFile)
    {
        kingDestFile = -1;

        Piece from = board.At(fromFile, fromRank);
        Piece to = board.At(toFile, toRank);

        if (from.Type != PieceType.King) return false;
        if (to.IsEmpty || to.Color != from.Color || to.Type != PieceType.Rook) return false;
        if (toRank != fromRank || (toFile != 0 && toFile != 7)) return false;

        var kingSquare = new Square(fromFile, fromRank);
        var target = new Square(toFile > fromFile ? 6 : 2, fromRank);

        foreach (Move m in board.LegalFrom(kingSquare))
            if (m.To == target) { kingDestFile = target.File; return true; }

        return false;
    }

}