namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Tuning values live here, not on CharacterDefinition, so this provider can be
    /// tested in isolation before anything is exposed to the inspector.
    /// </summary>
    public struct AngleAISettings
    {
        public float PerceptionRadius;
        public float DecisionInterval;

        /// <summary>How far ahead the wall probe looks. Keep small (1-3).</summary>
        public float WallLookAhead;
        public float BodyRadius;

        /// <summary>Half-angle, in degrees, banned around every stronger enemy.</summary>
        public float ThreatCone;

        public float PreyWeight;
        public float PickupWeight;

        /// <summary>Keeps the current heading. This is what wandering IS - no separate mode.</summary>
        public float CommitWeight;

        /// <summary>Prefers angles that end up far from a wall. Zero effect in open space.</summary>
        public float OpennessWeight;

        public float OpennessLookAhead;

        public float EngageRadius;
        public float EngageBand;

        public static AngleAISettings Default => new()
        {
            PerceptionRadius = 30f,
            DecisionInterval = 0.25f,

            WallLookAhead = 5f,
            BodyRadius = 1f,

            ThreatCone = 60f,

            PreyWeight = 1.0f,
            PickupWeight = 0.8f,
            CommitWeight = 0.3f,
            OpennessWeight = 0.6f,
            OpennessLookAhead = 6f,
            EngageRadius = 2.2f,
            EngageBand = 1.5f,
        };
    }
}