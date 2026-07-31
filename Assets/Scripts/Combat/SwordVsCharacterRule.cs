namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordVsCharacterRule : IInteractionRule
    {
        public bool TryApply(in InteractionReport report)
        {
            if (report.Source is not Sword sword)      return false;
            if (report.Target is not Character target) return false;

            if (sword.State != SwordState.Active) return false;   // cannot hit while neutralized
            if (!target.IsAlive)                  return false;   // filters a second hit in the same step

            target.ReceiveDamage(new DamageInfo(sword.Damage, sword, report.Point));
            return true;
        }
    }
}
