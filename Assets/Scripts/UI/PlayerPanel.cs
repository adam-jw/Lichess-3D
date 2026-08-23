using UnityEngine;

// Owns the two nameplates: decides who each one shows and which sits on top
//
// Nameplate positions mirror board position
//
// Holds NO game state of its own: who is playing lives in session.Snapshot 
// This class is purely formatting and layout
public class PlayerPanel : MonoBehaviour
{
    [SerializeField] private LichessClient _client;
    [SerializeField] private LichessSeekStream _seekStream;
    [SerializeField] private LichessGameSession _session;

    [Tooltip("Without this the nameplates don't reorder on board flip.")]
    [SerializeField] private BoardCameraController _cameraController;

    [Header("Nameplates")]
    [Tooltip("The two nameplates, in any order.")]
    [SerializeField] private PlayerNameplate _nameplateA;   // you
    [SerializeField] private PlayerNameplate _nameplateB;   // opponent

    [Header("Formatting")]
    [SerializeField] private string _loadingLabel = "Connecting...";
    [Tooltip("Shown in place of a rating when a player has none (e.g. AI opponents).")]
    [SerializeField] private string _unratedLabel = "unrated";
    [Tooltip("Lichess marks provisional ratings with a trailing '?'.")]
    [SerializeField] private bool _markProvisional = true;
    [Tooltip("Prefix titled players with GM / IM / etc.")]
    [SerializeField] private bool _showTitles = true;
    [Tooltip("Show 'Level N' instead of a rating for Lichess AI opponents.")]
    [SerializeField] private bool _showAiLevel = true;

    private GameSnapshot Snapshot => _session != null ? _session.Snapshot : null;

    private void OnEnable()
    {
        if (_client != null)
        {
            _client.OnAccountLoaded += HandleAccountLoaded;

            if (_client.Account != null)
                HandleAccountLoaded(_client.Account);
        }

        if (_session != null)
        {
            _session.OnGameStarted += HandleGameStarted;
            _session.OnGameFullReceived += HandleGameFull;
        }

        if (_cameraController != null)
            _cameraController.OnViewChanged += HandleViewChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_client != null)
            _client.OnAccountLoaded -= HandleAccountLoaded;

        if (_session != null)
        {
            _session.OnGameStarted -= HandleGameStarted;
            _session.OnGameFullReceived -= HandleGameFull;
        }

        if (_cameraController != null)
            _cameraController.OnViewChanged -= HandleViewChanged;
    }

    // Snapshot has already absorbed the event by the time it reaches us;
    // Nothing to record, only to redraw
    private void HandleAccountLoaded(LichessAccount account) => Refresh();
    private void HandleGameStarted(GameEventInfo game) => Refresh();
    private void HandleGameFull(GameFullEvent full) => Refresh();
    private void HandleViewChanged() => ApplyOrdering();

    public void Refresh()
    {
        if (_nameplateA == null || _nameplateB == null) return;

        LichessAccount account = _client != null ? _client.Account : null;
        GameSnapshot snap = Snapshot;

        // Our name: the wire's version once gameFull lands, otherwise the account
        string myName = snap != null && !string.IsNullOrEmpty(snap.MyName)
            ? snap.MyName
            : (account != null ? account.username : _loadingLabel);

        if (snap == null || !snap.HasGame)
        {
            _nameplateA.SetVisible(true);
            _nameplateA.SetPlayer(myName, IdleRatingText(account), null);
            _nameplateB.SetVisible(false);
            return;
        }

        _nameplateA.SetVisible(true);
        _nameplateB.SetVisible(true);

        _nameplateA.SetPlayer(Decorate(snap.MyTitle, myName),
                              MyRatingText(account, snap),
                              snap.MyColor);

        _nameplateB.SetPlayer(Decorate(snap.OpponentTitle, snap.OpponentName ?? "Opponent"),
                              OpponentRatingText(snap),
                              snap.OpponentColor);

        ApplyOrdering();
    }

    // A is you, B is the opponent. Normally you are nearest the camera, so you sit at
    // the bottom; flipped, you are at the far end and move to the top
    private void ApplyOrdering()
    {
        if (_nameplateA == null || _nameplateB == null) return;

        GameSnapshot snap = Snapshot;
        if (snap == null || !snap.HasGame) return;

        bool flipped = _cameraController != null && _cameraController.IsFlipped;

        _nameplateA.transform.SetSiblingIndex(flipped ? 0 : 1);
        _nameplateB.transform.SetSiblingIndex(flipped ? 1 : 0);
    }

    // No game exists, so guess the speed rating from the seek settings
    private string IdleRatingText(LichessAccount account)
    {
        if (account == null) return "";

        string speedKey = _seekStream != null ? _seekStream.SeekSpeed : LichessSpeed.Rapid;
        Perf perf = account.GetPerf(speedKey);

        return perf == null ? _unratedLabel : RatingText(perf.rating, perf.IsProvisional);
    }

    // Prefers gameFull's figure; falls back to the perfs lookup
    private string MyRatingText(LichessAccount account, GameSnapshot snap)
    {
        if (snap.MyRating.HasValue)
            return RatingText(snap.MyRating.Value, snap.MyProvisional);

        if (account == null) return "";

        string speedKey = !string.IsNullOrEmpty(snap.Speed)
            ? snap.Speed
            : (_seekStream != null ? _seekStream.SeekSpeed : LichessSpeed.Rapid);

        Perf perf = account.GetPerf(speedKey);
        return perf == null ? _unratedLabel : RatingText(perf.rating, perf.IsProvisional);
    }

    // if opponent is AI shows its 'Level' rather than unrated
    private string OpponentRatingText(GameSnapshot snap)
    {
        if (_showAiLevel && snap.OpponentIsAI)
            return "Level " + snap.OpponentAiLevel.Value;

        return RatingText(snap.OpponentRating, snap.OpponentProvisional);
    }

    private string RatingText(int? rating, bool provisional)
    {
        if (!rating.HasValue) return _unratedLabel;

        return (_markProvisional && provisional)
            ? rating.Value + "?"
            : rating.Value.ToString();
    }

    private string Decorate(string title, string name) =>
        (_showTitles && !string.IsNullOrEmpty(title)) ? title + " " + name : name;
}