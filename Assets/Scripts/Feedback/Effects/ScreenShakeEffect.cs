using DG.Tweening;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public readonly struct ShakeSettings
    {
        public readonly float PositionStrength; 
        public readonly float RotationStrength; 
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
            if (_target == null)
                return;

            bool shakesPosition = settings.PositionStrength > 0f;
            bool shakesRotation = settings.RotationStrength > 0f;

            if (!shakesPosition && !shakesRotation)
                return;

            Kill();
            RestoreTransform();

            _sequence = DOTween.Sequence()
                .SetLink(_target.gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(RestoreTransform);

            if (shakesPosition)
            {
                _sequence.Join(_target.DOShakePosition(
                    settings.Duration,
                    new Vector3(settings.PositionStrength, settings.PositionStrength, 0f),
                    settings.Vibrato, Randomness, snapping: false, fadeOut: settings.FadeOut));
            }

            if (shakesRotation)
            {
                _sequence.Join(_target.DOShakeRotation(
                    settings.Duration,
                    new Vector3(0f, 0f, settings.RotationStrength),
                    settings.Vibrato, Randomness, fadeOut: settings.FadeOut));
            }
        }

        public void Reset()
        {
            Kill();
            RestoreTransform();
        }

        private void Kill()
        {
            if (_sequence == null)
                return;

            Sequence s = _sequence;
            _sequence = null;           
            s.Kill(false);              
        }

        private void RestoreTransform()
        {
            if (_target == null)
                return;

            _target.localPosition = _basePosition;
            _target.localRotation = _baseRotation;
        }
    }
}
