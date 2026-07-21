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
        // castle from a broken one is f1; the ROOK must have jumped there. A
        // naive "just move the king" implementation would leave f1 empty
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1h1");

        Assert.AreEqual(PieceType.King, b.At("g1").Type);
        Assert.AreEqual(PieceType.Rook, b.At("f1").Type);
        Assert.IsTrue(b.At("e1").IsEmpty);
        Assert.IsTrue(b.At("h1").IsEmpty);
    }

    [Test]
    public void Castle_StandardNotation_LandsIdentically()
    {
        // Same position, but the standard e1g1 encoding. Proves the
        // ">= 2 files" detection handles BOTH notations: 
        // same king, same rook, same four squares as prev. test
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
        // black pawn is on d5, not d6 so d5 must be empty
        var b = BoardState.FromMoves("e2e4 a7a6 e4e5 d7d5 e5d6");

        Assert.AreEqual(PieceType.Pawn, b.At("d6").Type);
        Assert.IsTrue(b.At("d5").IsEmpty); // the discriminating square
        Assert.IsTrue(b.At("e5").IsEmpty);
    }

    [Test]
    public void Promotion_SwapsPawnForChosenPiece()
    {
        // h-pawn marches up and captures the h8 rook while promoting -> g7h8q
        // If promotion were ignored, h8 would hold a pawn, not a queen
        var b = BoardState.FromMoves("h2h4 a7a5 h4h5 a5a4 h5h6 a4a3 h6g7 a3b2 g7h8q");

        Assert.AreEqual(PieceType.Queen, b.At("h8").Type); // not Pawn
        Assert.AreEqual(PieceColor.White, b.At("h8").Color);
        Assert.IsTrue(b.At("g7").IsEmpty);
    }
    [Test]
    public void SideToMove_StartsWhite_AlternatesByPly()
    {
        Assert.AreEqual(PieceColor.White, BoardState.SideToMove(0)); // start
        Assert.AreEqual(PieceColor.Black, BoardState.SideToMove(1)); // after 1. e4
        Assert.AreEqual(PieceColor.White, BoardState.SideToMove(2)); // after 1...c5
        Assert.AreEqual(PieceColor.Black, BoardState.SideToMove(3));
    }

    [Test]
    public void ActiveColor_AlternatesFromWhite()
    {
        Assert.AreEqual(PieceColor.White, BoardState.FromMoves("").ActiveColor);
        Assert.AreEqual(PieceColor.Black, BoardState.FromMoves("e2e4").ActiveColor);
        Assert.AreEqual(PieceColor.White, BoardState.FromMoves("e2e4 c7c5").ActiveColor);
    }

    [Test]
    public void EnPassant_TargetIsTheSkippedSquare_ThenClears()
    {
        // After e2e4, a black pawn on d4/f4 could capture onto e3 -> target is e3,
        // NOT e4 (where the pawn landed)
        var afterPush = BoardState.FromMoves("e2e4");
        Assert.AreEqual(new Square("e3"), afterPush.EnPassantTarget);

        // Any non-double-push clears it, including the opponent simply declining.
        var afterReply = BoardState.FromMoves("e2e4 a7a6");
        Assert.IsFalse(afterReply.EnPassantTarget.HasValue);
    }

    [Test]
    public void Castling_KingMoveClearsBothOfThatColor()
    {
        // King steps out and the two White rights are gone; Black's are untouched.
        var b = BoardState.FromMoves("e2e4 e7e5 e1e2");
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.WhiteKingside));
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.WhiteQueenside));
        Assert.IsTrue(b.Castling.HasFlag(CastlingRights.BlackKingside));
        Assert.IsTrue(b.Castling.HasFlag(CastlingRights.BlackQueenside));
    }

    [Test]
    public void Castling_RookMoveClearsOnlyThatCorner()
    {
        // h1 rook moves -> White loses kingside only; queenside survives.
        var b = BoardState.FromMoves("h2h4 h7h5 h1h3");
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.WhiteKingside));
        Assert.IsTrue(b.Castling.HasFlag(CastlingRights.WhiteQueenside));
    }

    [Test]
    public void Castling_RookCapturedOnCorner_ClearsVictimsRight()
    {
        // A white bishop captures the a8 rook. Black never moved that rook,
        // but its queenside right must die because the rook is gone
        var b = BoardState.FromMoves("b1c3 d7d5 c3d5 b8c6 d5b6 c8f5 b6a8");
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.BlackQueenside));
        Assert.IsTrue(b.Castling.HasFlag(CastlingRights.BlackKingside)); // untouched
    }

    [Test]
    public void Castling_CastlingItselfClearsBothRights()
    {
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1g1");
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.WhiteKingside));
        Assert.IsFalse(b.Castling.HasFlag(CastlingRights.WhiteQueenside));
    }

    [Test]
    public void Clone_IsIndependent()
    {
        var original = BoardState.FromMoves("e2e4 e7e5");
        var clone = original.Clone();

        // Mutating the clone must not touch the original 
        clone.ApplyUci("g1f3");

        Assert.AreEqual(PieceType.Knight, clone.At("f3").Type);
        Assert.IsTrue(original.At("f3").IsEmpty);          // original untouched
        Assert.AreEqual(PieceColor.White, original.ActiveColor); // incl. the new fields
        Assert.AreEqual(PieceColor.Black, clone.ActiveColor);
    }
}