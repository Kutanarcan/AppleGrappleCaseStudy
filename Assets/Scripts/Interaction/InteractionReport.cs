using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public readonly struct InteractionReport
    {
        public readonly IInteractionEntity Source;
        public readonly IInteractionEntity Target;
        public readonly Vector2 Point;

        public InteractionReport(IInteractionEntity source, IInteractionEntity target, Vector2 point)
        {
            Source = source;
            Target = target;
            Point  = point;
        }
    }
}
