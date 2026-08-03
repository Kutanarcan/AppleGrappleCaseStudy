using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Prop Config")]
    public sealed class PropConfig : ScriptableObject
    {
        public Sprite[] Sprites;
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
