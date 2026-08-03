using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class AudioManager
    {
        private readonly AudioSource _source;

        public AudioManager(AudioSource source) => _source = source;

        public void Initialize() => Stop();

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _source == null)
                return;

            _source.PlayOneShot(clip, volume);
        }

        public void Deinitialize() => Stop();

        private void Stop()
        {
            if (_source != null)
                _source.Stop();
        }
    }
}
