using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Carves the ground where things move. Callers know nothing about the scratch card —
    /// only "paint this point this thick", so the technique can be swapped for decals or
    /// a trail renderer without touching a single call site.
    ///
    /// The implementation lives OUTSIDE this assembly: the ScratchCard plugin compiles
    /// into Assembly-CSharp-firstpass, and an asmdef cannot reference a predefined
    /// assembly. See Assets/Scratch/ScratchPainterBehaviour.cs.
    /// </summary>
    public interface IScratchPainter
    {
        void Paint(Vector2 worldPosition, float brushSize);

        /// <summary>Round reset — wipes every mark. Not named Reset: that is a Unity message.</summary>
        void ClearAll();
    }
}
