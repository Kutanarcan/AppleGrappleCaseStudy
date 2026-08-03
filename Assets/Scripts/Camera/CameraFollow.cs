using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CameraFollow
    {
        private readonly Transform _rig;
        private readonly float _smoothTime;

        private Transform _target;
        private Vector3 _velocity;

        public CameraFollow(Transform rig, float smoothTime)
        {
            _rig = rig;
            _smoothTime = Mathf.Max(0f, smoothTime);
        }

        public void Initialize(Transform target)
        {
            _target = target;
            _velocity = Vector3.zero;

            Snap();
        }

        public void Update(float deltaTime)
        {
            if (_rig == null || _target == null)
                return;

            Vector3 destination = Destination();

            _rig.position = _smoothTime > 0f
                ? Vector3.SmoothDamp(_rig.position, destination, ref _velocity, _smoothTime, Mathf.Infinity, deltaTime)
                : destination;
        }

        public void Deinitialize()
        {
            _target = null;
            _velocity = Vector3.zero;
        }

        private void Snap()
        {
            if (_rig == null || _target == null)
                return;

            _rig.position = Destination();
        }

        private Vector3 Destination() => new(_target.position.x, _target.position.y, _rig.position.z);
    }
}
