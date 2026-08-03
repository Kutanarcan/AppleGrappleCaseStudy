namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public readonly struct CharacterIdentity
    {
        public readonly string Name;
        public readonly int    FlagIndex;

        public CharacterIdentity(string name, int flagIndex)
        {
            Name      = name;
            FlagIndex = flagIndex;
        }
    }
}
