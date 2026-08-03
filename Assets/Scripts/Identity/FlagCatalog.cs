using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Index → flag sprite. The only editor-side piece of identity: sprites are assets,
    /// so they cannot come from code. Names do, see <see cref="IdentityPool"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Flag Catalog")]
    public sealed class FlagCatalog : ScriptableObject
    {
        [Tooltip("Order matters — the player always takes slot 0.")]
        public List<Sprite> Flags = new();

        public int Count => Flags != null ? Flags.Count : 0;

        /// <summary>Null for an out-of-range slot; the tag then just shows no flag.</summary>
        public Sprite Get(int index)
            => index >= 0 && index < Count ? Flags[index] : null;
    }
}
