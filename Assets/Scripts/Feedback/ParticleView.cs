using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Dumb MonoBehaviour. Prefab settings:
    ///   Play On Awake = false, Looping = false, Stop Action = None
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _system;

        /// <summary>Total lifetime of the effect — decides when it returns to the pool.</summary>
        public float Duration { get; private set; }

        private void Reset() => _system = GetComponent<ParticleSystem>();

        private void Awake()
        {
            if (_system == null) _system = GetComponent<ParticleSystem>();
            CacheDuration();
        }

        private void CacheDuration()
        {
            ParticleSystem.MainModule main = _system.main;
            Duration = main.duration + main.startLifetime.constantMax;
        }

        public void Play(Vector2 position, float rotationDeg)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

            gameObject.SetActive(true);
            _system.Clear(true);
            _system.Play(true);
        }

        public void StopAndClear()
        {
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _system.Clear(true);
            gameObject.SetActive(false);
        }
    }
}
