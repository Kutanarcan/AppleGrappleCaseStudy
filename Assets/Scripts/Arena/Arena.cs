using System;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class Arena : IDisposable
    {
        private readonly ArenaBorderSpawner _border;

        public float   Width  { get; }
        public float   Height { get; }
        public Vector2 Center { get; }
        public Vector2 Min    { get; }
        public Vector2 Max    { get; }

        public Arena(ArenaView view, float width, float height, float wallThickness, float offset)
        {
            Width  = Mathf.Max(1f, width);
            Height = Mathf.Max(1f, height);
            Center = view != null ? (Vector2)view.transform.position : Vector2.zero;

            Vector2 half = new(Width * 0.5f, Height * 0.5f);
            Min = Center - half;
            Max = Center + half;

            view?.SetArenaSize(new Vector2(Width, Height), wallThickness, offset);

            _border = new ArenaBorderSpawner(view, this);
        }

        public Vector2 ClampToBounds(Vector2 point, float margin = 0f)
        {
            return new Vector2(
                Mathf.Clamp(point.x, Min.x + margin, Max.x - margin),
                Mathf.Clamp(point.y, Min.y + margin, Max.y - margin)
                );
        }

        public bool Contains(Vector2 point, float margin = 0f)
            => point.x >= Min.x + margin && point.x <= Max.x - margin
            && point.y >= Min.y + margin && point.y <= Max.y - margin;

        public void Dispose() => _border?.Dispose();
    }
}
