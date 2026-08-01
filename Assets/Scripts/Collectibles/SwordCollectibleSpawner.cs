using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [Serializable]
    public struct SwordCollectibleSpawnSettings
    {
        public float SpawnInterval;      // 3f
        public int   MaxActive;          // 6
        public int   SwordAmount;        // 1
    }

    public sealed class SwordCollectibleSpawner : IDisposable
    {
        private readonly List<SwordCollectible> _active = new(16);
        private readonly List<SwordCollectible> _sweep  = new(8);

        private readonly Pool<SwordCollectible>        _pool;
        private readonly SpawnMap                      _map;
        private readonly SwordCollectibleSpawnSettings _settings;

        private float _timer;

        // ================= CREATE =================
        public SwordCollectibleSpawner(SwordCollectibleView prefab, InteractionResolver resolver,
                                       SpawnMap map, SwordCollectibleSpawnSettings settings,
                                       int prewarm)
        {
            _map      = map;
            _settings = settings;

            _pool = new Pool<SwordCollectible>(
                create: () =>
                {
                    SwordCollectibleView view = UnityEngine.Object.Instantiate(prefab);
                    view.gameObject.SetActive(false);
                    return new SwordCollectible(view, resolver);
                },
                destroy: collectible => UnityEngine.Object.Destroy(collectible.View.gameObject),
                prewarm: prewarm);
        }

        // ================= INITIALIZE =================
        public void Initialize()
        {
            _timer = 0f;
            _active.Clear();
        }

        /// <summary>Not physics — driven from Update.</summary>
        public void Update(float deltaTime)
        {
            SweepConsumed(deltaTime);

            _timer -= deltaTime;
            if (_timer > 0f) return;

            _timer = _settings.SpawnInterval;
            if (CountAvailable() >= _settings.MaxActive) return;

            Spawn();
        }

        public SwordCollectible Spawn()
        {
            SwordCollectible collectible = _pool.Rent();
            collectible.Initialize(_map.Next(SpawnCategory.Collectible), _settings.SwordAmount);

            _active.Add(collectible);
            return collectible;
        }

        /// <summary>Flying ones are not "on the field" — the limit does not count them.</summary>
        private int CountAvailable()
        {
            int n = 0;
            for (int i = 0; i < _active.Count; i++)
                if (!_active[i].IsConsumed) n++;

            return n;
        }

        private void SweepConsumed(float deltaTime)
        {
            _sweep.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].TickConsume(deltaTime);
                if (_active[i].IsConsumeFinished) _sweep.Add(_active[i]);
            }

            for (int i = 0; i < _sweep.Count; i++)
                Recycle(_sweep[i]);
        }

        private void Recycle(SwordCollectible collectible)
        {
            _active.Remove(collectible);
            collectible.Deinitialize();
            _pool.Return(collectible);
        }

        /// <summary>Null if nothing in range. Flying ones are not targets.</summary>
        public SwordCollectible FindNearest(Vector2 from, float maxRadius)
        {
            SwordCollectible best = null;
            float bestSqr = maxRadius * maxRadius;

            for (int i = 0; i < _active.Count; i++)
            {
                SwordCollectible candidate = _active[i];
                if (candidate.IsConsumed) continue;

                float sqr = (candidate.Position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best    = candidate;
            }

            return best;
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Deinitialize();
                _pool.Return(_active[i]);
            }

            _active.Clear();
            _sweep.Clear();
            _timer = 0f;
        }

        // ================= DISPOSE =================
        public void Dispose() => _pool.Dispose();
    }
}
