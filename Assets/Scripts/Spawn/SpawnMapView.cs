using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SpawnMapView : MonoBehaviour
    {
        [SerializeField] private Transform[] _playerPoints;
        [SerializeField] private Transform[] _enemyPoints;
        [SerializeField] private Transform[] _collectiblePoints;
        [SerializeField] private Transform[] _propPoints;

        public Transform[] GetPoints(SpawnCategory category) => category switch
        {
            SpawnCategory.Player      => _playerPoints,
            SpawnCategory.Enemy       => _enemyPoints,
            SpawnCategory.Collectible => _collectiblePoints,
            SpawnCategory.Prop        => _propPoints,
            _                         => System.Array.Empty<Transform>()
        };

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawCategory(_playerPoints,      Color.green,  0.45f);
            DrawCategory(_enemyPoints,       Color.red,    0.40f);
            DrawCategory(_collectiblePoints, Color.cyan,   0.30f);
            DrawCategory(_propPoints,        Color.yellow, 0.25f);
        }

        private static void DrawCategory(Transform[] points, Color color, float radius)
        {
            if (points == null) return;

            Gizmos.color = color;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                Gizmos.DrawWireSphere(points[i].position, radius);
            }
        }
#endif
    }
}
