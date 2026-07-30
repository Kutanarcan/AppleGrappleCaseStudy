using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class MovementSimulator
    {
        private const float ExternalDecay = 8f;

        private readonly Rigidbody2D    _body;
        private readonly CharacterStats _stats;

        private Vector2 _direction;
        private Vector2 _velocity;
        private Vector2 _externalVelocity;   // knockback / dash channel

        public Vector2 Position => _body.position;
        public Vector2 Velocity => _velocity;

        /// <summary>Position AFTER this physics step. The ring centers on this.</summary>
        public Vector2 PredictedPosition(float deltaTime)
            => _body.position + _body.linearVelocity * deltaTime;

        /// <summary>CREATE phase — Rigidbody settings are configured once.</summary>
        public MovementSimulator(Rigidbody2D body, CharacterStats stats)
        {
            _body  = body;
            _stats = stats;

            _body.gravityScale   = 0f;
            _body.freezeRotation = true;
            _body.interpolation  = RigidbodyInterpolation2D.Interpolate;
        }

        public void Initialize()   => ResetMotion();
        public void Deinitialize() => ResetMotion();

        private void ResetMotion()
        {
            _direction        = Vector2.zero;
            _velocity         = Vector2.zero;
            _externalVelocity = Vector2.zero;

            if (_body != null) _body.linearVelocity = Vector2.zero;
        }

        public void SetDirection(Vector2 direction)
            => _direction = Vector2.ClampMagnitude(direction, 1f);

        public void AddImpulse(Vector2 impulse) => _externalVelocity += impulse;

        public void FixedUpdate(float deltaTime)
        {
            Vector2 target = _direction * _stats.MoveSpeed;

            float rate = _direction.sqrMagnitude > 0.0001f
                ? _stats.Acceleration
                : _stats.Deceleration;

            _velocity = Vector2.MoveTowards(_velocity, target, rate * deltaTime);

            // Single computation point — this widens if modifiers arrive
            _body.linearVelocity = _velocity + _externalVelocity;

            _externalVelocity = Vector2.MoveTowards(
                _externalVelocity, Vector2.zero, ExternalDecay * deltaTime);
        }
    }
}
