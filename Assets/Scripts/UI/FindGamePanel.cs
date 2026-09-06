using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Idle-state UI: pick a time control, pick rated or casual, seek a game
//
// Hides via CanvasGroup rather than SetActive; makes UI invisible and untouchable
// while script keeps running
public class FindGamePanel : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private LichessSeekStream _seekStream;
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private LichessClient _client;
    [SerializeField] private LichessAuthManager _authManager;

    [Tooltip("Used to notice a revoked token.")]
    [SerializeField] private LichessEventStream _eventStream;

    [Header("UI")]
    [Tooltip("Hidden during a game. Defaults to a CanvasGroup on this object.")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TextMeshProUGUI _actionLabel;
    [SerializeField] private TMP_Dropdown _timeDropdown;
    [SerializeField] private Toggle _ratedToggle;

    [Tooltip("Optional line under the button.")]
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Presets")]
    [Tooltip("Only Rapid-or-slower entries are offered; the rest are dropped with a warning.")]
    [SerializeField]
    private TimeControl[] _presets =
    {
        new TimeControl(10f, 0),
        new TimeControl(10f, 5),
        new TimeControl(15f, 10),
        new TimeControl(30f, 0),
    };

    [SerializeField] private int _defaultPresetIndex = 2;

    [Header("Labels")]
    [SerializeField] private string _findLabel = "Find game";
    [SerializeField] private string _cancelLabel = "Cancel";
    [SerializeField] private string _signedOutLabel = "Not signed in";
    [SerializeField] private string _seekingStatus = "Waiting for an opponent...";

    [Header("Debug: challenge AI (testing only)")]
    [SerializeField] private bool _debugAiChallenge = true;
    [SerializeField] private KeyCode _debugAiKey = KeyCode.F9;
    [Range(1, 8)]
    [SerializeField] private int _debugAiLevel = 1;
    [Tooltip("white, black, or random.")]
    [SerializeField] private string _debugColor = "white";
    [Tooltip("Optional. If set, starts a From Position game at this FEN.")]
    [SerializeField] private string _debugFromFen = "";

    private bool _debugChallengeInFlight;

    private enum PanelState { Hidden, SignedOut, Idle, Seeking }

    private PanelState _state = PanelState.Hidden;
    private bool _stateValid;
    private bool _authLost;

    // Presets offered after the eligibility filter
    private readonly List<TimeControl> _offered = new List<TimeControl>();

    private void Awake()
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();

        BuildPresetList();

        if (_actionButton != null)
            _actionButton.onClick.AddListener(HandleActionClicked);

        // Opts out of selectable to avoid breaking arrow key history scrubber
        foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void OnEnable()
    {
        if (_eventStream != null)
            _eventStream.OnAuthenticationLost += HandleAuthenticationLost;

        if (_session != null)
            _session.OnGameEnded += HandleGameEnded;

        _stateValid = false;   // force a rebuild on the next Update
    }

    private void OnDisable()
    {
        if (_eventStream != null)
            _eventStream.OnAuthenticationLost -= HandleAuthenticationLost;

        if (_session != null)
            _session.OnGameEnded -= HandleGameEnded;
    }

    // Drops anything the Board API will reject
    private void BuildPresetList()
    {
        _offered.Clear();

        foreach (TimeControl tc in _presets)
        {
            if (!tc.IsBoardApiEligible)
            {
                Debug.LogWarning("Dropping preset " + tc.Label + ": classifies as " + tc.Speed +
                                 ", but the Board API only accepts Rapid and slower.", this);
                continue;
            }

            _offered.Add(tc);
        }

        if (_offered.Count == 0)
        {
            Debug.LogError("No eligible time controls; falling back to 10+0.", this);
            _offered.Add(new TimeControl(10f, 0));
        }

        if (_timeDropdown == null) return;

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (TimeControl tc in _offered)
            options.Add(new TMP_Dropdown.OptionData(tc.Label));

        _timeDropdown.ClearOptions();
        _timeDropdown.AddOptions(options);
        _timeDropdown.SetValueWithoutNotify(Mathf.Clamp(_defaultPresetIndex, 0, _offered.Count - 1));
    }

    private void Update()
    {
        PanelState next = ComputeState();

        if (!_stateValid || next != _state)
        {
            _state = next;
            _stateValid = true;
            ApplyState(next);
        }

        HandleDebugInput();
    }

    private PanelState ComputeState()
    {
        if (_session != null && _session.IsGameActive) return PanelState.Hidden;

        if (_authLost || _authManager == null || !_authManager.IsAuthenticated)
            return PanelState.SignedOut;

        if (_seekStream != null && _seekStream.IsSeeking) return PanelState.Seeking;

        return PanelState.Idle;
    }

    private void ApplyState(PanelState state)
    {
        bool visible = state != PanelState.Hidden;
        SetVisible(visible);

        if (!visible) return;

        bool seeking = state == PanelState.Seeking;
        bool signedOut = state == PanelState.SignedOut;

        if (_actionLabel != null)
            _actionLabel.text = signedOut ? _signedOutLabel : (seeking ? _cancelLabel : _findLabel);

        if (_actionButton != null)
            _actionButton.interactable = !signedOut;

        // Lock settings while a seek is open
        if (_timeDropdown != null) _timeDropdown.interactable = !seeking && !signedOut;
        if (_ratedToggle != null) _ratedToggle.interactable = !seeking && !signedOut;

        if (_statusText != null)
            _statusText.text = seeking ? _seekingStatus : "";
    }

    private void SetVisible(bool visible)
    {
        if (_group == null) return;

        _group.alpha = visible ? 1f : 0f;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;   // prevent hidden UI from eating board clicks
    }

    private void HandleActionClicked()
    {
        // Clicking leaves the button selected, and a selected button consumes arrow keys
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (_seekStream == null) return;

        if (_seekStream.IsSeeking)
        {
            _seekStream.CancelSeek();
        }
        else
        {
            _seekStream.Configure(SelectedTimeControl(), _ratedToggle != null && _ratedToggle.isOn);
            _seekStream.StartSeek();
        }

        _stateValid = false;   // reflect the change this frame, not next
    }

    private TimeControl SelectedTimeControl()
    {
        int index = _timeDropdown != null ? _timeDropdown.value : _defaultPresetIndex;
        index = Mathf.Clamp(index, 0, _offered.Count - 1);

        return _offered[index];
    }

    // Refetch account info to get updated player rating
    private void HandleGameEnded(GameEndReason reason, string status)
    {
        if (_client == null || _session == null) return;

        GameSnapshot snap = _session.Snapshot;
        if (snap == null || !snap.Rated || snap.Outcome == GameOutcome.Aborted) return;

        StartCoroutine(_client.FetchAccountInfo());
    }

    private void HandleAuthenticationLost()
    {
        _authLost = true;
        _stateValid = false;
    }

    // Quick tester: challenge Lichess AI straight from the start screen for debugging purposes
    private void HandleDebugInput()
    {
        if (!_debugAiChallenge || _debugChallengeInFlight) return;
        if (_state != PanelState.Idle) return;                 // start screen only
        if (!Input.GetKeyDown(_debugAiKey)) return;
        if (_client == null) return;

        var fields = new Dictionary<string, string>
    {
        { "level", Mathf.Clamp(_debugAiLevel, 1, 8).ToString() },
    };

        TimeControl tc = SelectedTimeControl();
        fields["clock.limit"] = Mathf.RoundToInt(tc.InitialSeconds).ToString();
        fields["clock.increment"] = tc.increment.ToString();

        if (!string.IsNullOrWhiteSpace(_debugColor))
            fields["color"] = _debugColor.Trim();

        if (!string.IsNullOrWhiteSpace(_debugFromFen))
        {
            fields["variant"] = "fromPosition";
            fields["fen"] = _debugFromFen.Trim();
        }

        _debugChallengeInFlight = true;
        Debug.Log($"[debug] Challenging Lichess AI level {_debugAiLevel}...");
        StartCoroutine(_client.Post("https://lichess.org/api/challenge/ai", fields,
            json => { _debugChallengeInFlight = false; Debug.Log("[debug] AI game started."); },
            err => { _debugChallengeInFlight = false; Debug.LogError("[debug] AI challenge failed: " + err); }));
    }
}