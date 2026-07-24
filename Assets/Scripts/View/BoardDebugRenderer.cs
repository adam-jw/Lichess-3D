using UnityEngine;

public class BoardDebugRenderer : MonoBehaviour
{
    [SerializeField] private BoardView _boardView;
    [SerializeField] private BoardHighlighter _highlighter;

    [TextArea]
    [SerializeField] private string _fen = "8/8/8/4k3/8/4R3/8/4K3 b - - 0 1";

    [Tooltip("Re-applies check every frame so inspector tweaks to the check style show instantly.")]
    [SerializeField] private bool _liveRefresh = true;

    [ContextMenu("Render FEN")]
    public void RenderFen()
    {
        if (_boardView == null) { Debug.LogError("No BoardView assigned", this); return; }
        _boardView.Render(BoardState.FromFen(_fen));
        if (_highlighter != null) _highlighter.RefreshCheck();
    }

    private void Update()
    {
        if (_liveRefresh && Application.isPlaying && _highlighter != null)
            _highlighter.RefreshCheck();
    }
}