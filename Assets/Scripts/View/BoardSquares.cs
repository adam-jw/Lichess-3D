using UnityEngine;
using UnityEngine.Rendering;

// Builds the visible 8x8 grid from BoardView's square mapping
// square positions = BoardView's local space
public class BoardSquares : MonoBehaviour
{
    private const string RootName = "Squares";

    [SerializeField] private BoardView _boardView;   // square -> local position mapping

    [Header("Appearance")]
    [SerializeField] private Material _squareMaterial;    // shared; per-square color comes from an MPB
    [SerializeField] private Color _lightColor = new Color(0.93f, 0.90f, 0.82f);
    [SerializeField] private Color _darkColor = new Color(0.42f, 0.53f, 0.38f);
    [SerializeField] private float _thickness = 0.12f;    // slab depth; the top face lands on y = 0
    [SerializeField] private float _surfaceOffset = 0f;   // top face relative to the logic plane
    [SerializeField] private Mesh _squareMesh;            // optional; unit cube when unassigned

    private MeshRenderer[] _squares;   // indexed file * 8 + rank
    private MaterialPropertyBlock _mpb;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static Mesh _unitCube;

    private BoardView View => _boardView != null ? _boardView : GetComponent<BoardView>();

    private static int Index(Square sq) => sq.File * 8 + sq.Rank;

    // a1 is dark, so an even file+rank sum is a dark square
    public Color BaseColorFor(Square sq) => ((sq.File + sq.Rank) % 2 == 0) ? _darkColor : _lightColor;

    public float SurfaceY => _surfaceOffset;   // local y of the board's top face

    private void Awake() => Rebuild();

    // ---------- Build ----------

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (View == null)
        {
            Debug.LogError("BoardSquares: no BoardView to take the square mapping from", this);
            return;
        }

        Transform existing = transform.Find(RootName);
        if (existing != null) DestroySafely(existing.gameObject);

        var root = new GameObject(RootName);
        root.transform.SetParent(transform, worldPositionStays: false);

        Mesh mesh = _squareMesh != null ? _squareMesh : UnitCube();
        _squares = new MeshRenderer[64];

        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
            {
                var sq = new Square(file, rank);
                var go = new GameObject(sq.ToString());
                go.transform.SetParent(root.transform, worldPositionStays: false);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _squareMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;   // nothing under the board to shade
                mr.receiveShadows = true;

                _squares[Index(sq)] = mr;
            }

        Layout();
        ApplyBaseColors();
    }

    // Position and scale every square from the current mapping. Safe to re-run.
    private void Layout()
    {
        BoardView view = View;
        if (view == null || !EnsureCache()) return;

        float size = view.SquareSize;

        for (int i = 0; i < 64; i++)
        {
            MeshRenderer mr = _squares[i];
            if (mr == null) continue;

            // top face sits at _surfaceOffset, so drop the center by half the thickness
            mr.transform.localPosition = view.SquareToLocal(i / 8, i % 8)
                                       + Vector3.up * (_surfaceOffset - _thickness * 0.5f);
            mr.transform.localScale = new Vector3(size, _thickness, size);
        }
    }

    // ---------- Color ----------

    [ContextMenu("Apply Colors")]
    public void ApplyBaseColors()
    {
        if (!EnsureCache()) return;

        for (int i = 0; i < 64; i++)
        {
            var sq = new Square(i / 8, i % 8);
            SetColor(sq, BaseColorFor(sq));
        }
    }

    public void SetColor(Square sq, Color color)
    {
        if (!EnsureCache()) return;

        MeshRenderer mr = _squares[Index(sq)];
        if (mr == null) return;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, color);
        mr.SetPropertyBlock(_mpb);
    }

    public void ResetColor(Square sq) => SetColor(sq, BaseColorFor(sq));

    // ---------- Plumbing ----------

    // The cache doesn't survive a recompile but the objects do, so rebuild it from the children
    private bool EnsureCache()
    {
        if (_squares != null) return true;

        Transform root = transform.Find(RootName);
        if (root == null || root.childCount == 0) return false;

        _squares = new MeshRenderer[64];
        foreach (Transform child in root)
        {
            if (child.name.Length != 2) continue;

            int file = child.name[0] - 'a';
            int rank = child.name[1] - '1';
            if (!Square.IsValid(file, rank)) continue;

            _squares[file * 8 + rank] = child.GetComponent<MeshRenderer>();
        }
        return true;
    }

    // Unity's built-in cube, borrowed off a throwaway primitive
    private static Mesh UnitCube()
    {
        if (_unitCube == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _unitCube = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroySafely(temp);
        }
        return _unitCube;
    }

    private static void DestroySafely(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private void OnValidate()
    {
        if (_thickness < 0.001f) _thickness = 0.001f;

        // color and geometry tweaks apply live; square size changes need a Rebuild
        Layout();
        ApplyBaseColors();
    }
}