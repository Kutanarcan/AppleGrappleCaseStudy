using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Flag Catalog")]
    public sealed class FlagCatalog : ScriptableObject
    {
        public List<Sprite> Flags = new();

        public int Count => Flags != null ? Flags.Count : 0;

        public Sprite Get(int index) => index >= 0 && index < Count ? Flags[index] : null;
    }
}
