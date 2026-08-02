using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// The single door combat rules open to presentation.
    /// Rules do not know about audio/particles/flash individually.
    /// </summary>
    public interface IGameFeedback
    {
        /// <summary>Both swords are passed so presentation can weigh the event —
        /// a clash the player is part of is louder than one across the arena.</summary>
        void SwordClash(Sword a, Sword b, Vector2 point);
        void CharacterHit(Character target, Vector2 point);
        void Collected(Vector2 point);
    }
}
