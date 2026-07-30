using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IDirectionProvider
    {
        /// <summary>
        /// PURE READ. No computation, no side effects, free to call.
        /// Reading twice in the same step returns the same value.
        /// The value is only updated inside Update().
        /// Magnitude 0..1 — NOT normalized, ClampMagnitude.
        /// </summary>
        Vector2 Direction { get; }

        /// <summary>Round start — accumulated state is reset (timer, target, direction).</summary>
        void Initialize();

        void Update(float deltaTime);
        void Deinitialize();
    }
}
