using UnityEngine;
using UnityEngine.InputSystem;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class PlayerInputDirectionProvider : IDirectionProvider
    {
        private readonly InputAction _move;

        private Vector2 _direction;

        public PlayerInputDirectionProvider(InputAction move) => _move = move;

        public Vector2 Direction => _direction;

        public void Initialize()
        {
            _direction = Vector2.zero;
            _move.Enable();
        }

        public void Deinitialize()
        {
            _direction = Vector2.zero;
            _move.Disable();
        }

        public void Update(float deltaTime)
        {
            _direction = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
        }
    }
}
