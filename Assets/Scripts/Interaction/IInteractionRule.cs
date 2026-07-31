namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IInteractionRule
    {
        /// <summary>
        /// True if the rule applied. If the types do not match or the state is not
        /// eligible, returns false — the resolver moves on to the next rule.
        /// </summary>
        bool TryApply(in InteractionReport report);
    }
}
