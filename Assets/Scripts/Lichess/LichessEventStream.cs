using UnityEngine;

// Account-level event stream: Starts on login, announces game starts/ends
public class LichessEventStream : LichessStreamBase
{
    public event System.Action<GameEventInfo> OnGameStart;
    public event System.Action<GameEventInfo> OnGameFinish;
    private void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
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
                    Debug.Log("Event: gameStart " + gameEvent.game.gameId);
                    OnGameStart?.Invoke(gameEvent.game);
                    break;
                }

            case "gameFinish":
                {
                    var gameEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<GameEvent>(line);
                    Debug.Log("Event: gameFinish " + gameEvent.game.gameId);
                    OnGameFinish?.Invoke(gameEvent.game);
                    break;
                }

            default:
                Debug.Log("Unhandled event type: " + baseEvent.type);
                break;
        }
    }
}