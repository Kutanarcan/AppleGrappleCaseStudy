using UnityEngine;
using UnityEngine.Serialization;

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

        // Only clashes involving the player shake the screen.
        [Header("Screen shake")]
        [Tooltip("World units the camera slides. Keep this low and let rotation carry the punch.")]
        [FormerlySerializedAs("ClashShakeStrength")]
        public float ClashShakePositionStrength = 0.3f;

        [Tooltip("Degrees of Z roll. Reads as impact without pushing the arena off-center.")]
        public float ClashShakeRotationStrength = 1.5f;

        public float ClashShakeDuration = 0.35f;
        public int   ClashShakeVibrato  = 18;

        [Tooltip("On: amplitude ramps down across the whole duration — softer tail, but a " +
                 "short duration then reads as a single tick. Off: full strength throughout.")]
        public bool ClashShakeFadeOut = true;

        public ShakeSettings ClashShake => new(ClashShakePositionStrength,
                                               ClashShakeRotationStrength,
                                               ClashShakeDuration,
                                               ClashShakeVibrato,
                                               ClashShakeFadeOut);

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
            ClashShakePositionStrength = Mathf.Max(0f, ClashShakePositionStrength);
            ClashShakeRotationStrength = Mathf.Max(0f, ClashShakeRotationStrength);
            ClashShakeDuration         = Mathf.Max(0.02f, ClashShakeDuration);
            ClashShakeVibrato          = Mathf.Max(1, ClashShakeVibrato);
            CollectFlyDuration = Mathf.Max(0.05f, CollectFlyDuration);
            CollectEndScale    = Mathf.Clamp(CollectEndScale, 0.01f, 1f);
        }
    }
}
