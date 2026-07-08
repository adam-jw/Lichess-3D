using UnityEngine;

// Account-level event stream: Starts on login, reports game starts/finishes
public class LichessEventStream : LichessStreamBase
{
    protected override void Awake()
    {
        base.Awake();   // run base Awake first to set up _authManager
        // Start streaming as soon as authentication completes
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
                    Debug.Log("gameEvent gameId: " + gameEvent.game.gameId);
                    Debug.Log("gameEvent Opponent Username: " + gameEvent.game.opponent.username);
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