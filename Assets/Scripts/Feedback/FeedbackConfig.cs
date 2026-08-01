using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [CreateAssetMenu(menuName = "Game/Feedback Config")]
    public sealed class FeedbackConfig : ScriptableObject
    {
        [Header("Sounds")]
        public AudioClip SwordClashClip;
        public AudioClip CollectClip;

        [Range(0f, 1f)] public float SwordClashVolume = 0.8f;
        [Range(0f, 1f)] public float CollectVolume    = 0.7f;

        [Header("Particles")]
        public ParticleView BloodSplashPrefab;
        public ParticleView SwordClashPrefab;
        public int ParticlePrewarm = 8;

        [Header("Sword throw")]
        public float SwordThrowDistance     = 3.5f;
        public float SwordThrowDuration     = 1.0f;
        public float SwordThrowSpin         = 720f;
        public float SwordThrowVerticalBias = 1.2f;

        [Header("Collectible pickup")]
        [Tooltip("Time for the bubble to fly into the character. Must be shorter than RingEntryTime.")]
        public float CollectFlyDuration = 0.25f;
        public float CollectEndScale    = 0.15f;

        private void OnValidate()
        {
            SwordThrowDuration = Mathf.Max(0.05f, SwordThrowDuration);
            SwordThrowDistance = Mathf.Max(0f, SwordThrowDistance);
            ParticlePrewarm    = Mathf.Max(0, ParticlePrewarm);
            CollectFlyDuration = Mathf.Max(0.05f, CollectFlyDuration);
            CollectEndScale    = Mathf.Clamp(CollectEndScale, 0.01f, 1f);
        }
    }
}
