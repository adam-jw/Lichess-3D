using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Http;

// Class for making authenticated HTTP requests so other classes don't need access to tokens/headers
public class LichessClient : MonoBehaviour
{
    private LichessAuthManager _authManager;

    // The logged-in account, or null until the fetch completes
    public LichessAccount Account { get; private set; }
    public event System.Action<LichessAccount> OnAccountLoaded;

    // nginx's 10s idle timer starts when a response completes; warmer uses this to decide whether to ping
    public float SecondsSinceLastRequest => Time.realtimeSinceStartup - _lastRequestCompletedAt;
    private float _lastRequestCompletedAt;

    public int RequestsInFlight { get; private set; }

    void Awake()
    {
        _authManager = GetComponent<LichessAuthManager>();
        // Subscribe so account info is fetched automatically once auth completes
        _authManager.OnAuthenticated += HandleAuthenticated;
    }

    void OnDestroy()
    {
        // Unsubscribe to avoid dangling references when this object is destroyed
        if (_authManager != null)
            _authManager.OnAuthenticated -= HandleAuthenticated;
    }

    private void HandleAuthenticated()
    {
        StartCoroutine(FetchAccountInfo());
    }

    public IEnumerator FetchAccountInfo()
    {
        yield return Get("https://lichess.org/api/account",
            onSuccess: json =>
            {
                LichessAccount account =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<LichessAccount>(json);

                if (account == null)
                {
                    Debug.LogError("Account fetch returned unparseable JSON.");
                    return;
                }

                Account = account;
                Debug.Log("Logged in as: " + account.username);
                Debug.Log("Rapid rating: " + account.GetPerf(LichessSpeed.Rapid)?.rating);
                OnAccountLoaded?.Invoke(account);
            },
            onError: error => Debug.LogError("Account fetch failed: " + error));
    }

    // One client for the whole process: a shared HttpClient keeps its connection pool alive
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = System.TimeSpan.FromSeconds(20)
    };

    public IEnumerator Get(string url, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        return Send(HttpMethod.Get, url, null, onSuccess, onError);
    }

    // Fields are optional: most Lichess POSTs carry everything in the URL path
    public IEnumerator Post(string url, Dictionary<string, string> fields,
                            System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        return Send(HttpMethod.Post, url, fields, onSuccess, onError);
    }

    private IEnumerator Send(HttpMethod method, string url, Dictionary<string, string> fields,
                             System.Action<string> onSuccess, System.Action<string> onError)
    {
        if (!_authManager.IsAuthenticated)
        {
            onError?.Invoke("Not authenticated");
            yield break;
        }

        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", "Bearer " + _authManager.AccessToken);

        if (fields != null && fields.Count > 0)
            request.Content = new FormUrlEncodedContent(fields);

        var pending = new Pending();
        RequestsInFlight += 1;
        _ = Dispatch(request, pending);          // leaves the main thread here

        yield return new WaitUntil(() => pending.Done);
        
        RequestsInFlight -= 1;
        _lastRequestCompletedAt = Time.realtimeSinceStartup;

        if (pending.Error != null)
            onError?.Invoke(pending.Error);
        else
            onSuccess?.Invoke(pending.Body);
    }

    private static async System.Threading.Tasks.Task Dispatch(HttpRequestMessage request, Pending pending)
    {
        try
        {
            using (HttpResponseMessage response = await _http.SendAsync(request))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    pending.Body = body;
                else
                    pending.Error = (int)response.StatusCode + " " + response.ReasonPhrase + ": " + body;
            }
        }
        catch (System.Exception e)
        {
            pending.Error = e.Message;
        }
        finally
        {
            request.Dispose();
            pending.Done = true;              // MUST be the last write
        }
    }

    // Carries one request's outcome from a thread pool thread back to the coroutine
    private class Pending
    {
        public string Body;
        public string Error;

        public volatile bool Done;
    }
}

[System.Serializable]
public class LichessAccount
{
    public string id;
    public string username;

    public Dictionary<string, Perf> perfs;

    // Returns null if the account has never played that speed
    public Perf GetPerf(string speedKey)
    {
        if (perfs == null || string.IsNullOrEmpty(speedKey))
            return null;

        return perfs.TryGetValue(speedKey, out Perf perf) ? perf : null;
    }
}

[System.Serializable]
public class Perf
{
    public int games;
    public int rating;
    public int rd;      // rating deviation
    public int prog;    // recent progression
    public bool? prov;

    public int? rank;
    public bool IsProvisional => prov == true;
}