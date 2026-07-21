using System;

public class BoardState
{
    // Squares indexed [file, rank] 0–7.
    //   file 0 = a, file 7 = h
    //   rank 0 = 1, rank 7 = 8
    // Ex: square "e2" is _squares[4, 1].
    private readonly Piece[,] _squares = new Piece[8, 8];

    public PieceColor ActiveColor { get; private set; }
    public CastlingRights Castling { get; private set; }
    public Square? EnPassantTarget { get; private set; }  //square a pawn may capture ONTO; usually null

    // Whose move it is after # of moves (e.g. e2e4) from start
    // 0 moves = game start = White to move; 1 = Black's turn, and so on
    public static PieceColor SideToMove(int moves) =>
        (moves % 2 == 0) ? PieceColor.White : PieceColor.Black;

    public Piece At(int file, int rank) => _squares[file, rank];
    public Piece At(Square square) => _squares[square.File, square.Rank];
    public Piece At(string square) => At(new Square(square));

    // Back-rank piece order, files a->h, Identical for both colors
    private static readonly PieceType[] BackRank =
    {
        PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
        PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook,
    };

    // Set board to starting layout
    public void Reset()
    {
        Array.Clear(_squares, 0, _squares.Length);

        for (int file = 0; file < 8; file++)
        {
            _squares[file, 0] = new Piece(BackRank[file], PieceColor.White); // rank 1
            _squares[file, 1] = new Piece(PieceType.Pawn, PieceColor.White); // rank 2
            _squares[file, 6] = new Piece(PieceType.Pawn, PieceColor.Black); // rank 7
            _squares[file, 7] = new Piece(BackRank[file], PieceColor.Black); // rank 8
        }

        ActiveColor = PieceColor.White;
        Castling = CastlingRights.All;
        EnPassantTarget = null;
    }

    public void ApplyUci(string move) => ApplyMove(Move.FromUci(move));

    public void ApplyMove(Move move)
    {
        int fromFile = move.From.File;
        int fromRank = move.From.Rank;
        int toFile = move.To.File;
        int toRank = move.To.Rank;

        Piece moving = _squares[fromFile, fromRank];
        if (moving.IsEmpty)
            throw new ArgumentException($"No piece on origin of '{move.ToUci()}' (desync?)");

        bool isCastle = moving.Type == PieceType.King && Math.Abs(toFile - fromFile) >= 2;

        bool isEnPassant = moving.Type == PieceType.Pawn
                           && fromFile != toFile
                           && _squares[toFile, toRank].IsEmpty;

        bool isPromotion = move.Promotion != PieceType.None;

        // position bookkeeping: depends only on the move, not on placement
        UpdateCastlingRights(moving, move.From, move.To);

        bool isDoublePush = moving.Type == PieceType.Pawn && Math.Abs(toRank - fromRank) == 2;
        EnPassantTarget = isDoublePush
            ? new Square(fromFile, (fromRank + toRank) / 2)   // the square skipped over
            : (Square?)null;                                  // any other move clears it

        ActiveColor = Opposite(ActiveColor);

        if (isCastle)
        {
            int rank = fromRank;                  // back rank: 0 White, 7 Black
            bool kingside = toFile > fromFile;

            int kingDestFile = kingside ? 6 : 2;  // g-file or c-file
            int rookFromFile = kingside ? 7 : 0;  // corner rook: h-file or a-file
            int rookDestFile = kingside ? 5 : 3;  // f-file or d-file

            _squares[fromFile, rank] = default;  // clear king origin (e-file)
            _squares[rookFromFile, rank] = default;  // clear rook origin (corner)
            _squares[kingDestFile, rank] = new Piece(PieceType.King, moving.Color);
            _squares[rookDestFile, rank] = new Piece(PieceType.Rook, moving.Color);
            return;
        }

        // Making non-castle move
        _squares[fromFile, fromRank] = default;   // lift the piece off its origin

        if (isEnPassant)
            // Captured pawn is on the moving pawn's rank, in the destination's file
            _squares[toFile, fromRank] = default;

        // Swap pawn in case of promotion, otherwise place moving piece on target
        _squares[toFile, toRank] = isPromotion
            ? new Piece(move.Promotion, moving.Color)
            : moving;
    }

    public static BoardState FromMoves(string moves)
    {
        var board = new BoardState();
        board.Reset();

        if (string.IsNullOrWhiteSpace(moves))
            return board;  // game just started -> starting position

        string[] tokens = moves.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string uci in tokens)
            board.ApplyUci(uci);

        return board;
    }

    private void UpdateCastlingRights(Piece moving, Square from, Square to)
    {
        // A king move (incl. castling) kills both of that color's rights
        if (moving.Type == PieceType.King)
            Castling &= ~(moving.Color == PieceColor.White
                ? CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside
                : CastlingRights.BlackKingside | CastlingRights.BlackQueenside);

        // A rook leaving a corner, OR anything capturing onto a corner, kills that
        // corner's right. Clearing an already-clear bit is a harmless no-op, which
        // is what makes this robust: we never have to ask "was that actually a rook?"
        Castling &= ~RightsForCorner(from);
        Castling &= ~RightsForCorner(to);
    }

    private static CastlingRights RightsForCorner(Square s)
    {
        if (s.Rank == 0 && s.File == 0) return CastlingRights.WhiteQueenside; // a1
        if (s.Rank == 0 && s.File == 7) return CastlingRights.WhiteKingside;  // h1
        if (s.Rank == 7 && s.File == 0) return CastlingRights.BlackQueenside; // a8
        if (s.Rank == 7 && s.File == 7) return CastlingRights.BlackKingside;  // h8
        return CastlingRights.None;
    }

    public static PieceColor Opposite(PieceColor c) =>
        c == PieceColor.White ? PieceColor.Black : PieceColor.White;

    public BoardState Clone()
    {
        var copy = new BoardState();
        Array.Copy(_squares, copy._squares, _squares.Length);
        copy.ActiveColor = ActiveColor;
        copy.Castling = Castling;
        copy.EnPassantTarget = EnPassantTarget;
        return copy;
    }
}
