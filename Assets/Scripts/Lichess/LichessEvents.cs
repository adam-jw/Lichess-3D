using UnityEngine;

// Data models for Lichess event-stream JSON

// First-pass parse: only reads type so we can branch on it
public class LichessEventBase
{
    public string type;
}

// The 'game' object carried by gameStart / gameFinish events
// Only modelling the fields we actually use for now
public class GameEventInfo
{
    public string gameId;
    public string fen;          // FEN board position
    public string color;        // "white" or "black"; which side we play
    public bool isMyTurn;
    public GameOpponent opponent;
    public GameStatusInfo status;   // populated on gameFinish
    public string winner;           // "white" / "black" / null (draw or aborted)
}

// Full second-pass shape for a gameStart or gameFinish event.
public class GameEvent
{
    public string type;
    public GameEventInfo game;
}

public class GameOpponent
{
    public string username;
    public int? rating;         // nullable; null == unrated
}

// Mirrors GameStateEvent: per-move line on the board stream
public class GameStateEvent
{
    public string type;      // "gameState"
    public string moves;     // full space-separated UCI list
    public string status;    // GameStatusName: "started", "mate", "resign", etc
    public string winner;    // "white" | "black" | null (no winner / not over)
    public int wtime;        // ms left on White's clock
    public int btime;        // ms left on Black's clock
    public int winc;         // White increment, ms
    public int binc;         // Black increment, ms

    // Offer flags; omitted on the wire when false, so an absent field stays false
    public bool wdraw;       // white is offering / has offered a draw
    public bool bdraw;       // black is offering / has offered a draw
    public bool wtakeback;   // white is proposing a takeback
    public bool btakeback;   // black is proposing a takeback
}

// Mirrors GameFullEvent: first line of the board stream, nests a gameState
public class GameFullEvent
{
    public string type;        // "gameFull"
    public string initialFen;  // "startpos" for standard games
    public string speed;       
    public bool rated;
    public GameClock clock;    // null for correspondence
    public GamePlayer white;
    public GamePlayer black;
    public GameStateEvent state;
}

// Clock settings, in MS
public class GameClock
{
    public int initial;
    public int increment;
}

public class GamePlayer
{
    public string id;
    public string name;
    public string title;        // "GM", "IM", ... or null
    public int? rating;         // absent for AI opponents
    public bool? provisional;   // absent means not provisional
    public int? aiLevel;        // present only for AI opponents

    public bool IsAI => aiLevel.HasValue;
    public bool IsProvisional => provisional == true;
}

// Whether Game is still going or not
public static class GameStatus
{
    public static bool IsTerminal(string status)
    {
        if (string.IsNullOrEmpty(status))
            return false;

        // Everything other than these two means the game is over, one way or another
        // e.g. (mate, resign, stalemate, timeout, draw, outoftime, aborted, etc)
        return status != "created" && status != "started";
    }
}

public class GameStatusInfo
{
    public int id;
    public string name;    // "resign", "mate", "outoftime", "draw", "aborted", etc.
}

// Why a game stopped
public enum GameEndReason
{
    Finished,        // terminal status (or Lichess sent gameFinish)
    ConnectionLost   // stream closed without the game ever ending
}

public class ChatLineEvent
{
    public string type;      // "chatLine"
    public string room;      // "player" or "spectator"
    public string username;
    public string text;
}

public class OpponentGoneEvent
{
    public string type;              // "opponentGone"
    public bool gone;
    public int? claimWinInSeconds;   // countdown until claimable; null when not gone
}