using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BearlyStanding.EditorTools
{
    /// <summary>
    /// One-click audio wiring. Builds <c>Assets/Resources/SfxBank.asset</c> and
    /// <c>Assets/Resources/MusicBank.asset</c> from the clips in <c>Assets/Audio/</c> and points
    /// the runtime (<see cref="SfxManager"/> / <see cref="MusicManager"/>, both auto-spawned by
    /// <c>JuiceBootstrap</c>) at them. Re-run any time you add or rename a clip.
    ///
    /// Mapping (by the filenames the clips shipped with):
    ///   dust        → PillowHit        (the lighter pillow hits)
    ///   bonk        → HeavyImpact + Knockout (the downing hit)
    ///   victory     → Victory          (you won the match)
    ///   defeat      → Defeat           (you lost the match)
    ///   mouse click → UIClick          (every menu / end-card button)
    ///   in game music / main menu music → looped background tracks per scene
    /// </summary>
    public static class AudioBuilder
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string SfxBankPath = ResourcesFolder + "/SfxBank.asset";
        private const string MusicBankPath = ResourcesFolder + "/MusicBank.asset";

        private const string SfxFolder = "Assets/Audio/SFX";
        private const string MenuMusicPath = "Assets/Audio/Music/main menu music.mp3";
        private const string GameMusicPath = "Assets/Audio/Music/in game music.mp3";

        [MenuItem("Bearly Standing/Build Audio", priority = 22)]
        public static void Build()
        {
            Day1SceneBuilder.EnsureFolder(ResourcesFolder);

            BuildSfxBank();
            BuildMusicBank();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Bearly Standing: audio wired.\n" +
                      $"  {SfxBankPath} — dust→PillowHit, bonk→HeavyImpact/Knockout, victory→Victory, defeat→Defeat, mouse click→UIClick\n" +
                      $"  {MusicBankPath} — main menu music (MainMenu) + in game music (arena), both looped.\n" +
                      "  SfxManager / MusicManager pick these up automatically at play.");
        }

        // ------------------------------------------------------------------ SFX

        private static void BuildSfxBank()
        {
            var bank = LoadOrCreate<SfxBank>(SfxBankPath);

            var entries = new List<SfxBank.Entry>
            {
                Entry(SfxId.PillowHit,    "dust.mp3",        0.9f, 0.10f),
                Entry(SfxId.HeavyImpact,  "bonk.mp3",        1.0f, 0.06f),
                Entry(SfxId.Knockout,     "bonk.mp3",        1.0f, 0.04f),
                Entry(SfxId.Victory,      "victory.mp3",     0.9f, 0f),
                Entry(SfxId.Defeat,       "defeat.mp3",      0.9f, 0f),
                Entry(SfxId.UIClick,      "mouse click.mp3", 0.8f, 0.04f),
            };
            entries.RemoveAll(e => e == null);

            bank.EditorSetEntries(entries.ToArray());
            EditorUtility.SetDirty(bank);
        }

        private static SfxBank.Entry Entry(SfxId id, string file, float volume, float jitter)
        {
            string path = SfxFolder + "/" + file;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"AudioBuilder: no clip at {path} — {id} left silent.");
                return null;
            }
            return new SfxBank.Entry { id = id, clips = new[] { clip }, volume = volume, pitchJitter = jitter };
        }

        // ------------------------------------------------------------------ Music

        private static void BuildMusicBank()
        {
            var bank = LoadOrCreate<MusicBank>(MusicBankPath);

            bank.menuMusic = LoadMusic(MenuMusicPath);
            bank.gameMusic = LoadMusic(GameMusicPath);
            bank.menuScenes = new[] { "MainMenu" };
            if (bank.menuVolume <= 0f) bank.menuVolume = 0.5f;
            if (bank.gameVolume <= 0f) bank.gameVolume = 0.45f;

            EditorUtility.SetDirty(bank);
        }

        private static AudioClip LoadMusic(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { Debug.LogWarning($"AudioBuilder: no music clip at {path}."); return null; }

            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                bool changed = false;
                if (settings.loadType != AudioClipLoadType.Streaming) { settings.loadType = AudioClipLoadType.Streaming; changed = true; }
                if (importer.forceToMono) { importer.forceToMono = false; changed = true; }
                if (!importer.loadInBackground) { importer.loadInBackground = true; changed = true; }
                if (changed)
                {
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
            return clip;
        }

        // ------------------------------------------------------------------ shared

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
