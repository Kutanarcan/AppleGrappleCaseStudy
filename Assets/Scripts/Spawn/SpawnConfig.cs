using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Who spawns, how many, and how far apart. Read once in the CREATE phase.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Spawn Config")]
    public sealed class SpawnConfig : ScriptableObject
    {
        [Header("Roster")]
        [Tooltip("Characters are never destroyed — this is the size of the pool, decided once.")]
        public int EnemyCount = 7;

        [Header("Character ring")]
        [Tooltip("Neighbour distance on the spawn polygon. Clamped if the arena cannot fit it.")]
        public float MinCharacterSeparation = 12f;

        [Tooltip("Keeps the spawn polygon this far inside the arena edge.")]
        public float CharacterMargin = 2f;

        [Header("Random placement")]
        [Tooltip("Inset for collectibles and props, so nothing lands on top of the fence.")]
        public float RandomMargin = 3f;

        [Tooltip("Fixed seed: the same run is reproducible. It is NOT reset on restart, " +
                 "so each round still looks different.")]
        public int Seed = 12345;

        private void OnValidate()
        {
            EnemyCount             = Mathf.Max(0, EnemyCount);
            MinCharacterSeparation = Mathf.Max(0.1f, MinCharacterSeparation);
            CharacterMargin        = Mathf.Max(0f, CharacterMargin);
            RandomMargin           = Mathf.Max(0f, RandomMargin);
        }
    }
}
