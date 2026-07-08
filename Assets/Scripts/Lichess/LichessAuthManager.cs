using System;
using System.Collections;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

// Class to handle Lichess OAuth2 PKCE authentication
public class LichessAuthManager : MonoBehaviour
{
    private const string ClientId = "lichess-3d-client";
    private const string RedirectUri = "http://localhost:5000/callback";
    private const string AuthEndpoint = "https://lichess.org/oauth";
    private const string TokenEndpoint = "https://lichess.org/api/token";
    private const string Scopes = "board:play";

    private string _codeVerifier;
    private string _accessToken;

    // Read-only public access to the token for other scripts
    public string AccessToken => _accessToken;

    // Other scripts check this before trying to use the API
    public bool IsAuthenticated => _accessToken != null;

    public event System.Action OnAuthenticated;

    void Start()
    {
        string savedToken = LoadToken();

        // Skip auth flow for returning users
        if (savedToken != null)
        {
            _accessToken = savedToken;
            Debug.Log("Found saved token, skipping auth flow");
            OnAuthenticated?.Invoke();   // notify subscribers token is ready
        }
        else
        {
            StartCoroutine(StartAuthFlow());
        }
    }

    private IEnumerator StartAuthFlow()
    {
        _codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(_codeVerifier);
        string state = Guid.NewGuid().ToString("N");

        // Will hold the auth code once the callback arrives
        string authorizationCode = null;

        // Start listening before opening the browser to not miss callback
        StartLocalHttpListener(state, code =>
        {
            authorizationCode = code;
        });

        string authUrl = AuthEndpoint +
            "?response_type=code" +
            "&client_id=" + ClientId +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&code_challenge_method=S256" +
            "&code_challenge=" + codeChallenge +
            "&scope=" + Scopes +
            "&state=" + state;

        Application.OpenURL(authUrl);

        // Pause coroutine each frame until the auth code arrives
        yield return new WaitUntil(() => authorizationCode != null);
        Debug.Log("Authorization code received: " + authorizationCode);

        // Exchange the short-lived code for a persistent access token
        yield return StartCoroutine(ExchangeCodeForToken(authorizationCode));

        OnAuthenticated?.Invoke();
    }

    private string GenerateCodeVerifier()
    {
        byte[] randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
    
    private void StartLocalHttpListener(string expectedState, Action<string> onCodeReceived)
    {
        // Run the listener on a background thread so it doesn't freeze Unity
        Thread listenerThread = new Thread(() =>
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri + "/");
            listener.Start();

            // Block this thread until a request comes in
            HttpListenerContext context = listener.GetContext();
            string rawUrl = context.Request.Url.Query;

            // Send a response so the browser doesn't just hang
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = "https://lichess.org";
            context.Response.OutputStream.Close();
            listener.Stop();

            // Parse the code and state from the query string e.g. ?code=abc&state=xyz
            System.Collections.Specialized.NameValueCollection queryParams = 
                System.Web.HttpUtility.ParseQueryString(rawUrl);
            string returnedState = queryParams["state"];
            string code = queryParams["code"];

            // Verify state matches what we sent to prevent cross-site request forgery
            if (returnedState != expectedState)
            {
                Debug.LogError("State mismatch, aborting auth");
                return;
            }

            // Send result back to the main Unity thread via the callback
            onCodeReceived?.Invoke(code);
        });

        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private IEnumerator ExchangeCodeForToken(string authorizationCode)
    {
        // Build the POST body fields required by the PKCE token exchange spec
        WWWForm form = new WWWForm();
        form.AddField("grant_type", "authorization_code");
        form.AddField("code", authorizationCode);
        form.AddField("redirect_uri", RedirectUri);
        form.AddField("client_id", ClientId);
        form.AddField("code_verifier", _codeVerifier);

        using (UnityEngine.Networking.UnityWebRequest request = 
            UnityEngine.Networking.UnityWebRequest.Post(TokenEndpoint, form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Token exchange failed: " + request.error);
                yield break;
            }

            // Parse just the access_token field from the response
            string json = request.downloadHandler.text;
            TokenResponse tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(json);
            _accessToken = tokenResponse.access_token;
            SaveToken(_accessToken);

            Debug.Log("Access token received: " + _accessToken);
        }
    }

    private void SaveToken(string token)
    {
        string path = Application.persistentDataPath + "/lichess_token.txt";
        System.IO.File.WriteAllText(path, token);
        Debug.Log("Token saved to: " + path);
    }

    private string LoadToken()
    {
        string path = Application.persistentDataPath + "/lichess_token.txt";
        
        if (System.IO.File.Exists(path))
            return System.IO.File.ReadAllText(path);
            
        return null;
    }
}

// Matches the JSON shape returned by the Lichess token endpoint
[Serializable]
public class TokenResponse
{
    public string access_token;
    public string token_type;
}
