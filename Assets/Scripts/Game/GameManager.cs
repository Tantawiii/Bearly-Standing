using Unity.Cinemachine;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Day-1 single-player bootstrap: spawns the human bear + AI bears from prefabs, gives each a
    /// distinct colour, wires the camera and HUD to the human, and kicks off the match. Multiplayer
    /// (Day 2) replaces player spawning with Mirror's NetworkManager — this script stays focused on
    /// local/offline play.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public SpawnManager spawnManager;
        public MatchManager matchManager;
        public GameObject humanBearPrefab;
        public GameObject aiBearPrefab;
        public Camera mainCamera;
        public CinemachineCamera bearCamera;
        public GameHUD hud;
        public int aiBearCount = 5;

        [Header("Per-bear appearance (wired by Day1SceneBuilder)")]
        [Tooltip("Colour-variant material for the human bear.")]
        public Material humanMaterial;
        [Tooltip("Colour-variant materials cycled across the AI bears.")]
        public Material[] aiMaterials;
        [Tooltip("Fallback tint per bear index (0 = human) used only when no variant materials are set.")]
        public Color[] bearTints;

        private void Start()
        {
            // Networked play (BearlyNetworkManager) drives spawning instead — stay out of its way.
            if (Mirror.NetworkServer.active || Mirror.NetworkClient.active)
            {
                enabled = false;
                return;
            }

            var human = Instantiate(humanBearPrefab, spawnManager.GetNextPlayerSpawn().position, Quaternion.identity);
            var humanHealth = human.GetComponent<PlayerHealth>();
            matchManager.RegisterPlayer(humanHealth);
            ApplyAppearance(human, 0);

            var inputHandler = human.GetComponent<PlayerInputHandler>();
            if (mainCamera != null && inputHandler != null) inputHandler.cameraTransform = mainCamera.transform;

            var humanCombat = human.GetComponent<PlayerCombat>();
            if (mainCamera != null && humanCombat != null) humanCombat.aimSource = mainCamera.transform;

            // Throw-arc preview (human only — never on the AI bears).
            if (human.GetComponent<ThrowTrajectory>() == null) human.AddComponent<ThrowTrajectory>();

            if (bearCamera != null)
            {
                var cameraTarget = human.transform.Find("CameraTarget");
                Transform followTarget = cameraTarget != null ? cameraTarget : human.transform;
                bearCamera.Follow = followTarget;
                bearCamera.LookAt = followTarget;

                var lookInput = bearCamera.GetComponent<CinemachineLookInput>();
                if (lookInput != null) lookInput.input = inputHandler;
            }

            if (hud != null) hud.SetTrackedPlayer(humanHealth);

            for (int i = 0; i < aiBearCount; i++)
            {
                var spawn = spawnManager.GetNextPlayerSpawn();
                var ai = Instantiate(aiBearPrefab, spawn.position, Quaternion.identity);
                matchManager.RegisterPlayer(ai.GetComponent<PlayerHealth>());
                ApplyAppearance(ai, i + 1);
            }

            foreach (var pillow in FindObjectsByType<Pillow>(FindObjectsInactive.Exclude))
                matchManager.RegisterPillow(pillow);

            matchManager.StartMatch();
        }

        private void ApplyAppearance(GameObject bear, int index)
        {
            var appearance = bear.GetComponent<BearAppearance>();
            if (appearance == null) return;

            Material material = index == 0
                ? humanMaterial
                : (aiMaterials != null && aiMaterials.Length > 0 ? aiMaterials[(index - 1) % aiMaterials.Length] : null);

            if (material != null)
            {
                appearance.SetMaterial(material);
            }
            else if (bearTints != null && bearTints.Length > 0)
            {
                appearance.SetTint(bearTints[index % bearTints.Length]);
            }
        }
    }
}
