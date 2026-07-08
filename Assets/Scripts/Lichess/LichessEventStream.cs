using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.ComponentModel;

public class LichessEventStream : MonoBehaviour
{
    private LichessAuthManager _authManager;
    private readonly ConcurrentQueue<string> _eventQueue = new ConcurrentQueue<string>();
    private Thread _streamThread;
    private volatile bool _isRunning;

    void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
        // Begin streaming as soon as authentication completes
        _authManager.OnAuthenticated += StartStream;
    }

    void OnDestroy()
    {
        if (_authManager != null)
            _authManager.OnAuthenticated -= StartStream;
        
        StopStream();
    }

    private void StartStream()
    {
        _isRunning = true;

        // Launch the reading loop on background thread
        _streamThread = new Thread(StreamLoop);
        _streamThread.IsBackground = true; 
        _streamThread.Start();

        Debug.Log("Event stream started");
    }

    private void StopStream()
    {
        _isRunning = false;
    }

    private void StreamLoop()
    {
        try
        {
            // HttpWebRequest to read the response body incrementally, UnityWebRequest waits for full response
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(
                "https://lichess.org/api/stream/event");
            request.Headers.Add("Authorization", "Bearer " + _authManager.AccessToken);

            using (var response = (System.Net.HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                // Keep reading lines until shutdown is signalled or the stream ends
                while (_isRunning && !reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    // Filter keepalive blanks at transport boundary, so queue only holds real events
                    if (!string.IsNullOrEmpty(line))
                    {
                        _eventQueue.Enqueue(line);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Event stream error: " + e.Message);
        }
    }

    private void Update()
    {
        while (_eventQueue.TryDequeue(out string line))
        {
            HandleEvent(line);
        }
    }

    private void HandleEvent(string line)
    {
        Debug.Log("Event received: " + line);
    }

}