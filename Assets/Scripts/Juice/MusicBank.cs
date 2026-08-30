using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Two looping tracks — one for the menu, one for the arena — plus their mix levels.
    /// Loaded from <c>Resources/MusicBank</c> by <see cref="MusicManager"/>. Create/refresh it
    /// with <c>Bearly Standing ▸ Build Audio</c> (or <c>Assets ▸ Create ▸ Bearly Standing ▸ Music Bank</c>).
    /// If the asset or a clip is missing the manager just stays silent.
    /// </summary>
    [CreateAssetMenu(menuName = "Bearly Standing/Music Bank", fileName = "MusicBank")]
    public class MusicBank : ScriptableObject
    {
        public AudioClip menuMusic;
        public AudioClip gameMusic;

        [Range(0f, 1f)] public float menuVolume = 0.5f;
        [Range(0f, 1f)] public float gameVolume = 0.45f;

        [Tooltip("Scene names that use the menu track. Every other scene uses the game track.")]
        public string[] menuScenes = { "MainMenu" };
    }
}
