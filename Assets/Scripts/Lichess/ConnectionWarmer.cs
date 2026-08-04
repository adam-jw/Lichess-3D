using UnityEngine;

// Lichess runs nginx with keepalive_timeout 10s, so a pooled connection dies after
// 10s idle and the next move pays a ~400ms handshake. PingUrl is pinged to prevent this 
public class ConnectionWarmer : MonoBehaviour
{
    [SerializeField] private LichessClient _client;
    [SerializeField] private LichessGameSession _session;

    [SerializeField] private float _idleSeconds = 7f;        // headroom under the 10s timeout
    [SerializeField] private float _backoffSeconds = 60f;    // per Lichess guidance on 429

    private const string PingUrl = "https://lichess.org/robots.txt";

    private float _pausedUntil;

    private void Update()
    {
        if (!_session.IsGameActive) return;                          // only during a game
        if (_client.RequestsInFlight > 0) return;                    // one request at a time
        if (Time.realtimeSinceStartup < _pausedUntil) return;
        if (_client.SecondsSinceLastRequest < _idleSeconds) return;


        float pingStart = Time.realtimeSinceStartup;
        float idleAtFire = _client.SecondsSinceLastRequest;

        StartCoroutine(_client.Get(PingUrl,
            onSuccess: _ =>
            {
                Debug.Log($"[NET] ping after {idleAtFire:F1}s idle, took " +
                        $"{(Time.realtimeSinceStartup - pingStart) * 1000f:F0} ms");
            },
            onError: error =>
            {

                if (error.StartsWith("429"))
                {
                    _pausedUntil = Time.realtimeSinceStartup + _backoffSeconds;
                    Debug.LogWarning("[NET] 429 received - pausing keep-alive for 60s.");
                }
                else Debug.LogWarning("[NET] ping failed: " + error);
            }));
    }
}