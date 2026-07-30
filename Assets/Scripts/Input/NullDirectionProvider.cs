using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class NullDirectionProvider : IDirectionProvider
    {
        // Stateless, so it can be shared and is unaffected by resets.
        public static readonly NullDirectionProvider Instance = new();

        public Vector2 Direction => Vector2.zero;

        public void Initialize() { }
        public void Update(float deltaTime) { }
        public void Deinitialize() { }
    }
}
