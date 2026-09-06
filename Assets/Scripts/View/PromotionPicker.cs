using System.Collections.Generic;
using UnityEngine;

// Off-board pop-up of the four promotion choices, drawn over everything via the
// picker layer + overlay camera. Owns placement, presentation, and hover feel;
// BoardInput reads the clicked option's PromotionOption to resolve the choice
public class PromotionPicker : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private Camera _camera;   // for the hover raycast; leave empty for Camera.main

    [Header("Layer")]
    [SerializeField] private string _pickerLayer = "PromotionPicker";

    [Header("Layout")]
    [SerializeField] private float _edgeOffset = 1.2f;   // gap from the promotion square out past the back rank
    [SerializeField] private float _spacing = 1.1f;      // gap between the four choices
    [SerializeField] private float _height = 0.6f;       // float above the board surface
    [SerializeField] private float _pieceScale = 1.1f;   // presentation size

    [Header("Facing")]
    [SerializeField] private float _faceYaw = 0f;
    [SerializeField] private bool _flipBlack = true;

    [Header("Backing (optional, one behind each piece)")]
    [SerializeField] private GameObject _backingPrefab;   // null = no backings
    [SerializeField] private float _backingScale = 1f;
    [SerializeField] private float _backingLift = 0f;     // nudge off the piece's base if it z-fights
    [SerializeField] private Color[] _backingColors;      // optional per-piece tint; empty = use the prefab as-is
    [SerializeField] private string _backingColorProperty = "_BaseColor";   

    [Header("Hover")]
    [SerializeField] private float _hoverScale = 1.25f;   // multiplies rest scale for the piece under the cursor

    [Header("Outline accent")]
    [SerializeField] private bool _useOutline = true;
    [SerializeField] private Color _outlineColor = new Color(1f, 0.82f, 0.25f);   // rest: warm gold
    [SerializeField] private float _outlineWidth = 0.015f;                        // world units
    [SerializeField] private Color _hoverOutlineColor = Color.white;
    [SerializeField] private float _hoverOutlineWidth = 0.03f;

    [Header("Idle motion (0 = off)")]
    [SerializeField] private float _spinSpeed = 0f;      // degrees/sec around Y
    [SerializeField] private float _bobSpeed = 0f;       // higher = faster bob
    [SerializeField] private float _bobAmplitude = 0f;   // world units up/down

    [Header("Preview (Play mode)")]
    [SerializeField] private PieceColor _previewColor = PieceColor.White;
    [SerializeField] private int _previewFile = 4;

    // Left to right across the row: queen, rook, bishop, knight
    private static readonly PieceType[] Choices =
        { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

    private struct Choice
    {
        public GameObject go;
        public GameObject backing;
        public Vector3 restScale;
        public Vector3 restPos;
        public Quaternion baseRot;
        public PieceOutline outline;
    }
    private readonly List<Choice> _choices = new List<Choice>();

    private int _layerMask;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        _layerMask = LayerMask.GetMask(_pickerLayer);

        if (LayerMask.NameToLayer(_pickerLayer) < 0)
            Debug.LogError($"Picker layer '{_pickerLayer}' doesn't exist; add it in Tags & Layers.", this);
    }

    // Returns false if no choices could be built, so the caller can fall back to auto-queen
    public bool Open(Square promotionSquare, PieceColor color)
    {
        Close();   // never stack two pickers

        int layer = LayerMask.NameToLayer(_pickerLayer);
        int dir = color == PieceColor.White ? 1 : -1;   // push off the correct back rank
        Vector3 rankPush = new Vector3(0f, 0f, dir);
        float centerIndex = (Choices.Length - 1) * 0.5f;
        Vector3 baseLocal = _boardView.SquareToLocal(promotionSquare.File, promotionSquare.Rank);

        for (int i = 0; i < Choices.Length; i++)
        {
            GameObject prefab = _boardView.GetPiecePrefab(Choices[i], color);
            if (prefab == null) continue;

            float across = (i - centerIndex) * _spacing;   // horizontal row, centered on the promotion file
            Vector3 pos = baseLocal + new Vector3(across, 0f, 0f) + rankPush * _edgeOffset + Vector3.up * _height;
            Quaternion baseRot = FacingFor(color);

            GameObject go = Instantiate(prefab, _boardView.transform);
            go.transform.localScale *= _pieceScale;
            go.transform.localPosition = pos;
            go.transform.localRotation = baseRot;
            go.AddComponent<PromotionOption>().Type = Choices[i];

            PieceOutline outline = null;
            if (_useOutline)
            {
                outline = _boardView.AttachOutline(go, color);   // shells parent under the piece
                outline?.Apply(_outlineColor, _outlineWidth);
            }

            SetLayerRecursive(go, layer);   // after the outline, so its shells inherit the picker layer

            GameObject backing = null;
            if (_backingPrefab != null)
            {
                backing = Instantiate(_backingPrefab, _boardView.transform);
                backing.transform.localPosition = pos + Vector3.up * _backingLift;
                backing.transform.localScale *= _backingScale;
                SetLayerRecursive(backing, layer);
                TintBacking(backing, i);
            }

            _choices.Add(new Choice
            {
                go = go,
                backing = backing,
                restScale = go.transform.localScale,
                restPos = pos,
                baseRot = baseRot,
                outline = outline
            });
        }

        IsOpen = _choices.Count > 0;
        return IsOpen;
    }

    public void Close()
    {
        foreach (Choice c in _choices)
        {
            if (c.go != null) Destroy(c.go);
            if (c.backing != null) Destroy(c.backing);
        }
        _choices.Clear();
        IsOpen = false;
    }

    private void Update()
    {
        if (IsOpen) UpdateChoices();
    }

    // Hover feel, idle motion, and outline state, recomputed from rest each frame
    private void UpdateChoices()
    {
        GameObject hovered = RaycastOption()?.gameObject;

        float t = Time.time;
        float bob = _bobAmplitude != 0f ? Mathf.Sin(t * _bobSpeed) * _bobAmplitude : 0f;
        float spin = _spinSpeed != 0f ? t * _spinSpeed : 0f;

        foreach (Choice c in _choices)
        {
            if (c.go == null) continue;
            bool isHovered = c.go == hovered;

            c.go.transform.localScale = isHovered ? c.restScale * _hoverScale : c.restScale;
            c.go.transform.localPosition = c.restPos + Vector3.up * bob;
            c.go.transform.localRotation = _spinSpeed != 0f
                ? c.baseRot * Quaternion.Euler(0f, spin, 0f)
                : c.baseRot;

            if (c.outline != null)
                c.outline.Apply(isHovered ? _hoverOutlineColor : _outlineColor,
                                isHovered ? _hoverOutlineWidth : _outlineWidth);
        }
    }

    private PromotionOption RaycastOption()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _layerMask))
            return hit.collider.GetComponentInParent<PromotionOption>();
        return null;
    }

    // True + the chosen type if the mouse is currently over one of the option pieces
    public bool TryPick(out PieceType type)
    {
        type = PieceType.None;
        if (!IsOpen) return false;

        PromotionOption option = RaycastOption();
        if (option == null) return false;

        type = option.Type;
        return true;
    }

    private Quaternion FacingFor(PieceColor color) =>
        Quaternion.Euler(0f, _faceYaw + (color == PieceColor.Black && _flipBlack ? 180f : 0f), 0f);

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // Per-piece tint via a property block so we don't spawn material instances
    private void TintBacking(GameObject backing, int index)
    {
        if (_backingColors == null || index >= _backingColors.Length) return;
        Renderer r = backing.GetComponentInChildren<Renderer>();
        if (r == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor(_backingColorProperty, _backingColors[index]);
        r.SetPropertyBlock(mpb);
    }

    [ContextMenu("Preview picker")]
    private void PreviewPicker() =>
        Open(new Square(_previewFile, _previewColor == PieceColor.White ? 7 : 0), _previewColor);

    [ContextMenu("Close picker")]
    private void ClosePreview() => Close();
}