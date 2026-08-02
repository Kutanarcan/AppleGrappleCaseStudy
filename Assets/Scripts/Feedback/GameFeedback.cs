using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class GameFeedback : IGameFeedback
    {
        private readonly AudioManager      _audio;
        private readonly ParticleManager   _particles;
        private readonly ScreenShakeEffect _screenShake;
        private readonly FeedbackConfig    _config;

        public GameFeedback(AudioManager audio, ParticleManager particles,
                            ScreenShakeEffect screenShake, FeedbackConfig config)
        {
            _audio       = audio;
            _particles   = particles;
            _screenShake = screenShake;
            _config      = config;
        }

        public void SwordClash(Sword a, Sword b, Vector2 point)
        {
            _audio.PlaySFX(_config.SwordClashClip, _config.SwordClashVolume);
            _particles.Play(ParticleId.SwordClash, point);

            // Every enemy pair clashing would turn the screen into permanent noise —
            // shake is reserved for what the player is actually part of.
            if (!BelongsToPlayer(a) && !BelongsToPlayer(b)) return;

            _screenShake.Play(_config.ClashShake);
        }

        private static bool BelongsToPlayer(Sword sword)
            => sword != null && sword.Owner != null && sword.Owner.IsPlayer;

        public void CharacterHit(Character target, Vector2 point)
        {
            if (target == null) return;

            _audio.PlaySFX(target.Definition.HitClip, target.Definition.HitVolume);

            // Blood splash should face the hit direction: no need to add a Normal to
            // InteractionReport, compute outward from the character.
            Vector2 outward = point - target.Position;
            float angle = outward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg
                : 0f;

            _particles.Play(ParticleId.BloodSplash, point, angle);
        }

        public void Collected(Vector2 point)
            => _audio.PlaySFX(_config.CollectClip, _config.CollectVolume);
    }
}
