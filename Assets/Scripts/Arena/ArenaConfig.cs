using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Arena geometry. An asset rather than scene fields so the same layout can be
    /// reused across scenes and tuned without touching the GameManager object.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Arena Config")]
    public sealed class ArenaConfig : ScriptableObject
    {
        [Tooltip("Play area in world units. Center/Min/Max derive from this and everyone reads them.")]
        public float Width  = 50f;
        public float Height = 25f;

        [Tooltip("Thickness of the fence strip laid along each edge.")]
        public float WallThickness = 0.3f;

        [Tooltip("Extra ground beyond the play area, so a camera roll never shows past the fence.")]
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
