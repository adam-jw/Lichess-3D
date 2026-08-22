using System.Collections.Generic;
using UnityEngine;

// Turns ply applied to the board into sound
//
// BoardView owns the facts (what happened) and knows nothing about audio; 
// this script owns the policy (e.g. which clip, when to stay silent)
[RequireComponent(typeof(AudioSource))]
public class BoardAudio : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BoardView _boardView;
    [SerializeField] private LichessGameSession _session;

    // One clip plays per move; Empty slot falls back to _move
    [Header("Move clips (empty falls back to Move)")]
    [SerializeField] private AudioClip _move;
    [SerializeField] private AudioClip _capture;
    [SerializeField] private AudioClip _castle;
    [SerializeField] private AudioClip _promotion;
    [SerializeField] private AudioClip _check;

    [Header("Game clips")]
    [SerializeField] private AudioClip _gameStart;
    [SerializeField] private AudioClip _gameEnd;

    [Header("Mix")]
    [Range(0f, 1f)][SerializeField] private float _moveVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float _gameVolume = 1f;

    [Header("Policy")]
    [Tooltip("Sound single history steps. Rapid (held-key) scrubbing is always silent.")]
    [SerializeField] private bool _soundOnHistoryStep = true;

    [Tooltip("Log every ply's classification. Turn off once trusted.")]
    [SerializeField] private bool _logClassification;

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;   // 2D; see Play() for the positional-audio seam
    }

    private void OnEnable()
    {
        if (_boardView != null)
            _boardView.OnPlyApplied += HandlePlyApplied;

        if (_session != null)
        {
            _session.OnGameStarted += HandleGameStarted;
            _session.OnGameEnded += HandleGameEnded;
        }
    }

    private void OnDisable()
    {
        if (_boardView != null)
            _boardView.OnPlyApplied -= HandlePlyApplied;

        if (_session != null)
        {
            _session.OnGameStarted -= HandleGameStarted;
            _session.OnGameEnded -= HandleGameEnded;
        }
    }

    // ---------- Move sounds ----------

    private void HandlePlyApplied(BoardView.PlyApplied ply)
    {
        if (!ply.Animated) return;

        if (ply.Source != BoardView.PlySource.Stream && !_soundOnHistoryStep) return;

        bool isCastle = CountMoveEdits(ply.Edits) == 2;
        bool isCapture = HasCapturedPiece(ply.Edits, ply.Move);
        bool isPromotion = ply.Move.Promotion != PieceType.None;

        bool isCheck = ply.Position != null && ply.Position.IsInCheck(ply.Position.ActiveColor);

        // Sound effect precedence: check > capture > castle > promotion > move

        AudioClip clip;
        string label;

        if (isCheck) { clip = Fallback(_check); label = "check"; }
        else if (isCapture) { clip = Fallback(_capture); label = "capture"; }
        else if (isCastle) { clip = Fallback(_castle); label = "castle"; }
        else if (isPromotion) { clip = Fallback(_promotion); label = "promotion"; }
        else { clip = _move; label = "move"; }

        if (_logClassification)
            Debug.Log($"[BoardAudio] {ply.Move.ToUci()} src={ply.Source} " +
                      $"castle={isCastle} capture={isCapture} promo={isPromotion} check={isCheck} " +
                      $"-> {label} ({(clip != null ? clip.name : "NO CLIP")})", this);

        Play(clip, _moveVolume, ply.Move.To);
    }

    // A Remove at the mover's origin is the promoting pawn being replaced by a new mesh;
    // Every other Remove is a captured piece
    private static bool HasCapturedPiece(IReadOnlyList<PieceEdit> edits, Move move)
    {
        if (edits == null) return false;

        foreach (PieceEdit e in edits)
            if (e.Kind == PieceEditKind.Remove && e.From != move.From)
                return true;

        return false;
    }

    // Castling is the only move that relocates two pieces
    private static int CountMoveEdits(IReadOnlyList<PieceEdit> edits)
    {
        if (edits == null) return 0;

        int n = 0;
        foreach (PieceEdit e in edits)
            if (e.Kind == PieceEditKind.Move) n++;

        return n;
    }

    // ---------- Game sounds ----------

    private void HandleGameStarted(GameEventInfo game) => Play(_gameStart, _gameVolume);

    // 'status' = Lichess's own verdict (mate / resign / outoftime / draw)
    private void HandleGameEnded(GameEndReason reason, string status) => Play(_gameEnd, _gameVolume);

    // ---------- Playback ----------

    private AudioClip Fallback(AudioClip clip) => clip != null ? clip : _move;

    // 'at' is unused while the source is 2D, but it is the seam positional audio needs
    // Threading it now means that change is confined to this method
    private void Play(AudioClip clip, float volume, Square at = default)
    {
        if (clip == null || _source == null) return;
        _source.PlayOneShot(clip, volume);
    }
}