using UnityEngine;

// Data models for Lichess event-stream JSON

// First-pass parse: only reads type so we can branch on it
public class LichessEventBase
{
    public string type;
}

// The 'game' object carried by gameStart / gameFinish events.
// Only modelling the fields we actually use for now.
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

// Mirrors GameStateEvent: per-move line
public class GameStateEvent
{
    public string type;    // "gameState"
    public string moves;   // full space-separated UCI list
}

// Mirrors GameFullEvent: first line of the board stream, nests a gameState
public class GameFullEvent
{
    public string type;        // "gameFull"
    public string initialFen;  // "startpos" for standard games
    public GameStateEvent state;
}