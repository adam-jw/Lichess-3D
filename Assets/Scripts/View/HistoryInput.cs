using UnityEngine;

// Arrow keys navigate move history. A tap steps once, animated.  
// Holding longer than _holdDelay switches to rapid, unanimated scrubbing
public class HistoryInput : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private float _holdDelay = 0.6f;        // hold this long before rapid scrub
    [SerializeField] private float _repeatInterval = 0.05f;  // seconds between rapid steps (~20/sec)

    private KeyCode _heldKey = KeyCode.None;
    private float _heldTime;
    private float _repeatTimer;

    private void Update()
    {
        if (_boardView == null) return;

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