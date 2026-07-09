using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using UnityEngine;

// Abstract class for any Lichess NDJSON stream (event, board, etc)
// Subclasses will supply URL and decide what to do with each line
public abstract class LichessStreamBase : MonoBehaviour
{
    // Shared with subclasses so they can build the Authorization header
    protected LichessAuthManager _authManager;

    // Thread-safe handoff between background reader and main thread
    private readonly ConcurrentQueue<string> _lineQueue = new ConcurrentQueue<string>();
    private Thread _streamThread;
    private volatile bool _isRunning;

    protected virtual void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
    }

    // Subclass will supply endpoint to connect to
    protected abstract string GetStreamUrl();

    // Subclass will decide what to do with each line
    protected abstract void HandleLine(string line);

    // Public entry point - start reading stream on background thread
    public void StartStream()
    {
        if (_isRunning) return;

        _isRunning = true;
        _streamThread = new Thread(StreamLoop);
        _streamThread.IsBackground = true;
        _streamThread.Start();
    }

    // Public entry point - stop stream
    public void StopStream()
    {
        _isRunning = false;
    }

    private void StreamLoop()
    {
        try
        {
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(GetStreamUrl());
            request.Headers.Add("Authorization", "Bearer " + _authManager.AccessToken);

            using (var response = (System.Net.HttpWebResponse)request.GetResponse())
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
        catch (System.Exception e)
        {
            Debug.LogError("Stream error (" + GetType().Name + "): " + e.Message);
        }
    }

    protected virtual void Update()
    {
        // Drain everything the background thread queued since last frame
        while (_lineQueue.TryDequeue(out string line))
        {
            HandleLine(line);
        }
    }

    protected virtual void OnDestroy()
    {
        StopStream();
    }
}