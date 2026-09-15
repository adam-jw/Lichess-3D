using UnityEngine;

// Test-only: fire off-board actions from the keyboard and report offer/chat/presence state
// Delete once the action buttons are wired up
public class GameActionDebugInput : MonoBehaviour
{
    [SerializeField] private LichessGameSession _session;

    [SerializeField] private KeyCode _resignKey = KeyCode.G;
    [SerializeField] private KeyCode _abortKey = KeyCode.B;
    [SerializeField] private KeyCode _drawOfferKey = KeyCode.O;        // offer or accept
    [SerializeField] private KeyCode _drawDeclineKey = KeyCode.K;      // decline
    [SerializeField] private KeyCode _takebackProposeKey = KeyCode.T;  // propose or accept
    [SerializeField] private KeyCode _takebackDeclineKey = KeyCode.Y;  // decline
    [SerializeField] private KeyCode _sendChatKey = KeyCode.C;
    [SerializeField] private KeyCode _giftTimeKey = KeyCode.V;
    [SerializeField] private KeyCode _claimVictoryKey = KeyCode.N;
    [SerializeField] private KeyCode _claimDrawKey = KeyCode.M;

    [SerializeField] private string _testChatText = "Hello";

    private bool _lastOpponentDraw;
    private bool _lastMyDraw;
    private bool _lastOpponentTakeback;
    private bool _lastMyTakeback;

    private void OnEnable()
    {
        if (_session != null)
        {
            _session.OnChatMessageReceived += LogChat;
            _session.OnOpponentGoneChanged += LogOpponentGone;
        }
    }

    private void OnDisable()
    {
        if (_session != null)
        {
            _session.OnChatMessageReceived -= LogChat;
            _session.OnOpponentGoneChanged -= LogOpponentGone;
        }
    }

    private void Update()
    {
        if (_session == null || !_session.IsGameActive)
            return;

        if (Input.GetKeyDown(_resignKey))
            _session.Resign();

        if (Input.GetKeyDown(_abortKey))
            _session.Abort();

        if (Input.GetKeyDown(_drawOfferKey))
            _session.SendDraw(true);

        if (Input.GetKeyDown(_drawDeclineKey))
            _session.SendDraw(false);

        if (Input.GetKeyDown(_takebackProposeKey))
            _session.SendTakeback(true);

        if (Input.GetKeyDown(_takebackDeclineKey))
            _session.SendTakeback(false);

        if (Input.GetKeyDown(_sendChatKey))
            _session.SendChat(_testChatText);

        if (Input.GetKeyDown(_giftTimeKey))
            _session.GiftTime();

        if (Input.GetKeyDown(_claimVictoryKey))
            _session.ClaimVictory();

        if (Input.GetKeyDown(_claimDrawKey))
            _session.ClaimDraw();

        ReportOfferChanges(); 
    }

    private void LogChat(ChatLineEvent line)
    {
        Debug.Log("[debug] Chat [" + line.room + "] " + line.username + ": " + line.text);
    }

    private void LogOpponentGone()
    {
        string countdown = _session.ClaimWinInSeconds.HasValue
            ? _session.ClaimWinInSeconds.Value + "s"
            : "n/a";
        Debug.Log("[debug] Opponent gone: " + _session.OpponentGone + " (claim in " + countdown + ")");
    }

    private void ReportOfferChanges()
    {
        var snap = _session.Snapshot;

        if (snap.OpponentOfferingDraw != _lastOpponentDraw)
        {
            _lastOpponentDraw = snap.OpponentOfferingDraw;
            Debug.Log("[debug] Opponent offering draw: " + _lastOpponentDraw);
        }

        if (snap.MyDrawOfferPending != _lastMyDraw)
        {
            _lastMyDraw = snap.MyDrawOfferPending;
            Debug.Log("[debug] My draw offer pending: " + _lastMyDraw);
        }

        if (snap.OpponentProposingTakeback != _lastOpponentTakeback)
        {
            _lastOpponentTakeback = snap.OpponentProposingTakeback;
            Debug.Log("[debug] Opponent proposing takeback: " + _lastOpponentTakeback);
        }

        if (snap.MyTakebackPending != _lastMyTakeback)
        {
            _lastMyTakeback = snap.MyTakebackPending;
            Debug.Log("[debug] My takeback pending: " + _lastMyTakeback);
        }
    }
}