using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class Character : ICombatant, IDisposable
    {
        private readonly CharacterView       _view;
        private readonly CharacterDefinition _definition;
        private readonly List<IAbility>      _abilities = new(2);
        private readonly CharacterAnimator   _animation;
        private readonly InteractionResolver _resolver;
        private readonly IGameFeedback       _feedback;
        private readonly FlashEffect         _flash;
        private readonly HealthBarEffect     _healthBar;

        private IDirectionProvider _directionProvider;
        private float _health;
        private float _stunTimer;
        private Sprite _flagSprite;

        /// <summary>Is it on the field? False for those waiting in the roster.</summary>
        public bool IsSpawned { get; private set; }

        public IInteractionEntity Root => this;
        public bool IsAlive => IsSpawned && _health > 0f;

        /// <summary>Single place that answers "is this the one the human drives?".</summary>
        public bool IsPlayer => _definition.BrainType == CharacterBrainType.Player;
        public event Action<Character> Died;

        /// <summary>Name + flag slot for this round. Set before Initialize, see SetIdentity.</summary>
        public CharacterIdentity Identity { get; private set; }

        public float HealthNormalized => Stats.MaxHealth > 0f ? _health / Stats.MaxHealth : 0f;

        public CharacterView       View       => _view;
        public CharacterDefinition Definition => _definition;
        public CharacterStats      Stats      { get; }
        public MovementSimulator   Movement   { get; }

        public Vector2 Position => Movement.Position;

        // ================= CREATE =================
        public Character(CharacterView view, CharacterDefinition definition,
                         InteractionResolver resolver, IGameFeedback feedback)
        {
            _view       = view;
            _definition = definition;
            _resolver   = resolver;
            _feedback   = feedback;
            _flash      = new FlashEffect(view.Sprite);
            _healthBar  = new HealthBarEffect(view.HealthBar);

            Stats     = new CharacterStats();
            Movement  = new MovementSimulator(view.Body, Stats);
            _animation = new CharacterAnimator(view.Animator, view.Sprite, view.FacesRightByDefault);

            _directionProvider = NullDirectionProvider.Instance;
            _view.gameObject.SetActive(false);
        }

        public void SetDirectionProvider(IDirectionProvider provider)
            => _directionProvider = provider ?? NullDirectionProvider.Instance;

        /// <summary>
        /// Called by the factory right before Initialize, every round — identity is dealt
        /// per round, not per character, so a restart reshuffles names and flags.
        /// The sprite is resolved by the caller: the index lives in logic, the asset does not.
        /// </summary>
        public void SetIdentity(in CharacterIdentity identity, Sprite flag)
        {
            Identity    = identity;
            _flagSprite = flag;
        }

        /// <summary>CREATE phase — an ability is attached once.</summary>
        public void AddAbility(IAbility ability) => _abilities.Add(ability);

        // ================= INITIALIZE =================
        public void Initialize(Vector2 position)
        {
            if (IsSpawned) return;

            Stats.ResetFrom(_definition);
            _health    = Stats.MaxHealth;
            _stunTimer = 0f;                     // roster reuse — never respawn stunned

            // transform first, then activate, then body — avoids an interpolation smear
            _view.transform.position = position;
            _view.gameObject.SetActive(true);
            _view.Body.position = position;
            _view.Body.rotation = 0f;
            _view.Bind(this, _resolver);
            _flash.Reset();
            _healthBar.Reset();

            if (_view.Tag != null) _view.Tag.Apply(Identity.Name, _flagSprite);

            Movement.Initialize();
            _directionProvider.Initialize();
            _animation.Initialize();

            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].Initialize(this);

            IsSpawned = true;
        }

        // ================= TICK =================
        public void Update(float deltaTime)
        {
            if (!IsAlive) return;

            _directionProvider.Update(deltaTime);          // direction is COMPUTED here

            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].Update(deltaTime);

            _animation.Update(Movement.Velocity);
        }

        public void FixedUpdate(float deltaTime)
        {
            if (!IsAlive) return;

            // Stunned: own input is muted so the knockback impulse reads as a clean push-back.
            Movement.SetDirection(_stunTimer > 0f ? Vector2.zero : _directionProvider.Direction);
            Movement.FixedUpdate(deltaTime);

            if (_stunTimer > 0f) _stunTimer -= deltaTime;

            // Ring moves AFTER the body so it centers on the predicted position
            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].FixedUpdate(deltaTime);
        }

        public void ReceiveDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            _health = Mathf.Max(0f, _health - info.Amount);

            _flash.Play(_definition.FlashColor, _definition.FlashDuration);
            _healthBar.Set(HealthNormalized, _definition.HealthBarDrainTime);
            _feedback.CharacterHit(this, info.Point);
            ApplyKnockback(info);

            if (_health > 0f) return;

            Movement.SetDirection(Vector2.zero);
            Died?.Invoke(this);        // registry queues it, GameManager sweeps
        }

        /// <summary>
        /// Lives here, not in the combat rule: reacting to damage is the character's own
        /// business, so every future damage source gets knockback for free.
        ///
        /// Direction comes from the contact point — pushing away from where the blade
        /// landed reads better than pushing away from the attacker's center.
        /// </summary>
        private void ApplyKnockback(in DamageInfo info)
        {
            if (Stats.KnockbackForce <= 0f) return;

            Vector2 away = Position - info.Point;
            if (away.sqrMagnitude < 0.0001f) return;   // dead-center hit — no usable direction

            Movement.AddImpulse(away.normalized * Stats.KnockbackForce);
            _stunTimer = Stats.KnockbackStunDuration;
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            if (!IsSpawned) return;
            IsSpawned = false;

            for (int i = _abilities.Count - 1; i >= 0; i--)
                _abilities[i].Deinitialize();

            _flash.Reset();                                // do not stay tinted red
            _healthBar.Reset();                            // ...and not respawn half-drained
            _stunTimer = 0f;

            if (_view.Tag != null) _view.Tag.Clear();
            _directionProvider.Deinitialize();
            Movement.Deinitialize();

            _view.Unbind();
            _view.gameObject.SetActive(false);
        }

        // ================= DISPOSE =================
        public void Dispose()
        {
            Deinitialize();

            _abilities.Clear();
            _directionProvider = null;
            Died = null;

            if (_view != null) UnityEngine.Object.Destroy(_view.gameObject);
        }
    }
}
