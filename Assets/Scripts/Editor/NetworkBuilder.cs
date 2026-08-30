using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BearlyStanding.EditorTools
{
    /// <summary>
    /// Layers Mirror on top of the offline build: makes the Bear_Net prefab (one prefab for players
    /// and bots), points the BearlyNetworkManager in the menu scene at it, and injects the arena
    /// scene's networked bits (NetworkMatchController + NetworkStartPosition on each spawn).
    /// Run "Build Day 1 Test Scene" and "Build Main Menu" first, or just use "Build Everything".
    /// </summary>
    public static class NetworkBuilder
    {
        private const string BearNetPrefabPath = Day1SceneBuilder.PrefabFolder + "/Bear_Net.prefab";

        [MenuItem("Bearly Standing/Build Everything", priority = -10)]
        public static void BuildEverything()
        {
            Day1SceneBuilder.Build();
            MainMenuBuilder.Build();
            BuildNetworking();
            AudioBuilder.Build();
            Debug.Log("Bearly Standing: full build complete.\n" +
                      "Set MainMenu as the first scene (it is), press Play → Host for a code, or Join to enter one, or Practice for offline.");
        }

        [MenuItem("Bearly Standing/Build Networking", priority = 21)]
        public static void BuildNetworking()
        {
            var prefab = BuildBearNetPrefab();
            ConfigureManagerInMenuScene(prefab);
            InjectArenaNetworkObjects();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Bearly Standing: networking wired. Bear_Net prefab at {BearNetPrefabPath}. " +
                      "Host from MainMenu for a room code; empty seats fill with bots.");
        }

        private static GameObject BuildBearNetPrefab()
        {
            var art = Day1SceneBuilder.LastArtValid ? Day1SceneBuilder.LastArt : Day1SceneBuilder.BuildBearArt();

            var root = Day1SceneBuilder.BuildBearRoot("Bear_Net", art);

            var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(Day1SceneBuilder.InputActionsPath);
            var input = root.AddComponent<PlayerInputHandler>();
            input.inputActions = inputActions;
            input.enabled = false; // NetworkBear enables it only on the local player

            var ai = root.AddComponent<TeddyAI>();
            ai.enabled = false;     // NetworkBear enables it only for host-owned bots

            root.AddComponent<NetworkIdentity>();

            var nt = root.AddComponent<NetworkTransformUnreliable>();
            nt.target = root.transform;
            nt.syncDirection = SyncDirection.ClientToServer;
            nt.syncPosition = true;
            nt.syncRotation = true;
            nt.syncScale = false;

            var visualAnimator = root.GetComponentInChildren<Animator>(true);
            var netAnim = root.AddComponent<NetworkAnimator>();
            netAnim.animator = visualAnimator;
            netAnim.clientAuthority = true;

            var playerAnimator = root.GetComponent<PlayerAnimator>();
            if (playerAnimator != null) playerAnimator.networkAnimator = netAnim;

            root.AddComponent<NetworkBear>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BearNetPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureManagerInMenuScene(GameObject bearNetPrefab)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuBuilder.MenuScenePath, OpenSceneMode.Single);

            var manager = Object.FindAnyObjectByType<BearlyNetworkManager>();
            if (manager == null)
            {
                Debug.LogWarning("NetworkBuilder: no BearlyNetworkManager in MainMenu — run 'Build Main Menu' first.");
                return;
            }

            manager.playerPrefab = bearNetPrefab;
            manager.botBearPrefab = bearNetPrefab;
            manager.onlineScene = Day1SceneBuilder.ScenePath;
            manager.offlineScene = MainMenuBuilder.MenuScenePath;
            manager.autoCreatePlayer = true;

            var spawnables = manager.spawnPrefabs.Where(p => p != null && p != bearNetPrefab).ToList();
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Day1SceneBuilder.PrefabFolder + "/Pickup.prefab");
            if (pickupPrefab != null && !spawnables.Contains(pickupPrefab)) spawnables.Add(pickupPrefab);
            manager.spawnPrefabs = spawnables;
            manager.bearMaterials = Day1SceneBuilder.BearPalette
                .Select(p => AssetDatabase.LoadAssetAtPath<Material>($"{Day1SceneBuilder.VariantFolder}/Ted_{p.name}.mat"))
                .Where(m => m != null)
                .ToArray();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void InjectArenaNetworkObjects()
        {
            var scene = EditorSceneManager.OpenScene(Day1SceneBuilder.ScenePath, OpenSceneMode.Single);

            if (Object.FindAnyObjectByType<NetworkMatchController>() == null)
            {
                var go = new GameObject("NetworkMatch");
                go.AddComponent<NetworkIdentity>();
                go.AddComponent<NetworkMatchController>();
            }

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("PlayerSpawn_")) continue;
                if (t.GetComponent<NetworkStartPosition>() == null) t.gameObject.AddComponent<NetworkStartPosition>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
