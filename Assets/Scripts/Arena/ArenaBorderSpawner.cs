using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Computes how many fence pieces fit along each edge and places them.
    /// Runs once in the CREATE phase, stays put until Dispose.
    /// </summary>
    public sealed class ArenaBorderSpawner : IDisposable
    {
        private readonly List<GameObject> _pieces = new(256);

        public ArenaBorderSpawner(ArenaView view, Arena arena)
        {
            if (view == null) return;

            Transform root = view.BorderRoot;

            // --- Corner posts ---
            float inset = HalfWidth(view.CornerPrefab);

            Place(view.CornerPrefab, new Vector2(arena.Min.x, arena.Max.y), root);
            Place(view.CornerPrefab, new Vector2(arena.Max.x, arena.Max.y), root);
            Place(view.CornerPrefab, new Vector2(arena.Min.x, arena.Min.y), root);
            Place(view.CornerPrefab, new Vector2(arena.Max.x, arena.Min.y), root);

            // --- Horizontal edges (top / bottom) ---
            FillEdge(view.HorizontalPrefab, root,
                     arena.Min.x + inset, arena.Max.x - inset, arena.Max.y, horizontal: true);
            FillEdge(view.HorizontalPrefab, root,
                     arena.Min.x + inset, arena.Max.x - inset, arena.Min.y, horizontal: true);

            // --- Vertical edges (left / right) ---
            float vInset = HalfHeight(view.CornerPrefab);

            FillEdge(view.VerticalPrefab, root,
                     arena.Min.y + vInset, arena.Max.y - vInset, arena.Min.x, horizontal: false);
            FillEdge(view.VerticalPrefab, root,
                     arena.Min.y + vInset, arena.Max.y - vInset, arena.Max.x, horizontal: false);
        }

        /// <summary>
        /// Counts pieces from the piece size, then re-divides the span so it is filled
        /// EXACTLY — no gap or overflow at the edge.
        /// </summary>
        private void FillEdge(SpriteRenderer prefab, Transform root,
                              float from, float to, float fixedCoord, bool horizontal)
        {
            if (prefab == null || prefab.sprite == null) return;

            Vector2 unit = prefab.sprite.bounds.size;
            float step = horizontal ? unit.x : unit.y;
            if (step <= 0.0001f) return;

            float length = to - from;
            if (length <= 0f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / step));
            float spacing = length / count;

            for (int i = 0; i < count; i++)
            {
                float t = from + spacing * (i + 0.5f);

                Vector2 position = horizontal
                    ? new Vector2(t, fixedCoord)
                    : new Vector2(fixedCoord, t);

                Place(prefab, position, root);
            }
        }

        private void Place(SpriteRenderer prefab, Vector2 position, Transform root)
        {
            if (prefab == null) return;

            SpriteRenderer instance = UnityEngine.Object.Instantiate(prefab, root);
            instance.transform.position = position;

            _pieces.Add(instance.gameObject);
        }

        private static float HalfWidth(SpriteRenderer r)
            => r != null && r.sprite != null ? r.sprite.bounds.size.x * 0.5f : 0f;

        private static float HalfHeight(SpriteRenderer r)
            => r != null && r.sprite != null ? r.sprite.bounds.size.y * 0.5f : 0f;

        public void Dispose()
        {
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieces[i] != null) UnityEngine.Object.Destroy(_pieces[i]);

            _pieces.Clear();
        }
    }
}
