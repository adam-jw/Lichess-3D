using UnityEngine;

// Owns the two clock displays: which player each belongs to, and which sits on top

// Holds no game state of its own; which color we are lives in session.Snapshot
public class ClockPanel : MonoBehaviour
{
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private GameClockModel _clock;

    [Tooltip("Optional. Without it the clocks never reorder on a board flip.")]
    [SerializeField] private BoardCameraController _cameraController;

    [Header("Displays")]
    [SerializeField] private ClockDisplay _clockA;   // yours
    [SerializeField] private ClockDisplay _clockB;   // opponent's

    [Header("Thresholds")]
    [Tooltip("Below this, the background turns red.")]
    [SerializeField] private float _lowTimeSeconds = 30f;

    [Tooltip("Below this, tenths of a second are shown.")]
    [SerializeField] private float _tenthsSeconds = 10f;

    private GameSnapshot Snapshot => _session != null ? _session.Snapshot : null;

    private void OnEnable()
    {
        if (_session != null)
            _session.OnGameStarted += HandleGameStarted;

        if (_cameraController != null)
            _cameraController.OnViewChanged += HandleViewChanged;

        ApplyOrdering();
    }

    private void OnDisable()
    {
        if (_session != null)
            _session.OnGameStarted -= HandleGameStarted;

        if (_cameraController != null)
            _cameraController.OnViewChanged -= HandleViewChanged;
    }

    private void HandleGameStarted(GameEventInfo game) => ApplyOrdering();

    private void HandleViewChanged() => ApplyOrdering();

    private void Update()
    {
        GameSnapshot snap = Snapshot;

        bool visible = snap != null && snap.HasGame && _clock != null && _clock.HasClock;

        if (_clockA != null) _clockA.SetVisible(visible);
        if (_clockB != null) _clockB.SetVisible(visible);

        if (!visible) return;

        Render(_clockA, snap.MyColor);
        Render(_clockB, snap.OpponentColor);
    }

    private void Render(ClockDisplay display, PieceColor color)
    {
        if (display == null) return;

        int remainingMs = _clock.GetRemainingMs(color);

        bool active = _clock.IsTicking(color) || !_clock.IsRunning;

        bool low = remainingMs < _lowTimeSeconds * 1000f;
        bool tenths = remainingMs < _tenthsSeconds * 1000f;

        display.SetTime(remainingMs, active, low, tenths);
    }

    // A is yours, B is opponent's. Normally you are nearest the camera and sit at
    // the bottom; on board flip, you are at the far end and move to the top
    private void ApplyOrdering()
    {
        if (_clockA == null || _clockB == null) return;

        bool flipped = _cameraController != null && _cameraController.IsFlipped;

        _clockA.transform.SetSiblingIndex(flipped ? 0 : 1);
        _clockB.transform.SetSiblingIndex(flipped ? 1 : 0);
    }
}