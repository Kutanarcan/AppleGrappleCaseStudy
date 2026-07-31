using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// An entity that takes part in combat. EMPTY TODAY — a marker.
    ///
    /// Being empty is not a shortcoming, it is the job: things like a collectible,
    /// a door or a checkpoint do not implement this, so they cannot enter combat
    /// rules AS A TYPE.
    ///
    /// When teams come back (co-op, a non-hostile enemy swarm), an `int TeamId`
    /// is added here and nothing else changes.
    /// </summary>
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
