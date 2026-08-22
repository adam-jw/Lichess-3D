using UnityEngine;
using UnityEngine.EventSystems;

// Arrow keys & scroll wheel navigate move history
// Isolated input animates the pieces & plays audio
// Holding/scrolling longer than _holdDelay/_scrollBurstWindow switches to rapid, unanimated scrubbing
public class HistoryInput : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private float _holdDelay = 0.6f;        // hold this long before rapid scrub
    [SerializeField] private float _repeatInterval = 0.05f;  // seconds between rapid steps (~20/sec)

    [Header("Scroll")]
    [Tooltip("Scroll motion needed for one step. A standard mouse notch is 1.0; trackpads " +
             "emit many small deltas that accumulate. Enable _logScroll to see real values.")]
    [SerializeField] private float _notchThreshold = 1f;

    [Tooltip("Steps closer together than this are counted as a flick rather than notch: they snap instead of " +
             "tweening, which also suppresses their sound via BoardAudio's animate gate.")]
    [SerializeField] private float _scrollBurstWindow = 0.25f;

    [Tooltip("Ceiling on steps consumed per frame, so a violent flick cannot spend the whole game.")]
    [SerializeField] private int _maxStepsPerFrame = 3;

    [Tooltip("Off: scroll up steps back, like scrolling up a move list.")]
    [SerializeField] private bool _invertScroll;

    [Tooltip("Log raw scroll deltas. Use to tune _notchThreshold.")]
    [SerializeField] private bool _logScroll;

    private KeyCode _heldKey = KeyCode.None;
    private float _heldTime;
    private float _repeatTimer;

    private float _scrollAccum;
    private float _lastScrollStepTime = -999f;

    private void Update()
    {
        if (_boardView == null) return;

        HandleScroll();

        if (Input.GetKeyDown(KeyCode.Alpha1)) { _boardView.JumpToStart(); ClearHold(); return; }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { _boardView.JumpToLive(); ClearHold(); return; }

        // Fresh press: step once, animated, and start tracking the hold.
        if (Input.GetKeyDown(KeyCode.LeftArrow)) BeginHold(KeyCode.LeftArrow);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) BeginHold(KeyCode.RightArrow);

        if (_heldKey == KeyCode.None) return;

        if (!Input.GetKey(_heldKey)) { ClearHold(); return; }   // released

        _heldTime += Time.deltaTime;
        if (_heldTime < _holdDelay) return;

        _repeatTimer -= Time.deltaTime;
        if (_repeatTimer > 0f) return;

        _repeatTimer = _repeatInterval;
        Step(_heldKey, animate: false);                          // rapid scrub: snap, don't tween
    }

    private void HandleScroll()
    {
        // UI panel under the cursor owns its own scrolling
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        float delta = Input.mouseScrollDelta.y; 

        if (Mathf.Abs(delta) > 0.0001f)
        {
            if (_logScroll) Debug.Log($"[HistoryInput] scroll delta={delta:F3} accum={_scrollAccum:F3}", this);

            // Reversing mid-flick: drop the leftover, or the first notch back the other way
            // gets swallowed cancelling out motion the user already considers spent
            if (delta * _scrollAccum < 0f) _scrollAccum = 0f;

            _scrollAccum += delta;
        }

        if (_notchThreshold <= 0f) return;   // notchThreshold misconfigured

        int steps = 0;
        while (Mathf.Abs(_scrollAccum) >= _notchThreshold && steps < _maxStepsPerFrame)
        {
            bool up = _scrollAccum > 0f;

            // Subtract rather than zero: leftover motion carries to the next frame, so one
            // fast flick becomes several steps instead of being rounded down to one
            _scrollAccum -= Mathf.Sign(_scrollAccum) * _notchThreshold;

            // Steps arriving in quick succession are a flick being spent, not deliberate
            // notches. Snapping keeps the tweens from interrupting each other and keeps
            // BoardAudio quiet, exactly as a held arrow key does
            bool burst = Time.unscaledTime - _lastScrollStepTime < _scrollBurstWindow;
            _lastScrollStepTime = Time.unscaledTime;

            bool back = up != _invertScroll;
            if (back) _boardView.StepBack(!burst);
            else _boardView.StepForward(!burst);

            steps++;
        }

        // Hit the ceiling: discard the rest so a violent flick does not 
        // keep stepping after the user input stopped
        if (steps >= _maxStepsPerFrame) _scrollAccum = 0f;
    }

    private void BeginHold(KeyCode key)
    {
        _heldKey = key;
        _heldTime = 0f;
        _repeatTimer = 0f;
        Step(key, animate: true);
    }

    private void Step(KeyCode key, bool animate)
    {
        if (key == KeyCode.LeftArrow) _boardView.StepBack(animate);
        else _boardView.StepForward(animate);
    }

    private void ClearHold() => _heldKey = KeyCode.None;
}