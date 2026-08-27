using System;
using System.Collections.Generic;
using UnityEngine;

namespace BearlyStanding
{
    public enum MatchState { Lobby, Countdown, Playing, GameOver }

    /// <summary>
    /// Tracks alive players and drives the win condition. Local-authority version for Day 1 —
    /// Day 2 makes this server-authoritative over Mirror. Only Eliminated removes a bear from
    /// contention; a Downed bear still counts as alive for the last-bear-standing check.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [SerializeField] private float countdownDuration = 3f;

        public MatchState State { get; private set; } = MatchState.Lobby;

        private readonly List<PlayerHealth> activePlayers = new List<PlayerHealth>();
        private readonly List<Pillow> activePillows = new List<Pillow>();

        public IReadOnlyList<PlayerHealth> ActivePlayers => activePlayers;
        public IReadOnlyList<Pillow> ActivePillows => activePillows;

        public event Action<PlayerHealth> OnMatchWon;
        public event Action OnCountdownStarted;
        public event Action OnFightStarted;

        private void Awake() => Instance = this;

        public void RegisterPlayer(PlayerHealth player)
        {
            if (!activePlayers.Contains(player)) activePlayers.Add(player);
        }

        public void RegisterPillow(Pillow pillow)
        {
            if (!activePillows.Contains(pillow)) activePillows.Add(pillow);
        }

        public void StartMatch()
        {
            State = MatchState.Countdown;
            OnCountdownStarted?.Invoke();
            Invoke(nameof(BeginFight), countdownDuration);
        }

        private void BeginFight()
        {
            State = MatchState.Playing;
            OnFightStarted?.Invoke();
        }

        /// <summary>Called by PlayerHealth.FinalizeElimination().</summary>
        public void NotifyEliminated(PlayerHealth player)
        {
            if (State != MatchState.Playing) return;

            int aliveCount = 0;
            PlayerHealth lastAlive = null;
            foreach (var p in activePlayers)
            {
                if (p == null || p.State == HealthState.Eliminated) continue;
                aliveCount++;
                lastAlive = p;
            }

            if (aliveCount <= 1)
            {
                State = MatchState.GameOver;
                OnMatchWon?.Invoke(lastAlive);
            }
        }
    }
}
