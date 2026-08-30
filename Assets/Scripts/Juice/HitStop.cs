using System.Collections;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Very brief time-freeze on heavy impacts (knockouts). Uses unscaled time so it always thaws.</summary>
    public class HitStop : MonoBehaviour
    {
        private static HitStop instance;
        private Coroutine running;
        private float baseFixedDelta;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            baseFixedDelta = Time.fixedDeltaTime;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Freeze the game for <paramref name="seconds"/> of real time (e.g. 0.06–0.12 for a knockout).</summary>
        public static void Freeze(float seconds)
        {
            if (instance == null || seconds <= 0f) return;
            if (instance.running != null) instance.StopCoroutine(instance.running);
            instance.running = instance.StartCoroutine(instance.FreezeRoutine(seconds));
        }

        private IEnumerator FreezeRoutine(float seconds)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = baseFixedDelta * 0.001f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDelta;
            running = null;
        }

        internal static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("HitStop");
            go.AddComponent<HitStop>();
            DontDestroyOnLoad(go);
        }
    }
}
