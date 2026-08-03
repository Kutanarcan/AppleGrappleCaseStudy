namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Who a character presents as for one round: a display name and a flag slot.
    ///
    /// The flag is an INDEX, not a Sprite — logic stays free of Unity assets and the
    /// same index can later drive a team color, a minimap pin or a scoreboard row.
    /// </summary>
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
