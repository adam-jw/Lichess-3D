using System.Globalization;
using System.Net;
using System.Text;
using UnityEngine;

// Seek endpoint = POST whose response carries no information

// The open connection is the seek; Hold it open to stay in the pool,
// close it and the seek is cancelled

// When someone accepts, Lichess closes this connection and announces
// the game on the account event stream
public class LichessSeekStream : LichessStreamBase
{
    [Header("Seek settings")]
    [Tooltip("Default clock. The panel overwrites this before each seek.")]
    [SerializeField] private TimeControl _timeControl = new TimeControl(10f, 0);

    [Tooltip("Leave off while testing - Lichess rates abandoned games.")]
    [SerializeField] private bool _rated = false;

    [SerializeField] private string _variant = "standard";

    private void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
    }

    public bool IsSeeking => IsStreaming;

    public TimeControl TimeControl => _timeControl;
    public bool Rated => _rated;

    // Used by the idle-state UI to pick which user rating to show before a game exists
    public string SeekSpeed => _timeControl.Speed;

    // Settings are baked into the POST body when the connection opens, so they cannot
    // change mid-seek: the caller must cancel and re-seek.
    public void Configure(TimeControl timeControl, bool rated)
    {
        if (IsSeeking)
        {
            Debug.LogWarning("Configure ignored: already seeking. Cancel first.");
            return;
        }

        if (!timeControl.IsBoardApiEligible)
        {
            Debug.LogWarning("Seek " + timeControl.Label + " is " + timeControl.Speed +
                             "; Lichess Board API only accepts Rapid and slower. Attempting seek anyway.");
        }

        _timeControl = timeControl;
        _rated = rated;
    }
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

        Debug.Log("Seeking " + SeekSpeed + ": " + _timeControl.Label +
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
        Append(form, "time", _timeControl.minutes.ToString(CultureInfo.InvariantCulture));
        Append(form, "increment", _timeControl.increment.ToString(CultureInfo.InvariantCulture));
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