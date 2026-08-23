using TMPro;
using UnityEngine;

// Shows the result of the finished game, Lichess-style: a reason line above a verdict
// line ("Black resigned" / "White is victorious")
//
// Hidden during play/before first game; appears on OnGameEnded and clears when next game starts
public class GameResultDisplay : MonoBehaviour
{
    [SerializeField] private LichessGameSession _session;

    [Header("Labels")]
    [Tooltip("Root that is shown/hidden. Defaults to this GameObject.")]
    [SerializeField] private GameObject _root;

    [Tooltip("Reason line, e.g. 'Black resigned'. Leave the verdict label empty to get " +
             "both parts combined into this one.")]
    [SerializeField] private TextMeshProUGUI _reasonText;

    [Tooltip("Verdict line, e.g. 'White is victorious'. Optional.")]
    [SerializeField] private TextMeshProUGUI _verdictText;

    [Header("Phrasing")]
    [Tooltip("On: 'You win'. Off: Lichess's neutral 'White is victorious'.")]
    [SerializeField] private bool _personalVerdict;

    [Tooltip("Separator used only when the verdict shares the reason label.")]
    [SerializeField] private string _combinedSeparator = " • ";

    [Header("Verdict color")]
    [SerializeField] private bool _colorVerdict = true;
    [SerializeField] private Color _winColor = new Color(0.30f, 0.65f, 0.30f);
    [SerializeField] private Color _lossColor = new Color(0.75f, 0.25f, 0.25f);
    [SerializeField] private Color _neutralColor = new Color(0.35f, 0.35f, 0.35f);

    private GameSnapshot Snapshot => _session != null ? _session.Snapshot : null;

    private void Awake()
    {
        if (_root == null) _root = gameObject;
    }

    private void OnEnable()
    {
        if (_session != null)
        {
            _session.OnGameStarted += HandleGameStarted;
            _session.OnGameEnded += HandleGameEnded;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (_session != null)
        {
            _session.OnGameStarted -= HandleGameStarted;
            _session.OnGameEnded -= HandleGameEnded;
        }
    }

    private void HandleGameStarted(GameEventInfo game) => Refresh();

    private void HandleGameEnded(GameEndReason reason, string status) => Refresh();

    public void Refresh()
    {
        GameSnapshot snap = Snapshot;
        bool show = snap != null && snap.IsFinished;

        SetVisible(show);
        if (!show) return;

        // With no separate verdict label, both parts share the reason label
        bool split = _verdictText != null;

        if (_reasonText != null)
        {
            _reasonText.text = split
                ? GameResultText.Reason(snap)
                : GameResultText.Combined(snap, _personalVerdict, _combinedSeparator);
        }

        if (_verdictText != null)
        {
            _verdictText.text = GameResultText.Verdict(snap, _personalVerdict);

            if (_colorVerdict)
                _verdictText.color = ColorFor(snap.Outcome);
        }
    }

    private Color ColorFor(GameOutcome outcome)
    {
        switch (outcome)
        {
            case GameOutcome.Win: return _winColor;
            case GameOutcome.Loss: return _lossColor;
            default: return _neutralColor;   // draw, abort, unknown
        }
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
            _root.SetActive(visible);
    }
}