using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// The single door combat rules open to presentation.
    /// Rules do not know about audio/particles/flash individually.
    /// </summary>
    public interface IGameFeedback
    {
        void SwordClash(Vector2 point);
        void CharacterHit(Character target, Vector2 point);
        void Collected(Vector2 point);
    }
}
