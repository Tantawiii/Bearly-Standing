using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Spins up the global juice singletons (shake / hit-stop / hit VFX / SFX / music) before any scene loads.</summary>
    internal static class JuiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            ScreenShake.EnsureExists();
            HitStop.EnsureExists();
            HitFeedback.EnsureExists();
            SfxManager.EnsureExists();
            MusicManager.EnsureExists();
        }
    }
}
