using System;

public class BoardState
{
    // Squares indexed [file, rank] 0–7.
    //   file 0 = a, file 7 = h
    //   rank 0 = 1, rank 7 = 8
    // Ex: square "e2" is _squares[4, 1].
    private readonly Piece[,] _squares = new Piece[8, 8];

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
    }

    public void ApplyUci(string move)
    {
        if (move == null || (move.Length != 4 && move.Length != 5))
            throw new ArgumentException($"Malformed UCI move: '{move}'");

        int fromFile = move[0] - 'a';
        int fromRank = move[1] - '1';
        int toFile = move[2] - 'a';
        int toRank = move[3] - '1';

        if (!InBounds(fromFile) || !InBounds(fromRank) ||
            !InBounds(toFile) || !InBounds(toRank))
            throw new ArgumentException($"UCI move off the board: '{move}'");

        Piece moving = _squares[fromFile, fromRank];
        if (moving.IsEmpty)
            throw new ArgumentException($"No piece on origin of '{move}' (desync?)");

        bool isCastle = moving.Type == PieceType.King && Math.Abs(toFile - fromFile) >= 2;

        bool isEnPassant = moving.Type == PieceType.Pawn
                           && fromFile != toFile
                           && _squares[toFile, toRank].IsEmpty;

        bool isPromotion = move.Length == 5;    // e.g. e7e8q

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
            ? new Piece(PromotionFromChar(move[4]), moving.Color)
            : moving;
    }

    private static bool InBounds(int index) => index >= 0 && index < 8;

    private static PieceType PromotionFromChar(char c) => c switch
    {
        'q' => PieceType.Queen,
        'r' => PieceType.Rook,
        'b' => PieceType.Bishop,
        'n' => PieceType.Knight,
        _ => throw new ArgumentException($"Bad promotion piece '{c}'"),
    };
}
