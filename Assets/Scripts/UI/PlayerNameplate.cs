using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Renders one player's name, rating and color dot
public class PlayerNameplate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;

    [Header("Colour dot")]
    [Tooltip("Parent circle. Stays the outline color; the fill sits on top, inset")]
    [SerializeField] private GameObject _dotRoot;
    [SerializeField] private Image _dotFill;

    [Tooltip("Fill color when this player is White. The black parent circle produces the outline")]
    [SerializeField] private Color _whiteFill = Color.white;

    [Tooltip("Fill color when this player is Black")]
    [SerializeField] private Color _blackFill = Color.black;

    // color = null in the idle state
    public void SetPlayer(string displayName, string ratingText, PieceColor? colour)
    {
        if (_text != null)
        {
            _text.text = string.IsNullOrEmpty(ratingText)
                ? displayName
                : displayName + " (" + ratingText + ")";
        }

        if (_dotRoot != null)
            _dotRoot.SetActive(colour.HasValue);

        if (_dotFill != null && colour.HasValue)
            _dotFill.color = colour.Value == PieceColor.White ? _whiteFill : _blackFill;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}