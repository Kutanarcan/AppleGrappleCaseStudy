using DG.Tweening;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// How hard to shake. A struct so call sites stay one line and callers can keep
    /// named presets instead of passing five loose numbers.
    /// </summary>
    public readonly struct ShakeSettings
    {
        public readonly float PositionStrength;   // world units
        public readonly float RotationStrength;   // degrees of Z roll
        public readonly float Duration;
        public readonly int   Vibrato;
        public readonly bool  FadeOut;

        public ShakeSettings(float positionStrength, float rotationStrength,
                             float duration, int vibrato, bool fadeOut)
        {
            PositionStrength = positionStrength;
            RotationStrength = rotationStrength;
            Duration         = duration;
            Vibrato          = vibrato;
            FadeOut          = fadeOut;
        }
    }

    /// <summary>
    /// Camera shake on impact. Plain C# — the camera stays a dumb transform.
    ///
    /// Two channels: position and Z roll. Rotation carries the punch without pushing
    /// the arena off-center, so position strength can stay low.
    ///
    /// Reset is MANDATORY: if a shake is running when the round ends, the camera
    /// would stay parked at a random offset for the whole next round.
    /// </summary>
    public sealed class ScreenShakeEffect
    {
        private const float Randomness = 90f;

        private readonly Transform  _target;
        private readonly Vector3    _basePosition;
        private readonly Quaternion _baseRotation;

        private Sequence _sequence;

        public ScreenShakeEffect(Transform target)
        {
            _target       = target;
            _basePosition = target != null ? target.localPosition : Vector3.zero;
            _baseRotation = target != null ? target.localRotation : Quaternion.identity;
        }

        public void Play(in ShakeSettings settings)
        {
            if (_target == null) return;

            bool shakesPosition = settings.PositionStrength > 0f;
            bool shakesRotation = settings.RotationStrength > 0f;
            if (!shakesPosition && !shakesRotation) return;

            // Restore before shaking again: DOTween's shakes sample the CURRENT transform
            // as their origin, so an overlapping shake would bake in the previous offset.
            Kill();
            RestoreTransform();

            // One handle for both channels — a single Kill and a single OnKill cover them.
            _sequence = DOTween.Sequence()
                .SetLink(_target.gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(RestoreTransform);

            if (shakesPosition)
            {
                // Z is left at zero: on an orthographic camera it changes nothing visible.
                _sequence.Join(_target.DOShakePosition(
                    settings.Duration,
                    new Vector3(settings.PositionStrength, settings.PositionStrength, 0f),
                    settings.Vibrato, Randomness, snapping: false, fadeOut: settings.FadeOut));
            }

            if (shakesRotation)
            {
                // Z ONLY. Shaking X/Y would skew a 2D view instead of rolling it.
                _sequence.Join(_target.DOShakeRotation(
                    settings.Duration,
                    new Vector3(0f, 0f, settings.RotationStrength),
                    settings.Vibrato, Randomness, fadeOut: settings.FadeOut));
            }
        }

        /// <summary>Called from Deinitialize — the camera must not stay offset.</summary>
        public void Reset()
        {
            Kill();
            RestoreTransform();
        }

        private void Kill()
        {
            if (_sequence == null) return;

            Sequence s = _sequence;
            _sequence = null;              // stop OnKill -> RestoreTransform from re-entering Kill
            s.Kill(false);                 // complete: false -> OnComplete does not fire
        }

        private void RestoreTransform()
        {
            if (_target == null) return;

            _target.localPosition = _basePosition;
            _target.localRotation = _baseRotation;
        }
    }
}
