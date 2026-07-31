using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    // Dumb holder — references and serialized config only, no logic.
    // Detection is source-side (sword/collectible reports), so no trigger here;
    // the character is only resolved via attachedRigidbody.
    public sealed class CharacterView : InteractionBody
    {
        [SerializeField] private Rigidbody2D    _body;
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Animator       _animator;
        [SerializeField] private bool           _facesRightByDefault = true;

        public Rigidbody2D    Body                => _body;
        public SpriteRenderer Sprite              => _sprite;
        public Animator       Animator            => _animator;
        public bool           FacesRightByDefault => _facesRightByDefault;

        public void SetTint(Color color)
        {
            if (_sprite != null) _sprite.color = color;
        }

        private void Reset()
        {
            _body   = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
        }
    }
}
