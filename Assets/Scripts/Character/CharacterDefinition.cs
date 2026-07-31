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

        [Header("Sword Ring")]
        public int   SwordCount        = 3;
        public int   MaxSwordCount     = 12;
        public float OrbitRadius       = 1.5f;
        public float OrbitAngularSpeed = 120f;
        public float SwordDamage       = 10f;

        [Header("Sword Ring — Interpolation")]
        [Tooltip("How long the remaining swords take to slide into their new slots after one is lost.")]
        public float RingSettleTime = 0.18f;

        [Tooltip("How long a new sword takes to spiral inward.")]
        public float RingEntryTime = 0.35f;

        [Tooltip("A new sword starts at this multiple of OrbitRadius.")]
        public float RingEntryRadiusScale = 2.5f;

        [Header("Feedback")]
        public AudioClip HitClip;                       // PlayerHit / EnemyHit
        [Range(0f, 1f)] public float HitVolume = 0.9f;
        public Color FlashColor    = Color.red;
        public float FlashDuration = 0.14f;

        [Header("AI (used when BrainType = AI)")]
        public float AggroEnterRadius      = 6f;
        public float AggroExitRadius       = 8f;
        public float CollectibleSeekRadius = 12f;
        public int   SwordAdvantageMargin  = 1;
        public float DecisionInterval      = 0.25f;
        public float RoamRadius            = 5f;
        public float RoamRepathInterval    = 2f;

        private void OnValidate()
        {
            MaxSwordCount        = Mathf.Max(MaxSwordCount, SwordCount);
            FlashDuration        = Mathf.Max(0.02f, FlashDuration);
            RingSettleTime       = Mathf.Max(0.01f, RingSettleTime);
            RingEntryTime        = Mathf.Max(0.01f, RingEntryTime);
            RingEntryRadiusScale = Mathf.Max(1f, RingEntryRadiusScale);
            AggroExitRadius      = Mathf.Max(AggroExitRadius, AggroEnterRadius);
            SwordAdvantageMargin = Mathf.Max(1, SwordAdvantageMargin);
        }
    }
}
