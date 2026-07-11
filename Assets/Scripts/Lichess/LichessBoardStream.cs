using System.Runtime.CompilerServices;
using UnityEngine;

// Per-game stream: Starts when a game begins, carries moves,
// closes when the game ends. One of these runs per active game
public class LichessBoardStream : LichessStreamBase
{
    private string _gameId;
    private LichessClient _client;
    public event System.Action<string> OnMovesReceived;


    protected override void Awake()
    {
        base.Awake();
        _client = GetComponent<LichessClient>();    
    }
    
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
        var baseEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<LichessEventBase>(line);
        string moves;

        switch (baseEvent.type)
        {
            case "gameFull":
                var full = Newtonsoft.Json.JsonConvert.DeserializeObject<GameFullEvent>(line);
                if (full.initialFen != "startpos")
                {
                    Debug.LogError("Non-standard starting position not supported: " + full.initialFen);
                    return;
                }
                moves = full.state.moves;
                break;

            case "gameState":
                moves = Newtonsoft.Json.JsonConvert.DeserializeObject<GameStateEvent>(line).moves;
                break;

            default:   // chatLine, opponentGone, etc. nothing to render on board
                return;
        }

        OnMovesReceived?.Invoke(moves);
    }

    // Send a move in UCI format for current game
    public void SendMove(string uciMove)
    {
        string url = "https://lichess.org/api/board/game/" + _gameId + "/move/" + uciMove;

        WWWForm emptyForm = new WWWForm(); // endpoint reads gameId and move from the URL path, so no body to send

        StartCoroutine(_client.Post(url, emptyForm,
            onSuccess: response => Debug.Log("Move sent successfully: " + uciMove),
            onError: error => Debug.LogError("Move failed (" + uciMove + "): " + error)));
    }

    // =====================================================================================
    // TEMP SEND INPUT TEST, TO BE REMOVED LATER: press key to fire a hardcoded opening move
    // =====================================================================================
    protected override void Update()
    {
        base.Update();   // keep draining stream queue

        
        // W for white opening move
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("Test: sending d2d4");
            SendMove("d2d4");
        }
        // B for slav opening as black
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("Test: sending d7d5");
            SendMove("d7d5");
        }
        // C for caro kann opening as black
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("Test: sending c7c6");
            SendMove("c7c6");
        }
    }
}
