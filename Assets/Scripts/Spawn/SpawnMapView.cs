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
            SpawnCategory.Player => _playerPoints,
            SpawnCategory.Enemy => _enemyPoints,
            SpawnCategory.Collectible => _collectiblePoints,
            SpawnCategory.Prop => _propPoints,
            _ => System.Array.Empty<Transform>()
        };
    }
}
