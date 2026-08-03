using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class HealthBarEffect
    {
        private readonly HealthBarView _view;

        private Tween _tween;
        private float _fill = 1f;

        public HealthBarEffect(HealthBarView view) => _view = view;

        public void Set(float normalized, float duration)
        {
            if (_view == null) 
                return;

            normalized = Mathf.Clamp01(normalized);

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

        public void Reset()
        {
            Kill();
            Apply(1f);
        }

        private void Kill()
        {
            if (_tween == null)
                return;

            Tween t = _tween;
            _tween = null;               
            t.Kill(false);               
        }

        private void Apply(float value)
        {
            _fill = value;

            if (_view != null)
                _view.SetFill(value);
        }
    }
}
