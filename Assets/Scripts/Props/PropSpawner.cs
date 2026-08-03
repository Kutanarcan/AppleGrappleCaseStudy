using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [Serializable]
    public struct PropSpawnSettings
    {
        public Sprite[] Sprites;

        public int MinCount;
        public int MaxCount;
    }

    public sealed class PropSpawner : IDisposable
    {
        private readonly List<PropView> _active = new(32);
        private readonly SpawnMap _map;
        private readonly System.Random _random;

        private readonly PropSpawnSettings _settings;
        private readonly Pool<PropView> _pool;

        public PropSpawner(PropView prefab, SpawnMap map, in PropSpawnSettings settings, int seed)
        {
            _map = map;
            _settings = settings;
            _random = new System.Random(seed);

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

        public void Initialize()
        {
            if (_pool == null)
                return;

            int count = RollCount();

            for (int i = 0; i < count; i++)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            PropView view = _pool.Rent();
            view.Show(_map.Next(SpawnCategoryType.Prop), PickSprite());

            _active.Add(view);
        }

        private int RollCount()
        {
            int min = Mathf.Max(0, _settings.MinCount);
            int max = Mathf.Max(min, _settings.MaxCount);

            return _random.Next(min, max + 1);
        }

        private Sprite PickSprite() => _settings.Sprites[_random.Next(_settings.Sprites.Length)];

        public void Deinitialize()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Hide();
                _pool.Return(_active[i]);
            }

            _active.Clear();
        }

        public void Dispose() => _pool?.Dispose();
    }
}
