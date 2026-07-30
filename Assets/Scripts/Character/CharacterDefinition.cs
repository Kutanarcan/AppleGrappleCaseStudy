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

        [Header("Movement")]
        public float MoveSpeed    = 5f;
        public float Acceleration = 40f;
        public float Deceleration = 60f;
        public float MaxHealth    = 100f;
    }
}
