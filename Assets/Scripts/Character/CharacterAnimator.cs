using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterAnimator
    {
        private const float MovingThresholdSqr = 0.01f;
        private const float FacingDeadzone = 0.01f;

        private static readonly int IdleStateHash = Animator.StringToHash("Character_Idle");
        private static readonly int MoveStateHash = Animator.StringToHash("Character_Movement");

        private readonly Animator _animator;
        private readonly SpriteRenderer _sprite;
        private readonly bool _facesRightByDefault;

        private int _currentStateHash;

        public CharacterAnimator(Animator animator, SpriteRenderer sprite, bool facesRightByDefault)
        {
            _animator = animator;
            _sprite = sprite;
            _facesRightByDefault = facesRightByDefault;
        }

        public void Initialize()
        {
            _currentStateHash = 0;

            Play(IdleStateHash);

            _sprite.flipX = !_facesRightByDefault;
        }

        public void Update(Vector2 velocity)
        {
            Play(velocity.sqrMagnitude > MovingThresholdSqr ? MoveStateHash : IdleStateHash);

            Face(velocity.x);
        }

        private void Play(int stateHash)
        {
            if (stateHash == _currentStateHash)
                return;

            _currentStateHash = stateHash;
            _animator.Play(stateHash);
        }

        private void Face(float directionX)
        {
            if (Mathf.Abs(directionX) < FacingDeadzone)
                return;

            bool faceRight = directionX > 0f;
            _sprite.flipX = faceRight ? !_facesRightByDefault : _facesRightByDefault;
        }
    }
}
