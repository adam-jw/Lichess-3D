using UnityEngine;

// Account-level event stream: Starts on login, reports game starts/finishes
public class LichessEventStream : LichessStreamBase
{
    private LichessBoardStream _boardStream;
    protected override void Awake()
    {
        base.Awake();   // run base Awake first to set up _authManager
        _boardStream = GetComponent<LichessBoardStream>();
        _authManager.OnAuthenticated += StartStream;
    }

    protected override void OnDestroy()
    {
        if (_authManager != null)
            _authManager.OnAuthenticated -= StartStream;
        base.OnDestroy();   // let base stop the thread
    }

    protected override string GetStreamUrl()
    {
        return "https://lichess.org/api/stream/event";
    }

    protected override void HandleLine(string line)
    {
        var baseEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<LichessEventBase>(line);

        switch (baseEvent.type)
        {
            case "gameStart":
                {
                    var gameEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<GameEvent>(line);
                    Debug.Log("Game started, opening board stream: " + gameEvent.game.gameId);
                    _boardStream.BeginGame(gameEvent.game.gameId);
                    break;
                }

            case "gameFinish":
                {
                    var gameEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<GameEvent>(line);
                    Debug.Log("Game finished: " + gameEvent.game.gameId);
                    break;
                }

            default:
                Debug.Log("Unhandled event type: " + baseEvent.type);
                break;
        }
    }
}