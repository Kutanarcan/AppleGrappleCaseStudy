namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordVsSwordRule : IInteractionRule
    {
        private readonly IGameFeedback _feedback;

        public SwordVsSwordRule(IGameFeedback feedback) => _feedback = feedback;

        public bool TryApply(in InteractionReport report)
        {
            if (report.Source is not Sword a)
                return false;

            if (report.Target is not Sword b)
                return false;

            if (!a.IsActive || !b.IsActive)
                return false;

            if (a.Ring == null || b.Ring == null) 
                return false;

            float aSign = a.Position.y >= b.Position.y ? 1f : -1f;

            a.Ring.Detach(a,  aSign);
            b.Ring.Detach(b, -aSign);

            _feedback.SwordClash(a, b, (a.Position + b.Position) * 0.5f);

            return true;
        }
    }
}
