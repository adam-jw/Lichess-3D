using System.Collections;
using UnityEngine;

// Account-level event stream: Starts on login, announces game starts/ends
public class LichessEventStream : LichessStreamBase
{
    [Header("Reconnect")]
    [Tooltip("First retry waits this long, then it doubles each attempt.")]
    [SerializeField] private float _baseDelaySeconds = 1f;

    [Tooltip("Backoff never waits longer than this between attempts.")]
    [SerializeField] private float _maxDelaySeconds = 8f;

    private StreamReconnector _reconnector;

    public event System.Action<GameEventInfo> OnGameStart;
    public event System.Action<GameEventInfo> OnGameFinish;

    // Raised when the auth token is rejected 
    public event System.Action OnAuthenticationLost;

    private void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
        _authManager.OnAuthenticated += HandleAuthenticated;
        OnStreamEnded += HandleStreamEnded;
        _reconnector = new StreamReconnector(this, StartStream, _baseDelaySeconds, _maxDelaySeconds);
    }

    protected override void OnDestroy()
    {
        if (_authManager != null)
            _authManager.OnAuthenticated -= HandleAuthenticated;
        OnStreamEnded -= HandleStreamEnded;
        _reconnector?.Cancel();
        base.OnDestroy();
    }

    private void HandleAuthenticated()
    {
        _reconnector.NotifyConnected();
        StartStream();
    }

    protected override string GetStreamUrl()
    {
        return "https://lichess.org/api/stream/event";
    }

    // ---------- EVENT STREAM SELF-HEALING LIFECYCLE ----------

    // Fires on the main thread when the connection closes, carrying WHY
    private void HandleStreamEnded(StreamEndReason reason)
    {
        switch (reason)
        {
            case StreamEndReason.StoppedByUs:
                _reconnector.Cancel();
                break;
            case StreamEndReason.AuthFailed:
                _reconnector.Cancel();
                Debug.LogError("Event stream: authentication lost, re-login required.");
                OnAuthenticationLost?.Invoke();
                break;
            case StreamEndReason.ClosedByServer:
            case StreamEndReason.Error:
                Debug.LogWarning("Event stream dropped; reconnecting (attempt " + (_reconnector.Attempt + 1) + ")");
                _reconnector.Schedule();
                break;
        }
    }

    protected override void HandleLine(string line)
    {
        // A line arriving is proof the connection is up
        if (_reconnector.Attempt != 0)
        {
            Debug.Log("Event stream reconnected.");
            _reconnector.NotifyConnected();
        }

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