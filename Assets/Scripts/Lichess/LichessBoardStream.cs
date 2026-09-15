using System.Runtime.CompilerServices;
using UnityEngine;
using System.Collections.Generic;

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

    public event System.Action<ChatLineEvent> OnChatLineReceived;

    public event System.Action<OpponentGoneEvent> OnOpponentGoneReceived;

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

            case "chatLine":
                var chat = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatLineEvent>(line);
                OnChatLineReceived?.Invoke(chat);
                return;

            case "opponentGone":
                var gone = Newtonsoft.Json.JsonConvert.DeserializeObject<OpponentGoneEvent>(line);
                OnOpponentGoneReceived?.Invoke(gone);
                return;

            default:   // opponentGone, etc. nothing handled yet
                return;
        }

        OnGameStateReceived?.Invoke(state);
    }

    // Send a move in UCI format for this game
    public void SendMove(string uciMove)
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/move/" + uciMove;

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Move sent successfully: " + uciMove),
            onError: error => Debug.LogError("Move failed (" + uciMove + "): " + error)));
    }

    // room is "player" (to the opponent) or "spectator"
    public void SendChat(string text, string room = "player")
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/chat";

        var fields = new Dictionary<string, string>
        {
            { "room", room },
            { "text", text },
        };

        StartCoroutine(_client.Post(url, fields,
            onSuccess: response => Debug.Log("Chat sent: " + text),
            onError: error => Debug.LogError("Chat failed (" + _gameId + "): " + error)));
    }

    // Resign this game. No turn requirement - valid on either side's move
    public void Resign()
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/resign";

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Resigned game " + _gameId),
            onError: error => Debug.LogError("Resign failed (" + _gameId + "): " + error)));
    }

    // Abort this game - only valid before move 2; resign otherwise
    public void Abort()
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/abort";

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Aborted game " + _gameId),
            onError: error => Debug.LogError("Abort failed (" + _gameId + "): " + error)));
    }

    // Draw offers share one endpoint: "yes" both offers and accepts, "no" declines
    public void Draw(bool accept)
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/draw/" + (accept ? "yes" : "no");

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Draw " + (accept ? "yes" : "no") + " sent for " + _gameId),
            onError: error => Debug.LogError("Draw failed (" + _gameId + "): " + error)));
    }

    // Claim the win once the opponent's been gone long enough; the server enforces the timer
    public void ClaimVictory()
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/claim-victory";

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Claimed victory in " + _gameId),
            onError: error => Debug.LogError("Claim victory failed (" + _gameId + "): " + error)));
    }

    // Claim a draw when the opponent's gone
    public void ClaimDraw()
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/claim-draw";

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Claimed draw in " + _gameId),
            onError: error => Debug.LogError("Claim draw failed (" + _gameId + "): " + error)));
    }

    // Same as draw: "yes" proposes or accepts, "no" declines
    public void Takeback(bool accept)
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/takeback/" + (accept ? "yes" : "no");

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Takeback " + (accept ? "yes" : "no") + " sent for " + _gameId),
            onError: error => Debug.LogError("Takeback failed (" + _gameId + "): " + error)));
    }

    // Gift time to the opponent's clock. Uses round API, not board,
    // and needs the challenge:write scope
    public void AddTime(int seconds)
    {
        string url = "https://lichess.org/api/round/" + _gameId + "/add-time/" + seconds;

        StartCoroutine(_client.Post(url, null,
            onSuccess: response => Debug.Log("Gifted " + seconds + "s to opponent in " + _gameId),
            onError: error => Debug.LogError("Add-time failed (" + _gameId + "): " + error)));
    }
}
