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
        public int   SwordCount         = 3;
        public int   MaxSwordCount      = 12;
        public float OrbitRadius        = 1.5f;
        public float OrbitAngularSpeed  = 120f;
        public float SwordDamage        = 10f;
        public float NeutralizeDuration = 1.5f;

        private void OnValidate()
        {
            MaxSwordCount = Mathf.Max(MaxSwordCount, SwordCount);
        }
    }
}
