using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    // Pure logic — decides Idle/Movement and facing, drives the Animator and
    // SpriteRenderer directly. Mirrors MovementSimulator: not a MonoBehaviour.
    public sealed class CharacterAnimator
    {
        // Below this speed (units/s, squared) the character is considered idle.
        private const float MovingThresholdSqr = 0.01f;
        private const float FacingDeadzone     = 0.01f;

        // State names must match the Animator Controller states exactly.
        private static readonly int IdleStateHash = Animator.StringToHash("Character_Idle");
        private static readonly int MoveStateHash = Animator.StringToHash("Character_Movement");

        private readonly Animator       _animator;
        private readonly SpriteRenderer _sprite;
        private readonly bool           _facesRightByDefault;

        private int _currentStateHash;

        public CharacterAnimator(Animator animator, SpriteRenderer sprite, bool facesRightByDefault)
        {
            _animator            = animator;
            _sprite              = sprite;
            _facesRightByDefault = facesRightByDefault;
        }

        /// <summary>INITIALIZE — force Idle and default facing on (re)spawn.</summary>
        public void Initialize()
        {
            _currentStateHash = 0;
            Play(IdleStateHash);
            _sprite.flipX = !_facesRightByDefault;
        }

        /// <summary>Driven every frame from the visible velocity.</summary>
        public void Update(Vector2 velocity)
        {
            Play(velocity.sqrMagnitude > MovingThresholdSqr ? MoveStateHash : IdleStateHash);
            Face(velocity.x);
        }

        // Only switches when the state actually changes, so the clip is not restarted.
        private void Play(int stateHash)
        {
            if (stateHash == _currentStateHash) return;

            _currentStateHash = stateHash;
            _animator.Play(stateHash);
        }

        // Faces left/right by X only. Near-zero X keeps the last facing.
        private void Face(float directionX)
        {
            if (Mathf.Abs(directionX) < FacingDeadzone) return;

            bool faceRight = directionX > 0f;
            _sprite.flipX = faceRight ? !_facesRightByDefault : _facesRightByDefault;
        }
    }
}
