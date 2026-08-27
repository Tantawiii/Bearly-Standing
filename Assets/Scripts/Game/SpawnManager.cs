using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Holds spawn point references for players and pillows and hands them out at match start.</summary>
    public class SpawnManager : MonoBehaviour
    {
        public Transform[] playerSpawnPoints;
        public Transform[] pillowSpawnPoints;

        private int nextPlayerSpawn;

        public Transform GetNextPlayerSpawn()
        {
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0) return transform;
            var t = playerSpawnPoints[nextPlayerSpawn % playerSpawnPoints.Length];
            nextPlayerSpawn++;
            return t;
        }
    }
}
