namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public enum SpawnCategory
    {
        Player,
        Enemy,
        Collectible,
        Prop        // unused today — reserves the spot in the map
    }

    public enum SpawnPick
    {
        /// <summary>Walk in order. Cursor resets on retry, so the layout stays the same.</summary>
        Sequential,

        /// <summary>Shuffle bag: every point is dealt once per pass, then reshuffled.</summary>
        Random
    }
}
