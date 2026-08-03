using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Scenery scattered across the arena each round.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Prop Config")]
    public sealed class PropConfig : ScriptableObject
    {
        [Tooltip("One is picked at random per prop.")]
        public Sprite[] Sprites;

        [Tooltip("Count is rolled between these two, inclusive, on every round.")]
        public int MinCount = 10;
        public int MaxCount = 50;

        public PropSpawnSettings Settings => new()
        {
            Sprites  = Sprites,
            MinCount = MinCount,
            MaxCount = MaxCount
        };

        private void OnValidate()
        {
            MinCount = Mathf.Max(0, MinCount);
            MaxCount = Mathf.Max(MinCount, MaxCount);
        }
    }
}
