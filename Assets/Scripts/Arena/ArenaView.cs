using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class ArenaView : MonoBehaviour
    {
        [Header("Ground")]
        [SerializeField] private SpriteRenderer _ground;
        [SerializeField] private SpriteRenderer _mask;

        [Header("Wall collider")]
        [SerializeField] private BoxCollider2D _wallTop;
        [SerializeField] private BoxCollider2D _wallBottom;
        [SerializeField] private BoxCollider2D _wallLeft;
        [SerializeField] private BoxCollider2D _wallRight;

        [Header("Fence prefabs")]
        [SerializeField] private Transform _borderRoot;
        [SerializeField] private SpriteRenderer _cornerPrefab;
        [SerializeField] private SpriteRenderer _horizontalPrefab;
        [SerializeField] private SpriteRenderer _verticalPrefab;

        public Transform BorderRoot => _borderRoot != null ? _borderRoot : transform;
        public SpriteRenderer CornerPrefab => _cornerPrefab;
        public SpriteRenderer HorizontalPrefab => _horizontalPrefab;
        public SpriteRenderer VerticalPrefab => _verticalPrefab;

        public void SetArenaSize(Vector2 size, float wallThickness, float offset)
        {
            ApplyScale(_ground, size, offset);
            ApplyScale(_mask, size, offset);

            float hw = size.x * 0.5f;
            float hh = size.y * 0.5f;
            float t = Mathf.Max(0.1f, wallThickness);

            SetBox(_wallTop, new Vector2(0f, hh + t * 0.5f), new Vector2(size.x + 2f * t, t));
            SetBox(_wallBottom, new Vector2(0f, -hh - t * 0.5f), new Vector2(size.x + 2f * t, t));
            SetBox(_wallLeft, new Vector2(-hw - t * 0.5f, 0f), new Vector2(t, size.y));
            SetBox(_wallRight, new Vector2(hw + t * 0.5f, 0f), new Vector2(t, size.y));
        }

        private static void ApplyScale(SpriteRenderer renderer, Vector2 size, float offset)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            Vector2 unit = renderer.sprite.bounds.size;

            if (unit.x <= 0.0001f || unit.y <= 0.0001f)
                return;

            size += Vector2.one * offset;

            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localScale = new Vector3(size.x / unit.x, size.y / unit.y, 1f);
        }

        private static void SetBox(BoxCollider2D box, Vector2 center, Vector2 size)
        {
            if (box == null)
                return;

            box.offset = center;
            box.size = size;
        }
    }
}
