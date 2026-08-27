using Unity.Cinemachine;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Day-1 single-player bootstrap: spawns the human bear + AI bears from prefabs, wires the
    /// camera and HUD to the human, and kicks off the match. Multiplayer (Day 2) replaces player
    /// spawning with Mirror's NetworkManager — this script stays focused on local/offline play.
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

        private void Start()
        {
            var human = Instantiate(humanBearPrefab, spawnManager.GetNextPlayerSpawn().position, Quaternion.identity);
            var humanHealth = human.GetComponent<PlayerHealth>();
            matchManager.RegisterPlayer(humanHealth);

            var inputHandler = human.GetComponent<PlayerInputHandler>();
            if (mainCamera != null && inputHandler != null) inputHandler.cameraTransform = mainCamera.transform;

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
            }

            foreach (var pillow in FindObjectsByType<Pillow>(FindObjectsInactive.Exclude))
                matchManager.RegisterPillow(pillow);

            matchManager.StartMatch();
        }
    }
}
