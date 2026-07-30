namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterStats
    {
        public float MoveSpeed;
        public float Acceleration;
        public float Deceleration;
        public float MaxHealth;

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
        }
    }
}
