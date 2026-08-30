using System.Collections.Generic;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// One call site for all sound: <c>SfxManager.Play(SfxId.PillowHit, worldPos)</c> or
    /// <c>SfxManager.PlayUI(SfxId.UIClick)</c>. Pulls clips from a <see cref="SfxBank"/> loaded from
    /// <c>Resources/SfxBank</c>; if there's no bank or no clip for an id, the call is a no-op.
    /// Pooled AudioSources, 3D for world sounds and 2D for UI.
    /// </summary>
    public class SfxManager : MonoBehaviour
    {
        private static SfxManager instance;

        private SfxBank bank;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private int next;
        private AudioSource ui;

        private const int PoolSize = 12;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;

            bank = Resources.Load<SfxBank>("SfxBank");

            for (int i = 0; i < PoolSize; i++)
            {
                var src = new GameObject($"Sfx_{i}").AddComponent<AudioSource>();
                src.transform.SetParent(transform);
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 28f;
                pool.Add(src);
            }
            ui = new GameObject("Sfx_UI").AddComponent<AudioSource>();
            ui.transform.SetParent(transform);
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(SfxId id, Vector3 worldPosition)
        {
            if (instance == null || instance.bank == null) return;
            if (!instance.bank.TryGet(id, out var clip, out var vol, out var pitch)) return;

            var src = instance.pool[instance.next];
            instance.next = (instance.next + 1) % instance.pool.Count;
            src.transform.position = worldPosition;
            src.pitch = pitch;
            src.PlayOneShot(clip, vol);
        }

        public static void PlayUI(SfxId id)
        {
            if (instance == null || instance.bank == null || instance.ui == null) return;
            if (!instance.bank.TryGet(id, out var clip, out var vol, out var pitch)) return;
            instance.ui.pitch = pitch;
            instance.ui.PlayOneShot(clip, vol);
        }

        internal static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("SfxManager");
            go.AddComponent<SfxManager>();
            DontDestroyOnLoad(go);
        }
    }
}
