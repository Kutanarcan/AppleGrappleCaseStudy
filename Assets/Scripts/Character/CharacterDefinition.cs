using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public enum CharacterBrainType
    {
        Player,
        AI,
        None
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
        public float MoveSpeed = 5f;
        public float Acceleration = 40f;
        public float Deceleration = 60f;
        public float MaxHealth = 100f;

        [Header("Knockback")]
        public float KnockbackForce = 4f;
        public float KnockbackStunDuration = 0.15f;

        [Header("Sword Ring")]
        public int SwordCount = 3;
        public int MaxSwordCount = 12;
        public float OrbitRadius = 1.5f;
        public float OrbitAngularSpeed = 120f;
        public float SwordDamage = 10f;

        [Header("Sword Ring — Interpolation")]
        public float RingSettleTime = 0.18f;
        public float RingEntryTime = 0.35f;

        [Header("Feedback")]
        public AudioClip HitClip;
        [Range(0f, 1f)] public float HitVolume = 0.9f;
        public Color FlashColor = Color.red;
        public float FlashDuration = 0.14f;
        public float HealthBarDrainTime = 0.25f;

        [Header("Scratch")]
        public float ScratchBrushSize = 1.5f;
        public float SwordScratchBrushSize = 0.6f;

        private void OnValidate()
        {
            MaxSwordCount = Mathf.Max(MaxSwordCount, SwordCount);
            FlashDuration = Mathf.Max(0.02f, FlashDuration);
            HealthBarDrainTime = Mathf.Max(0f, HealthBarDrainTime);
            ScratchBrushSize = Mathf.Max(0f, ScratchBrushSize);
            SwordScratchBrushSize = Mathf.Max(0f, SwordScratchBrushSize);
            KnockbackForce = Mathf.Max(0f, KnockbackForce);
            KnockbackStunDuration = Mathf.Max(0f, KnockbackStunDuration);
            RingSettleTime = Mathf.Max(0.01f, RingSettleTime);
            RingEntryTime = Mathf.Max(0.01f, RingEntryTime);
        }
    }
}
