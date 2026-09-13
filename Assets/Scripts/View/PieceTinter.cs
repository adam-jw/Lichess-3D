using UnityEngine;

// Allows us to select the color for each side's pieces from the Inspector
public class PieceTinter : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;

    [Header("Piece colors")]
    [SerializeField] private Color _whiteColor = new Color(0.90f, 0.88f, 0.82f);
    [SerializeField] private Color _blackColor = new Color(0.16f, 0.16f, 0.18f);

    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void OnEnable()
    {
        if (_boardView != null) _boardView.OnPieceSpawned += Tint;
    }

    private void OnDisable()
    {
        if (_boardView != null) _boardView.OnPieceSpawned -= Tint;
    }

    private void Tint(GameObject piece)
    {
        PieceRef pr = piece.GetComponent<PieceRef>();
        if (pr == null) return;

        Color color = pr.Color == PieceColor.White ? _whiteColor : _blackColor;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // only recolor the renderers that carry the piece material, skip the outlines
        foreach (MeshRenderer mr in piece.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.GetComponent<MeshFilter>() != null &&
                mr.transform.name == "Outline") continue;   

            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            mr.SetPropertyBlock(_mpb);
        }
    }

    // Repaint everything already on the board when you tweak a color in edit mode
    [ContextMenu("Apply To Existing")]
    public void ApplyToExisting()
    {
        foreach (PieceRef pr in GetComponentsInChildren<PieceRef>())
            Tint(pr.gameObject);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) ApplyToExisting();
    }
}