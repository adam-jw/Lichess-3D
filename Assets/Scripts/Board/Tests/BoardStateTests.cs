using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

using NUnit.Framework;

public class BoardStateTests
{
    [Test]
    public void NormalMove_CaptureIsOverwrite()
    {
        // e4 pawn captures the d5 pawn. confirm
        // base case: a capture is a plain overwrite
        var b = BoardState.FromMoves("e2e4 d7d5 e4d5");

        var d5 = b.At("d5");
        Assert.AreEqual(PieceType.Pawn, d5.Type);
        Assert.AreEqual(PieceColor.White, d5.Color); // the WHITE pawn now sits where Black's was
        Assert.IsTrue(b.At("e4").IsEmpty);
        Assert.IsTrue(b.At("e2").IsEmpty);
    }

    [Test]
    public void Castle_KingToRookNotation()
    {
        // Lichess's own encoding: e1h1. The square that discriminates a correct
        // castle from a broken one is f1 — the ROOK must have jumped there. A
        // naive "just move the king" implementation would leave f1 empty.
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1h1");

        Assert.AreEqual(PieceType.King, b.At("g1").Type);
        Assert.AreEqual(PieceType.Rook, b.At("f1").Type); // the real test
        Assert.IsTrue(b.At("e1").IsEmpty);
        Assert.IsTrue(b.At("h1").IsEmpty);
    }

    [Test]
    public void Castle_StandardNotation_LandsIdentically()
    {
        // Same position, but the standard e1g1 encoding. This is what proves the
        // ">= 2 files" detection handles BOTH notations — same king, same rook,
        // same four squares as the test above.
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1g1");

        Assert.AreEqual(PieceType.King, b.At("g1").Type);
        Assert.AreEqual(PieceType.Rook, b.At("f1").Type);
        Assert.IsTrue(b.At("e1").IsEmpty);
        Assert.IsTrue(b.At("h1").IsEmpty);
    }

    [Test]
    public void EnPassant_RemovesTheBypassedPawn()
    {
        // White e5 pawn takes en passant onto the EMPTY d6 square. The captured
        // black pawn is on d5, not d6 — so d5-must-be-empty is the whole point.
        // Drop the en-passant branch and d5 stays occupied: this is the assert
        // that catches it.
        var b = BoardState.FromMoves("e2e4 a7a6 e4e5 d7d5 e5d6");

        Assert.AreEqual(PieceType.Pawn, b.At("d6").Type);
        Assert.IsTrue(b.At("d5").IsEmpty); // the discriminating square
        Assert.IsTrue(b.At("e5").IsEmpty);
    }

    [Test]
    public void Promotion_SwapsPawnForChosenPiece()
    {
        // A legal (if silly) pawn race: the h-pawn marches up and captures the
        // h8 rook while promoting -> g7h8q. If promotion were ignored, h8 would
        // hold a PAWN, not a QUEEN. That type check is the test.
        var b = BoardState.FromMoves("h2h4 a7a5 h4h5 a5a4 h5h6 a4a3 h6g7 a3b2 g7h8q");

        Assert.AreEqual(PieceType.Queen, b.At("h8").Type); // not Pawn
        Assert.AreEqual(PieceColor.White, b.At("h8").Color);
        Assert.IsTrue(b.At("g7").IsEmpty);
    }
}