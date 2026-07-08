using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Class for making authenticated HTTP requests so other classes don't need access to tokens/headers
public class LichessClient : MonoBehaviour
{
    private LichessAuthManager _authManager;

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
                LichessAccount account = Newtonsoft.Json.JsonConvert.DeserializeObject<LichessAccount>(json);
                Debug.Log("Logged in as: " + account.username);
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
}