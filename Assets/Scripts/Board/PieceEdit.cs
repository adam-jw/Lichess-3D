using System;

// One primitive change to the set of piece GameObjects, emitted by
// BoardState.DescribeMove and consumed mechanically by BoardView
//   From  = the square this edit EMPTIES  (Move, Remove)
//   To    = the square this edit FILLS    (Move, Spawn)
//   Piece = what to create                (Spawn only)
// Read only the fields the Kind uses; the factories keep the others default
public enum PieceEditKind { Remove, Move, Spawn }

public readonly struct PieceEdit
{
    public readonly PieceEditKind Kind;
    public readonly Square From;
    public readonly Square To;
    public readonly Piece Piece;

    private PieceEdit(PieceEditKind kind, Square from, Square to, Piece piece)
    {
        Kind = kind;
        From = from;
        To = to;
        Piece = piece;
    }

    public static PieceEdit Remove(Square at) => new(PieceEditKind.Remove, at, default, default);
    public static PieceEdit Move(Square from, Square to) => new(PieceEditKind.Move, from, to, default);
    public static PieceEdit Spawn(Square at, Piece piece) => new(PieceEditKind.Spawn, default, at, piece);

    public override string ToString() => Kind switch
    {
        PieceEditKind.Remove => $"Remove {From}",
        PieceEditKind.Move => $"Move {From}->{To}",
        PieceEditKind.Spawn => $"Spawn {To} {Piece.Color}{Piece.Type}",
        _ => "?",
    };
}