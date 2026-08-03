using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class Character : ICombatant, IDisposable
    {
        private readonly CharacterView _view;
        private readonly CharacterDefinition _definition;

        private readonly List<IAbility> _abilities = new(2);

        private readonly CharacterAnimator _animation;

        private readonly InteractionResolver _resolver;

        private readonly IGameFeedback _feedback;

        private readonly FlashEffect _flash;

        private readonly HealthBarEffect _healthBar;

        private readonly IScratchPainter _scratch;

        private IDirectionProvider _directionProvider;
        private float _health;
        private float _stunTimer;
        private Sprite _flagSprite;

        public bool IsSpawned { get; private set; }

        public IInteractionEntity Root => this;
        public bool IsAlive => IsSpawned && _health > 0f;

        public bool IsPlayer => _definition.BrainType == CharacterBrainType.Player;
        public event Action<Character> Died;

        public CharacterIdentity Identity { get; private set; }

        public float HealthNormalized => Stats.MaxHealth > 0f ? _health / Stats.MaxHealth : 0f;

        public CharacterView View => _view;
        public CharacterDefinition Definition => _definition;
        public CharacterStats Stats { get; }
        public MovementSimulator Movement { get; }

        public Vector2 Position => Movement.Position;

        public Character(CharacterView view, CharacterDefinition definition,
                         InteractionResolver resolver, IGameFeedback feedback,
                         IScratchPainter scratch)
        {
            _view = view;
            _definition = definition;
            _resolver = resolver;
            _feedback = feedback;
            _scratch = scratch ?? NullScratchPainter.Instance;
            _flash = new FlashEffect(view.Sprite);
            _healthBar = new HealthBarEffect(view.HealthBar);

            Stats = new CharacterStats();
            Movement = new MovementSimulator(view.Body, Stats);
            _animation = new CharacterAnimator(view.Animator, view.Sprite, view.FacesRightByDefault);

            _directionProvider = NullDirectionProvider.Instance;
            _view.gameObject.SetActive(false);
        }

        public void SetDirectionProvider(IDirectionProvider provider)
            => _directionProvider = provider ?? NullDirectionProvider.Instance;

        public void SetIdentity(in CharacterIdentity identity, Sprite flag)
        {
            Identity = identity;
            _flagSprite = flag;
        }

        public void AddAbility(IAbility ability) => _abilities.Add(ability);

        public void Initialize(Vector2 position)
        {
            if (IsSpawned) return;

            Stats.ResetFrom(_definition);
            _health = Stats.MaxHealth;
            _stunTimer = 0f;

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

        public void Update(float deltaTime)
        {
            if (!IsAlive)
                return;

            _directionProvider.Update(deltaTime);

            for (int i = 0; i < _abilities.Count; i++)
            {
                _abilities[i].Update(deltaTime);
            }

            _animation.Update(Movement.Velocity);

            if (_definition.ScratchBrushSize > 0f)
            {
                _scratch.Paint(_view.transform.position, _definition.ScratchBrushSize);
            }
        }

        public void FixedUpdate(float deltaTime)
        {
            if (!IsAlive)
                return;

            Movement.SetDirection(_stunTimer > 0f ? Vector2.zero : _directionProvider.Direction);
            Movement.FixedUpdate(deltaTime);

            if (_stunTimer > 0f)
                _stunTimer -= deltaTime;

            for (int i = 0; i < _abilities.Count; i++)
            {
                _abilities[i].FixedUpdate(deltaTime);
            }
        }

        public void ReceiveDamage(in DamageInfo info)
        {
            if (!IsAlive)
                return;

            _health = Mathf.Max(0f, _health - info.Amount);

            _flash.Play(_definition.FlashColor, _definition.FlashDuration);
            _healthBar.Set(HealthNormalized, _definition.HealthBarDrainTime);

            _feedback.CharacterHit(this, info.Point);

            ApplyKnockback(info);

            if (_health > 0f)
                return;

            Movement.SetDirection(Vector2.zero);
            Died?.Invoke(this);
        }

        private void ApplyKnockback(in DamageInfo info)
        {
            if (Stats.KnockbackForce <= 0f)
                return;

            Vector2 away = Position - info.Point;

            if (away.sqrMagnitude < 0.0001f)
                return;

            Movement.AddImpulse(away.normalized * Stats.KnockbackForce);

            _stunTimer = Stats.KnockbackStunDuration;
        }

        public void Deinitialize()
        {
            if (!IsSpawned)
                return;

            IsSpawned = false;

            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                _abilities[i].Deinitialize();
            }

            _flash.Reset();
            _healthBar.Reset();
            _stunTimer = 0f;

            if (_view.Tag != null)
                _view.Tag.Clear();

            _directionProvider.Deinitialize();
            Movement.Deinitialize();

            _view.Unbind();
            _view.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            Deinitialize();

            _abilities.Clear();
            _directionProvider = null;
            Died = null;

            if (_view != null)
                UnityEngine.Object.Destroy(_view.gameObject);
        }
    }
}
