using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordRingAbility : IAbility
    {
        private readonly List<Sword> _swords = new(8);
        private readonly Pool<Sword> _pool;

        private Character _owner;
        private float _currentAngle;

        public SwordRingAbility(Pool<Sword> pool) => _pool = pool;

        public int ActiveSwordCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _swords.Count; i++)
                    if (_swords[i].State == SwordState.Active) count++;
                return count;
            }
        }

        public void Initialize(Character owner)
        {
            _owner = owner;
            _currentAngle = 0f;
            SyncCount();
        }

        public void Update(float deltaTime) { }

        public void FixedUpdate(float deltaTime)
        {
            SyncCount();                                    // catches stat changes
            if (_swords.Count == 0) return;

            _currentAngle = Mathf.Repeat(
                _currentAngle + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

            float radius = _owner.Stats.OrbitRadius;
            float step   = 360f / _swords.Count;

            // CRITICAL: the center AFTER this physics step.
            // Using body.position leaves the ring velocity*dt behind while moving.
            Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

            for (int i = 0; i < _swords.Count; i++)
            {
                float rad = (_currentAngle + i * step) * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

                _swords[i].FixedUpdate(deltaTime);          // neutralization timer
                _swords[i].MoveTo(center + offset, rad);
            }
        }

        /// <summary>
        /// Single source of truth: accumulate the angle, compute the slot.
        ///   angle_i = _currentAngle + i * (360 / count)
        /// Add/remove a sword = only count changes, spacing redistributes on its own.
        /// </summary>
        private void SyncCount()
        {
            int desired = Mathf.Clamp(_owner.Stats.SwordCount, 0, _owner.Stats.MaxSwordCount);

            while (_swords.Count < desired)
            {
                Sword sword = _pool.Rent();               // prewarm means no Instantiate
                sword.Initialize(_owner);
                _swords.Add(sword);
            }

            while (_swords.Count > desired)
            {
                int last = _swords.Count - 1;
                ReturnSword(_swords[last]);
                _swords.RemoveAt(last);
            }
        }

        private void ReturnSword(Sword sword)
        {
            sword.Deinitialize();
            _pool.Return(sword);
        }

        public void Deinitialize()
        {
            // A sword does not return itself — the ability returns them all at once
            for (int i = 0; i < _swords.Count; i++)
                ReturnSword(_swords[i]);

            _swords.Clear();
            _owner = null;
            _currentAngle = 0f;
        }
    }
}
