using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Arena Config")]
    public sealed class ArenaConfig : ScriptableObject
    {
        public float Width  = 50f;
        public float Height = 25f;
        public float WallThickness = 0.3f;
        public float MarginOffset = 4f;

        private void OnValidate()
        {
            Width         = Mathf.Max(1f, Width);
            Height        = Mathf.Max(1f, Height);
            WallThickness = Mathf.Max(0.01f, WallThickness);
            MarginOffset  = Mathf.Max(0f, MarginOffset);
        }
    }
}
