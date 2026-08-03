using TMPro;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Nameplate above a character: flag sprite + world-space label.
    /// Dumb holder — it is told what to show, it never decides.
    /// </summary>
    public sealed class CharacterTagView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _flag;
        [SerializeField] private TMP_Text       _label;

        public void Apply(string displayName, Sprite flag)
        {
            if (_label != null) _label.text   = displayName;
            if (_flag  != null) _flag.sprite  = flag;
        }

        /// <summary>Roster reuse: a despawned character must not keep the last round's tag.</summary>
        public void Clear() => Apply(string.Empty, null);
    }
}
