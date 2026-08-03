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

        public Sword(SwordView view, InteractionResolver resolver)
        {
            _view = view;
            _resolver = resolver;
        }

        public IInteractionEntity Root => _owner;
        public Character Owner => _owner;

        public SwordState State { get; private set; }
        public SwordView View => _view;
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

        public void BeginDetach(Vector2 direction, FeedbackConfig config)
        {
            if (State == SwordState.Detached)
                return;

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

        public void SnapTo(Vector2 position, float angleRad)
        {
            _view.Body.position = position;
            _view.Body.rotation = angleRad * Mathf.Rad2Deg;
        }

        public void MoveTo(Vector2 position, float angleRad)
        {
            _view.Body.MovePosition(position);
            _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg);
        }

        public void SetScale(float scale) => _view.SetScale(scale);

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
