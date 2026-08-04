using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Class for making authenticated HTTP requests so other classes don't need access to tokens/headers
public class LichessClient : MonoBehaviour
{
    private LichessAuthManager _authManager;

    // The logged-in account, or null until the fetch completes
    public LichessAccount Account { get; private set; }
    public event System.Action<LichessAccount> OnAccountLoaded;

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

    // Makes an authenticated GET request and returns the response body via callback
    public IEnumerator Get(string url, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        if (!_authManager.IsAuthenticated)
        {
            onError?.Invoke("Not authenticated");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "Bearer " + _authManager.AccessToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error);
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }
    }

    // Makes an authenticated POST request with form data
    public IEnumerator Post(string url, WWWForm form, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        if (!_authManager.IsAuthenticated)
        {
            onError?.Invoke("Not authenticated");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + _authManager.AccessToken);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error);
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }
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