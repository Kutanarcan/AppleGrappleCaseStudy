namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IInteractionEntity
    {
        /// <summary>
        /// ROOT of the interaction group. On a sword it is the owning character;
        /// on a character and a collectible it is itself.
        ///
        /// CONTRACT: return the top of the chain, not the link above.
        /// The resolver does a single ReferenceEquals and does not walk the chain —
        /// this only works if everyone returns the very top. If a chain like
        /// sword → shield → character is ever built, the sword must still return the
        /// CHARACTER, not the shield.
        ///
        /// This removes the need for an int identity and an id generator;
        /// a reference comparison is enough.
        /// </summary>
        IInteractionEntity Root { get; }
    }
}
