using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{

    public sealed class AngleAIDirectionProvider : IDirectionProvider
    {
        private const int AngleCount = 24;

        private static readonly Vector2[] Angles = BuildAngles();

        private readonly Character _self;
        private readonly Arena _arena;
        private readonly CharacterRegistry _characters;
        private readonly SwordCollectibleSpawner _collectibles;
        private readonly AngleAISettings _settings;

        private readonly List<Attractor> _threats = new(8);
        private readonly List<Attractor> _preys = new(8);
        private readonly List<Attractor> _pickups = new(4);

        private Vector2 _direction;
        private Vector2 _heading;
        private float _spin;
        private float _timer;

        public Vector2 Direction => _direction;

        private readonly struct Attractor
        {
            public readonly Vector2 Direction;
            public readonly float Closeness;

            public Attractor(Vector2 direction, float closeness)
            {
                Direction = direction;
                Closeness = closeness;
            }
        }

        public AngleAIDirectionProvider(Character self, Arena arena,
                                        CharacterRegistry characters,
                                        SwordCollectibleSpawner collectibles)
            : this(self, arena, characters, collectibles, AngleAISettings.Default) { }

        public AngleAIDirectionProvider(Character self, Arena arena,
                                        CharacterRegistry characters,
                                        SwordCollectibleSpawner collectibles,
                                        AngleAISettings settings)
        {
            _self = self;
            _arena = arena;
            _characters = characters;
            _collectibles = collectibles;
            _settings = settings;
        }

        public void Initialize()
        {
            float radians = Random.Range(0f, Mathf.PI * 2f);

            _heading = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            _direction = Vector2.zero;
            _timer = Random.Range(0f, _settings.DecisionInterval);
            _spin = Random.value < 0.5f ? 1f : -1f;

            _threats.Clear();
            _preys.Clear();
            _pickups.Clear();
        }

        public void Update(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            _timer = _settings.DecisionInterval;

            Perceive();

            _direction = ChooseAngle();
            _heading = _direction;
        }

        private void Perceive()
        {
            _threats.Clear();
            _preys.Clear();
            _pickups.Clear();

            Vector2 position = _self.Position;
            float radius = _settings.PerceptionRadius;
            int mine = _self.Stats.SwordCount;

            IReadOnlyList<Character> active = _characters.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Character other = active[i];
                if (other == _self || !other.IsAlive) continue;

                if (!TryDescribe(position, other.Position, radius, out Attractor attractor)) continue;

                int theirs = other.Stats.SwordCount;

                if (theirs > mine)
                {
                    _threats.Add(attractor);
                }
                else if (theirs < mine)
                {
                    _preys.Add(Spiral(attractor, position, other.Position));
                }
            }

            SwordCollectible pickup = _collectibles.FindNearest(position, radius);
            if (pickup != null && TryDescribe(position, pickup.Position, radius, out Attractor p))
                _pickups.Add(p);
        }

        private static bool TryDescribe(Vector2 from, Vector2 to, float radius, out Attractor attractor)
        {
            attractor = default;

            Vector2 delta = to - from;
            float distance = delta.magnitude;

            if (distance < 0.0001f || distance > radius) return false;

            attractor = new Attractor(delta / distance, 1f - distance / radius);
            return true;
        }

        private Vector2 ChooseAngle()
        {
            Vector2 position = _self.Position;
            float coneLimit = Mathf.Cos(_settings.ThreatCone * Mathf.Deg2Rad);

            int best = -1;
            float bestScore = float.NegativeInfinity;

            int fallback = -1;
            float lowestThreat = float.MaxValue;

            for (int i = 0; i < AngleCount; i++)
            {
                Vector2 angle = Angles[i];
                Vector2 probe = position + angle * _settings.WallLookAhead;

                if (!Inside(probe)) continue;

                float threat = MaxAlignment(_threats, angle);

                float score = Score(position, angle);

                if (threat > coneLimit)
                {
                    if (threat >= lowestThreat) continue;

                    lowestThreat = threat;
                    fallback = i;
                    continue;
                }

                if (score <= bestScore) continue;

                bestScore = score;
                best = i;
            }

            if (best >= 0) return Angles[best];
            if (fallback >= 0) return Angles[fallback];

            return (_arena.Center - position).normalized;
        }

        private float Score(Vector2 position, Vector2 angle)
        {
            float score = Vector2.Dot(angle, _heading) * _settings.CommitWeight;

            score += Pull(_preys, angle) * _settings.PreyWeight;
            score += Pull(_pickups, angle) * _settings.PickupWeight;

            Vector2 farProbe = position + angle * _settings.OpennessLookAhead;

            score += Mathf.Clamp01(WallDistance(farProbe) / _settings.OpennessLookAhead)
                   * _settings.OpennessWeight;

            return score;
        }

        private static float Pull(List<Attractor> attractors, Vector2 angle)
        {
            float total = 0f;

            for (int i = 0; i < attractors.Count; i++)
            {
                float alignment = Vector2.Dot(angle, attractors[i].Direction);
                if (alignment <= 0f) continue;                 // behind us, no pull

                total += alignment * attractors[i].Closeness;
            }

            return total;
        }

        /// <summary>How directly this angle points at the worst threat. 1 = straight at it.</summary>
        private static float MaxAlignment(List<Attractor> attractors, Vector2 angle)
        {
            float worst = -1f;

            for (int i = 0; i < attractors.Count; i++)
            {
                float alignment = Vector2.Dot(angle, attractors[i].Direction);

                alignment *= attractors[i].Closeness;

                if (alignment > worst) worst = alignment;
            }

            return worst;
        }

        private bool Inside(Vector2 point)
        {
            float margin = _settings.BodyRadius;

            return point.x >= _arena.Min.x + margin && point.x <= _arena.Max.x - margin
                && point.y >= _arena.Min.y + margin && point.y <= _arena.Max.y - margin;
        }

        private float WallDistance(Vector2 point)
        {
            float margin = _settings.BodyRadius;

            float left = point.x - (_arena.Min.x + margin);
            float right = (_arena.Max.x - margin) - point.x;
            float bottom = point.y - (_arena.Min.y + margin);
            float top = (_arena.Max.y - margin) - point.y;

            return Mathf.Max(0f, Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top)));
        }


        private Attractor Spiral(Attractor raw, Vector2 from, Vector2 targetPosition)
        {
            float distance = Vector2.Distance(from, targetPosition);

            // -1 outside the ring (close in), +1 inside it (back off), 0 exactly on it.
            float radial = Mathf.Clamp((_settings.EngageRadius - distance) / _settings.EngageBand, -1f, 1f);

            // Tangent strength peaks ON the ring and fades as we approach or retreat.
            float tangential = 1f - Mathf.Abs(radial);

            Vector2 inward = raw.Direction;
            Vector2 tangent = new Vector2(-inward.y, inward.x) * _spin;

            Vector2 blended = inward * -radial + tangent * tangential;

            return new Attractor(blended.normalized, raw.Closeness);
        }

        public void Deinitialize()
        {
            _direction = Vector2.zero;
            _heading = Vector2.zero;
            _timer = 0f;

            _threats.Clear();
            _preys.Clear();
            _pickups.Clear();
        }

        private static Vector2[] BuildAngles()
        {
            var angles = new Vector2[AngleCount];

            for (int i = 0; i < AngleCount; i++)
            {
                float radians = i * (Mathf.PI * 2f / AngleCount);
                angles[i] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }

            return angles;
        }
    }
}