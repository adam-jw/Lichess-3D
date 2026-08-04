using UnityEngine;

// Per-game stream: one of these exists for exactly one game
// Created by LichessGameSession when a game starts, destroyed when it ends
public class LichessBoardStream : LichessStreamBase
{
    private string _gameId;
    private LichessClient _client;

    // Carries whole state line, not just the moves (status, clocks, etc)
    public event System.Action<GameStateEvent> OnGameStateReceived;

    // Fires on every connect, including reconnects
    public event System.Action<GameFullEvent> OnGameFullReceived;

    public string GameId => _gameId;

    public void Initialize(LichessAuthManager authManager, LichessClient client, string gameId)
    {
        _authManager = authManager;
        _client = client;
        _gameId = gameId;
    }

    protected override string GetStreamUrl()
    {
        return "https://lichess.org/api/board/game/stream/" + _gameId;
    }

    protected override void HandleLine(string line)
    {
        // DEBUG TOOL: TO BE REMOVED WHEN GAME RESULT ISSUE IS FIXED
        Debug.Log("[DIAG] board line: " + line);

        var baseEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<LichessEventBase>(line);
        GameStateEvent state;

        switch (baseEvent.type)
        {
            case "gameFull":
                var full = Newtonsoft.Json.JsonConvert.DeserializeObject<GameFullEvent>(line);

                if (full.initialFen != "startpos")
                {
                    Debug.LogError("Non-standard starting position not supported: " + full.initialFen);
                    return;
                }

                OnGameFullReceived?.Invoke(full);
                state = full.state;   // gameFull nests a gameState
                break;

            case "gameState":
                state = Newtonsoft.Json.JsonConvert.DeserializeObject<GameStateEvent>(line);
                break;

            default:   // chatLine, opponentGone, etc. nothing to render on board
                return;
        }

        OnGameStateReceived?.Invoke(state);
    }

    // Send a move in UCI format for this game
    public void SendMove(string uciMove)
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/move/" + uciMove;

        WWWForm emptyForm = new WWWForm(); // endpoint reads gameId and move from the URL path, so no body to send

        StartCoroutine(_client.Post(url, emptyForm,
            onSuccess: response => Debug.Log("Move sent successfully: " + uciMove),
            onError: error => Debug.LogError("Move failed (" + uciMove + "): " + error)));
    }
}
