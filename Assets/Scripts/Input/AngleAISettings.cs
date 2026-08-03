namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public struct AngleAISettings
    {
        public float PerceptionRadius;
        public float DecisionInterval;

        public float WallLookAhead;
        public float BodyRadius;

        public float ThreatCone;

        public float PreyWeight;
        public float PickupWeight;

        public float CommitWeight;

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