using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BearlyStanding
{
    /// <summary>Minimal Day-1 HUD: the local bear's hearts, how many bears are left, and a win/lose banner.</summary>
    public class GameHUD : MonoBehaviour
    {
        public PlayerHealth trackedPlayer;
        public Text heartsText;
        public Text bearsLeftText;
        public Text winnerText;

        private void OnEnable()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnMatchWon += HandleMatchWon;
            if (winnerText != null) winnerText.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnMatchWon -= HandleMatchWon;
        }

        private void Update()
        {
            if (MatchManager.Instance == null || bearsLeftText == null) return;

            int alive = MatchManager.Instance.ActivePlayers.Count(p => p != null && p.State != HealthState.Eliminated);
            bearsLeftText.text = $"BEARS LEFT: {alive}";
        }

        public void SetTrackedPlayer(PlayerHealth player)
        {
            if (trackedPlayer != null) trackedPlayer.OnHeartsChanged -= RefreshHearts;
            trackedPlayer = player;
            if (trackedPlayer != null)
            {
                trackedPlayer.OnHeartsChanged += RefreshHearts;
                RefreshHearts(trackedPlayer.CurrentHearts);
            }
        }

        private void RefreshHearts(int hearts)
        {
            if (heartsText != null) heartsText.text = string.Concat(Enumerable.Repeat("❤", hearts));
        }

        private void HandleMatchWon(PlayerHealth winner)
        {
            if (winnerText == null) return;
            winnerText.gameObject.SetActive(true);
            winnerText.text = winner == trackedPlayer ? "VICTORY!" : "DEFEAT";
        }
    }
}
