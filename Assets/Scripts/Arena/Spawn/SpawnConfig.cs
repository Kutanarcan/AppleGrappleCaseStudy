using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Spawn Config")]
    public sealed class SpawnConfig : ScriptableObject
    {
        [Header("Roster")]
        public int EnemyCount = 7;

        [Header("Character ring")]
        public float MinCharacterSeparation = 12f;
        public float CharacterMargin = 2f;

        [Header("Random placement")]
        public float RandomMargin = 3f;
        public int Seed = 12345;

        private void OnValidate()
        {
            EnemyCount = Mathf.Max(0, EnemyCount);
            MinCharacterSeparation = Mathf.Max(0.1f, MinCharacterSeparation);
            CharacterMargin = Mathf.Max(0f, CharacterMargin);
            RandomMargin = Mathf.Max(0f, RandomMargin);
        }
    }
}
