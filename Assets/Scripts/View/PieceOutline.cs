using UnityEngine;

// Drives the inverted-hull shell(s) on one piece
// Single shared outline material; color/width pushed per-piece via MaterialPropertyBlock
[DisallowMultipleComponent]
public class PieceOutline : MonoBehaviour
{
    public PieceColor PieceColor { get; private set; }

    private MeshRenderer[] _shells;
    private MaterialPropertyBlock _mpb;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    // Called by BoardView right after the shells are built. Takes only the shell
    // renderers, not the piece's own, so Apply can't tint the piece itself
    public void Initialize(PieceColor color, MeshRenderer[] shells)
    {
        PieceColor = color;
        _shells = shells;
        _mpb = new MaterialPropertyBlock();
    }

    public void Apply(Color color, float width)
    {
        if (_shells == null) return;
        _mpb.SetColor(OutlineColorId, color);
        _mpb.SetFloat(OutlineWidthId, width);
        foreach (MeshRenderer r in _shells)
            if (r != null) r.SetPropertyBlock(_mpb);
    }
}