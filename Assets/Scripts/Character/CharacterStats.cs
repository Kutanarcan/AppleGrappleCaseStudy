namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterStats
    {
        public float MoveSpeed;
        public float Acceleration;
        public float Deceleration;
        public float MaxHealth;

        public int   SwordCount;
        public int   MaxSwordCount;
        public float OrbitRadius;
        public float OrbitAngularSpeed;      // degrees / second
        public float SwordDamage;
        public float NeutralizeDuration;

        /// <summary>
        /// INITIALIZE phase. Does NOT allocate a new object — overwrites the fields.
        /// Required so retry produces no allocation.
        /// </summary>
        public void ResetFrom(CharacterDefinition def)
        {
            MoveSpeed    = def.MoveSpeed;
            Acceleration = def.Acceleration;
            Deceleration = def.Deceleration;
            MaxHealth    = def.MaxHealth;

            SwordCount         = def.SwordCount;
            MaxSwordCount      = def.MaxSwordCount;
            OrbitRadius        = def.OrbitRadius;
            OrbitAngularSpeed  = def.OrbitAngularSpeed;
            SwordDamage        = def.SwordDamage;
            NeutralizeDuration = def.NeutralizeDuration;
        }
    }
}
