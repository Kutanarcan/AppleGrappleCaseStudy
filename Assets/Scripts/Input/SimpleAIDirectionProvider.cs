using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// One goal item, one heading, one wall term.
    ///
    /// Two independent clocks:
    ///   DecisionInterval (~0.25s) - re-pick goal and re-score the direction
    ///   WanderDuration   (~3.0s)  - how long a random heading survives
    ///
    /// The wall is NOT summed into the direction. Every candidate direction is scored
    /// on its own and the best one wins, so nothing can cancel out to a dead stop.
    /// </summary>
    public sealed class SimpleAIDirectionProvider : IDirectionProvider
    {
        private const float TurnSpread = 60f;

        private readonly Character _self;
        private readonly Arena _arena;
        private readonly SwordCollectibleSpawner _collectibles;

        private readonly float _perceptionRadius;
        private readonly float _decisionInterval;
        private readonly float _wanderDuration;
        private readonly float _wallLookAhead;
        private readonly float _bodyRadius;

        private Vector2 _direction;
        private Vector2 _wanderHeading;

        private float _decisionTimer;
        private float _wanderTimer;

        public Vector2 Direction => _direction;

        public SimpleAIDirectionProvider(Character self, Arena arena,
                                         SwordCollectibleSpawner collectibles,
                                         CharacterDefinition definition)
        {
            _self = self;
            _arena = arena;
            _collectibles = collectibles;

            _perceptionRadius = Mathf.Max(1f, definition.PerceptionRadius);
            _decisionInterval = Mathf.Max(0.05f, definition.DecisionInterval);
            _wanderDuration = Mathf.Max(0.1f, definition.WanderDuration);
            _wallLookAhead = Mathf.Max(0.1f, definition.WallLookAhead);
            _bodyRadius = Mathf.Max(0f, definition.BodyRadius);

        }

        public void Initialize()
        {
            _direction = Vector2.zero;
            _wanderHeading = Vector2.zero;

            _decisionTimer = 0f;    
            _wanderTimer = 0f;
        }

        public void Update(float deltaTime)
        {
            _decisionTimer -= deltaTime;
            _wanderTimer -= deltaTime;

            if (_decisionTimer > 0f) return;

            _decisionTimer = _decisionInterval;
            Decide();
        }

        private void Decide()
        {
            _direction = DesiredDirection();
        }

        private Vector2 DesiredDirection()
        {
            SwordCollectible goal = _collectibles.FindNearest(_self.Position, _perceptionRadius);

            if (goal != null)
            {
                Vector2 toGoal = goal.Position - _self.Position;

                if (toGoal.sqrMagnitude > 0.0001f) return toGoal.normalized;
            }

            if (_wanderTimer <= 0f || _wanderHeading == Vector2.zero)
                NewHeading();

            AvoidWall();

            return _wanderHeading;
        }

        private void NewHeading()
        {
            float radians = Random.Range(0f, Mathf.PI * 2f);
            _wanderHeading = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            _wanderTimer = _wanderDuration;
        }

        private void AvoidWall()
        {
            Vector2 position = _self.Position;
            Vector2 probe = position + _wanderHeading * _wallLookAhead;

            Vector2 min = _arena.Min + Vector2.one * _bodyRadius;
            Vector2 max = _arena.Max - Vector2.one * _bodyRadius;

            Vector2 inward = Vector2.zero;

            if (probe.x < min.x) inward.x += 1f;
            if (probe.x > max.x) inward.x -= 1f;
            if (probe.y < min.y) inward.y += 1f;
            if (probe.y > max.y) inward.y -= 1f;

            if (inward == Vector2.zero) return;

            float baseAngle = Mathf.Atan2(inward.y, inward.x);
            float angle = baseAngle + Random.Range(-TurnSpread, TurnSpread) * Mathf.Deg2Rad;

            _wanderHeading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            _wanderTimer = _wanderDuration;
        }

        public void Deinitialize()
        {
            _direction = Vector2.zero;
            _wanderHeading = Vector2.zero;
            _decisionTimer = 0f;
            _wanderTimer = 0f;
        }
    }
}