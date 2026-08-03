using TMPro;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterTagView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _flag;
        [SerializeField] private TMP_Text _label;

        public void Apply(string displayName, Sprite flag)
        {
            if (_label != null) _label.text = displayName;
            if (_flag != null) _flag.sprite = flag;
        }

        public void Clear() => Apply(string.Empty, null);
    }
}
