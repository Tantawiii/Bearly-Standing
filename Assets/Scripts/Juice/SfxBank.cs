using System;
using UnityEngine;

namespace BearlyStanding
{
    public enum SfxId
    {
        PillowSwing, PillowHit, PillowThrow, HeavyImpact, WallImpact, Footstep, Grunt,
        HitReaction, Knockout, JumpHop, UIHover, UIClick, Countdown, CountdownGo, Victory,
        PickupSpawn, PickupGrab, Defeat
    }

    /// <summary>
    /// Drop-in sound table. Create one via <c>Assets ▸ Create ▸ Bearly Standing ▸ SFX Bank</c>
    /// (or the build tool makes an empty one at <c>Assets/Resources/SfxBank.asset</c>) and fill in
    /// clips as you get them. Empty entries are silent — nothing breaks.
    /// </summary>
    [CreateAssetMenu(menuName = "Bearly Standing/SFX Bank", fileName = "SfxBank")]
    public class SfxBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.9f;
            [Range(0f, 0.5f)] public float pitchJitter = 0.08f;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

#if UNITY_EDITOR
        /// <summary>Editor-only: lets <c>AudioBuilder</c> repopulate the table in one click. Compiled out of player builds.</summary>
        public void EditorSetEntries(Entry[] newEntries) => entries = newEntries ?? Array.Empty<Entry>();
#endif

        public bool TryGet(SfxId id, out AudioClip clip, out float volume, out float pitch)
        {
            clip = null;
            volume = 0.9f;
            pitch = 1f;
            foreach (var e in entries)
            {
                if (e == null || e.id != id || e.clips == null || e.clips.Length == 0) continue;
                clip = e.clips[UnityEngine.Random.Range(0, e.clips.Length)];
                if (clip == null) continue;
                volume = e.volume;
                pitch = 1f + UnityEngine.Random.Range(-e.pitchJitter, e.pitchJitter);
                return true;
            }
            return false;
        }
    }
}
