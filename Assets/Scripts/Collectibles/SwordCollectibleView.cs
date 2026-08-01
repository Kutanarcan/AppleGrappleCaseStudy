using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Presentation for a sword pickup. Reports contact itself — the character view
    /// stays out of it. The consume flight is pure visual; logic lives in SwordCollectible.
    /// </summary>
    public sealed class SwordCollectibleView : InteractionBody
    {
        [SerializeField] private Rigidbody2D    _body;
        [SerializeField] private Collider2D     _pickupCollider;
        [SerializeField] private SpriteRenderer _sprite;

        private Sequence _consumeSequence;
        private Color    _baseColor;
        private Vector3  _baseScale;

        public Rigidbody2D Body => _body;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (_sprite != null) _baseColor = _sprite.color;
        }

        // The collectible reports contact; we never touch CharacterView.
        private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

        public void SetPickupEnabled(bool value)
        {
            if (_pickupCollider != null) _pickupCollider.enabled = value;
        }

        public void SetVisible(bool value)
        {
            if (_sprite != null) _sprite.enabled = value;
        }

        /// <summary>
        /// Shrinks toward the character. Visual only. The target is a live Transform —
        /// the character keeps moving during the flight, so a fixed Vector2 would land
        /// in empty space. Homing: Lerp(start, target.position, t) reads the target every
        /// frame and always converges onto it at t = 1.
        /// </summary>
        public void PlayConsume(Transform target, float duration, float endScale)
        {
            KillConsume();

            Vector3 start = transform.position;

            _consumeSequence = DOTween.Sequence()
                .Append(DOTween.To(() => 0f, t =>
                {
                    Vector3 to = target != null ? target.position : transform.position;
                    transform.position = Vector3.Lerp(start, to, t);
                }, 1f, duration).SetEase(Ease.InQuad))
                .Join(transform.DOScale(_baseScale * endScale, duration).SetEase(Ease.InQuad))
                // Pooled object: KillOnDestroy never fires, KillOnDisable is required.
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (_sprite != null)
            {
                // DOTween.ToAlpha (core) instead of the SpriteRenderer DOFade shortcut,
                // which lives in a DOTween module not present in the DLL-only install.
                _consumeSequence.Join(DOTween
                    .ToAlpha(() => _sprite.color, c => _sprite.color = c, 0f, duration * 0.5f)
                    .SetDelay(duration * 0.5f));
            }
        }

        public void KillConsume()
        {
            if (_consumeSequence == null) return;

            Sequence s = _consumeSequence;
            _consumeSequence = null;
            s.Kill(false);                 // complete: false -> OnComplete does not fire
        }

        /// <summary>Fully reset the visual state when returning to the pool.</summary>
        public void ResetVisual()
        {
            KillConsume();
            transform.localScale = _baseScale;
            if (_sprite != null)
            {
                _sprite.color   = _baseColor;
                _sprite.enabled = true;
            }
        }
    }
}
