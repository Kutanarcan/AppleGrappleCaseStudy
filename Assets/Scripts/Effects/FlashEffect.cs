using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Color flash on damage. Plain C# — the view stays dumb.
    ///
    /// Because we use a roster, reset is MANDATORY: if a character dies mid-flash
    /// and the tint is not restored, it respawns red on retry.
    /// </summary>
    public sealed class FlashEffect
    {
        private readonly SpriteRenderer _sprite;
        private readonly Color _baseColor;

        private Tween _tween;

        public FlashEffect(SpriteRenderer sprite)
        {
            _sprite    = sprite;
            _baseColor = sprite != null ? sprite.color : Color.white;
        }

        public void Play(Color flashColor, float duration)
        {
            if (_sprite == null) return;

            Kill();

            // DOTween.To (core) instead of the SpriteRenderer DOColor shortcut,
            // which lives in a DOTween module not present in the DLL-only install.
            _tween = DOTween
                .To(() => _sprite.color, c => _sprite.color = c, flashColor, duration * 0.5f)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(_sprite.gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(RestoreColor);
        }

        /// <summary>Called from Initialize and Deinitialize.</summary>
        public void Reset()
        {
            Kill();
            RestoreColor();
        }

        private void Kill()
        {
            if (_tween == null) return;

            Tween t = _tween;
            _tween = null;                 // stop OnKill -> RestoreColor from re-entering Kill
            t.Kill(false);                 // complete: false -> OnComplete does not fire
        }

        private void RestoreColor()
        {
            if (_sprite != null) _sprite.color = _baseColor;
        }
    }
}
