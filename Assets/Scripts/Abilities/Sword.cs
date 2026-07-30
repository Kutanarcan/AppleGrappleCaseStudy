using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public enum SwordState { Active, Neutralized }

    // Neutralization logic is COMPLETE here — just not wired to combat yet.
    // In Phase 3 `: ICombatant`, `Root` and the resolver link will be added.
    public sealed class Sword
    {
        private readonly SwordView _view;

        private Character _owner;
        private float _recoveryTimer;

        /// <summary>CREATE phase — the pool's create function calls this.</summary>
        public Sword(SwordView view) => _view = view;

        public SwordState State { get; private set; }
        public SwordView  View  => _view;

        public float Damage             => _owner != null ? _owner.Stats.SwordDamage        : 0f;
        public float NeutralizeDuration => _owner != null ? _owner.Stats.NeutralizeDuration : 1f;

        public void Initialize(Character owner)
        {
            _owner = owner;
            State  = SwordState.Active;
            _recoveryTimer = 0f;

            _view.gameObject.SetActive(true);
            _view.SetNeutralized(false);
        }

        public void FixedUpdate(float deltaTime)
        {
            if (State != SwordState.Neutralized) return;

            _recoveryTimer -= deltaTime;
            if (_recoveryTimer > 0f) return;

            State = SwordState.Active;
            _view.SetNeutralized(false);          // collider re-opens
        }

        public void Neutralize(float duration)
        {
            if (State == SwordState.Neutralized)
            {
                _recoveryTimer = Mathf.Max(_recoveryTimer, duration);   // refresh, not stack
                return;
            }

            State = SwordState.Neutralized;
            _recoveryTimer = duration;
            _view.SetNeutralized(true);           // collider closes
        }

        public void MoveTo(Vector2 position, float angleRad)
        {
            _view.Body.MovePosition(position);

            // Sprite points RIGHT (+X = blade tip). The ring angle already points
            // radially outward, so aligning +X to it puts the tip outward and the
            // handle toward the center — no offset needed.
            _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg);
        }

        public void Deinitialize()
        {
            _view.Body.linearVelocity = Vector2.zero;
            _view.SetNeutralized(false);
            _view.gameObject.SetActive(false);

            _owner = null;
            State  = SwordState.Active;
            _recoveryTimer = 0f;
        }
    }
}
