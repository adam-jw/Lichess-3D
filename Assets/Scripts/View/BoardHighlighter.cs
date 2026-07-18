using System.Collections.Generic;
using UnityEngine;

// Renders board highlights as transparent quads overlaid on squares

public class BoardHighlighter : MonoBehaviour
{
    public enum HighlightLayer { Hover, Selection, LastMove, Premove }   // add LegalMove, Check later

    [SerializeField] private BoardView _boardView;              // shared square->local mapping
    [SerializeField] private LichessGameSession _session;
    [SerializeField] private GameObject _highlightPrefab;
    [SerializeField] private float _heightOffset = 0.02f;       // float above the board, dodge z-fighting

    [Header("Palette")]
    [SerializeField] private Color _hoverColor = new Color(1f, 1f, 1f, 0.12f);      
    [SerializeField] private Color _selectableColor = new Color(0.4f, 0.9f, 0.4f, 0.35f);
    [SerializeField] private Color _selectionColor = new Color(0.3f, 0.7f, 1f, 0.5f);   
    [SerializeField] private Color _lastMoveColor = new Color(0.95f, 0.85f, 0.25f, 0.35f); 
    [SerializeField] private Color _premoveColor = new Color(0.65f, 0.4f, 0.95f, 0.45f);


    // Active quads per layer, plus a shared pool of hidden quads to reuse, so a
    // moving hover square doesn't instantiate/destroy a quad every frame
    private readonly Dictionary<HighlightLayer, List<GameObject>> _active =
        new Dictionary<HighlightLayer, List<GameObject>>();
    private readonly Stack<GameObject> _pool = new Stack<GameObject>();

    private void OnEnable()
    {
        if (_boardView != null)
            _boardView.OnViewedMoveChanged += HandleViewedMove;
    }

    private void OnDisable()
    {
        if (_boardView != null)
            _boardView.OnViewedMoveChanged -= HandleViewedMove;
    }

    // Follows the *displayed* position, so the highlight tracks wherever you've scrolled
    private void HandleViewedMove(string move)
    {
        if (string.IsNullOrEmpty(move)) { ClearLayer(HighlightLayer.LastMove); return; }

        if (TryParseSquare(move, 0, out int ff, out int fr) &&
            TryParseSquare(move, 2, out int tf, out int tr))
            SetLayer(HighlightLayer.LastMove, _lastMoveColor, (ff, fr), (tf, tr));
        else
            ClearLayer(HighlightLayer.LastMove);
    }

    // ---------- Intent API (called by BoardInput) ----------

    public void SetHover(int file, int rank, bool selectable) =>
        SetLayer(HighlightLayer.Hover, selectable ? _selectableColor : _hoverColor, (file, rank));

    public void ClearHover() => ClearLayer(HighlightLayer.Hover);

    public void SetSelection(int file, int rank) =>
        SetLayer(HighlightLayer.Selection, _selectionColor, (file, rank));

    public void ClearSelection() => ClearLayer(HighlightLayer.Selection);

    public void SetPremove(int fromFile, int fromRank, int toFile, int toRank) =>
        SetLayer(HighlightLayer.Premove, _premoveColor, (fromFile, fromRank), (toFile, toRank));

    public void ClearPremove() => ClearLayer(HighlightLayer.Premove);

    // ---------- Game-state layer: last move pulled from the stream ----------

    private void HandleMovesReceived(string moves)
    {
        if (string.IsNullOrWhiteSpace(moves))
        {
            ClearLayer(HighlightLayer.LastMove);   // new game / no moves yet
            return;
        }

        string[] tokens = moves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        string last = tokens[tokens.Length - 1];   // the move just played, e.g. "e2e4" or "e7e8q"

        if (TryParseSquare(last, 0, out int fromFile, out int fromRank) &&
            TryParseSquare(last, 2, out int toFile, out int toRank))
            SetLayer(HighlightLayer.LastMove, _lastMoveColor, (fromFile, fromRank), (toFile, toRank));
        else
            ClearLayer(HighlightLayer.LastMove);
    }

    // Reconcile a layer's quads to exactly 'squares', in 'color'
    private void SetLayer(HighlightLayer layer, Color color, params (int file, int rank)[] squares)
    {
        if (_highlightPrefab == null || _boardView == null)
            return;

        if (!_active.TryGetValue(layer, out List<GameObject> quads))
        {
            quads = new List<GameObject>();
            _active[layer] = quads;
        }

        Recycle(quads);

        foreach ((int file, int rank) in squares)
        {
            GameObject quad = Take();
            quad.transform.localPosition = _boardView.SquareToLocal(file, rank) + Vector3.up * _heightOffset;

            quad.GetComponent<Renderer>().material.SetColor("_BaseColor", color);

            quads.Add(quad);
        }
    }

    private void ClearLayer(HighlightLayer layer)
    {
        if (_active.TryGetValue(layer, out List<GameObject> quads))
            Recycle(quads);
    }

    // ---------- Pool helpers ----------

    private GameObject Take()
    {
        // Parent under the BOARD ROOT, not this component
        GameObject quad = _pool.Count > 0
            ? _pool.Pop()
            : Instantiate(_highlightPrefab, _boardView.transform);
        quad.SetActive(true);
        return quad;
    }

    private void Recycle(List<GameObject> quads)
    {
        foreach (GameObject quad in quads)
        {
            quad.SetActive(false);
            _pool.Push(quad);
        }
        quads.Clear();
    }

    // Parse two chars of a UCI move (offset 0 = from-square, 2 = to-square).
    private static bool TryParseSquare(string uci, int offset, out int file, out int rank)
    {
        file = rank = -1;
        if (uci == null || uci.Length < offset + 2)
            return false;
        file = uci[offset] - 'a';
        rank = uci[offset + 1] - '1';
        return file >= 0 && file < 8 && rank >= 0 && rank < 8;
    }
}