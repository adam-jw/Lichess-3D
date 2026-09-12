// Turns a finished game into Lichess's two-part result phrasing:
//
// "Black resigned" ("reason")  +  "White is victorious" ("verdict")
//
// Status strings are GameStatusName from the Lichess API spec:
//   created, started, aborted, mate, resign, stalemate, timeout, draw,
//   outoftime, cheat, noStart, unknownFinish, insufficientMaterialClaim, variantEnd
public static class GameResultText
{
    // Names the loser where Lichess does, since "Black resigned" reads
    // better than "White won by resignation".
    public static string Reason(GameSnapshot snap)
    {
        if (snap == null || !snap.IsFinished) return "";

        // Stream died without a verdict; the game may still be live on Lichess
        if (snap.EndReason == GameEndReason.ConnectionLost && !GameStatus.IsTerminal(snap.FinalStatus))
            return "Connection lost";

        string loser = LoserName(snap);

        switch (snap.FinalStatus)
        {
            case "mate": return "Checkmate";
            case "stalemate": return "Stalemate";
            case "insufficientMaterialClaim": return "Insufficient material";
            case "draw": return "Draw agreed";
            case "aborted": return "Game aborted";
            case "cheat": return "Cheat detected";
            case "variantEnd": return "Variant ending";

            // 'resign' and 'outoftime' always have a winner, so the loser is known
            // Null guard is for potential malformed payloads
            case "resign": return loser == null ? "Resignation" : loser + " resigned";
            case "outoftime": return loser == null ? "Out of time" : loser + " time out";

            // NOT the same as 'outoftime'; This is a player abandoning the game
            // (id 33 vs 35 in the spec) a flag fall is 'outoftime'
            case "timeout": return loser == null ? "Abandoned" : loser + " left the game";

            case "noStart": return loser == null ? "Game aborted" : loser + " didn't start";

            case "unknownFinish": return "Game ended";
            default: return "Game ended";
        }
    }

    // Who got the point; Personal phrasing ("You win") or Lichess's neutral colour
    // phrasing ("White is victorious").
    public static string Verdict(GameSnapshot snap, bool personal)
    {
        if (snap == null || !snap.IsFinished) return "";

        switch (snap.Outcome)
        {
            case GameOutcome.Win:
                return personal ? "You win" : ColorName(snap.MyColor) + " is victorious";

            case GameOutcome.Loss:
                return personal ? "You lose" : ColorName(snap.OpponentColor) + " is victorious";

            case GameOutcome.Draw:
                return "Draw";

            // An abort is not a result; no point, no rating change
            case GameOutcome.Aborted:
                return "No result";

            case GameOutcome.Unknown:
                return "Result unknown";

            default:
                return "";
        }
    }

    // Both parts on one line, for a single-label layout
    public static string Combined(GameSnapshot snap, bool personal, string separator = " • ")
    {
        string reason = Reason(snap);
        string verdict = Verdict(snap, personal);

        if (string.IsNullOrEmpty(reason)) return verdict;
        if (string.IsNullOrEmpty(verdict)) return reason;

        return reason + separator + verdict;
    }

    // The side that did NOT win. Null on a draw, an abort, or an unknown result
    private static string LoserName(GameSnapshot snap) =>
        snap.Winner.HasValue ? ColorName(Opposite(snap.Winner.Value)) : null;

    private static PieceColor Opposite(PieceColor color) =>
        color == PieceColor.White ? PieceColor.Black : PieceColor.White;

    private static string ColorName(PieceColor color) =>
        color == PieceColor.White ? "White" : "Black";
}