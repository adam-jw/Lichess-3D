using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;
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

    [Test]
    public void Attack_WhitePawnHitsDiagonals_NotForward()
    {
        // White pawn on e4. It attacks d5 and f5, but NOT e5 (the push).
        var b = BoardState.FromMoves("e2e4");
        Assert.IsTrue(b.IsAttacked(new Square("d5"), PieceColor.White));
        Assert.IsTrue(b.IsAttacked(new Square("f5"), PieceColor.White));
        Assert.IsFalse(b.IsAttacked(new Square("e5"), PieceColor.White)); // the discriminator
    }

    [Test]
    public void Attack_BlackPawnHitsDownward()
    {
        // Direction inverts: black pawn on d5 attacks c4 and e4 (toward rank 1).
        var b = BoardState.FromMoves("e2e4 d7d5");
        Assert.IsTrue(b.IsAttacked(new Square("e4"), PieceColor.Black));
        Assert.IsTrue(b.IsAttacked(new Square("c4"), PieceColor.Black));
    }

    [Test]
    public void Attack_KnightReachesLShape()
    {
        var b = BoardState.FromMoves("g1f3"); // knight to f3
        Assert.IsTrue(b.IsAttacked(new Square("e5"), PieceColor.White));
        Assert.IsTrue(b.IsAttacked(new Square("d4"), PieceColor.White));
        Assert.IsFalse(b.IsAttacked(new Square("f5"), PieceColor.White)); // not a knight jump from f3
    }

    [Test]
    public void Attack_SliderBlockedByOwnPiece()
    {
        // a-pawn to a4, rook up to a3. The rook's own pawn on a4 blocks the file:
        // a4 (the blocker's square) IS attacked, a5 beyond it is NOT
        var b = BoardState.FromMoves("a2a4 b7b6 a1a3");
        Assert.IsTrue(b.IsAttacked(new Square("a4"), PieceColor.White));  // attacks the blocker
        Assert.IsFalse(b.IsAttacked(new Square("a5"), PieceColor.White)); // blocked beyond it
    }

    [Test]
    public void Attack_BishopBlockedByEnemyPiece_StopsAtIt()
    {
        // f1 bishop's diagonal opens after e2e4. A black pawn on b5 sits on that
        // diagonal: b5 is attacked (capturable), a6 behind it is not
        var b = BoardState.FromMoves("e2e4 b7b5");
        Assert.IsTrue(b.IsAttacked(new Square("b5"), PieceColor.White));
        Assert.IsFalse(b.IsAttacked(new Square("a6"), PieceColor.White));
    }

    [Test]
    public void InCheck_FoolsMate_WhiteKingAttacked()
    {
        // 1. f3 e5 2. g4 Qh4# - the queen hits e1 down the emptied h4-e1 diagonal
        var b = BoardState.FromMoves("f2f3 e7e5 g2g4 d8h4");
        Assert.IsTrue(b.IsInCheck(PieceColor.White));
        Assert.IsFalse(b.IsInCheck(PieceColor.Black));
    }

    [Test]
    public void InCheck_StartingPosition_NeitherInCheck()
    {
        var b = BoardState.FromMoves("");
        Assert.IsFalse(b.IsInCheck(PieceColor.White));
        Assert.IsFalse(b.IsInCheck(PieceColor.Black));
    }

    private static HashSet<string> UciSet(List<Move> moves)
    {
        var set = new HashSet<string>();
        foreach (Move m in moves) set.Add(m.ToUci());
        return set;
    }

    [Test]
    public void Knight_CenterOfEmptyBoard_HasEightMoves()
    {
        var b = BoardState.FromFen("8/8/8/8/3N4/8/8/8 w - - 0 1");
        var moves = UciSet(b.PseudoLegalFrom(new Square("d4")));

        CollectionAssert.AreEquivalent(
            new[] { "d4e6", "d4f5", "d4f3", "d4e2", "d4c2", "d4b3", "d4b5", "d4c6" },
            moves);
    }

    [Test]
    public void Knight_InCorner_IsClippedToTwoMoves()
    {
        var b = BoardState.FromFen("8/8/8/8/8/8/8/N7 w - - 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "a1b3", "a1c2" },
            UciSet(b.PseudoLegalFrom(new Square("a1"))));
    }

    [Test]
    public void Rook_CapturesBlocker_ButNotBeyondIt()
    {
        // Rook a4, black pawn d4. Rank: b4, c4, d4(capture) -- and NOT e4+
        var b = BoardState.FromFen("8/8/8/8/R2p4/8/8/8 w - - 0 1");
        var moves = UciSet(b.PseudoLegalFrom(new Square("a4")));

        Assert.IsTrue(moves.Contains("a4d4"));   // capture the blocker
        Assert.IsFalse(moves.Contains("a4e4"));  // discriminator: stopped by it
        Assert.AreEqual(10, moves.Count);        // 4 up + 3 down + 3 along
    }

    //
    // Pawn tests
    //

    [Test]
    public void Pawn_OnStartRank_PushesOneOrTwo()
    {
        var b = BoardState.FromFen("8/8/8/8/8/8/4P3/8 w - - 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "e2e3", "e2e4" },
            UciSet(b.PseudoLegalFrom(new Square("e2"))));
    }

    [Test]
    public void Pawn_BlockedOnFirstSquare_CannotDoublePush()
    {
        // The nesting test: a piece on e3 kills the double push to e4 as well
        var b = BoardState.FromFen("8/8/8/8/8/4n3/4P3/8 w - - 0 1");
        Assert.IsEmpty(b.PseudoLegalFrom(new Square("e2")));
    }

    [Test]
    public void Pawn_CapturesDiagonally_AndPushesForward()
    {
        var b = BoardState.FromFen("8/8/8/3p1p2/4P3/8/8/8 w - - 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "e4e5", "e4d5", "e4f5" },
            UciSet(b.PseudoLegalFrom(new Square("e4"))));
    }

    [Test]
    public void Pawn_ReachingLastRank_ExpandsToFourPromotions()
    {
        var b = BoardState.FromFen("8/4P3/8/8/8/8/8/8 w - - 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "e7e8q", "e7e8r", "e7e8b", "e7e8n" },
            UciSet(b.PseudoLegalFrom(new Square("e7"))));
    }

    [Test]
    public void Pawn_EnPassantCapture_IsGeneratedInTheWindow()
    {
        // Black's d7d5 sets the target to d6; the white e5 pawn may take it
        var b = BoardState.FromMoves("e2e4 a7a6 e4e5 d7d5");
        CollectionAssert.AreEquivalent(
            new[] { "e5e6", "e5d6" },
            UciSet(b.PseudoLegalFrom(new Square("e5"))));
    }

    [Test]
    public void Pawn_EnPassantTargetOnWrongSide_IsIgnored()
    {
        // Contrived: en passant target e6 belongs to WHITE (it lands on rank 6)
        // Black pawn on d7 also has e6 as a diagonal, and must not be offered the capture
        var b = BoardState.FromFen("8/3p4/8/8/8/8/8/8 b - e6 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "d7d6", "d7d5" },
            UciSet(b.PseudoLegalFrom(new Square("d7"))));
    }

    //
    // Legal move tests
    //

    [Test]
    public void Legal_PinnedPieceCannotMove()
    {
        // Knight e2 shields the king from the e8 rook: every knight move is illegal
        var b = BoardState.FromFen("4r3/8/8/8/8/8/4N3/4K3 w - - 0 1");
        Assert.IsNotEmpty(b.PseudoLegalFrom(new Square("e2")));   // movement rules allow them
        Assert.IsEmpty(b.LegalFrom(new Square("e2")));            // legality does not
    }

    [Test]
    public void Legal_KingCannotStepIntoAttack()
    {
        // Rook d8 covers the whole d-file: d1 and d2 are off-limits
        var b = BoardState.FromFen("3r4/8/8/8/8/8/8/4K3 w - - 0 1");
        CollectionAssert.AreEquivalent(
            new[] { "e1e2", "e1f1", "e1f2" },
            UciSet(b.LegalFrom(new Square("e1"))));
    }

    [Test]
    public void Legal_KingCannotRetreatAlongTheCheckingRay()
    {
        // In check from e8. Stepping to e2 is still on the e-file
        var b = BoardState.FromFen("4r3/8/8/8/8/8/8/4K3 w - - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));

        Assert.IsFalse(moves.Contains("e1e2"));   // the discriminator
        CollectionAssert.AreEquivalent(new[] { "e1d1", "e1f1", "e1d2", "e1f2" }, moves);
    }

    [Test]
    public void Legal_EnPassantExposingKing_IsRejected()
    {
        // Rank 5: black rook a5, black pawn c5 (just double-pushed), white pawn d5,
        // white king h5. Taking en passant removes BOTH pawns from rank 5 and hands
        // the rook the king. The push to d6 stays legal
        var b = BoardState.FromFen("8/8/8/r1pP3K/8/8/8/8 w - c6 0 1");

        Assert.IsTrue(UciSet(b.PseudoLegalFrom(new Square("d5"))).Contains("d5c6"));
        CollectionAssert.AreEquivalent(
            new[] { "d5d6" },
            UciSet(b.LegalFrom(new Square("d5"))));
    }

    [Test]
    public void Legal_MustAddressCheck_OtherPiecesRestricted()
    {
        // White in check from e8; the a1 rook must interpose on e1 - it can't reach,
        // so it has no legal moves at all while the king is attacked
        var b = BoardState.FromFen("4r3/8/8/8/8/8/8/R3K3 w - - 0 1");
        Assert.IsEmpty(b.LegalFrom(new Square("a1")));
    }

    [Test]
    public void Checkmate_BackRank()
    {
        // Rook a8 checks along the 8th; f7/g7/h7 pawns block the king's own escape
        var b = BoardState.FromFen("R5k1/5ppp/8/8/8/8/8/6K1 b - - 0 1");
        Assert.IsTrue(b.IsCheckmate(PieceColor.Black));
        Assert.IsFalse(b.IsStalemate(PieceColor.Black));
    }

    [Test]
    public void Stalemate_NotInCheckButNoMoves()
    {
        var b = BoardState.FromFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        Assert.IsFalse(b.IsInCheck(PieceColor.Black));
        Assert.IsTrue(b.IsStalemate(PieceColor.Black));
        Assert.IsEmpty(b.LegalAll(PieceColor.Black));
    }

    //
    // Castling tests
    //

    [Test]
    public void Castle_BothSidesAvailableWhenClear()
    {
        var b = BoardState.FromFen("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));
        Assert.IsTrue(moves.Contains("e1g1"));
        Assert.IsTrue(moves.Contains("e1c1"));
    }

    [Test]
    public void Castle_BlockedByPieceInPath()
    {
        // Knight on b1 blocks queenside only.
        var b = BoardState.FromFen("4k3/8/8/8/8/8/8/RN2K2R w KQ - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));
        Assert.IsTrue(moves.Contains("e1g1"));
        Assert.IsFalse(moves.Contains("e1c1"));
    }

    [Test]
    public void Castle_QueensideAllowedWhenOnlyBFileAttacked()
    {
        // The discriminator for the b-file asymmetry: a black rook on b8 attacks b1,
        // which the KING never touches. Queenside castling stays legal.
        var b = BoardState.FromFen("1r2k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        Assert.IsTrue(UciSet(b.LegalFrom(new Square("e1"))).Contains("e1c1"));
    }

    [Test]
    public void Castle_BlockedWhenKingTransitAttacked()
    {
        // Rook on f8 attacks f1 -- the king's transit square. Kingside dies,
        // queenside is untouched.
        var b = BoardState.FromFen("5r2/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));
        Assert.IsFalse(moves.Contains("e1g1"));
        Assert.IsTrue(moves.Contains("e1c1"));
    }

    [Test]
    public void Castle_BlockedWhileInCheck()
    {
        var b = BoardState.FromFen("4r3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));
        Assert.IsFalse(moves.Contains("e1g1"));
        Assert.IsFalse(moves.Contains("e1c1"));
    }

    [Test]
    public void Castle_RightsRevokedAfterKingMoves()
    {
        var b = BoardState.FromMoves("e2e4 e7e5 e1e2 e8e7 e2e1 e7e8");
        Assert.IsEmpty(UciSet(b.LegalFrom(new Square("e1"))).Contains("e1g1")
            ? new[] { "leaked" } : new string[0]);
    }

    [Test]
    public void Castle_RightsPresentButRookMissing_IsRejected()
    {
        // Malformed FEN claims KQ with no rooks. Must not fabricate one.
        var b = BoardState.FromFen("4k3/8/8/8/8/8/8/4K3 w KQ - 0 1");
        var moves = UciSet(b.LegalFrom(new Square("e1")));
        Assert.IsFalse(moves.Contains("e1g1"));
        Assert.IsFalse(moves.Contains("e1c1"));
    }

    //
    // Perft tests
    //

    [Test]
    public void Perft_StartPosition_Depth1To3()
    {
        var b = BoardState.FromMoves("");
        Assert.AreEqual(20, BoardState.Perft(b, 1));
        Assert.AreEqual(400, BoardState.Perft(b, 2));
        Assert.AreEqual(8902, BoardState.Perft(b, 3));
    }

    [Test]
    public void Perft_StartPosition_Depth4()
    {
        Assert.AreEqual(197281, BoardState.Perft(BoardState.FromMoves(""), 4));
    }

    [Test]
    public void Perft_Kiwipete_Depth1To3()
    {
        // Dense with castling, en passant, and pins -- the position that catches
        // what the start position never exercises.
        var b = BoardState.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.AreEqual(48, BoardState.Perft(b, 1));
        Assert.AreEqual(2039, BoardState.Perft(b, 2));
        Assert.AreEqual(97862, BoardState.Perft(b, 3));
    }

    [Test, Explicit("Slow: minutes with clone-per-move. Run manually.")]
    public void Perft_StartPosition_Depth5()
    {
        Assert.AreEqual(4865609, BoardState.Perft(BoardState.FromMoves(""), 5));
    }

    [Test, Explicit("Slow. Run manually.")]
    public void Perft_Kiwipete_Depth4()
    {
        var b = BoardState.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.AreEqual(4085603, BoardState.Perft(b, 4));
    }

    [Test]
    public void Premove_OntoSquareOpponentJustBlocked_IsIllegal()
    {
        // You queue Rook a1-a8 while it's clear; Opponent drops a piece on a4
        var board = BoardState.FromFen("8/8/8/8/n7/8/8/R3K2R w - - 0 1");
        var premove = new Move(new Square("a1"), new Square("a8"));

        bool legal = false;
        foreach (Move m in board.LegalFrom(premove.From))
            if (m.To == premove.To) { legal = true; break; }

        Assert.IsFalse(legal);   // blocked at a4
    }


    //
    // DescribeMove / PieceEdit Tests
    //

    private static List<string> EditStrings(IReadOnlyList<PieceEdit> edits)
    {
        var list = new List<string>();
        foreach (PieceEdit e in edits) list.Add(e.ToString());
        return list;
    }

    //
    // DescribeMove: per-type expansions
    //

    [Test]
    public void Describe_Quiet_IsSingleMove()
    {
        var b = BoardState.FromMoves("");
        CollectionAssert.AreEqual(
            new[] { "Move e2->e4" },
            EditStrings(b.DescribeMove(Move.FromUci("e2e4"))));
    }

    [Test]
    public void Describe_Capture_RemovesVictimThenMoves()
    {
        // Order matters: if the Move ran first it would overwrite d5, then Remove
        // would destroy the ATTACKER. Remove-before-Move is the whole point.
        var b = BoardState.FromMoves("e2e4 d7d5");
        CollectionAssert.AreEqual(
            new[] { "Remove d5", "Move e4->d5" },
            EditStrings(b.DescribeMove(Move.FromUci("e4d5"))));
    }

    [Test]
    public void Describe_EnPassant_RemovesBypassedPawnNotDestination()
    {
        // The removed pawn is on d5, not the empty destination d6.
        var b = BoardState.FromMoves("e2e4 a7a6 e4e5 d7d5");
        CollectionAssert.AreEqual(
            new[] { "Remove d5", "Move e5->d6" },
            EditStrings(b.DescribeMove(Move.FromUci("e5d6"))));
    }

    [Test]
    public void Describe_CastleKingToRookNotation_ExpandsToKingAndRook()
    {
        // Token is e1h1, but the edits are the king to g1 and the rook to f1.
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6");
        CollectionAssert.AreEqual(
            new[] { "Move e1->g1", "Move h1->f1" },
            EditStrings(b.DescribeMove(Move.FromUci("e1h1"))));
    }

    [Test]
    public void Describe_CastleStandardNotation_ExpandsIdentically()
    {
        // Same edits from the e1g1 encoding: notation never leaks into the output.
        var b = BoardState.FromMoves("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6");
        CollectionAssert.AreEqual(
            new[] { "Move e1->g1", "Move h1->f1" },
            EditStrings(b.DescribeMove(Move.FromUci("e1g1"))));
    }

    [Test]
    public void Describe_CastleQueenside_MovesRookToDFile()
    {
        var b = BoardState.FromFen("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        CollectionAssert.AreEqual(
            new[] { "Move e1->c1", "Move a1->d1" },
            EditStrings(b.DescribeMove(Move.FromUci("e1a1"))));
    }

    [Test]
    public void Describe_Promotion_RemovesPawnThenSpawnsQueen()
    {
        // Identity breaks: the pawn object is destroyed, a queen is created.
        var b = BoardState.FromFen("8/4P3/8/8/8/8/8/8 w - - 0 1");
        CollectionAssert.AreEqual(
            new[] { "Remove e7", "Spawn e8 WhiteQueen" },
            EditStrings(b.DescribeMove(Move.FromUci("e7e8q"))));
    }

    [Test]
    public void Describe_CapturePromotion_RemovesVictimAndPawnThenSpawns()
    {
        var b = BoardState.FromMoves("h2h4 a7a5 h4h5 a5a4 h5h6 a4a3 h6g7 a3b2");
        CollectionAssert.AreEqual(
            new[] { "Remove h8", "Remove g7", "Spawn h8 WhiteQueen" },
            EditStrings(b.DescribeMove(Move.FromUci("g7h8q"))));
    }

    // DescribeMove: Applying the edit set the same way the view will must reproduce ApplyMove's placement
    [Test]
    public void Describe_MatchesApplyMove_AcrossMoveTypes()
    {
        AssertEditsMatchApplyMove_Game("e2e4 d7d5 e4d5");                              // quiet, capture
        AssertEditsMatchApplyMove_Game("e2e4 a7a6 e4e5 d7d5 e5d6");                    // en passant
        AssertEditsMatchApplyMove_Game("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1h1");          // castle (king-to-rook)
        AssertEditsMatchApplyMove_Game("e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1g1");          // castle (standard)
        AssertEditsMatchApplyMove_Game("h2h4 a7a5 h4h5 a5a4 h5h6 a4a3 h6g7 a3b2 g7h8q"); // capture-promotion

        // Single positions for the cases awkward to reach from the start.
        AssertEditsMatchApplyMove(BoardState.FromFen("8/4P3/8/8/8/8/8/8 w - - 0 1"),
                                  Move.FromUci("e7e8q"));                              // plain promotion
        AssertEditsMatchApplyMove(BoardState.FromFen("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1"),
                                  Move.FromUci("e1a1"));                               // queenside castle
    }

    private static void AssertEditsMatchApplyMove_Game(string moves)
    {
        var board = BoardState.FromMoves("");
        foreach (string uci in moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            Move move = Move.FromUci(uci);
            AssertEditsMatchApplyMove(board, move);  
            board.ApplyMove(move);                    
        }
    }

    private static void AssertEditsMatchApplyMove(BoardState pre, Move move)
    {
        // Apply the edits to a scratch placement exactly as the view's consume loop will
        Piece[,] scratch = new Piece[8, 8];
        for (int f = 0; f < 8; f++)
            for (int r = 0; r < 8; r++)
                scratch[f, r] = pre.At(f, r);

        foreach (PieceEdit e in pre.DescribeMove(move))
        {
            switch (e.Kind)
            {
                case PieceEditKind.Remove:
                    scratch[e.From.File, e.From.Rank] = default;
                    break;
                case PieceEditKind.Move:
                    Piece p = scratch[e.From.File, e.From.Rank];
                    scratch[e.From.File, e.From.Rank] = default;
                    scratch[e.To.File, e.To.Rank] = p;
                    break;
                case PieceEditKind.Spawn:
                    scratch[e.To.File, e.To.Rank] = e.Piece;
                    break;
            }
        }

        // ApplyMove on an independent clone
        BoardState post = pre.Clone();
        post.ApplyMove(move);

        for (int f = 0; f < 8; f++)
            for (int r = 0; r < 8; r++)
                Assert.AreEqual(post.At(f, r), scratch[f, r],
                    $"Mismatch at {new Square(f, r)} applying '{move.ToUci()}'");
    }
}