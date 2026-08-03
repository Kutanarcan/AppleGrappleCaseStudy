using UnityEngine;
using UnityEngine.Serialization;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Drives _FillAmount on the fill sprite's material. Dumb holder — it renders a
    /// number someone else decided. Animation lives in <see cref="HealthBarEffect"/>.
    ///
    /// A MaterialPropertyBlock instead of a material instance: every character shares
    /// one material, so no per-instance material is allocated.
    /// </summary>
    public sealed class HealthBarView : MonoBehaviour
    {
        public enum FillDirection { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }

        [SerializeField, FormerlySerializedAs("direction")]
        private FillDirection _direction = FillDirection.LeftToRight;

        [SerializeField, Range(0f, 1f), FormerlySerializedAs("currentFill")]
        private float _currentFill = 1f;

        [SerializeField, Range(0f, 0.1f), FormerlySerializedAs("softEdge")]
        private float _softEdge = 0f;

        // Bands, not a gradient: a hard switch is a clearer read than sliding through
        // the muddy olive/orange hues that sit between these three.
        [Header("Tint bands")]
        [SerializeField] private Color _highColor = new(0.44f, 0.78f, 0.42f);   // 100-67%
        [SerializeField] private Color _midColor  = new(0.93f, 0.80f, 0.35f);   //  66-34%
        [SerializeField] private Color _lowColor  = new(0.87f, 0.40f, 0.40f);   //  33-0%

        private const float HighBandFloor = 0.67f;
        private const float MidBandFloor  = 0.34f;

        private static readonly int ColorID         = Shader.PropertyToID("_Color");
        private static readonly int FillAmountID    = Shader.PropertyToID("_FillAmount");
        private static readonly int FillDirectionID = Shader.PropertyToID("_FillDirection");
        private static readonly int SoftEdgeID      = Shader.PropertyToID("_SoftEdge");

        private SpriteRenderer        _sprite;
        private MaterialPropertyBlock _block;

        public float Fill => _currentFill;

        private void Awake() => Apply();

        public void SetFill(float normalizedValue)
        {
            _currentFill = Mathf.Clamp01(normalizedValue);
            Apply();
        }

        /// <summary>Lets the band colors be tuned live in the inspector.</summary>
        private void OnValidate() => Apply();

        private void Apply()
        {
            // OnValidate can run before Awake, so the refs are resolved here, not there.
            if (_sprite == null) _sprite = GetComponent<SpriteRenderer>();
            if (_sprite == null) return;

            _block ??= new MaterialPropertyBlock();

            _sprite.GetPropertyBlock(_block);
            _block.SetColor(ColorID,         BandColor(_currentFill));
            _block.SetFloat(FillAmountID,    _currentFill);
            _block.SetFloat(FillDirectionID, (float)_direction);
            _block.SetFloat(SoftEdgeID,      _softEdge);
            _sprite.SetPropertyBlock(_block);
        }

        private Color BandColor(float fill)
        {
            if (fill >= HighBandFloor) return _highColor;
            return fill >= MidBandFloor ? _midColor : _lowColor;
        }
    }
}
