using System;

// A board coordinate. file 0-7 = a-h, rank 0-7 = 1-8.
public readonly struct Square : IEquatable<Square>
{
    public readonly int File;
    public readonly int Rank;

    public Square(int file, int rank)
    {
        File = file;
        Rank = rank;
    }

    // Parse e.g. "e4" and validates 
    public Square(string name)
    {
        if (name == null || name.Length != 2)
            throw new ArgumentException($"Malformed square: '{name}'");

        int file = name[0] - 'a';
        int rank = name[1] - '1';
        if (!IsValid(file, rank))
            throw new ArgumentException($"Square off the board: '{name}'");

        File = file;
        Rank = rank;
    }

    public static bool IsValid(int file, int rank) =>
        file >= 0 && file < 8 && rank >= 0 && rank < 8;

    public bool InBounds => IsValid(File, Rank);

    public override string ToString() => $"{(char)('a' + File)}{(char)('1' + Rank)}";

    // Value equality
    public bool Equals(Square other) => File == other.File && Rank == other.Rank;
    public override bool Equals(object obj) => obj is Square s && Equals(s);
    public override int GetHashCode() => File * 8 + Rank;   // perfect hash 0-63
    public static bool operator ==(Square a, Square b) => a.Equals(b);
    public static bool operator !=(Square a, Square b) => !a.Equals(b);
}