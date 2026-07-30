using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    // In Phase 3 the base class will become InteractionBody.
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D    _body;
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private Animator       _animator;

        public Rigidbody2D Body     => _body;
        public Animator    Animator => _animator;

        public void SetTint(Color color) => _sprite.color = color;
    }
}
