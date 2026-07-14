using System.Globalization;
using System.Net;
using System.Text;
using UnityEngine;

// A seek endpoint is a POST whose response carries no information

// The open connection is the seek; Hold it open to stay in the pool,
// close it and the seek is cancelled

// When someone accepts, Lichess closes this connection and announces
// the game on the account event stream
public class LichessSeekStream : LichessStreamBase
{
    [Header("Seek settings")]
    [Tooltip("Initial clock in minutes. Board API allows Rapid and slower.")]
    [SerializeField] private float _timeMinutes = 10f;

    [Tooltip("Clock increment in seconds.")]
    [SerializeField] private int _incrementSeconds = 0;

    [Tooltip("Leave off while testing - Lichess rates abandoned games.")]
    [SerializeField] private bool _rated = false;

    [SerializeField] private string _variant = "standard";
    private void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
    }

    public bool IsSeeking => IsStreaming;

    public void StartSeek()
    {
        if (IsSeeking)
        {
            Debug.LogWarning("Already seeking.");
            return;
        }

        if (string.IsNullOrEmpty(_authManager.AccessToken))
        {
            Debug.LogError("Cannot seek: not authenticated.");
            return;
        }

        Debug.Log("Seeking " + _timeMinutes + "+" + _incrementSeconds +
                  (_rated ? " rated" : " casual") + "...");

        StartStream();   // Open the connection = place the seek
    }

    // Close the connection = cancel a seek
    public void CancelSeek()
    {
        if (!IsSeeking)
            return;

        Debug.Log("Cancelling seek.");
        StopStream();
    }

    protected override string GetStreamUrl()
    {
        return "https://lichess.org/api/board/seek";
    }

    protected override void ConfigureRequest(HttpWebRequest request)
    {
        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";

        byte[] body = Encoding.UTF8.GetBytes(BuildFormBody());
        request.ContentLength = body.Length;

        // This opens the connection and sends the headers; must happen before GetResponse
        using (var requestStream = request.GetRequestStream())
        {
            requestStream.Write(body, 0, body.Length);
        }
    }

    private string BuildFormBody()
    {
        var form = new StringBuilder();

        // Lichess wants "true" or "false" (lowercase)
        Append(form, "rated", _rated.ToString().ToLowerInvariant());

        // InvariantCulture : Need 10.5 not e.g. 10,5 in Europe
        Append(form, "time", _timeMinutes.ToString(CultureInfo.InvariantCulture));
        Append(form, "increment", _incrementSeconds.ToString(CultureInfo.InvariantCulture));
        Append(form, "variant", _variant);

        return form.ToString();
    }

    private static void Append(StringBuilder sb, string key, string value)
    {
        if (sb.Length > 0)
            sb.Append('&');

        sb.Append(UnityWebRequestEscape(key));
        sb.Append('=');
        sb.Append(UnityWebRequestEscape(value));
    }

    private static string UnityWebRequestEscape(string s)
    {
        return UnityEngine.Networking.UnityWebRequest.EscapeURL(s);
    }

    // Don't expect this to be called (need to write abstract method)
    protected override void HandleLine(string line)
    {
        Debug.Log("Seek stream said something unexpected: " + line);
    }
}