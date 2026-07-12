using UnityEngine;

// Attached to every spawned piece to easily reference what square
// they are on; makes sending UCI moves from board interface easier
public class PieceRef : MonoBehaviour
{
    public int File;
    public int Rank;
    public PieceType Type;
    public PieceColor Color;  // Only let users affect their own pieces
}