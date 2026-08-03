using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class FlashEffect
    {
        private readonly SpriteRenderer _sprite;
        private readonly Color _baseColor;

        private Tween _tween;

        public FlashEffect(SpriteRenderer sprite)
        {
            _sprite = sprite;
            _baseColor = sprite != null ? sprite.color : Color.white;
        }

        public void Play(Color flashColor, float duration)
        {
            if (_sprite == null)
                return;

            Kill();

            _tween = DOTween
                .To(() => _sprite.color, c => _sprite.color = c, flashColor, duration * 0.5f)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(_sprite.gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(RestoreColor);
        }

        public void Reset()
        {
            Kill();
            RestoreColor();
        }

        private void Kill()
        {
            if (_tween == null)
                return;

            Tween t = _tween;
            _tween = null;
            t.Kill(false);
        }

        private void RestoreColor()
        {
            if (_sprite != null) _sprite.color = _baseColor;
        }
    }
}
