using System;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Net;
using UnityEngine;

// How a stream's connection ended
public enum StreamEndReason
{
    ClosedByServer,   // reader hit EndOfStream; normal close or dropped connection
    StoppedByUs,      // called StopStream / Abort; deliberate, never reconnect
    AuthFailed,       // 401: token revoked or expired
    Error             // anything else (DNS, refused, reset) - treat as a drop
}

// Abstract class for any Lichess NDJSON stream (event, board, seek etc)
// Subclasses will supply URL and decide what to do with each line
public abstract class LichessStreamBase : MonoBehaviour
{
    // ---------- PROPERTIES ----------

    private Thread _streamThread;

    // Shared with subclasses so they can build the Authorization header
    protected LichessAuthManager _authManager;

    // Thread-safe handoff between background reader and main thread
    private readonly ConcurrentQueue<string> _lineQueue = new ConcurrentQueue<string>();

    // Stop signal, polled by background loop 
    private volatile bool _isRunning;

    // Held so StopStream can abort a blocked read
    private volatile HttpWebRequest _request;

    // Set by background thread when the loop exits; consumed on main thread (Update)
    private volatile bool _endedSignal;

    // Why the loop ended, written by the background thread before it signals
    private StreamEndReason _endReason;

    // Fired on the main thread once the stream has closed, carrying why it closed
    public event Action<StreamEndReason> OnStreamEnded;

    // Answers whether a stream is live
    public bool IsStreaming => _streamThread != null && _streamThread.IsAlive;


    // ---------- TEMPLATE METHOD HOLES ----------

    // Subclass will supply endpoint to connect to
    protected abstract string GetStreamUrl();

    // Subclass will decide what to do with each line
    protected abstract void HandleLine(string line);

    // Optionally reconfigure the request before it is sent
    protected virtual void ConfigureRequest(HttpWebRequest request) { }


    // ---------- LIFECYCLE ----------

    // Public entry point - start reading stream on background thread
    public void StartStream()
    {
        if (IsStreaming)
        {
            Debug.LogWarning(GetType().Name + ": StartStream called while already streaming; ignored.");
            return;
        }

        _isRunning = true;
        _endedSignal = false;

        _streamThread = new Thread(StreamLoop);
        _streamThread.IsBackground = true;
        _streamThread.Start();
    }

    // Stops stream and does not return until background thread is gone
    public void StopStream()
    {
        if (!IsStreaming)
        {
            _isRunning = false;
            return;
        }

        _isRunning = false;                     // tell the loop to stop

        try { _request?.Abort(); } catch { }    // unblock it

        _streamThread.Join(500);                // bounded: don't hang the main thread forever
        _streamThread = null;
    }

    private void StreamLoop()
    {
        StreamEndReason reason = StreamEndReason.ClosedByServer;

        try
        {
            var request = (HttpWebRequest)WebRequest.Create(GetStreamUrl());
            _request = request;

            request.Headers.Add("Authorization", "Bearer " + _authManager.AccessToken);
            ConfigureRequest(request);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                Debug.Log("Stream connected: " + GetStreamUrl());

                while (_isRunning && !reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    // Filter keepalive blanks
                    if (!string.IsNullOrEmpty(line))
                    {
                        _lineQueue.Enqueue(line);
                    }
                }
            }
        }
        catch (WebException we)
        {
            // If we aborted the request ourself, not a failure
            if (!_isRunning)
            {
                reason = StreamEndReason.StoppedByUs;
            }
            else if (we.Response is HttpWebResponse http &&
                     http.StatusCode == HttpStatusCode.Unauthorized)
            {
                // 401: token is revoked or expired
                reason = StreamEndReason.AuthFailed;
                Debug.LogError("Stream auth failed (" + GetType().Name + "): 401 Unauthorized");
            }
            else
            {
                reason = StreamEndReason.Error;
                Debug.LogError("Stream error (" + GetType().Name + "): " + we.Message);
            }
        }
        catch (Exception e)
        {
            // Non-Web exceptions (e.g. a parse fault)
            reason = _isRunning ? StreamEndReason.Error : StreamEndReason.StoppedByUs;
            if (_isRunning)
                Debug.LogError("Stream error (" + GetType().Name + "): " + e.Message);
        }
        finally
        {
            // Exited cleanly because _isRunning went false, classify as deliberate
            if (!_isRunning && reason == StreamEndReason.ClosedByServer)
                reason = StreamEndReason.StoppedByUs;

            _endReason = reason;
            _isRunning = false;
            _request = null;
            _endedSignal = true;   // Update() will turn this into OnStreamEnded
        }
    }

    protected virtual void Update()
    {
        // Drain everything the background thread queued since last frame
        while (_lineQueue.TryDequeue(out string line))
        {
            HandleLine(line);
        }

        // Report the end after draining
        if (_endedSignal)
        {
            _endedSignal = false;
            OnStreamEnded?.Invoke(_endReason);
        }
    }

    protected virtual void OnDestroy()
    {
        StopStream();
    }
}