using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BearlyStanding
{
    /// <summary>
    /// Cute front door: Host (get a room code), Join (paste a friend's code), Practice (offline vs
    /// bots), Quit. Wired by MainMenuBuilder — every reference is optional so a half-built menu
    /// still runs.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        public Button hostButton;
        public Button openJoinButton;
        public Button joinConfirmButton;
        public Button joinBackButton;
        public Button practiceButton;
        public Button quitButton;
        public Button copyCodeButton;

        [Header("Panels & fields")]
        public GameObject joinPanel;
        public GameObject codePanel;
        public InputField codeInput;
        public Text statusText;
        public Text roomCodeText;

        [Header("Flavour")]
        public Transform spinner;
        public float spinSpeed = 20f;

        [Header("Scenes")]
        public string practiceScene = "Day1_TestArena";

        private bool codeShown; // room-code panel latched open once hosting

        private void Awake()
        {
            Hook(hostButton, OnHost);
            Hook(openJoinButton, () => Toggle(joinPanel, true));
            Hook(joinBackButton, () => { Toggle(joinPanel, false); SetStatus(string.Empty); });
            Hook(joinConfirmButton, OnJoinConfirm);
            Hook(practiceButton, OnPractice);
            Hook(quitButton, OnQuit);
            Hook(copyCodeButton, CopyCode);

            Toggle(joinPanel, false);
            Toggle(codePanel, false);
            SetStatus(string.Empty);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (spinner != null) spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

            var mgr = BearlyNetworkManager.Bearly;
            if (mgr == null) return;

            if (!codeShown && !string.IsNullOrEmpty(mgr.RoomCode) && NetworkServer.active)
            {
                codeShown = true;
                Toggle(codePanel, true);
                if (roomCodeText != null) roomCodeText.text = mgr.RoomCode;
                SetStatus("Hosting — share the code. Empty seats fill with bots, then the match starts.");
            }
            else if (NetworkClient.active && !NetworkClient.isConnected)
            {
                SetStatus("Connecting…");
            }
        }

        private void OnHost()
        {
            var mgr = BearlyNetworkManager.Bearly;
            if (mgr == null) { SetStatus("No BearlyNetworkManager in the scene."); return; }
            SetStatus("Starting host…");
            mgr.HostGame();
        }

        private void OnJoinConfirm()
        {
            var mgr = BearlyNetworkManager.Bearly;
            if (mgr == null) { SetStatus("No BearlyNetworkManager in the scene."); return; }
            string code = codeInput != null ? codeInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(code)) { SetStatus("Enter a room code (or ip / ip:port)."); return; }

            SetStatus("Connecting…");
            if (!mgr.JoinGame(code)) SetStatus("That doesn't look like a valid code.");
        }

        private void OnPractice()
        {
            // Offline: don't touch the NetworkManager, just load the arena — GameManager runs there.
            SetStatus("Loading practice arena…");
            SceneManager.LoadScene(practiceScene);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CopyCode()
        {
            var mgr = BearlyNetworkManager.Bearly;
            if (mgr != null && !string.IsNullOrEmpty(mgr.RoomCode))
            {
                GUIUtility.systemCopyBuffer = mgr.RoomCode;
                SetStatus($"Copied {mgr.RoomCode} to clipboard.");
            }
        }

        private static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(() => { SfxManager.PlayUI(SfxId.UIClick); action(); });
        }

        private static void Toggle(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
