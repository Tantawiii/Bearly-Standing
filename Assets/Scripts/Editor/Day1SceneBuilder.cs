using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BearlyStanding.EditorTools
{
    /// <summary>
    /// One-click Day 1 test scene builder: capsule placeholder bears (no rig — see TASKS.md),
    /// a blockout arena, pillows, spawn points, and all the manager/UI wiring needed to play
    /// 1 human vs 5 AI to last-bear-standing, without hand-authoring fragile scene/prefab files.
    /// </summary>
    public static class Day1SceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/Day1_TestArena.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Bearly Standing/Build Day 1 Test Scene")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildArena();
            var (playerSpawns, pillowSpawns) = BuildSpawnPoints();
            var pillowPrefab = BuildPillowPrefab();
            var (humanPrefab, aiPrefab) = BuildBearPrefabs();

            foreach (var spawn in pillowSpawns)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pillowPrefab);
                instance.transform.position = spawn.position;
            }

            var spawnManagerGO = new GameObject("SpawnManager");
            var spawnManager = spawnManagerGO.AddComponent<SpawnManager>();
            spawnManager.playerSpawnPoints = playerSpawns;
            spawnManager.pillowSpawnPoints = pillowSpawns;

            var matchManagerGO = new GameObject("MatchManager");
            var matchManager = matchManagerGO.AddComponent<MatchManager>();

            var (camera, bearCamera) = BuildCamera();
            var hud = BuildHUD();

            var gameManagerGO = new GameObject("GameManager");
            var gameManager = gameManagerGO.AddComponent<GameManager>();
            gameManager.spawnManager = spawnManager;
            gameManager.matchManager = matchManager;
            gameManager.humanBearPrefab = humanPrefab;
            gameManager.aiBearPrefab = aiPrefab;
            gameManager.mainCamera = camera;
            gameManager.bearCamera = bearCamera;
            gameManager.hud = hud;
            gameManager.aiBearCount = 5;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Bearly Standing: Day 1 test scene built at {ScenePath}. Enter Play Mode to fight!");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void BuildArena()
        {
            const float size = 20f;
            const float wallHeight = 4f;
            const float wallThickness = 0.5f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f); // default Plane is 10x10

            GameObject MakeWall(string name, Vector3 pos, Vector3 scale)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = name;
                wall.transform.position = pos;
                wall.transform.localScale = scale;
                return wall;
            }

            float half = size / 2f;
            MakeWall("Wall_North", new Vector3(0f, wallHeight / 2f, half), new Vector3(size, wallHeight, wallThickness));
            MakeWall("Wall_South", new Vector3(0f, wallHeight / 2f, -half), new Vector3(size, wallHeight, wallThickness));
            MakeWall("Wall_East", new Vector3(half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size));
            MakeWall("Wall_West", new Vector3(-half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size));

            var light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static (Transform[] playerSpawns, Transform[] pillowSpawns) BuildSpawnPoints()
        {
            var spawnRoot = new GameObject("SpawnPoints");

            const int playerCount = 6;
            var playerSpawns = new Transform[playerCount];
            const float radius = 7f;
            for (int i = 0; i < playerCount; i++)
            {
                float angle = i * Mathf.PI * 2f / playerCount;
                var go = new GameObject($"PlayerSpawn_{i}");
                go.transform.SetParent(spawnRoot.transform);
                go.transform.position = new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
                go.transform.LookAt(new Vector3(0f, 1f, 0f));
                playerSpawns[i] = go.transform;
            }

            var pillowPositions = new[]
            {
                new Vector3(0f, 0.5f, 0f),
                new Vector3(4f, 0.5f, 4f),
                new Vector3(-4f, 0.5f, 4f),
                new Vector3(4f, 0.5f, -4f),
                new Vector3(-4f, 0.5f, -4f),
                new Vector3(6f, 0.5f, 0f),
                new Vector3(-6f, 0.5f, 0f),
                new Vector3(0f, 0.5f, 6f),
            };
            var pillowSpawns = new Transform[pillowPositions.Length];
            for (int i = 0; i < pillowPositions.Length; i++)
            {
                var go = new GameObject($"PillowSpawn_{i}");
                go.transform.SetParent(spawnRoot.transform);
                go.transform.position = pillowPositions[i];
                pillowSpawns[i] = go.transform;
            }

            return (playerSpawns, pillowSpawns);
        }

        private static GameObject BuildPillowPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pillow";
            go.transform.localScale = new Vector3(0.6f, 0.3f, 0.4f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            go.AddComponent<Pillow>();
            go.AddComponent<PillowProjectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Pillow.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static (GameObject human, GameObject ai) BuildBearPrefabs()
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            GameObject root = null; // reused by the local function below
            GameObject BuildBaseBear(string name)
            {
                root = new GameObject(name);

                var cc = root.AddComponent<CharacterController>();
                cc.center = new Vector3(0f, 1f, 0f);
                cc.height = 2f;
                cc.radius = 0.5f;

                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                var physicsCollider = root.AddComponent<CapsuleCollider>();
                physicsCollider.center = new Vector3(0f, 1f, 0f);
                physicsCollider.height = 2f;
                physicsCollider.radius = 0.5f;
                physicsCollider.enabled = false; // only turned on while Downed/carried/thrown

                root.AddComponent<PlayerController>();
                root.AddComponent<PlayerHealth>();
                root.AddComponent<DownedPlayerHandle>();

                // Placeholder visual — no rig yet (see TASKS.md); collision stays on CharacterController/physicsCollider.
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

                var animator = root.AddComponent<PlayerAnimator>();
                animator.visualRenderer = visual.GetComponent<Renderer>();

                var holdSocket = new GameObject("HoldSocket");
                holdSocket.transform.SetParent(root.transform, false);
                holdSocket.transform.localPosition = new Vector3(0.4f, 1.1f, 0.5f);

                var hitboxGO = new GameObject("MeleeHitbox");
                hitboxGO.transform.SetParent(root.transform, false);
                hitboxGO.transform.localPosition = new Vector3(0f, 1f, 1f);
                var hitboxCollider = hitboxGO.AddComponent<SphereCollider>();
                hitboxCollider.radius = 1f;
                hitboxCollider.isTrigger = true;
                var hitbox = hitboxGO.AddComponent<Hitbox>();

                var camTarget = new GameObject("CameraTarget");
                camTarget.transform.SetParent(root.transform, false);
                camTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);

                var combat = root.AddComponent<PlayerCombat>();
                combat.holdSocket = holdSocket.transform;
                combat.meleeHitbox = hitbox;

                return root;
            }

            var humanGO = BuildBaseBear("Bear_Human");
            var inputHandler = humanGO.AddComponent<PlayerInputHandler>();
            inputHandler.inputActions = inputActions;
            var humanPrefab = PrefabUtility.SaveAsPrefabAsset(humanGO, $"{PrefabFolder}/Bear_Human.prefab");
            Object.DestroyImmediate(humanGO);

            var aiGO = BuildBaseBear("Bear_AI");
            aiGO.AddComponent<TeddyAI>();
            var aiPrefab = PrefabUtility.SaveAsPrefabAsset(aiGO, $"{PrefabFolder}/Bear_AI.prefab");
            Object.DestroyImmediate(aiGO);

            return (humanPrefab, aiPrefab);
        }

        private static (Camera camera, CinemachineCamera bearCamera) BuildCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<CinemachineBrain>();

            var vcamGO = new GameObject("CM Bear Camera");
            var vcam = vcamGO.AddComponent<CinemachineCamera>();
            var orbital = vcamGO.AddComponent<CinemachineOrbitalFollow>();
            orbital.Radius = 4.5f; // OrbitStyle already defaults to Sphere
            var panTilt = vcamGO.AddComponent<CinemachinePanTilt>();
            vcamGO.AddComponent<CinemachineDeoccluder>(); // keeps the camera from clipping through arena walls

            var lookInput = vcamGO.AddComponent<CinemachineLookInput>();
            lookInput.panTilt = panTilt;

            return (cam, vcam);
        }

        private static GameHUD BuildHUD()
        {
            var canvasGO = new GameObject("HUD Canvas");
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            Text MakeText(string name, Vector2 anchor, Vector2 anchoredPos, string content, int fontSize, TextAnchor alignment)
            {
                var go = new GameObject(name);
                go.transform.SetParent(canvasGO.transform, false);
                var text = go.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // "Arial.ttf" was removed in Unity 6
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.text = content;
                text.color = Color.white;
                var rect = text.rectTransform;
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.anchoredPosition = anchoredPos;
                rect.sizeDelta = new Vector2(400f, 60f);
                return text;
            }

            var hearts = MakeText("HeartsText", new Vector2(0f, 1f), new Vector2(120f, -40f), "❤❤❤", 32, TextAnchor.MiddleLeft);
            var bearsLeft = MakeText("BearsLeftText", new Vector2(1f, 1f), new Vector2(-160f, -40f), "BEARS LEFT: 6", 28, TextAnchor.MiddleRight);
            var winner = MakeText("WinnerText", new Vector2(0.5f, 0.5f), Vector2.zero, "VICTORY!", 48, TextAnchor.MiddleCenter);
            winner.gameObject.SetActive(false);

            var hud = canvasGO.AddComponent<GameHUD>();
            hud.heartsText = hearts;
            hud.bearsLeftText = bearsLeft;
            hud.winnerText = winner;
            return hud;
        }
    }
}
