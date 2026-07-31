namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordVsSwordRule : IInteractionRule
    {
        private readonly IGameFeedback _feedback;

        public SwordVsSwordRule(IGameFeedback feedback) => _feedback = feedback;

        public bool TryApply(in InteractionReport report)
        {
            // My own sword cannot touch my other sword — the resolver filtered by Root.
            if (report.Source is not Sword a) return false;
            if (report.Target is not Sword b) return false;

            // This guard also acts as dedup: when the B->A call arrives, both are
            // already Detached so it is a no-op.
            if (!a.IsActive || !b.IsActive) return false;
            if (a.Ring == null || b.Ring == null) return false;

            // Deterministic direction: the higher sword goes up.
            // Random or "first reported goes up" would look artificial.
            float aSign = a.Position.y >= b.Position.y ? 1f : -1f;

            a.Ring.Detach(a,  aSign);
            b.Ring.Detach(b, -aSign);

            _feedback.SwordClash((a.Position + b.Position) * 0.5f);
            return true;
        }
    }
}
