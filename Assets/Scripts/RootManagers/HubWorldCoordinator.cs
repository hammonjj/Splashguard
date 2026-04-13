using BitBox.Library;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Bitbox
{
    public class HubWorldCoordinator : MonoBehaviourBase
    {
        [SerializeField, Required] private Transform[] SpawnPoints;

        public Transform ResolveSpawnPoint(int playerIndex)
        {
            Assert.IsNotNull(SpawnPoints, $"{nameof(HubWorldCoordinator)} requires an authored spawn-point array.");
            Assert.IsTrue(playerIndex >= 0, $"Player index must be non-negative. Received {playerIndex}.");
            Assert.IsTrue(
                playerIndex < SpawnPoints.Length,
                $"{nameof(HubWorldCoordinator)} requires a spawn point for player index {playerIndex}. Authored count: {SpawnPoints.Length}.");

            Transform spawnPoint = SpawnPoints[playerIndex];
            Assert.IsNotNull(spawnPoint, $"{nameof(HubWorldCoordinator)} has a null spawn point at index {playerIndex}.");
            return spawnPoint;
        }
    }
}
