using TMPro;
using UnityEngine;

// Owns the two nameplates: decides who each one shows and which sits on top
//
// Nameplate positions mirror board position; Panel clears @ OnGameStarted 
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

    // ----- Snapshot of the current or most recent game -----
    private bool _hasGame;
    private PieceColor _myColour;
    private string _opponentName;
    private int? _opponentRating;
    private bool _opponentProvisional;
    private string _opponentTitle;
    private int? _myRating;              // from gameFull; null until it arrives
    private bool _myProvisional;
    private string _gameSpeed;           // authoritative once gameFull lands

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

    private void HandleAccountLoaded(LichessAccount account) => Refresh();

    // Only place the snapshot is reset
    private void HandleGameStarted(GameEventInfo game)
    {
        _hasGame = true;
        _myColour = _session.MyColor ?? PieceColor.White;

        _opponentName = game.opponent != null ? game.opponent.username : "Opponent";
        _opponentRating = game.opponent != null ? game.opponent.rating : null;

        // Cleared rather than left stale: these belong to the previous game until the
        // gameFull for this one arrives.
        _opponentProvisional = false;
        _opponentTitle = null;
        _myRating = null;
        _myProvisional = false;
        _gameSpeed = null;

        Refresh();
    }

    private void HandleGameFull(GameFullEvent full)
    {
        if (!_hasGame) return;

        _gameSpeed = full.speed;

        GamePlayer me = _myColour == PieceColor.White ? full.white : full.black;
        GamePlayer them = _myColour == PieceColor.White ? full.black : full.white;

        if (me != null)
        {
            _myRating = me.rating;
            _myProvisional = me.IsProvisional;
        }

        if (them != null)
        {
            // gameFull uses "name" where the gameStart opponent object uses "username".
            if (!string.IsNullOrEmpty(them.name))
                _opponentName = them.name;

            _opponentRating = them.rating;
            _opponentProvisional = them.IsProvisional;
            _opponentTitle = them.title;
        }

        Refresh();
    }

    private void HandleViewChanged() => ApplyOrdering();

    public void Refresh()
    {
        if (_nameplateA == null || _nameplateB == null) return;

        LichessAccount account = _client != null ? _client.Account : null;
        string myName = account != null ? account.username : _loadingLabel;

        if (!_hasGame)
        {
            _nameplateA.SetVisible(true);
            _nameplateA.SetPlayer(myName, IdleRatingText(account), null);
            _nameplateB.SetVisible(false);
            return;
        }

        PieceColor opponentColour =
            _myColour == PieceColor.White ? PieceColor.Black : PieceColor.White;

        _nameplateA.SetVisible(true);
        _nameplateB.SetVisible(true);
        _nameplateA.SetPlayer(myName, MyRatingText(account), _myColour);
        _nameplateB.SetPlayer(Decorate(_opponentTitle, _opponentName),
                              RatingText(_opponentRating, _opponentProvisional),
                              opponentColour);

        ApplyOrdering();
    }

    // A is you, B is the opponent. Normally you are nearest the camera, so you sit at
    // the bottom; flipped, you are at the far end and move to the top.
    private void ApplyOrdering()
    {
        if (_nameplateA == null || _nameplateB == null || !_hasGame) return;

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

    // Prefers gameFull's figure; Falls back to the perfs lookup
    private string MyRatingText(LichessAccount account)
    {
        if (_myRating.HasValue)
            return RatingText(_myRating.Value, _myProvisional);

        if (account == null) return "";

        string speedKey = !string.IsNullOrEmpty(_gameSpeed)
            ? _gameSpeed
            : (_seekStream != null ? _seekStream.SeekSpeed : LichessSpeed.Rapid);

        Perf perf = account.GetPerf(speedKey);
        return perf == null ? _unratedLabel : RatingText(perf.rating, perf.IsProvisional);
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