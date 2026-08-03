using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
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

            if (_sprite != null)
                _baseColor = _sprite.color;
        }

        private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

        public void SetPickupEnabled(bool value)
        {
            if (_pickupCollider != null)
                _pickupCollider.enabled = value;
        }

        public void SetVisible(bool value)
        {
            if (_sprite != null)
                _sprite.enabled = value;
        }

        
        public void PlayConsume(Transform target, float duration, float endScale)
        {
            KillConsume();

            Vector3 start = transform.position;

            _consumeSequence = DOTween.Sequence()
                .Append(
                DOTween.To(() => 0f, t =>
                {
                    Vector3 to = target != null ? target.position : transform.position;
                    transform.position = Vector3.Lerp(start, to, t);
                },
                1f, duration).SetEase(Ease.InQuad))
                .Join(transform.DOScale(_baseScale * endScale, duration).SetEase(Ease.InQuad))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (_sprite != null)
            {
                _consumeSequence.Join(DOTween
                    .ToAlpha(() => _sprite.color, c => _sprite.color = c, 0f, duration * 0.5f)
                    .SetDelay(duration * 0.5f));
            }
        }

        public void KillConsume()
        {
            if (_consumeSequence == null)
                return;

            Sequence s = _consumeSequence;
            _consumeSequence = null;
            s.Kill(false);               
        }

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
