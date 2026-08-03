using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IDirectionProvider
    {
        Vector2 Direction { get; }
        void Initialize();

        void Update(float deltaTime);
        void Deinitialize();
    }
}
