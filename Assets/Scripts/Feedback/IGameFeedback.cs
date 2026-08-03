using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IGameFeedback
    {
        void SwordClash(Sword a, Sword b, Vector2 point);
        void CharacterHit(Character target, Vector2 point);
        void Collected(Vector2 point);
    }
}
