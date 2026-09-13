using System.Collections.Generic;
using UnityEngine;

// Right-click square marks and arrows, stored per ply
// This is the one piece of board state nothing else can re-derive from the position,
// so it's owned here rather than pushed every frame like hover and selection
public class BoardMarkup : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private BoardInput _boardInput;
    [SerializeField] private BoardHighlighter _highlighter;

    [Header("Colors")]
    [SerializeField] private Color _plainColor = new Color(0.85f, 0.20f, 0.16f, 0.80f);
    [SerializeField] private Color _shiftColor = new Color(0.35f, 0.68f, 0.25f, 0.80f);
    [SerializeField] private Color _altColor = new Color(0.25f, 0.52f, 0.85f, 0.80f);
    [SerializeField] private Color _ctrlColor = new Color(0.95f, 0.60f, 0.15f, 0.80f);

    // One ply's worth of drawing
    private class MarkupSet
    {
        public readonly Dictionary<Square, Color> squares = new Dictionary<Square, Color>();
        public readonly Dictionary<(Square from, Square to), Color> arrows = new Dictionary<(Square, Square), Color>();

        public bool IsEmpty => squares.Count == 0 && arrows.Count == 0;

        public void CopyFrom(MarkupSet other)
        {
            foreach (KeyValuePair<Square, Color> kv in other.squares) squares[kv.Key] = kv.Value;
            foreach (KeyValuePair<(Square, Square), Color> kv in other.arrows) arrows[kv.Key] = kv.Value;
        }
    }

    private readonly Dictionary<int, MarkupSet> _byPly = new Dictionary<int, MarkupSet>();

    private int _shownPly;
    private int _lastLiveCount;
    private bool _drawing;
    private Square _drawOrigin;

    private void OnEnable()
    {
        if (_boardView != null) _boardView.OnViewedMoveChanged += HandleViewedMoveChanged;
        Resync();
    }

    private void OnDisable()
    {
        if (_boardView != null) _boardView.OnViewedMoveChanged -= HandleViewedMoveChanged;
    }

    private void Update()
    {
        if (_boardInput == null) return;

        if (Input.GetMouseButtonDown(0) && !BoardInput.IsPointerOverUI())
            ClearCurrentPly();

        if (Input.GetMouseButtonDown(1))
            BeginDraw();
        else if (Input.GetMouseButtonUp(1))
            EndDraw();
    }

    // ---------- Gesture ----------

    private void BeginDraw()
    {
        if (BoardInput.IsPointerOverUI()) return;

        _drawing = _boardInput.TryGetSquareUnderPointer(out Square sq);
        _drawOrigin = sq;
    }

    private void EndDraw()
    {
        if (!_drawing) return;
        _drawing = false;

        if (!_boardInput.TryGetSquareUnderPointer(out Square sq)) return;   // released off the board

        if (sq == _drawOrigin) ToggleSquare(sq, ActiveColor());
        else ToggleArrow(_drawOrigin, sq, ActiveColor());
    }

    // Priority when several modifiers are held
    private Color ActiveColor()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return _shiftColor;
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return _altColor;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return _ctrlColor;
        return _plainColor;
    }

    // ---------- Marks ----------

    private void ToggleSquare(Square sq, Color color)
    {
        MarkupSet set = CurrentSet(create: true);

        if (set.squares.TryGetValue(sq, out Color existing) && existing == color)
            set.squares.Remove(sq);     // same color again means erase
        else
            set.squares[sq] = color;

        Refresh();
    }

    // Stored now, drawn once the arrow renderer lands
    private void ToggleArrow(Square from, Square to, Color color)
    {
        MarkupSet set = CurrentSet(create: true);
        (Square, Square) key = (from, to);

        if (set.arrows.TryGetValue(key, out Color existing) && existing == color)
            set.arrows.Remove(key);
        else
            set.arrows[key] = color;

        Refresh();
    }

    public void ClearCurrentPly()
    {
        MarkupSet set = CurrentSet(create: false);
        if (set == null || set.IsEmpty) return;

        _byPly.Remove(_shownPly);
        Refresh();
    }

    private MarkupSet CurrentSet(bool create)
    {
        if (_byPly.TryGetValue(_shownPly, out MarkupSet set)) return set;
        if (!create) return null;

        return _byPly[_shownPly] = new MarkupSet();
    }

    private void Refresh()
    {
        if (_highlighter == null) return;

        MarkupSet set = CurrentSet(create: false);

        if (set == null || set.squares.Count == 0) _highlighter.ClearMarkup();
        else _highlighter.SetMarkup(set.squares);
    }

    // ---------- History ----------

    private void HandleViewedMoveChanged(string _) => Resync();

    private void Resync()
    {
        if (_boardView == null) return;

        int live = _boardView.LiveMoveCount;
        int ply = _boardView.ViewedMoveCount;

        if (live < _lastLiveCount)
            _byPly.Clear();                         // moves went backwards: new game
        else if (live > _lastLiveCount && _shownPly == _lastLiveCount && ply == live)
            CarryForward(_lastLiveCount, ply);      // a move landed while we were watching live

        _lastLiveCount = live;
        _shownPly = ply;
        _drawing = false;                           // position change mid-gesture cancels it

        Refresh();
    }

    private void CarryForward(int fromPly, int toPly)
    {
        if (!_byPly.TryGetValue(fromPly, out MarkupSet source) || source.IsEmpty) return;

        if (!_byPly.TryGetValue(toPly, out MarkupSet target))
            _byPly[toPly] = target = new MarkupSet();

        target.CopyFrom(source);   // the old ply keeps its copy
    }
}