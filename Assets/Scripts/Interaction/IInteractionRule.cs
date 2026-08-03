namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IInteractionRule
    {
        bool TryApply(in InteractionReport report);
    }
}
