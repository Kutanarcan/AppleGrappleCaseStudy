using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class NullScratchPainter : IScratchPainter
    {
        public static readonly NullScratchPainter Instance = new();

        private NullScratchPainter() { }

        public void Paint(Vector2 worldPosition, float brushSize) { }
        public void ClearAll() { }
    }
}
