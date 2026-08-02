using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public enum CharacterBrainType
    {
        Player,
        AI,
        None        // training dummy, fixed target
    }

    [CreateAssetMenu(menuName = "Game/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Prefab")]
        public CharacterView ViewPrefab;

        [Header("Brain")]
        public CharacterBrainType BrainType = CharacterBrainType.Player;
        public bool HasSwordRing = true;

        [Header("Movement")]
        public float MoveSpeed    = 5f;
        public float Acceleration = 40f;
        public float Deceleration = 60f;
        public float MaxHealth    = 100f;

        [Header("Knockback")]
        [Tooltip("Applied to THIS character when it takes a hit. ExternalDecay is 8/s, " +
                 "so a force F slides roughly F*F/16 units over F/8 seconds.")]
        public float KnockbackForce = 4f;

        [Tooltip("How long own input stays muted after a hit. Keep it below the slide duration.")]
        public float KnockbackStunDuration = 0.15f;

        [Header("Sword Ring")]
        public int   SwordCount        = 3;
        public int   MaxSwordCount     = 12;
        public float OrbitRadius       = 1.5f;
        public float OrbitAngularSpeed = 120f;
        public float SwordDamage       = 10f;

        [Header("Sword Ring — Interpolation")]
        [Tooltip("How long the remaining swords take to slide into their new slots after one is lost.")]
        public float RingSettleTime = 0.18f;

        [Tooltip("How long a new sword takes to grow from zero to full size.")]
        public float RingEntryTime = 0.35f;

        [Header("Feedback")]
        public AudioClip HitClip;                       // PlayerHit / EnemyHit
        [Range(0f, 1f)] public float HitVolume = 0.9f;
        public Color FlashColor    = Color.red;
        public float FlashDuration = 0.14f;

        [Header("AI")]
        public float PerceptionRadius = 10f;
        public float DecisionInterval = 0.25f;
        public float WanderDuration = 3f;
        public float WallLookAhead = 4f;
        public float BodyRadius = 0.5f;

        [Header("AI Weights")]
        public float GoalWeight = 1.0f;
        public float CommitWeight = 0.35f;
        public float WallWeight = 0.8f;

        private void OnValidate()
        {
            MaxSwordCount        = Mathf.Max(MaxSwordCount, SwordCount);
            FlashDuration        = Mathf.Max(0.02f, FlashDuration);
            KnockbackForce        = Mathf.Max(0f, KnockbackForce);
            KnockbackStunDuration = Mathf.Max(0f, KnockbackStunDuration);
            RingSettleTime       = Mathf.Max(0.01f, RingSettleTime);
            RingEntryTime        = Mathf.Max(0.01f, RingEntryTime);
        }
    }
}
