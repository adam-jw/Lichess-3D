using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws right-click arrows as one procedural mesh, color baked per vertex so the
// whole set is a single draw call. Geometry is built in the board's local XZ plane,
// same space as SquareToLocal, then lifted to the board surface
public class BoardArrows : MonoBehaviour
{
    private const string RootName = "Arrows";

    [SerializeField] private BoardView _boardView;
    [SerializeField] private BoardSquares _boardSquares;   // for the surface height; optional
    [SerializeField] private Material _arrowMaterial;      // Custom/BoardArrow

    [Header("Shape")]
    [SerializeField] private float _width = 0.18f;         // shaft width, world units
    [SerializeField] private float _headLength = 0.36f;
    [SerializeField] private float _headWidth = 0.46f;
    [SerializeField] private float _tailInset = 0.32f;     // gap from origin center to the tail
    [SerializeField] private float _headInset = 0.15f;   // gap from destination center to the tip
    [SerializeField] private float _heightOffset = 0.02f;  // above the board surface

    private Mesh _mesh;
    private MeshRenderer _renderer;

    private readonly List<Vector3> _verts = new List<Vector3>();
    private readonly List<Color> _colors = new List<Color>();
    private readonly List<int> _tris = new List<int>();

    private float _y;   // current emit height, set per rebuild

    private float SurfaceY => _boardSquares != null ? _boardSquares.SurfaceY : 0f;

    private void Awake() => EnsureMesh();

    // ---------- Public API (driven by BoardMarkup) ----------

    public void SetArrows(IReadOnlyDictionary<(Square from, Square to), Color> arrows)
    {
        EnsureMesh();
        _verts.Clear();
        _colors.Clear();
        _tris.Clear();

        _y = SurfaceY + _heightOffset;

        if (arrows != null)
            foreach (KeyValuePair<(Square from, Square to), Color> kv in arrows)
                BuildArrow(kv.Key.from, kv.Key.to, kv.Value);

        Commit();
    }

    public void ClearArrows() => SetArrows(null);

    // ---------- Geometry ----------

    private void BuildArrow(Square from, Square to, Color color)
    {
        if (_boardView == null || from == to) return;

        Vector3 a = Flat(_boardView.SquareToLocal(from.File, from.Rank));
        Vector3 c = Flat(_boardView.SquareToLocal(to.File, to.Rank));

        int df = Mathf.Abs(to.File - from.File);
        int dr = Mathf.Abs(to.Rank - from.Rank);
        bool knight = (df == 1 && dr == 2) || (df == 2 && dr == 1);

        if (knight) BuildBentArrow(from, to, a, c, color);
        else BuildStraightArrow(a, c, color);
    }

    private void BuildStraightArrow(Vector3 a, Vector3 c, Color color)
    {
        Vector3 dir = (c - a).normalized;
        Vector3 tail = a + dir * _tailInset;
        Vector3 tip = c - dir * _headInset;
        Vector3 headBase = tip - dir * _headLength;

        // if the squares are close, keep at least a sliver of shaft
        if (Vector3.Dot(headBase - tail, dir) <= 0f) headBase = tail + dir * 0.001f;

        AddShaftSegment(tail, headBase, dir, color);
        AddHead(headBase, tip, dir, color);
    }

    private void BuildBentArrow(Square from, Square to, Vector3 a, Vector3 c, Color color)
    {
        // Long leg (the 2-square axis) runs first, then the turn
        bool longIsRank = Mathf.Abs(to.Rank - from.Rank) == 2;
        Square cornerSq = longIsRank
            ? new Square(from.File, to.Rank)
            : new Square(to.File, from.Rank);

        Vector3 corner = Flat(_boardView.SquareToLocal(cornerSq.File, cornerSq.Rank));

        Vector3 d1 = (corner - a).normalized;
        Vector3 d2 = (c - corner).normalized;

        Vector3 tail = a + d1 * _tailInset;
        Vector3 tip = c - d2 * _headInset;
        Vector3 headBase = tip - d2 * _headLength;

        float h = _width * 0.5f;
        Vector3 n1 = Perp(d1);
        Vector3 n2 = Perp(d2);

        // Miter: both legs meet along one shared edge, so translucent color never overlaps
        Vector3 m = (n1 + n2).normalized;
        float miterLen = h / Vector3.Dot(m, n1);
        Vector3 cornerL = corner + m * miterLen;
        Vector3 cornerR = corner - m * miterLen;

        AddQuad(tail + n1 * h, cornerL, cornerR, tail - n1 * h, color);   // long leg
        AddQuad(cornerL, headBase + n2 * h, headBase - n2 * h, cornerR, color);   // short leg
        AddHead(headBase, tip, d2, color);
    }

    // A plain rectangular shaft between two points
    private void AddShaftSegment(Vector3 start, Vector3 end, Vector3 dir, Color color)
    {
        Vector3 n = Perp(dir) * (_width * 0.5f);
        AddQuad(start + n, end + n, end - n, start - n, color);
    }

    private void AddHead(Vector3 baseCenter, Vector3 tip, Vector3 dir, Color color)
    {
        Vector3 n = Perp(dir) * (_headWidth * 0.5f);
        AddTri(tip, baseCenter + n, baseCenter - n, color);
    }

    // ---------- Mesh assembly ----------

    private void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
    {
        int i = _verts.Count;
        AddVert(p0, color);
        AddVert(p1, color);
        AddVert(p2, color);
        AddVert(p3, color);
        _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
    }

    private void AddTri(Vector3 p0, Vector3 p1, Vector3 p2, Color color)
    {
        int i = _verts.Count;
        AddVert(p0, color);
        AddVert(p1, color);
        AddVert(p2, color);
        _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
    }

    private void AddVert(Vector3 p, Color color)
    {
        _verts.Add(new Vector3(p.x, _y, p.z));
        _colors.Add(color);
    }

    private void Commit()
    {
        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_tris, 0);
        _mesh.RecalculateBounds();
    }

    // ---------- Plumbing ----------

    private void EnsureMesh()
    {
        if (_mesh != null && _renderer != null) return;

        Transform root = transform.Find(RootName);
        if (root == null)
        {
            var go = new GameObject(RootName);
            go.transform.SetParent(transform, worldPositionStays: false);
            root = go.transform;
        }

        MeshFilter mf = root.GetComponent<MeshFilter>();
        if (mf == null) mf = root.gameObject.AddComponent<MeshFilter>();

        _renderer = root.GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = root.gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _arrowMaterial;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "BoardArrows" };
            _mesh.MarkDynamic();   // rebuilt whenever arrows change
        }
        mf.sharedMesh = _mesh;
    }

    private static Vector3 Flat(Vector3 local) => new Vector3(local.x, 0f, local.z);

    // Left normal in the XZ plane
    private static Vector3 Perp(Vector3 d) => new Vector3(-d.z, 0f, d.x);
}