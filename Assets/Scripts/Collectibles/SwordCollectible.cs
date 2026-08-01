using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Not a combatant — just an interaction entity. Any character passing over it
    /// collects it. Same lifecycle pattern as Sword: a consume timer decides when
    /// the flight is finished and the spawner may return it to the pool.
    /// </summary>
    public sealed class SwordCollectible : IInteractionEntity
    {
        private readonly SwordCollectibleView _view;
        private readonly InteractionResolver  _resolver;

        private float _consumeTimer;

        /// <summary>CREATE phase — the pool's create function calls this.</summary>
        public SwordCollectible(SwordCollectibleView view, InteractionResolver resolver)
        {
            _view     = view;
            _resolver = resolver;
        }

        public IInteractionEntity Root => this;      // its own root, belongs to no one

        public int  SwordAmount { get; private set; }
        public bool IsConsumed  { get; private set; }

        /// <summary>Flight finished? The spawner checks this before recycling.</summary>
        public bool IsConsumeFinished => IsConsumed && _consumeTimer <= 0f;

        public SwordCollectibleView View => _view;
        public Vector2 Position => _view.Body.position;

        public void Initialize(Vector2 position, int swordAmount)
        {
            SwordAmount   = swordAmount;
            IsConsumed    = false;
            _consumeTimer = 0f;

            _view.transform.position = position;
            _view.gameObject.SetActive(true);
            _view.Body.position = position;

            _view.ResetVisual();
            _view.SetPickupEnabled(true);
            _view.Bind(this, _resolver);
        }

        /// <summary>
        /// Collected. The collider closes IMMEDIATELY (no double pickup) while the
        /// visual keeps flying toward the character.
        /// </summary>
        public void Consume(Transform flyTarget, float duration, float endScale)
        {
            if (IsConsumed) return;

            IsConsumed    = true;
            _consumeTimer = duration;

            _view.Unbind();                        // no longer interacts
            _view.SetPickupEnabled(false);
            _view.PlayConsume(flyTarget, duration, endScale);
        }

        public void TickConsume(float deltaTime)
        {
            if (!IsConsumed) return;
            _consumeTimer -= deltaTime;
        }

        public void Deinitialize()
        {
            _view.Unbind();
            _view.ResetVisual();                   // tween kill + scale/alpha reset
            _view.SetPickupEnabled(false);
            _view.gameObject.SetActive(false);

            IsConsumed    = false;
            SwordAmount   = 0;
            _consumeTimer = 0f;
        }
    }
}
