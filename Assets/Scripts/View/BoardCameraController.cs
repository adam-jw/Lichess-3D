using UnityEngine;

// Orients the player's camera to their side of the board each game
// White keeps the default scene camera; Black gets mirrored
// 180 degrees around the board's vertical axis
public class BoardCameraController : MonoBehaviour
{
    // ---------- Preview (debug) ----------
    [ContextMenu("Preview Black View")]
    private void PreviewBlack() => PreviewAs("black");

    [ContextMenu("Preview White View (reset)")]
    private void PreviewWhite() => PreviewAs("white");

    private void PreviewAs(string color)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Camera preview is Play-mode only (baseline is captured on " +
                             "Start; edit-mode transform changes would be saved to the scene).", this);
            return;
        }
        Orient(color);
    }

    // ---------- Main class ----------
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private Camera _camera;         // empty = Camera.main
    [SerializeField] private Transform _boardCenter;  // board root; its origin is the board's center

    private Vector3 _basePosition;
    private Quaternion _baseRotation;

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;
    }

    private void Start()
    {
        if (_camera == null || _boardCenter == null)
        {
            Debug.LogError("BoardCameraController: missing camera or board centre; disabling.", this);
            enabled = false;   // triggers OnDisable -> unsubscribes cleanly
            return;
        }

        _basePosition = _camera.transform.position;
        _baseRotation = _camera.transform.rotation;
    }

    private void OnEnable()
    {
        if (_session != null)
            _session.OnGameStarted += HandleGameStarted;
    }

    private void OnDisable()
    {
        if (_session != null)
            _session.OnGameStarted -= HandleGameStarted;
    }

    private void HandleGameStarted(GameEventInfo game) => Orient(game.color);

    private void Orient(string color)
    {
        _camera.transform.SetPositionAndRotation(_basePosition, _baseRotation);

        bool playingBlack = string.Equals(color, "black", System.StringComparison.OrdinalIgnoreCase);

        if (playingBlack)
            _camera.transform.RotateAround(_boardCenter.position, Vector3.up, 180f);
    }
}