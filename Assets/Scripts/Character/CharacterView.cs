using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    // In Phase 3 the base class will become InteractionBody.
    // Dumb holder — references and serialized config only, no logic.
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D    _body;
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Animator       _animator;
        [SerializeField] private bool           _facesRightByDefault = true;

        public Rigidbody2D    Body                => _body;
        public SpriteRenderer Sprite              => _sprite;
        public Animator       Animator            => _animator;
        public bool           FacesRightByDefault => _facesRightByDefault;

        public void SetTint(Color color) => _sprite.color = color;
    }
}
