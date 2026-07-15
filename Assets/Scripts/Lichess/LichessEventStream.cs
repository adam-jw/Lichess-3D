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

    public event System.Action<GameEventInfo> OnGameStart;
    public event System.Action<GameEventInfo> OnGameFinish;

    // Raised when the auth token is rejected 
    public event System.Action OnAuthenticationLost;

    private Coroutine _reconnectRoutine;
    private int _attempt;   // # consecutive failed reconnects
    private void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
        _authManager.OnAuthenticated += HandleAuthenticated;
        OnStreamEnded += HandleStreamEnded;
    }

    protected override void OnDestroy()
    {
        if (_authManager != null)
            _authManager.OnAuthenticated -= HandleAuthenticated;
        OnStreamEnded -= HandleStreamEnded;

        if (_reconnectRoutine != null)
            StopCoroutine(_reconnectRoutine);

        base.OnDestroy();   // let base stop the thread
    }

    // First connect, on login
    private void HandleAuthenticated()
    {
        _attempt = 0;
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
                // Deliberate stop, stay down
                CancelPendingReconnect();
                break;

            case StreamEndReason.AuthFailed:
                // Token is dead; ask for re-login to authenticate
                CancelPendingReconnect();
                Debug.LogError("Event stream: authentication lost, re-login required.");
                OnAuthenticationLost?.Invoke();
                break;

            case StreamEndReason.ClosedByServer:
            case StreamEndReason.Error:
                ScheduleReconnect();
                break;
        }
    }

    private void ScheduleReconnect()
    {
        // Guard against double-scheduling; one pending retry at a time
        if (_reconnectRoutine != null)
            return;

        float delay = ComputeBackoff(_attempt);
        _attempt++;   // next failure waits longer; reset happens on a confirmed connect
        Debug.LogWarning("Event stream dropped; reconnecting in " +
                         delay.ToString("0.#") + "s (attempt " + _attempt + ")");
        _reconnectRoutine = StartCoroutine(ReconnectAfter(delay));
    }

    private IEnumerator ReconnectAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _reconnectRoutine = null;   // this retry is being spent; allow the NEXT drop to schedule again
        StartStream();
        // If this attempt fails, StreamLoop fires OnStreamEnded again and we come back through ScheduleReconnect
    }

    private void CancelPendingReconnect()
    {
        if (_reconnectRoutine != null)
        {
            StopCoroutine(_reconnectRoutine);
            _reconnectRoutine = null;
        }
    }

    private float ComputeBackoff(int attempt)
    {
        float delay = _baseDelaySeconds * Mathf.Pow(2f, attempt);
        return Mathf.Min(delay, _maxDelaySeconds);
    }

    protected override void HandleLine(string line)
    {
        // A line arriving is proof the connection is up
        if (_attempt != 0)
        {
            Debug.Log("Event stream reconnected.");
            _attempt = 0;
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