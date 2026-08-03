using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Sword collectible drip feed. <see cref="Settings"/> hands the spawner a plain
    /// struct, so the logic side never holds on to the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Collectible Config")]
    public sealed class CollectibleConfig : ScriptableObject
    {
        [Tooltip("Seconds between spawn attempts. An attempt is skipped while MaxActive is met.")]
        public float SpawnInterval = 0.2f;

        [Tooltip("Cap on collectibles waiting on the field. Ones flying to a character do not count.")]
        public int MaxActive = 30;

        [Tooltip("Swords granted per pickup.")]
        public int SwordAmount = 1;

        [Tooltip("Instantiated up front so a spawn never allocates. Match it to MaxActive.")]
        public int Prewarm = 30;

        public SwordCollectibleSpawnSettings Settings => new()
        {
            SpawnInterval = SpawnInterval,
            MaxActive     = MaxActive,
            SwordAmount   = SwordAmount
        };

        private void OnValidate()
        {
            SpawnInterval = Mathf.Max(0.02f, SpawnInterval);
            MaxActive     = Mathf.Max(0, MaxActive);
            SwordAmount   = Mathf.Max(1, SwordAmount);
            Prewarm       = Mathf.Max(0, Prewarm);
        }
    }
}
