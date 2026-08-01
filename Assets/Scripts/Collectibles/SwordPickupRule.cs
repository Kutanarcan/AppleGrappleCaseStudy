using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordPickupRule : IInteractionRule
    {
        private readonly IGameFeedback  _feedback;
        private readonly FeedbackConfig _config;

        public SwordPickupRule(IGameFeedback feedback, FeedbackConfig config)
        {
            _feedback = feedback;
            _config   = config;
        }

        public bool TryApply(in InteractionReport report)
        {
            if (report.Source is not SwordCollectible collectible) return false;
            if (report.Target is not Character character)          return false;

            if (collectible.IsConsumed) return false;
            if (!character.IsAlive)     return false;

            int before = character.Stats.SwordCount;
            int after  = Mathf.Min(before + collectible.SwordAmount, character.Stats.MaxSwordCount);

            // Ring full -> collectible is NOT consumed, stays on the field, someone else may take it
            if (after == before) return false;

            // The rule STILL only bumps a count. It knows nothing of the ring, the
            // sword or the pool — the spiral entry comes free from SyncCount.
            character.Stats.SwordCount = after;

            // Live Transform, not a snapshot: the character moves during the flight,
            // so a fixed point would leave the bubble drifting into empty space.
            collectible.Consume(character.View.transform,
                                _config.CollectFlyDuration,
                                _config.CollectEndScale);

            _feedback.Collected(report.Point);
            return true;
        }
    }
}
