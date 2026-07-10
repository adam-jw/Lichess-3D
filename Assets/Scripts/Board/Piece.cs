public enum PieceColor
{
    White,
    Black,
}

public enum PieceType
{
    None = 0, // empty square, default value
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King,
}

public readonly struct Piece
{
    public readonly PieceType Type;
    public readonly PieceColor Color;

    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }

    public bool IsEmpty => Type == PieceType.None;
}