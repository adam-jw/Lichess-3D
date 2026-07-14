using UnityEngine;

// Temporary seek input for testing - will be deleted later
//   S - seek a game
//   C - cancel the seek
public class SeekInput : MonoBehaviour
{
    [SerializeField] private LichessSeekStream _seekStream;
    [SerializeField] private LichessGameSession _session;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (_session.IsGameActive)
            {
                Debug.LogWarning("Already in a game - finish it before seeking another.");
                return;
            }

            _seekStream.StartSeek();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            _seekStream.CancelSeek();
        }
    }
}