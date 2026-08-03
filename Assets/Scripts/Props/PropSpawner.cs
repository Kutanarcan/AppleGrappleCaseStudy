using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [Serializable]
    public struct PropSpawnSettings
    {
        [Tooltip("One is picked at random per prop.")]
        public Sprite[] Sprites;

        public int MinCount;
        public int MaxCount;
    }

    /// <summary>
    /// Scatters decoration across the arena once per round. Plain C# — the props
    /// themselves are dumb views.
    ///
    /// Pooled to MaxCount rather than Instantiated per round: the roster pattern again,
    /// a restart must not allocate. Props carry no behaviour, so the view IS the pooled
    /// item — wrapping it in an empty logic class would be ceremony.
    /// </summary>
    public sealed class PropSpawner : IDisposable
    {
        private readonly List<PropView>  _active = new(32);
        private readonly SpawnMap        _map;
        private readonly System.Random   _random;

        private readonly PropSpawnSettings _settings;
        private readonly Pool<PropView>    _pool;      // null when nothing is configured

        // ================= CREATE =================
        public PropSpawner(PropView prefab, SpawnMap map, in PropSpawnSettings settings, int seed)
        {
            _map      = map;
            _settings = settings;
            _random   = new System.Random(seed);

            if (prefab == null || settings.Sprites == null || settings.Sprites.Length == 0)
            {
                Debug.LogWarning("PropSpawner: no prefab or no sprites — props are disabled.");
                return;
            }

            _pool = new Pool<PropView>(
                create: () =>
                {
                    PropView view = UnityEngine.Object.Instantiate(prefab);
                    view.gameObject.SetActive(false);
                    return view;
                },
                destroy: view => UnityEngine.Object.Destroy(view.gameObject),
                prewarm: Mathf.Max(0, settings.MaxCount));
        }

        // ================= INITIALIZE =================
        public void Initialize()
        {
            if (_pool == null) return;

            int count = RollCount();
            for (int i = 0; i < count; i++)
                Spawn();
        }

        private void Spawn()
        {
            PropView view = _pool.Rent();
            view.Show(_map.Next(SpawnCategory.Prop), PickSprite());

            _active.Add(view);
        }

        /// <summary>MaxCount is inclusive — Random.Next's upper bound is not.</summary>
        private int RollCount()
        {
            int min = Mathf.Max(0, _settings.MinCount);
            int max = Mathf.Max(min, _settings.MaxCount);

            return _random.Next(min, max + 1);
        }

        private Sprite PickSprite()
            => _settings.Sprites[_random.Next(_settings.Sprites.Length)];

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Hide();
                _pool.Return(_active[i]);
            }

            _active.Clear();
        }

        // ================= DISPOSE =================
        public void Dispose() => _pool?.Dispose();
    }
}
