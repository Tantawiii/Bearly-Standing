using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BearlyStanding
{
    /// <summary>
    /// In-match HUD: the local bear's hearts, bears-left, a 3-2-1-FIGHT! countdown, a fading
    /// control-hint strip, and a victory / defeat card with Rematch + Menu buttons.
    /// Every reference is optional so a partially-built HUD still runs.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public PlayerHealth trackedPlayer;

        [Header("Core readouts")]
        public Text heartsText;
        public Text bearsLeftText;

        [Header("Countdown")]
        public Text countdownText;

        [Header("Control hints")]
        public GameObject controlHints;
        public float hintSeconds = 10f;

        [Header("End card")]
        public GameObject endCard;
        public Text winnerText;
        public Button rematchButton;
        public Button menuButton;

        private MatchManager match;
        // subscribed in Start (after MatchManager.Awake), unsubscribed in OnDestroy

        private void Start()
        {
            match = MatchManager.Instance;
            if (match != null)
            {
                match.OnMatchWon += HandleMatchWon;
                match.OnCountdownStarted += HandleCountdown;
                match.OnFightStarted += HandleFight;
            }
            if (endCard != null) endCard.SetActive(false);
            if (countdownText != null) countdownText.gameObject.SetActive(false);

            if (rematchButton != null) rematchButton.onClick.AddListener(OnRematch);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);

            if (controlHints != null)
            {
                controlHints.SetActive(true);
                StartCoroutine(HideHintsAfter(hintSeconds));
            }
        }

        private void OnDestroy()
        {
            if (match == null) return;
            match.OnMatchWon -= HandleMatchWon;
            match.OnCountdownStarted -= HandleCountdown;
            match.OnFightStarted -= HandleFight;
        }

        private void Update()
        {
            if (match == null || bearsLeftText == null) return;
            int alive = match.ActivePlayers.Count(p => p != null && p.State != HealthState.Eliminated);
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
            if (heartsText != null) heartsText.text = string.Concat(Enumerable.Repeat("❤ ", Mathf.Max(0, hearts)));
        }

        // ---- countdown ----

        private void HandleCountdown() => StartCoroutine(CountdownRoutine());

        private IEnumerator CountdownRoutine()
        {
            if (countdownText == null) yield break;
            countdownText.gameObject.SetActive(true);
            for (int n = 3; n >= 1; n--)
            {
                countdownText.text = n.ToString();
                SfxManager.PlayUI(SfxId.Countdown);
                yield return PunchScale(countdownText.transform, 0.8f);
            }
        }

        private void HandleFight()
        {
            if (countdownText == null) return;
            StopCoroutine(nameof(CountdownRoutine));
            StartCoroutine(FightFlash());
        }

        private IEnumerator FightFlash()
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "FIGHT!";
            SfxManager.PlayUI(SfxId.CountdownGo);
            yield return PunchScale(countdownText.transform, 0.9f);
            yield return new WaitForSeconds(0.4f);
            countdownText.gameObject.SetActive(false);
        }

        private static IEnumerator PunchScale(Transform t, float hold)
        {
            float e = 0f;
            while (e < hold)
            {
                e += Time.deltaTime;
                float k = e / hold;
                float s = k < 0.25f ? Mathf.Lerp(0.4f, 1.25f, k / 0.25f) : Mathf.Lerp(1.25f, 1f, (k - 0.25f) / 0.75f);
                t.localScale = Vector3.one * s;
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ---- hints ----

        private IEnumerator HideHintsAfter(float seconds)
        {
            float e = 0f;
            var pc = trackedPlayer != null ? trackedPlayer.GetComponent<PlayerController>() : null;
            while (e < seconds)
            {
                e += Time.deltaTime;
                if (pc != null && pc.PlanarSpeed > 0.5f) break;
                yield return null;
            }
            if (controlHints != null) controlHints.SetActive(false);
        }

        // ---- end card ----

        private void HandleMatchWon(PlayerHealth winner)
        {
            if (endCard != null) endCard.SetActive(true);
            bool localWon = winner != null && winner == trackedPlayer;
            if (winnerText != null) winnerText.text = localWon ? "VICTORY!" : "DEFEAT";
            SfxManager.PlayUI(localWon ? SfxId.Victory : SfxId.Defeat);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Rematch is offline-only for now; networked players use Menu (re-host to play again).
            if (rematchButton != null)
                rematchButton.gameObject.SetActive(!Mirror.NetworkClient.active && !Mirror.NetworkServer.active);
        }

        private void OnRematch()
        {
            SfxManager.PlayUI(SfxId.UIClick);
            MatchManager.Instance?.RequestRematch();
        }

        private void OnMenu()
        {
            SfxManager.PlayUI(SfxId.UIClick);
            var mgr = BearlyNetworkManager.Bearly;
            if (mgr != null && (Mirror.NetworkClient.active || Mirror.NetworkServer.active))
                mgr.LeaveGame();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
}
