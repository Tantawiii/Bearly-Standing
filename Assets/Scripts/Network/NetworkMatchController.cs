using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Tiny scene NetworkBehaviour that starts the shared match clock. The server flips
    /// <see cref="playing"/> once the lobby is filled (players + bots); the hook makes every client
    /// start its local <see cref="MatchManager"/> so eliminations and the win banner stay in step.
    /// </summary>
    public class NetworkMatchController : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnPlayingChanged))] private bool playing;

        [Server]
        public void ServerBeginMatch()
        {
            if (playing) return;
            playing = true;
            if (MatchManager.Instance != null) MatchManager.Instance.StartMatch();
        }

        private void OnPlayingChanged(bool wasPlaying, bool now)
        {
            if (now && !isServer && MatchManager.Instance != null)
                MatchManager.Instance.StartMatch();
        }
    }
}
