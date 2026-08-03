using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// One pool per prefab. Sweeps expired particles back — same pattern the
    /// collectible spawner uses.
    ///
    /// We use a countdown instead of the OnParticleSystemStopped callback:
    /// deterministic, clean on reset, respects the tick discipline.
    /// </summary>
    public sealed class ParticleManager : IDisposable
    {
        private sealed class Entry
        {
            public ParticleView View;
            public ParticleType   Id;
            public float        Remaining;
        }

        private readonly Dictionary<ParticleType, Pool<ParticleView>> _pools = new(2);
        private readonly List<Entry> _active = new(32);
        private readonly List<Entry> _sweep  = new(8);

        // ================= CREATE =================
        public ParticleManager(FeedbackConfig config)
        {
            AddPool(ParticleType.BloodSplash, config.BloodSplashPrefab, config.ParticlePrewarm);
            AddPool(ParticleType.SwordClash,  config.SwordClashPrefab,  config.ParticlePrewarm);
        }

        private void AddPool(ParticleType id, ParticleView prefab, int prewarm)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"ParticleManager: '{id}' prefab is not assigned.");
                return;
            }

            _pools[id] = new Pool<ParticleView>(
                create: () =>
                {
                    ParticleView view = UnityEngine.Object.Instantiate(prefab);
                    view.gameObject.SetActive(false);
                    return view;
                },
                destroy: view => UnityEngine.Object.Destroy(view.gameObject),
                prewarm: prewarm);
        }

        // ================= INITIALIZE =================
        public void Initialize() => _active.Clear();

        public void Play(ParticleType id, Vector2 position, float rotationDeg = 0f)
        {
            if (!_pools.TryGetValue(id, out Pool<ParticleView> pool)) return;

            ParticleView view = pool.Rent();
            view.Play(position, rotationDeg);

            _active.Add(new Entry { View = view, Id = id, Remaining = view.Duration });
        }

        public void Update(float deltaTime)
        {
            if (_active.Count == 0) return;

            _sweep.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].Remaining -= deltaTime;
                if (_active[i].Remaining <= 0f) _sweep.Add(_active[i]);
            }

            for (int i = 0; i < _sweep.Count; i++)
                Recycle(_sweep[i]);
        }

        private void Recycle(Entry entry)
        {
            _active.Remove(entry);
            entry.View.StopAndClear();
            _pools[entry.Id].Return(entry.View);
        }

        // ================= DEINITIALIZE =================
        // Without this, old blood splashes stay on screen after a restart.
        public void Deinitialize()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);

            _active.Clear();
            _sweep.Clear();
        }

        // ================= DISPOSE =================
        public void Dispose()
        {
            foreach (Pool<ParticleView> pool in _pools.Values)
                pool.Dispose();

            _pools.Clear();
        }
    }
}
