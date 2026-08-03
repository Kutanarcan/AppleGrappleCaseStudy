using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class PropView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        public void Show(Vector2 position, Sprite sprite)
        {
            transform.position = position;

            if (_sprite != null)
                _sprite.sprite = sprite;

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Reset() => _sprite = GetComponentInChildren<SpriteRenderer>();
    }
}
