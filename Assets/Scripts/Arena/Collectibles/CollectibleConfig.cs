using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Collectible Config")]
    public sealed class CollectibleConfig : ScriptableObject
    {
        public float SpawnInterval = 0.2f;
        public int MaxActive = 30;
        public int SwordAmount = 1;
        public int Prewarm = 30;

        public SwordCollectibleSpawnSettings Settings => new()
        {
            SpawnInterval = SpawnInterval,
            MaxActive = MaxActive,
            SwordAmount = SwordAmount
        };

        private void OnValidate()
        {
            SpawnInterval = Mathf.Max(0.02f, SpawnInterval);
            MaxActive = Mathf.Max(0, MaxActive);
            SwordAmount = Mathf.Max(1, SwordAmount);
            Prewarm = Mathf.Max(0, Prewarm);
        }
    }
}
