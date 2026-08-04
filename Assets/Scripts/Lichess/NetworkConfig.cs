using System.Net;
using UnityEngine;

// Process-wide HTTP connection settings. Must run before any request to
// lichess.org, because ServicePoint config is fixed when the host is first contacted
public static class NetworkConfig
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        ServicePointManager.DefaultConnectionLimit = 10;
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.UseNagleAlgorithm = false;

        Debug.Log("[NET] ServicePointManager configured.");
    }
}