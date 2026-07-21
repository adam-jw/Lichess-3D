using System;

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

[Flags]
public enum CastlingRights
{
    None = 0,
    WhiteKingside = 1,
    WhiteQueenside = 2,
    BlackKingside = 4,
    BlackQueenside = 8,
    All = WhiteKingside | WhiteQueenside | BlackKingside | BlackQueenside,
}