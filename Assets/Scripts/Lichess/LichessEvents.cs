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
}

// Mirrors GameFullEvent: first line of the board stream, nests a gameState
public class GameFullEvent
{
    public string type;        // "gameFull"
    public string initialFen;  // "startpos" for standard games
    public GameStateEvent state;
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

// Why a game stopped
public enum GameEndReason
{
    Finished,        // terminal status (or Lichess sent gameFinish)
    ConnectionLost   // stream closed without the game ever ending
}