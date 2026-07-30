using UnityEngine;

// Keyboard/mouse camera control:
//   Up / Down Arrows                   - tilt up/down (more/less overhead)
//   Mouse wheel (middle click)-drag    - tilt by dragging vertically
//   R                                  - restore default view
//   F                                  - flip the board (view from the other side)
public class CameraInput : MonoBehaviour
{
    private const int MiddleMouseButton = 2;   // Unity: 0 left, 1 right, 2 middle

    [SerializeField] private BoardCameraController _cameraController;

    [Header("Keyboard")]
    [Tooltip("Degrees of tilt per second while a key is held. At 60, sweeping the full " +
             "default range takes a little over a second.")]
    [SerializeField] private float _keyPitchRate = 60f;

    [Header("Mouse")]
    [Tooltip("Degrees of tilt per pixel of vertical middle-drag.")]
    [SerializeField] private float _dragDegreesPerPixel = 0.25f;

    [Tooltip("Off: dragging up tilts toward overhead. On: dragging up tilts away.")]
    [SerializeField] private bool _invertDrag = false;

    private bool _dragging;
    private Vector3 _lastMousePosition;

    private void Update()
    {
        if (_cameraController == null) return;

        HandleKeys();
        HandleDrag();

        if (Input.GetKeyDown(KeyCode.F))
            _cameraController.ToggleFlip();

        if (Input.GetKeyDown(KeyCode.R))
            _cameraController.ResetView();
    }

    private void HandleKeys()
    {
        float direction = 0f;

        // Summed rather than if/else-if so that holding both keys cancels to zero
        if (Input.GetKey(KeyCode.UpArrow)) direction += 1f;      // toward overhead
        if (Input.GetKey(KeyCode.DownArrow)) direction -= 1f;

        if (direction != 0f)
            _cameraController.AddPitch(direction * _keyPitchRate * Time.deltaTime);
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(MiddleMouseButton))
        {
            _dragging = true;
            _lastMousePosition = Input.mousePosition;
            return;
        }

        if (!Input.GetMouseButton(MiddleMouseButton))
        {
            _dragging = false;
            return;
        }

        if (!_dragging) return;

        Vector3 mousePosition = Input.mousePosition;
        float deltaPixels = mousePosition.y - _lastMousePosition.y;
        _lastMousePosition = mousePosition;

        if (Mathf.Approximately(deltaPixels, 0f)) return;

        float sign = _invertDrag ? -1f : 1f;
        _cameraController.AddPitch(sign * deltaPixels * _dragDegreesPerPixel);
    }
}