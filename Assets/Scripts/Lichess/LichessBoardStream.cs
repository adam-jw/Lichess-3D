using UnityEngine;

// Per-game stream: Starts when a game begins, carries moves,
// closes when the game ends. One of these runs per active game
public class LichessBoardStream : LichessStreamBase
{
    private string _gameId;

    public void BeginGame(string gameId)
    {
        _gameId = gameId;
        StartStream();
    }

    protected override string GetStreamUrl()
    {
        return "https://lichess.org/api/board/game/stream/" + _gameId;
    }

    protected override void HandleLine(string line)
    {
        Debug.Log("Board stream line: " + line);
    }
}
