using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IScratchPainter
    {
        void Paint(Vector2 worldPosition, float brushSize);
        void ClearAll();
    }
}
