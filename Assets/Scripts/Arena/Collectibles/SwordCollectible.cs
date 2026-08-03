using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordCollectible : IInteractionEntity
    {
        private readonly SwordCollectibleView _view;
        private readonly InteractionResolver _resolver;

        private float _consumeTimer;
        public SwordCollectible(SwordCollectibleView view, InteractionResolver resolver)
        {
            _view = view;
            _resolver = resolver;
        }

        public IInteractionEntity Root => this;

        public int SwordAmount { get; private set; }
        public bool IsConsumed { get; private set; }

        public bool IsConsumeFinished => IsConsumed && _consumeTimer <= 0f;

        public SwordCollectibleView View => _view;
        public Vector2 Position => _view.Body.position;

        public void Initialize(Vector2 position, int swordAmount)
        {
            SwordAmount = swordAmount;
            IsConsumed = false;
            _consumeTimer = 0f;

            _view.transform.position = position;
            _view.gameObject.SetActive(true);
            _view.Body.position = position;

            _view.ResetVisual();
            _view.SetPickupEnabled(true);
            _view.Bind(this, _resolver);
        }

        public void Consume(Transform flyTarget, float duration, float endScale)
        {
            if (IsConsumed)
                return;

            IsConsumed = true;
            _consumeTimer = duration;

            _view.Unbind();
            _view.SetPickupEnabled(false);
            _view.PlayConsume(flyTarget, duration, endScale);
        }

        public void TickConsume(float deltaTime)
        {
            if (!IsConsumed)
                return;

            _consumeTimer -= deltaTime;
        }

        public void Deinitialize()
        {
            _view.Unbind();
            _view.ResetVisual();
            _view.SetPickupEnabled(false);
            _view.gameObject.SetActive(false);

            IsConsumed = false;
            SwordAmount = 0;
            _consumeTimer = 0f;
        }
    }
}
