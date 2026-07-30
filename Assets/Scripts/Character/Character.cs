using System;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class Character : IDisposable
    {
        private readonly CharacterView       _view;
        private readonly CharacterDefinition _definition;

        private IDirectionProvider _directionProvider;

        /// <summary>Is it on the field? False for those waiting in the roster.</summary>
        public bool IsSpawned { get; private set; }

        public CharacterView     View     => _view;
        public CharacterStats    Stats    { get; }
        public MovementSimulator Movement { get; }

        public Vector2 Position => Movement.Position;

        // ================= CREATE =================
        public Character(CharacterView view, CharacterDefinition definition)
        {
            _view       = view;
            _definition = definition;

            Stats    = new CharacterStats();
            Movement = new MovementSimulator(view.Body, Stats);

            _directionProvider = NullDirectionProvider.Instance;
            _view.gameObject.SetActive(false);
        }

        public void SetDirectionProvider(IDirectionProvider provider)
            => _directionProvider = provider ?? NullDirectionProvider.Instance;

        // ================= INITIALIZE =================
        public void Initialize(Vector2 position)
        {
            if (IsSpawned) return;

            Stats.ResetFrom(_definition);

            // transform first, then activate, then body — avoids an interpolation smear
            _view.transform.position = position;
            _view.gameObject.SetActive(true);
            _view.Body.position = position;
            _view.Body.rotation = 0f;

            Movement.Initialize();
            _directionProvider.Initialize();

            IsSpawned = true;
        }

        // ================= TICK =================
        public void Update(float deltaTime)
        {
            if (!IsSpawned) return;

            _directionProvider.Update(deltaTime);          // direction is COMPUTED here
        }

        public void FixedUpdate(float deltaTime)
        {
            if (!IsSpawned) return;

            Movement.SetDirection(_directionProvider.Direction);   // pure read
            Movement.FixedUpdate(deltaTime);
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            if (!IsSpawned) return;
            IsSpawned = false;

            _directionProvider.Deinitialize();
            Movement.Deinitialize();

            _view.gameObject.SetActive(false);
        }

        // ================= DISPOSE =================
        public void Dispose()
        {
            Deinitialize();

            _directionProvider = null;

            if (_view != null) UnityEngine.Object.Destroy(_view.gameObject);
        }
    }
}
