using UnityEngine;

// How a game ended, from our client's point of view
public enum GameOutcome
{
    None,       // no game has finished yet
    Win,
    Loss,
    Draw,
    Aborted,    // game never started; no result recorded
    Unknown     // ended, but result is not knowable (dropped connection, unknownFinish)
}

// A durable record of the current or most recent game

// Owned by the session as a plain object (not MonoBehaviour) so every consumer that
// already has a _session reference gets it for free, with no extra wiring
// Never null: HasGame is false until the first game begins
public class GameSnapshot
{
    // ----- Identity -----
    public bool HasGame { get; private set; }        // a game has begun at some point
    public bool IsActive { get; private set; }       // that game is still being played
    public string GameId { get; private set; }
    public PieceColor MyColor { get; private set; }

    public PieceColor OpponentColor =>
        MyColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

    // ----- Game settings (authoritative once gameFull lands) -----
    public string Speed { get; private set; }        // "bullet" / "blitz" / "rapid" etc.
    public bool Rated { get; private set; }
    public bool HasClockSettings { get; private set; }
    public int InitialMs { get; private set; }
    public int IncrementMs { get; private set; }

    // ----- Players -----
    public string MyName { get; private set; }
    public int? MyRating { get; private set; }
    public bool MyProvisional { get; private set; }
    public string MyTitle { get; private set; }

    public string OpponentName { get; private set; }
    public int? OpponentRating { get; private set; }
    public bool OpponentProvisional { get; private set; }
    public string OpponentTitle { get; private set; }
    public int? OpponentAiLevel { get; private set; }   // present only for Lichess AI
    public bool OpponentIsAI => OpponentAiLevel.HasValue;

    // ----- Progress -----
    public int PlyCount { get; private set; }

    // ----- Result (populated at End) -----
    public GameEndReason? EndReason { get; private set; }
    public string FinalStatus { get; private set; }     // raw wire status; see GameStatusName in Lichess-API
    public PieceColor? Winner { get; private set; }     // null on draw, abort, or unknown
    public GameOutcome Outcome { get; private set; }

    public bool IsFinished => HasGame && !IsActive && EndReason.HasValue;

    // ----- Offers (derived from the latest state; re-sent every gameState) -----
    public bool OpponentOfferingDraw { get; private set; }
    public bool MyDrawOfferPending { get; private set; }
    public bool OpponentProposingTakeback { get; private set; }
    public bool MyTakebackPending { get; private set; }

    // ---------- Population ----------

    // Clears everything; The single reset point, mirroring the session's own HandleGameStart
    public void Begin(GameEventInfo game, PieceColor myColor)
    {
        HasGame = true;
        IsActive = true;
        GameId = game != null ? game.gameId : null;
        MyColor = myColor;

        Speed = null;
        Rated = false;
        HasClockSettings = false;
        InitialMs = 0;
        IncrementMs = 0;

        MyName = null;
        MyRating = null;
        MyProvisional = false;
        MyTitle = null;

        // gameStart gives us a username immediately; gameFull will refine it shortly
        OpponentName = game != null && game.opponent != null ? game.opponent.username : null;
        OpponentRating = game != null && game.opponent != null ? game.opponent.rating : null;
        OpponentProvisional = false;
        OpponentTitle = null;
        OpponentAiLevel = null;

        PlyCount = 0;

        OpponentOfferingDraw = false;
        MyDrawOfferPending = false;
        OpponentProposingTakeback = false;
        MyTakebackPending = false;

        EndReason = null;
        FinalStatus = null;
        Winner = null;
        Outcome = GameOutcome.None;
    }

    // Fires once per connection, including on every reconnect
    // Every assignment is a straight overwrite from server truth
    public void ApplyGameFull(GameFullEvent full)
    {
        if (!HasGame || full == null) return;

        Speed = full.speed;
        Rated = full.rated;

        if (full.clock != null)
        {
            HasClockSettings = true;
            InitialMs = full.clock.initial;
            IncrementMs = full.clock.increment;
        }
        else
        {
            HasClockSettings = false;   // correspondence: days per turn, no clock object
        }

        GamePlayer me = MyColor == PieceColor.White ? full.white : full.black;
        GamePlayer them = MyColor == PieceColor.White ? full.black : full.white;

        if (me != null)
        {
            if (!string.IsNullOrEmpty(me.name)) MyName = me.name;
            MyRating = me.rating;
            MyProvisional = me.IsProvisional;
            MyTitle = me.title;
        }

        if (them != null)
        {
            // gameFull uses "name" where gameStart's opponent object uses "username"
            if (!string.IsNullOrEmpty(them.name)) OpponentName = them.name;
            OpponentRating = them.rating;
            OpponentProvisional = them.IsProvisional;
            OpponentTitle = them.title;
            OpponentAiLevel = them.aiLevel;
        }
    }

    public void ApplyState(GameStateEvent state)
    {
        if (!HasGame || state == null) return;

        PlyCount = CountPlies(state.moves);

        OpponentOfferingDraw = OpponentColor == PieceColor.White ? state.wdraw : state.bdraw;
        MyDrawOfferPending = MyColor == PieceColor.White ? state.wdraw : state.bdraw;

        OpponentProposingTakeback = OpponentColor == PieceColor.White ? state.wtakeback : state.btakeback;
        MyTakebackPending = MyColor == PieceColor.White ? state.wtakeback : state.btakeback;
    }

    public void End(GameEndReason reason, string finalStatus, string winnerWire)
    {
        if (!HasGame) return;

        IsActive = false;
        EndReason = reason;
        FinalStatus = finalStatus;
        Winner = ParseColor(winnerWire);
        Outcome = ComputeOutcome(reason, finalStatus, Winner, MyColor);
    }

    // ---------- Result classification ----------

    private static GameOutcome ComputeOutcome(GameEndReason reason, string status,
                                              PieceColor? winner, PieceColor myColor)
    {
        // Stream died without the game ever resolving
        if (reason == GameEndReason.ConnectionLost && !GameStatus.IsTerminal(status))
            return GameOutcome.Unknown;

        switch (status)
        {
            // Never really started; Lichess records no result and no rating change
            case "aborted":
            case "noStart":
                return GameOutcome.Aborted;

            // Terminal, but the result is not known
            case "unknownFinish":
                return GameOutcome.Unknown;
        }

        if (winner.HasValue)
            return winner.Value == myColor ? GameOutcome.Win : GameOutcome.Loss;

        // Terminal with nobody winning: stalemate, draw, insufficientMaterialClaim,
        // and drawn variantEnd
        if (GameStatus.IsTerminal(status))
            return GameOutcome.Draw;

        return GameOutcome.Unknown;
    }

    private static PieceColor? ParseColor(string wire) => wire switch
    {
        "white" => PieceColor.White,
        "black" => PieceColor.Black,
        _ => null,
    };

    private static int CountPlies(string moves) =>
        string.IsNullOrWhiteSpace(moves)
            ? 0
            : moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
}