using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Animates the health bar's fill. Same shape as <see cref="FlashEffect"/>: plain C#,
    /// holds one view reference, drives DOTween, cleans up through Reset().
    ///
    /// Tweening instead of snapping is not just polish — a bar that slides shows HOW MUCH
    /// was lost, which a single-frame jump cannot.
    ///
    /// Reset is MANDATORY under the roster pattern: a bar left mid-drain would make the
    /// character respawn looking wounded.
    /// </summary>
    public sealed class HealthBarEffect
    {
        private readonly HealthBarView _view;

        private Tween _tween;
        private float _fill = 1f;

        public HealthBarEffect(HealthBarView view) => _view = view;

        /// <summary>Slides to the new value. Duration &lt;= 0 snaps.</summary>
        public void Set(float normalized, float duration)
        {
            if (_view == null) return;

            normalized = Mathf.Clamp01(normalized);

            // From the CURRENT fill, not from the last target: overlapping hits chain
            // instead of jumping back to where the previous tween started.
            Kill();

            if (duration <= 0f)
            {
                Apply(normalized);
                return;
            }

            _tween = DOTween.To(() => _fill, Apply, normalized, duration)
                            .SetEase(Ease.OutQuad)
                            .SetLink(_view.gameObject, LinkBehaviour.KillOnDisable);
        }

        /// <summary>Called from Initialize and Deinitialize — back to a full bar, no tween.</summary>
        public void Reset()
        {
            Kill();
            Apply(1f);
        }

        private void Kill()
        {
            if (_tween == null) return;

            Tween t = _tween;
            _tween = null;                 // guard against OnKill re-entering Kill
            t.Kill(false);                 // complete: false -> do not snap to the target
        }

        private void Apply(float value)
        {
            _fill = value;
            if (_view != null) _view.SetFill(value);
        }
    }
}
