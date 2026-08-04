using UnityEngine;

// Orients the player's camera to their side of the board each game
//
// The camera is parameterised as an ORBIT: it rides an arc of fixed radius centred
// on the board, always looking at the pivot; Pitch is the single continuous axis
public class BoardCameraController : MonoBehaviour
{
    // ---------- Previews (debug, Play-mode only) ----------

    [ContextMenu("Preview White View")]
    private void PreviewWhite() => Preview(() => Orient(PieceColor.White));

    [ContextMenu("Preview Black View")]
    private void PreviewBlack() => Preview(() => Orient(PieceColor.Black));

    [ContextMenu("Preview Overhead")]
    private void PreviewOverhead() => Preview(() => SetPitch(_maxPitch));

    [ContextMenu("Preview Min Tilt")]
    private void PreviewMinTilt() => Preview(() => SetPitch(_minPitch));

    [ContextMenu("Preview Flip")]
    private void PreviewFlip() => Preview(ToggleFlip);

    [ContextMenu("Preview Reset View")]
    private void PreviewReset() => Preview(ResetView);

    private void Preview(System.Action action)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Camera preview is Play-mode only (the arc is derived from the " +
                             "authored pose on Start; edit-mode transform changes would be " +
                             "saved into the scene).", this);
            return;
        }
        action();
    }

    // ---------- Configuration ----------
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private Camera _camera;          // empty = Camera.main
    [SerializeField] private Transform _boardCenter;  // board root; its origin is the board's center

    [Header("Orbit")]
    [Tooltip("Height above the board root that the camera aims at and orbits around. " +
             "0 reproduces the original behaviour (aiming at the board surface). Raising " +
             "it toward piece height makes shallow views sit better.")]
    [SerializeField] private float _pivotHeight = 0f;

    [Tooltip("Starting tilt in degrees above the board plane; 90 is perfectly overhead")]
    [Range(0f, 90f)]
    [SerializeField] private float _defaultPitch = 45f;

    [Tooltip("Shallowest allowed tilt")]
    [Range(0f, 90f)]
    [SerializeField] private float _minPitch = 20f;

    [Tooltip("Steepest allowed tilt. 90 = overhead view")]
    [Range(0f, 90f)]
    [SerializeField] private float _maxPitch = 90f;

    // ---------- Derived once, from the authored scene pose ----------
    private float _baseYaw;    // the yaw that seats the camera on White's side
    private float _distance;   // the arc's radius

    // ---------- Live state ----------
    private float _pitch;          // the one continuous axis; user-owned, persists across games
    private bool _playingBlack;    // seat layer: written only by Orient
    private bool _flipped;         // view layer: written only by ToggleFlip, cleared by Orient

    private Vector3 Pivot => _boardCenter.position + Vector3.up * _pivotHeight;

    public float Pitch => _pitch;
    public float MinPitch => _minPitch;
    public float MaxPitch => _maxPitch;
    public bool IsFlipped => _flipped;

    // Fired whenever the pose changes
    public event System.Action OnViewChanged;

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

        if (_minPitch > _maxPitch)
        {
            Debug.LogWarning($"BoardCameraController: _minPitch ({_minPitch}) exceeds _maxPitch " +
                             $"({_maxPitch}); treating the range as [{_maxPitch}, {_maxPitch}].", this);
        }

        if (!DecomposeAuthoredPose())
        {
            enabled = false;
            return;
        }

        float clamped = ClampPitch(_defaultPitch);
        if (!Mathf.Approximately(clamped, _defaultPitch))
        {
            Debug.LogWarning($"BoardCameraController: _defaultPitch ({_defaultPitch:F1}) is outside " +
                             $"[{_minPitch:F1}, {_maxPitch:F1}]; starting at {clamped:F1} instead.", this);
        }

        _pitch = clamped;
        Apply();
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

    // ---------- Deriving the arc from the scene ----------

    // The authored camera pose stays the authoring surface, but only for the arc: its distance
    // from the pivot becomes the radius, and its horizontal direction defines White's seat
    private bool DecomposeAuthoredPose()
    {
        Vector3 offset = _camera.transform.position - Pivot;

        _distance = offset.magnitude;
        if (_distance < 0.001f)
        {
            Debug.LogError("BoardCameraController: the authored camera sits on the pivot, so no " +
                           "orbit can be derived from it. Move the camera away from the board " +
                           "centre (or check _pivotHeight).", this);
            return false;
        }

        _baseYaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;

        // Clamped before Asin to keep float error out of the domain
        float authoredPitch = Mathf.Asin(Mathf.Clamp(offset.y / _distance, -1f, 1f)) * Mathf.Rad2Deg;
        /*Debug.Log($"BoardCameraController: scene pose implies pitch {authoredPitch:F1} at distance " +
                  $"{_distance:F2} (White seat yaw {_baseYaw:F1}). Starting pitch is _defaultPitch " +
                  $"= {_defaultPitch:F1}; paste {authoredPitch:F1} into it to keep the authored framing.", this);*/

        return true;
    }

    // ---------- Public intent API ----------
    public void SetPitch(float degrees)
    {
        _pitch = ClampPitch(degrees);
        Apply();
    }

    public void AddPitch(float degrees) => SetPitch(_pitch + degrees);

    public void ToggleFlip()
    {
        _flipped = !_flipped;
        Apply();
    }

    // Back to default tilt, facing your own seat
    public void ResetView()
    {
        _pitch = ClampPitch(_defaultPitch);
        _flipped = false;
        Apply();
    }

    private float ClampPitch(float degrees) =>
        Mathf.Clamp(degrees, Mathf.Min(_minPitch, _maxPitch), _maxPitch);

    // ---------- Orientation ----------

    private void HandleGameStarted(GameEventInfo game)
    {
        Orient(_session.MyColor ?? PieceColor.White);
    }

    private void Orient(PieceColor color)
    {
        _playingBlack = color == PieceColor.Black;

        _flipped = false;

        Apply();
    }

    // The single write path: pose as a pure function of the parameters
    private void Apply()
    {
        float yaw = _baseYaw + ((_playingBlack != _flipped) ? 180f : 0f);

        Quaternion rotation = Quaternion.Euler(_pitch, yaw, 0f);
        Vector3 position = Pivot - rotation * Vector3.forward * _distance;

        _camera.transform.SetPositionAndRotation(position, rotation);
        OnViewChanged?.Invoke();
    }
}