using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class InteractionResolver
    {
        private readonly List<IInteractionRule> _rules = new(4);

        /// <summary>Filled in the CREATE phase — rules have no game state.</summary>
        public void AddRule(IInteractionRule rule) => _rules.Add(rule);

        /// <summary>
        /// Called directly from a trigger callback — resolved immediately.
        /// No queue: inside the callback we only close a collider, change a count
        /// and add to a list. Nothing is destroyed.
        /// </summary>
        public void Resolve(IInteractionEntity source, IInteractionEntity target, Vector2 point)
        {
            if (source == null || target == null) return;
            if (source.Root == null || target.Root == null) return;

            // UNIVERSAL filter: nothing interacts with its own parts.
            // My own sword does not touch me or my other swords.
            // The game is free-for-all so there is no other elimination; if teams
            // are added, that check goes INTO the combat rules, NOT here.
            if (ReferenceEquals(source.Root, target.Root)) return;

            var report = new InteractionReport(source, target, point);

            for (int i = 0; i < _rules.Count; i++)
                if (_rules[i].TryApply(report)) return;
        }

        public void Dispose() => _rules.Clear();
    }
}
