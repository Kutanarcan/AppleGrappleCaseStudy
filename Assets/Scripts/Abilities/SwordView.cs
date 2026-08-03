using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordView : InteractionBody
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private Collider2D _hitCollider;
        [SerializeField] private SpriteRenderer _sprite;

        private Sequence _throwSequence;
        private Color _baseColor;
        private Vector3 _baseSpriteScale = Vector3.one;

        public Rigidbody2D Body => _body;

        private void Awake()
        {
            if (_sprite != null)
            {
                _baseColor = _sprite.color;
                _baseSpriteScale = _sprite.transform.localScale;
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

        public void SetDetached(bool value)
        {
            if (_hitCollider != null) 
                _hitCollider.enabled = !value;

            if (_body != null) 
                _body.simulated = !value;
        }

        public void SetScale(float scale)
        {
            if (_sprite != null) _sprite.transform.localScale = _baseSpriteScale * scale;
        }

        public void PlayThrow(Vector2 direction, float distance, float duration, float spin)
        {
            KillThrow();

            Vector3 target = transform.position + (Vector3)(direction * distance);

            _throwSequence = DOTween.Sequence()
                .Append(transform
                    .DOMove(target, duration)
                    .SetEase(Ease.OutQuad))
                .Join(transform
                    .DORotate(new Vector3(0f, 0f, spin), duration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (_sprite != null)
            {
                _throwSequence.Join(DOTween
                    .ToAlpha(() => _sprite.color, c => _sprite.color = c, 0f, duration * 0.45f)
                    .SetDelay(duration * 0.55f));
            }
        }

        public void KillThrow()
        {
            if (_throwSequence == null)
                return;

            Sequence s = _throwSequence;
            _throwSequence = null;
            s.Kill(false);             
        }

        public void ResetVisual()
        {
            KillThrow();

            transform.rotation = Quaternion.identity;

            if (_sprite != null)
            {
                _sprite.color = _baseColor;
                _sprite.transform.localScale = _baseSpriteScale;
            }
        }
    }
}
