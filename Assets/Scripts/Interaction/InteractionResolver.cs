using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class InteractionResolver
    {
        private readonly List<IInteractionRule> _rules = new(4);

        public void AddRule(IInteractionRule rule) => _rules.Add(rule);

        public void Resolve(IInteractionEntity source, IInteractionEntity target, Vector2 point)
        {
            if (source == null || target == null)
                return;

            if (source.Root == null || target.Root == null)
                return;

            if (ReferenceEquals(source.Root, target.Root)) return;

            var report = new InteractionReport(source, target, point);

            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].TryApply(report))
                    return;
            }
        }

        public void Dispose() => _rules.Clear();
    }
}
