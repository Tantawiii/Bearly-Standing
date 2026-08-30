using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BearlyStanding
{
    /// <summary>
    /// One persistent, always-looping music source. Spun up by <see cref="JuiceBootstrap"/> before
    /// the first scene loads; picks the menu vs. arena track from <see cref="MusicBank"/> (Resources)
    /// by scene name and cross-fades whenever the active scene changes. Survives scene loads
    /// (DontDestroyOnLoad) so the loop never restarts mid-track within the same section.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        private static MusicManager instance;

        private MusicBank bank;
        private AudioSource source;
        private string currentKey; // "menu" / "game" / null

        private const float FadeOut = 0.25f;
        private const float FadeIn = 0.6f;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;

            bank = Resources.Load<MusicBank>("MusicBank");

            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;          // "always looped on end"
            source.playOnAwake = false;
            source.spatialBlend = 0f;    // 2D — plays regardless of listener position
            source.ignoreListenerPause = true;
            source.priority = 0;

            SceneManager.activeSceneChanged += OnSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        private void OnSceneChanged(Scene _, Scene next) => Apply(next.name);
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(SceneManager.GetActiveScene().name);

        private void Apply(string sceneName)
        {
            if (bank == null || source == null) return;

            bool menu = bank.menuScenes != null && Array.IndexOf(bank.menuScenes, sceneName) >= 0;
            AudioClip clip = menu ? bank.menuMusic : bank.gameMusic;
            float volume = menu ? bank.menuVolume : bank.gameVolume;
            string key = menu ? "menu" : "game";

            if (clip == null) { StopAllCoroutines(); source.Stop(); currentKey = null; return; }
            if (key == currentKey && source.isPlaying) { source.volume = volume; return; }

            currentKey = key;
            StopAllCoroutines();
            StartCoroutine(CrossFade(clip, volume));
        }

        private IEnumerator CrossFade(AudioClip clip, float target)
        {
            if (source.isPlaying && source.clip != null)
            {
                float from = source.volume;
                for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
                {
                    source.volume = Mathf.Lerp(from, 0f, t / FadeOut);
                    yield return null;
                }
            }

            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.Play();

            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(0f, target, t / FadeIn);
                yield return null;
            }
            source.volume = target;
        }

        internal static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("MusicManager");
            go.AddComponent<MusicManager>();
            DontDestroyOnLoad(go);
        }
    }
}
