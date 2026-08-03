using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Does nothing, so no scratch card in the scene means no null checks scattered
    /// across every call site. Same trick as <see cref="NullDirectionProvider"/>.
    /// </summary>
    public sealed class NullScratchPainter : IScratchPainter
    {
        public static readonly NullScratchPainter Instance = new();

        private NullScratchPainter() { }

        public void Paint(Vector2 worldPosition, float brushSize) { }
        public void ClearAll() { }
    }
}
