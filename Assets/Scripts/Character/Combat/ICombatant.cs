using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface ICombatant : IInteractionEntity
    {
    }

    public readonly struct DamageInfo
    {
        public readonly float      Amount;
        public readonly ICombatant Source;
        public readonly Vector2    Point;

        public DamageInfo(float amount, ICombatant source, Vector2 point)
        {
            Amount = amount;
            Source = source;
            Point  = point;
        }
    }
}
