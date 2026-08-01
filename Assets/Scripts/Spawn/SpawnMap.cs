using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// COMPUTES spawn points — no hand-placed Transforms in the scene.
    ///
    ///   Player / Enemy   → Polygonal (regular polygon around the center)
    ///   Collectible/Prop → Random    (anywhere inside Min/Max, may overlap)
    /// </summary>
    public sealed class SpawnMap
    {
        private readonly Arena         _arena;
        private readonly System.Random _random;
        private readonly float         _minCharacterSeparation;
        private readonly float         _characterMargin;
        private readonly float         _randomMargin;

        private int _characterCount = 1;
        private int _characterCursor;

        /// <summary>CREATE phase.</summary>
        public SpawnMap(Arena arena, int seed,
                        float minCharacterSeparation, float characterMargin, float randomMargin)
        {
            _arena                  = arena;
            _random                 = new System.Random(seed);
            _minCharacterSeparation = Mathf.Max(0.1f, minCharacterSeparation);
            _characterMargin        = Mathf.Max(0f, characterMargin);
            _randomMargin           = Mathf.Max(0f, randomMargin);
        }

        /// <summary>
        /// INITIALIZE phase. The total character count decides how many corners the
        /// polygon has — Player and Enemy SHARE the same polygon.
        /// </summary>
        public void Initialize(int characterCount)
        {
            _characterCount  = Mathf.Max(1, characterCount);
            _characterCursor = 0;
        }

        public Vector2 Next(SpawnCategory category)
        {
            switch (category)
            {
                case SpawnCategory.Player:
                case SpawnCategory.Enemy:
                    return NextPolygonal();

                default:
                    return NextRandom();
            }
        }

        // ================= POLYGONAL =================
        private Vector2 NextPolygonal()
        {
            if (_characterCount <= 1) return _arena.Center;

            int index = _characterCursor % _characterCount;
            _characterCursor++;

            float radius = CharacterRadius(_characterCount);
            float angle  = index * (360f / _characterCount) * Mathf.Deg2Rad;

            return _arena.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// Neighbour distance = 2R·sin(π/N). The R that makes it MinCharacterSeparation.
        /// If it does not fit it is clamped — sword rings may overlap at the start.
        /// </summary>
        private float CharacterRadius(int total)
        {
            float needed = _minCharacterSeparation / (2f * Mathf.Sin(Mathf.PI / total));
            float max    = Mathf.Min(_arena.Width, _arena.Height) * 0.5f - _characterMargin;

            if (max <= 0f)
            {
                Debug.LogWarning("SpawnMap: CharacterMargin is larger than the arena.");
                return 0f;
            }

            if (needed > max)
            {
                Debug.LogWarning(
                    $"SpawnMap: {total} characters need radius {needed:F2}, only {max:F2} available. " +
                    $"Sword rings may overlap at the start. " +
                    $"Increase the arena size or reduce MinCharacterSeparation.");
            }

            return Mathf.Min(needed, max);
        }

        // ================= RANDOM =================
        private Vector2 NextRandom()
        {
            Vector2 min = _arena.Min + Vector2.one * _randomMargin;
            Vector2 max = _arena.Max - Vector2.one * _randomMargin;

            return new Vector2(
                Mathf.Lerp(min.x, max.x, (float)_random.NextDouble()),
                Mathf.Lerp(min.y, max.y, (float)_random.NextDouble()));
        }

        public void Deinitialize() => _characterCursor = 0;
    }
}
