using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SpawnMap
    {
        private sealed class Bucket
        {
            public Vector2[] Points;
            public SpawnPick Pick;
            public int[]     Bag;      // shuffled index list for Random
            public int       Cursor;
        }

        private readonly Dictionary<SpawnCategory, Bucket> _buckets = new(4);

        // Built once in CREATE. NOT recreated in Initialize:
        // characters are Sequential so their retry layout is already identical,
        // while collectibles are meant to lay out differently each retry.
        private readonly System.Random _random;

        /// <summary>CREATE phase — Transforms are copied to Vector2, no link to the view remains.</summary>
        public SpawnMap(SpawnMapView view, int seed)
        {
            _random = new System.Random(seed);

            AddBucket(view, SpawnCategory.Player,      SpawnPick.Sequential);
            AddBucket(view, SpawnCategory.Enemy,       SpawnPick.Sequential);
            AddBucket(view, SpawnCategory.Collectible, SpawnPick.Random);
            AddBucket(view, SpawnCategory.Prop,        SpawnPick.Random);
        }

        private void AddBucket(SpawnMapView view, SpawnCategory category, SpawnPick pick)
        {
            Transform[] transforms = view != null
                ? view.GetPoints(category)
                : Array.Empty<Transform>();

            int count = transforms?.Length ?? 0;
            var points = new Vector2[count];
            var bag    = new int[count];

            for (int i = 0; i < count; i++)
            {
                points[i] = transforms[i] != null ? (Vector2)transforms[i].position : Vector2.zero;
                bag[i]    = i;
            }

            _buckets[category] = new Bucket { Points = points, Pick = pick, Bag = bag, Cursor = 0 };
        }

        public int Count(SpawnCategory category) => _buckets[category].Points.Length;

        /// <summary>INITIALIZE phase — cursor resets, Random buckets are reshuffled.</summary>
        public void Initialize()
        {
            foreach (Bucket bucket in _buckets.Values)
            {
                bucket.Cursor = 0;
                if (bucket.Pick == SpawnPick.Random) Shuffle(bucket);
            }
        }

        public Vector2 Next(SpawnCategory category)
        {
            Bucket bucket = _buckets[category];

            if (bucket.Points.Length == 0)
            {
                Debug.LogWarning($"SpawnMap: no points defined for '{category}'.");
                return Vector2.zero;
            }

            if (bucket.Pick == SpawnPick.Sequential)
            {
                Vector2 point = bucket.Points[bucket.Cursor];
                bucket.Cursor = (bucket.Cursor + 1) % bucket.Points.Length;
                return point;
            }

            // Shuffle bag: no point repeats until every point has been dealt once
            if (bucket.Cursor >= bucket.Bag.Length) Shuffle(bucket);
            return bucket.Points[bucket.Bag[bucket.Cursor++]];
        }

        private void Shuffle(Bucket bucket)
        {
            int[] bag = bucket.Bag;

            for (int i = bag.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            bucket.Cursor = 0;
        }

        public void Deinitialize()
        {
            foreach (Bucket bucket in _buckets.Values)
                bucket.Cursor = 0;
        }
    }
}
