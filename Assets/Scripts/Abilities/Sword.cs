using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public enum SwordState { Active, Detached }

    public sealed class Sword : ICombatant
    {
        private readonly SwordView _view;
        private readonly InteractionResolver _resolver;

        private Character _owner;
        private float _detachTimer;

        /// <summary>CREATE phase — the pool's create function calls this.</summary>
        public Sword(SwordView view, InteractionResolver resolver)
        {
            _view = view;
            _resolver = resolver;
        }

        // Root is the owning character — the top of the chain, not the sword itself.
        public IInteractionEntity Root => _owner;

        public SwordState State { get; private set; }
        public SwordView View => _view;

        /// <summary>
        /// Which ring owns it? When detached the RING must be called, not the counter,
        /// otherwise SyncCount would return the wrong sword to the pool.
        /// </summary>
        public SwordRingAbility Ring { get; private set; }

        public bool IsActive => State == SwordState.Active;
        public bool IsDetachFinished => State == SwordState.Detached && _detachTimer <= 0f;
        public Vector2 Position => _view.Body.position;

        public float Damage => _owner != null ? _owner.Stats.SwordDamage : 0f;

        public void Initialize(Character owner, SwordRingAbility ring)
        {
            _owner = owner;
            Ring = ring;
            State = SwordState.Active;
            _detachTimer = 0f;

            _view.gameObject.SetActive(true);
            _view.ResetVisual();
            _view.SetDetached(false);
            _view.Bind(this, _resolver);
        }

        /// <summary>
        /// Permanent cancel. The sword leaves the ring, is thrown, returns to the pool
        /// when its timer elapses. The collider closes immediately → no more reports.
        /// </summary>
        public void BeginDetach(Vector2 direction, FeedbackConfig config)
        {
            if (State == SwordState.Detached) return;

            State = SwordState.Detached;
            _detachTimer = config.SwordThrowDuration;

            _view.Unbind();                 // no longer interacts
            _view.SetDetached(true);
            _view.PlayThrow(direction,
                            config.SwordThrowDistance,
                            config.SwordThrowDuration,
                            config.SwordThrowSpin);
        }

        public void TickDetach(float deltaTime)
        {
            if (State != SwordState.Detached) return;
            _detachTimer -= deltaTime;
        }

        /// <summary>
        /// Hard teleport (no sweep). Used the moment a sword is activated so it never
        /// spends a frame at the pool origin, where Continuous detection would sweep it
        /// across the map and clash it against every other freshly-spawned sword.
        /// MovePosition would sweep from the previous position; setting Body.position does not.
        /// </summary>
        public void SnapTo(Vector2 position, float angleRad)
        {
            _view.Body.position = position;
            _view.Body.rotation = angleRad * Mathf.Rad2Deg;
        }

        // The position is already the exact target for this step — smoothing lives in
        // the ring's polar offset/radius, not here. MovePosition on the interpolated
        // kinematic body handles the visual glide between physics steps.
        public void MoveTo(Vector2 position, float angleRad)
        {
            _view.Body.MovePosition(position);
            _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg);
        }

        public void Deinitialize()
        {
            _view.Unbind();
            _view.ResetVisual();            // tween kill + rotation/alpha reset
            _view.SetDetached(false);
            _view.Body.linearVelocity = Vector2.zero;
            _view.gameObject.SetActive(false);

            _owner = null;
            Ring = null;
            State = SwordState.Active;
            _detachTimer = 0f;
        }
    }
}
