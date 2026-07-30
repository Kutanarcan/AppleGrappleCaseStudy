using UnityEngine;
using UnityEngine.InputSystem;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    // Reads the generated Input System "Move" action (keyboard, gamepad, joystick).
    // Deadzone and normalization are handled by the action bindings.
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
            // Clamp keeps diagonal keyboard input within unit magnitude;
            // a half-pushed stick still yields a half-magnitude vector → half speed.
            _direction = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
        }
    }
}
