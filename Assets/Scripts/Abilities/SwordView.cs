using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordView : InteractionBody
    {
        [SerializeField] private Rigidbody2D    _body;
        [SerializeField] private Collider2D     _hitCollider;
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Color          _neutralizedTint = new(1f, 1f, 1f, 0.35f);

        public Rigidbody2D Body => _body;

        private void Reset()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType      = RigidbodyType2D.Kinematic;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.useFullKinematicContacts = true;    // becomes MANDATORY in Phase 3, set it right now
        }

        // Enter only — no Stay. The sword rotates: enters, hits, exits, re-enters.
        private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

        public void SetNeutralized(bool value)
        {
            _hitCollider.enabled = !value;
            _sprite.color = value ? _neutralizedTint : Color.white;
        }
    }
}
