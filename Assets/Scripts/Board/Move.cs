using System;

// A parsed move intent: where from, where to, and (if promotion) into what
// (Promotion == PieceType.None means "not a promotion")
public readonly struct Move : IEquatable<Move>
{
    public readonly Square From;
    public readonly Square To;
    public readonly PieceType Promotion;

    public Move(Square from, Square to, PieceType promotion = PieceType.None)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    // Syntactic validation only
    public static Move FromUci(string uci)
    {
        if (uci == null || (uci.Length != 4 && uci.Length != 5))
            throw new ArgumentException($"Malformed UCI move: '{uci}'");

        var from = new Square(uci.Substring(0, 2));
        var to = new Square(uci.Substring(2, 2));
        PieceType promotion = uci.Length == 5 ? PromotionFromChar(uci[4]) : PieceType.None;

        return new Move(from, to, promotion);
    }

    public string ToUci()
    {
        string s = From.ToString() + To.ToString();
        if (Promotion != PieceType.None)
            s += PromotionToChar(Promotion);
        return s;
    }

    private static PieceType PromotionFromChar(char c) => c switch
    {
        'q' => PieceType.Queen,
        'r' => PieceType.Rook,
        'b' => PieceType.Bishop,
        'n' => PieceType.Knight,
        _ => throw new ArgumentException($"Bad promotion piece '{c}'"),
    };

    private static char PromotionToChar(PieceType type) => type switch
    {
        PieceType.Queen => 'q',
        PieceType.Rook => 'r',
        PieceType.Bishop => 'b',
        PieceType.Knight => 'n',
        _ => throw new ArgumentException($"Not a promotion piece: {type}"),
    };

    public bool Equals(Move other) =>
        From == other.From && To == other.To && Promotion == other.Promotion;
    public override bool Equals(object obj) => obj is Move m && Equals(m);
    public override int GetHashCode() =>
        (From.GetHashCode() * 64 + To.GetHashCode()) * 8 + (int)Promotion;
    public static bool operator ==(Move a, Move b) => a.Equals(b);
    public static bool operator !=(Move a, Move b) => !a.Equals(b);

    public override string ToString() => ToUci();
}