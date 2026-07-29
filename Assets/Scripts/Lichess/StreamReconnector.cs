using System;
using System.Collections;
using UnityEngine;

// Exponential-backoff reconnect driver. Host runs the wait coroutine; caller 
// supplies the retry action. Bounded when maxAttempts > 0: after that many failed
// retries it reports exhaustion instead of retrying again
public class StreamReconnector
{
    private readonly MonoBehaviour _host;
    private readonly Action _retry;
    private readonly Action _onExhausted;   // null = unlimited
    private readonly float _baseDelay;
    private readonly float _maxDelay;
    private readonly int _maxAttempts;      // 0 = unlimited

    private Coroutine _pending;
    private int _attempt;                   // consecutive failed retries since last confirmed connection

    public StreamReconnector(MonoBehaviour host, Action retry,
                             float baseDelay, float maxDelay,
                             int maxAttempts = 0, Action onExhausted = null)
    {
        _host = host;
        _retry = retry;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _maxAttempts = maxAttempts;
        _onExhausted = onExhausted;
    }

    public int Attempt => _attempt;

    // Queue one retry after a backoff delay. No-op if one is already pending
    public void Schedule()
    {
        if (_pending != null) return;

        if (_maxAttempts > 0 && _attempt >= _maxAttempts)
        {
            _onExhausted?.Invoke();
            return;
        }

        float delay = Mathf.Min(_baseDelay * Mathf.Pow(2f, _attempt), _maxDelay);
        _attempt++;
        _pending = _host.StartCoroutine(WaitThenRetry(delay));
    }

    private IEnumerator WaitThenRetry(float delay)
    {
        yield return new WaitForSeconds(delay);
        _pending = null;   // this retry is spent; the next drop may schedule again
        _retry();
    }

    // Stop any pending retry: deliberate stop, auth failure, game over, teardown
    public void Cancel()
    {
        if (_pending == null) return;
        _host.StopCoroutine(_pending);
        _pending = null;
    }

    // Call when data proves the link is up: the next drop starts fresh backoff
    public void NotifyConnected() => _attempt = 0;
}