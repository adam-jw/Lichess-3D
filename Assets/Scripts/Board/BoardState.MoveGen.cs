using System.Collections.Generic;

public partial class BoardState
{
    private static readonly (int df, int dr)[] RookDirs =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };
    private static readonly (int df, int dr)[] BishopDirs =
        { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private static readonly (int df, int dr)[] AllDirs =      // queen and king
        { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private static readonly (int df, int dr)[] KnightOffsets =
        { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };
    
    // Castling
    private static readonly int[] KingsideEmpty = { 5, 6 };      // f, g
    private static readonly int[] KingsideSafe = { 5, 6 };      // f, g
    private static readonly int[] QueensideEmpty = { 1, 2, 3 };   // b, c, d
    private static readonly int[] QueensideSafe = { 2, 3 };      // c, d; b doesn't need to be safe

    private static readonly int[] PawnCaptureFiles = { -1, 1 };
    private static readonly PieceType[] PromotionChoices =
        { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

    // Moves for the piece on 'from' by movement rules alone
    public List<Move> PseudoLegalFrom(Square from)
    {
        var moves = new List<Move>();
        GenerateFrom(from, moves);
        return moves;
    }

    public List<Move> PseudoLegalAll(PieceColor color)
    {
        var moves = new List<Move>();
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Piece p = _squares[file, rank];
                if (p.IsEmpty || p.Color != color) continue;
                GenerateFrom(new Square(file, rank), moves);
            }
        return moves;
    }

    private void GenerateFrom(Square from, List<Move> moves)
    {
        Piece piece = _squares[from.File, from.Rank];
        if (piece.IsEmpty) return;

        switch (piece.Type)
        {
            case PieceType.Pawn: GeneratePawn(from, piece.Color, moves); break;
            case PieceType.Knight: GenerateSteps(from, piece.Color, KnightOffsets, moves); break;
            case PieceType.King:
                GenerateSteps(from, piece.Color, AllDirs, moves);
                GenerateCastling(from, piece.Color, moves);
                break;
            case PieceType.Bishop: GenerateSlides(from, piece.Color, BishopDirs, moves); break;
            case PieceType.Rook: GenerateSlides(from, piece.Color, RookDirs, moves); break;
            case PieceType.Queen: GenerateSlides(from, piece.Color, AllDirs, moves); break;
        }
    }

    // Ride each direction until something stops us
    private void GenerateSlides(Square from, PieceColor color, (int df, int dr)[] dirs, List<Move> moves)
    {
        foreach ((int df, int dr) in dirs)
        {
            int f = from.File + df, r = from.Rank + dr;
            while (Square.IsValid(f, r))
            {
                Piece occupant = _squares[f, r];
                if (occupant.IsEmpty)
                {
                    moves.Add(new Move(from, new Square(f, r)));
                }
                else
                {
                    if (occupant.Color != color)
                        moves.Add(new Move(from, new Square(f, r)));  // capture is legal
                    break;                                            // blocked either way
                }
                f += df; r += dr;
            }
        }
    }

    // One hop per offset: knights and the king
    private void GenerateSteps(Square from, PieceColor color, (int df, int dr)[] offsets, List<Move> moves)
    {
        foreach ((int df, int dr) in offsets)
        {
            int f = from.File + df, r = from.Rank + dr;
            if (!Square.IsValid(f, r)) continue;

            Piece occupant = _squares[f, r];
            if (occupant.IsEmpty || occupant.Color != color)
                moves.Add(new Move(from, new Square(f, r)));
        }
    }

    private void GeneratePawn(Square from, PieceColor color, List<Move> moves)
    {
        bool white = color == PieceColor.White;
        int forward = white ? 1 : -1;
        int startRank = white ? 1 : 6;
        int epRank = white ? 5 : 2;      // the rank an en-passant capture LANDS on
        int oneRank = from.Rank + forward;

        if (!Square.IsValid(from.File, oneRank))
            return;                      // unpromoted pawn on the last rank: impossible

        // Push, and the double push nested inside it -- the single square must be
        // empty too, which is what makes "jumping over a blocker" impossible
        if (_squares[from.File, oneRank].IsEmpty)
        {
            AddPawnMove(from, new Square(from.File, oneRank), color, moves);

            int twoRank = from.Rank + 2 * forward;   // always on-board from startRank
            if (from.Rank == startRank && _squares[from.File, twoRank].IsEmpty)
                moves.Add(new Move(from, new Square(from.File, twoRank)));  // never a promotion
        }

        foreach (int df in PawnCaptureFiles)
        {
            int f = from.File + df;
            if (!Square.IsValid(f, oneRank)) continue;

            var target = new Square(f, oneRank);
            Piece occupant = _squares[f, oneRank];

            if (!occupant.IsEmpty)
            {
                if (occupant.Color != color)
                    AddPawnMove(from, target, color, moves);   // ordinary capture
            }
            else if (EnPassantTarget.HasValue
                     && EnPassantTarget.Value == target
                     && target.Rank == epRank)                 // see below
            {
                moves.Add(new Move(from, target));             // never a promotion
            }
        }
    }

    // A pawn arriving on the last rank becomes four distinct moves, not one
    private static void AddPawnMove(Square from, Square to, PieceColor color, List<Move> moves)
    {
        int lastRank = color == PieceColor.White ? 7 : 0;
        if (to.Rank == lastRank)
            foreach (PieceType promo in PromotionChoices)
                moves.Add(new Move(from, to, promo));
        else
            moves.Add(new Move(from, to));
    }

    // Legal moves for the piece on 'from'
    // Filters moves that place own king under attack
    public List<Move> LegalFrom(Square from)
    {
        var result = new List<Move>();
        Piece piece = _squares[from.File, from.Rank];
        if (piece.IsEmpty) return result;

        foreach (Move move in PseudoLegalFrom(from))
            if (!LeavesKingAttacked(move, piece.Color))
                result.Add(move);

        return result;
    }

    public List<Move> LegalAll(PieceColor color)
    {
        var result = new List<Move>();
        foreach (Move move in PseudoLegalAll(color))
            if (!LeavesKingAttacked(move, color))
                result.Add(move);
        return result;
    }

    // Play the move on a copy, then ask whether our king stands attacked
    private bool LeavesKingAttacked(Move move, PieceColor color)
    {
        BoardState after = Clone();
        after.ApplyMove(move);

        Square? king = after.FindKing(color);
        if (!king.HasValue) return false;

        return after.IsAttacked(king.Value, Opposite(color));
    }

    public bool IsCheckmate(PieceColor color) => IsInCheck(color) && LegalAll(color).Count == 0;
    public bool IsStalemate(PieceColor color) => !IsInCheck(color) && LegalAll(color).Count == 0;

    // Castling
    private void GenerateCastling(Square from, PieceColor color, List<Move> moves)
    {
        int rank = color == PieceColor.White ? 0 : 7;
        if (from.File != 4 || from.Rank != rank) return;   // king not on its home square

        if (IsAttacked(from, Opposite(color))) return;     // cannot castle out of check

        bool white = color == PieceColor.White;

        if (Castling.HasFlag(white ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside)
            && CanCastle(rank, color, 7, KingsideEmpty, KingsideSafe))
            moves.Add(new Move(from, new Square(6, rank)));

        if (Castling.HasFlag(white ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside)
            && CanCastle(rank, color, 0, QueensideEmpty, QueensideSafe))
            moves.Add(new Move(from, new Square(2, rank)));
    }

    private bool CanCastle(int rank, PieceColor color, int rookFile, int[] emptyFiles, int[] safeFiles)
    {
        Piece rook = _squares[rookFile, rank];
        if (rook.Type != PieceType.Rook || rook.Color != color) return false;

        foreach (int f in emptyFiles)
            if (!_squares[f, rank].IsEmpty) return false;

        PieceColor enemy = Opposite(color);
        foreach (int f in safeFiles)
            if (IsAttacked(new Square(f, rank), enemy)) return false;

        return true;
    }

    // Perft
    public static long Perft(BoardState board, int depth)
    {
        if (depth == 0) return 1;

        List<Move> moves = board.LegalAll(board.ActiveColor);
        if (depth == 1) return moves.Count;   // leaf shortcut: no need to apply them

        long nodes = 0;
        foreach (Move move in moves)
        {
            BoardState next = board.Clone();
            next.ApplyMove(move);
            nodes += Perft(next, depth - 1);
        }
        return nodes;
    }

    // Per-root-move breakdown; debugging tool
    public static Dictionary<string, long> PerftDivide(BoardState board, int depth)
    {
        var result = new Dictionary<string, long>();
        foreach (Move move in board.LegalAll(board.ActiveColor))
        {
            BoardState next = board.Clone();
            next.ApplyMove(move);
            result[move.ToUci()] = Perft(next, depth - 1);
        }
        return result;
    }
}