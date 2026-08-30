using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Drips power-up orbs into the arena. Runs offline (local Instantiate) or on the server
    /// (NetworkServer.Spawn); on a pure client it does nothing. Wired by Day1SceneBuilder.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        public GameObject pickupPrefab;
        public Transform[] points;

        [SerializeField] private float firstDelay = 6f;
        [SerializeField] private float interval = 13f;
        [SerializeField] private int maxActive = 3;

        private void Start()
        {
            if (points == null || points.Length == 0) points = AutoPoints();
            if (ShouldRun()) InvokeRepeating(nameof(TrySpawn), firstDelay, interval);
        }

        private bool ShouldRun() => NetworkServer.active || (!NetworkClient.active && !NetworkServer.active);

        private void TrySpawn()
        {
            if (pickupPrefab == null || points.Length == 0) return;
            if (Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Length >= maxActive) return;

            Transform p = points[Random.Range(0, points.Length)];
            var effect = (PickupEffectType)Random.Range(0, System.Enum.GetValues(typeof(PickupEffectType)).Length);

            var go = Instantiate(pickupPrefab, p.position, Quaternion.identity);
            var pickup = go.GetComponent<Pickup>();
            if (pickup != null) pickup.Configure(effect);

            if (NetworkServer.active) NetworkServer.Spawn(go);
            SfxManager.Play(SfxId.PickupSpawn, p.position);
        }

        private Transform[] AutoPoints()
        {
            var root = new GameObject("PickupPoints").transform;
            root.SetParent(transform);
            var offsets = new[]
            {
                new Vector3(0f, 1.2f, 0f), new Vector3(8f, 1.2f, 8f), new Vector3(-8f, 1.2f, -8f),
                new Vector3(8f, 1.2f, -8f), new Vector3(-8f, 1.2f, 8f), new Vector3(0f, 1.2f, 11f),
                new Vector3(0f, 1.2f, -11f),
            };
            var result = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var t = new GameObject($"PickupPoint_{i}").transform;
                t.SetParent(root);
                t.position = offsets[i];
                result[i] = t;
            }
            return result;
        }
    }
}
