using System;
using System.Collections.Generic;
public partial class BoardState
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
        // Placement edits, computed against the PRE-move board
        IReadOnlyList<PieceEdit> edits = DescribeMove(move);

        Piece moving = _squares[move.From.File, move.From.Rank];   // non-empty: DescribeMove checked

        // Position metadata depends only on the mover + from/to
        UpdateCastlingRights(moving, move.From, move.To);

        bool isDoublePush = moving.Type == PieceType.Pawn
                            && Math.Abs(move.To.Rank - move.From.Rank) == 2;
        EnPassantTarget = isDoublePush
            ? new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2)  // the square skipped over
            : (Square?)null;                                                   // any other move clears it

        ActiveColor = Opposite(ActiveColor);

        // Placement: the same edits the view will consume
        foreach (PieceEdit e in edits)
            ApplyEdit(e);
    }

    // Applies one placement primitive to _squares 
    private void ApplyEdit(PieceEdit edit)
    {
        switch (edit.Kind)
        {
            case PieceEditKind.Remove:
                _squares[edit.From.File, edit.From.Rank] = default;
                break;
            case PieceEditKind.Move:
                Piece p = _squares[edit.From.File, edit.From.Rank];
                _squares[edit.From.File, edit.From.Rank] = default;
                _squares[edit.To.File, edit.To.Rank] = p;
                break;
            case PieceEditKind.Spawn:
                _squares[edit.To.File, edit.To.Rank] = edit.Piece;
                break;
        }
    }

    // Classifies 'move' against current (pre-move) placement and returns the
    // GameObject-level edits that transform the piece set into the post-move one
    public IReadOnlyList<PieceEdit> DescribeMove(Move move)
    {
        int fromFile = move.From.File;
        int fromRank = move.From.Rank;
        int toFile = move.To.File;
        int toRank = move.To.Rank;

        Piece moving = _squares[fromFile, fromRank];
        if (moving.IsEmpty)
            throw new ArgumentException($"No piece on origin of '{move.ToUci()}' (desync?)");

        var edits = new List<PieceEdit>(3);   // capture-promotion is the 3-edit maximum

        // Castle: two pieces move - Detected by the king crossing >= 2 files
        bool isCastle = moving.Type == PieceType.King && Math.Abs(toFile - fromFile) >= 2;
        if (isCastle)
        {
            bool kingside = toFile > fromFile;
            int rank = fromRank;
            int kingDestFile = kingside ? 6 : 2;   // g / c
            int rookFromFile = kingside ? 7 : 0;   // h / a 
            int rookDestFile = kingside ? 5 : 3;   // f / d

            edits.Add(PieceEdit.Move(move.From, new Square(kingDestFile, rank)));            // king first
            edits.Add(PieceEdit.Move(new Square(rookFromFile, rank), new Square(rookDestFile, rank)));
            return edits;
        }

        bool isEnPassant = moving.Type == PieceType.Pawn
                           && fromFile != toFile
                           && _squares[toFile, toRank].IsEmpty;
        bool isPromotion = move.Promotion != PieceType.None;

        // A normal capture is an occupied destination; En passant's capture is NOT
        bool capturesOnTo = !isEnPassant && !_squares[toFile, toRank].IsEmpty;

        // Removes first, so nothing a later Move/Spawn writes gets clobbered
        if (capturesOnTo)
            edits.Add(PieceEdit.Remove(move.To));                        // captured piece
        if (isEnPassant)
            edits.Add(PieceEdit.Remove(new Square(toFile, fromRank)));   // the bypassed pawn (e.g. d5)
        if (isPromotion)
            edits.Add(PieceEdit.Remove(move.From));                      // pawn promotion

        // Then fill 'to': Spawn for a promotion (new mesh), Move otherwise
        if (isPromotion)
            edits.Add(PieceEdit.Spawn(move.To, new Piece(move.Promotion, moving.Color)));
        else
            edits.Add(PieceEdit.Move(move.From, move.To));

        return edits;
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

    public static BoardState FromFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException("Empty FEN");

        string[] fields = fen.Split(' ');
        if (fields.Length < 4)
            throw new ArgumentException($"FEN needs at least 4 fields: '{fen}'");

        var board = new BoardState();   // array defaults to empty

        // Field 1: placement, listed rank 8 first, each rank a->h.
        string[] ranks = fields[0].Split('/');
        if (ranks.Length != 8)
            throw new ArgumentException($"FEN placement needs 8 ranks: '{fields[0]}'");

        for (int i = 0; i < 8; i++)
        {
            int rank = 7 - i;           // FEN's first rank string is rank 8 -> internal rank 7
            int file = 0;
            foreach (char c in ranks[i])
            {
                if (char.IsDigit(c))
                    file += c - '0';    // run of empty squares
                else
                {
                    if (file > 7)
                        throw new ArgumentException($"FEN rank overflows: '{ranks[i]}'");
                    board._squares[file, rank] = PieceFromFenChar(c);
                    file++;
                }
            }
            if (file != 8)
                throw new ArgumentException($"FEN rank wrong length: '{ranks[i]}'");
        }

        // Field 2: active color
        board.ActiveColor = fields[1] == "b" ? PieceColor.Black : PieceColor.White;

        // Field 3: castling rights
        board.Castling = CastlingRights.None;
        if (fields[2] != "-")
            foreach (char c in fields[2])
                board.Castling |= c switch
                {
                    'K' => CastlingRights.WhiteKingside,
                    'Q' => CastlingRights.WhiteQueenside,
                    'k' => CastlingRights.BlackKingside,
                    'q' => CastlingRights.BlackQueenside,
                    _ => throw new ArgumentException($"Bad castling char '{c}'"),
                };

        // Field 4: en passant target square, or "-"
        board.EnPassantTarget = fields[3] == "-" ? (Square?)null : new Square(fields[3]);

        // Fields 5-6 (halfmove clock, fullmove number) ignored
        return board;
    }

    private static Piece PieceFromFenChar(char c)
    {
        PieceColor color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
        PieceType type = char.ToLower(c) switch
        {
            'p' => PieceType.Pawn,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => throw new ArgumentException($"Bad FEN piece char '{c}'"),
        };
        return new Piece(type, color);
    }

    // Whether 'target' is attacked by any piece of color 'byColor' 
    // Irrelevant of occupant of 'target'
    public bool IsAttacked(Square target, PieceColor byColor)
    {
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Piece p = _squares[file, rank];
                if (p.IsEmpty || p.Color != byColor) continue;
                if (AttacksSquare(new Square(file, rank), p, target)) return true;
            }
        return false;
    }

    private bool AttacksSquare(Square from, Piece piece, Square target)
    {
        int df = target.File - from.File;
        int dr = target.Rank - from.Rank;

        switch (piece.Type)
        {
            case PieceType.Pawn:
                // Attacks the two forward diagonals ONLY. The push is not an attack.
                int forward = piece.Color == PieceColor.White ? 1 : -1;
                return dr == forward && Math.Abs(df) == 1;

            case PieceType.Knight:
                int adf = Math.Abs(df), adr = Math.Abs(dr);
                return (adf == 1 && adr == 2) || (adf == 2 && adr == 1);

            case PieceType.King:
                return Math.Max(Math.Abs(df), Math.Abs(dr)) == 1;

            case PieceType.Bishop:
                return Math.Abs(df) == Math.Abs(dr) && df != 0 && PathClear(from, target);

            case PieceType.Rook:
                return (df == 0) != (dr == 0) && PathClear(from, target);

            case PieceType.Queen:
                bool diagonal = Math.Abs(df) == Math.Abs(dr) && df != 0;
                bool straight = (df == 0) != (dr == 0);
                return (diagonal || straight) && PathClear(from, target);

            default:
                return false;
        }
    }

    // Whether squares between (not including) from and to are empty 
    // Assumes the two are aligned (diagonal or orthogonal)
    private bool PathClear(Square from, Square to)
    {
        int stepF = Math.Sign(to.File - from.File);
        int stepR = Math.Sign(to.Rank - from.Rank);

        int f = from.File + stepF;
        int r = from.Rank + stepR;
        while (f != to.File || r != to.Rank)
        {
            if (!_squares[f, r].IsEmpty) return false;
            f += stepF;
            r += stepR;
        }
        return true;
    }

    public Square? FindKing(PieceColor color)
    {
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                Piece p = _squares[file, rank];
                if (p.Type == PieceType.King && p.Color == color)
                    return new Square(file, rank);
            }
        return null;
    }

    public bool IsInCheck(PieceColor color)
    {
        Square? king = FindKing(color);
        return king.HasValue && IsAttacked(king.Value, Opposite(color));
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
