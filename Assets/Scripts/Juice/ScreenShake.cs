using Unity.Cinemachine;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Global camera-shake entry point built on Cinemachine Impulse. A single hidden
    /// <see cref="CinemachineImpulseSource"/> emits impulses at world positions; the vcam's
    /// <see cref="CinemachineImpulseListener"/> (added by the scene builders) picks them up with
    /// natural distance falloff, so a bonk across the room barely nudges you.
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class ScreenShake : MonoBehaviour
    {
        private static ScreenShake instance;

        private CinemachineImpulseSource source;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            source = GetComponent<CinemachineImpulseSource>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Kick the camera. <paramref name="force"/> ~0.15 for a jab, ~0.6 for a heavy hit, ~1.2 for a knockout.</summary>
        public static void Bump(Vector3 worldPosition, float force)
        {
            if (instance == null || instance.source == null) return;
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.4f + 0.2f; // bias a little upward, never straight down
            instance.source.GenerateImpulseAtPositionWithVelocity(worldPosition, dir.normalized * force);
        }

        internal static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("ScreenShake");
            go.AddComponent<CinemachineImpulseSource>();
            go.AddComponent<ScreenShake>();
            DontDestroyOnLoad(go);
        }
    }
}
