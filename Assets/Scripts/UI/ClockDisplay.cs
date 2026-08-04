using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Renders one clock. Dumb; is told a time and two flags and draws them
public class ClockDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Image _background;

    [Header("Active / inactive")]

    [SerializeField] private Color _activeText = Color.black;

    [Tooltip("Text color for the side that is waiting.")]
    [SerializeField] private Color _inactiveText = new Color(0f, 0f, 0f, 0.4f);

    [Header("Digit spacing")]
    [Tooltip("Fixed character width, in em, applied via TMP's <mspace> tag.")]
    [SerializeField] private float _monospaceEm = 0.62f;

    [Header("Low time")]
    [SerializeField] private Color _normalBackground = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color _lowBackground = new Color(0.8f, 0.15f, 0.15f, 0.6f);
    [SerializeField] private Color _lowText = Color.white;

    private string _lastText;

    public void SetTime(int remainingMs, bool active, bool low, bool showTenths)
    {
        string formatted = Monospaced(Format(remainingMs, showTenths));

        if (_text != null)
        {
            if (formatted != _lastText)
            {
                _text.text = formatted;
                _lastText = formatted;
            }

            _text.color = low ? _lowText : (active ? _activeText : _inactiveText);
        }

        if (_background != null)
            _background.color = low ? _lowBackground : _normalBackground;
    }

    private string Monospaced(string text) =>
        _monospaceEm > 0f
            ? "<mspace=" + _monospaceEm.ToString("0.###") + "em>" + text + "</mspace>"
            : text;

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    // Minutes are always shown, so the string keeps a stable shape as time drains and
    // the layout does not shuffle. Tenths appear only when time is below 10s
    private static string Format(int remainingMs, bool showTenths)
    {
        if (remainingMs < 0) remainingMs = 0;

        int totalSeconds = remainingMs / 1000;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (!showTenths)
            return minutes.ToString("00") + ":" + seconds.ToString("00");

        int tenths = (remainingMs % 1000) / 100;
        return minutes.ToString("00") + ":" + seconds.ToString("00") + "." + tenths;
    }
}