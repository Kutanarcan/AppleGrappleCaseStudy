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
            if (report.Source is not SwordCollectible collectible)
                return false;

            if (report.Target is not Character character)       
                return false;

            if (collectible.IsConsumed) 
                return false;

            if (!character.IsAlive)   
                return false;

            int before = character.Stats.SwordCount;
            int after  = Mathf.Min(before + collectible.SwordAmount, character.Stats.MaxSwordCount);
            
            if (after == before)
                return false;

            character.Stats.SwordCount = after;

            collectible.Consume(character.View.transform,
                                _config.CollectFlyDuration,
                                _config.CollectEndScale);

            _feedback.Collected(report.Point);

            return true;
        }
    }
}
