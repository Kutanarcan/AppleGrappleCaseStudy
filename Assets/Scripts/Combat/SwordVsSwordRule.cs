namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordVsSwordRule : IInteractionRule
    {
        public bool TryApply(in InteractionReport report)
        {
            // My own sword cannot touch my other sword — the resolver filtered by Root.
            // Every pair reaching here already belongs to different characters.
            if (report.Source is not Sword a) return false;
            if (report.Target is not Sword b) return false;

            // This guard also acts as dedup: when the B→A call arrives, both are
            // already Neutralized so it is a no-op.
            if (a.State != SwordState.Active) return false;
            if (b.State != SwordState.Active) return false;

            a.Neutralize(a.NeutralizeDuration);
            b.Neutralize(b.NeutralizeDuration);
            // VFX spark + SFX clang — NO damage

            return true;
        }
    }
}
