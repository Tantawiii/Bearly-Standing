using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BearlyStanding.EditorTools
{
    /// <summary>
    /// One-click builder for the offline arena scene (Practice / Day 1 loop): a big closed
    /// pillow-fort room, throwable pillow, spawn points, managers, HUD and camera — 1 human vs 5 AI
    /// to last-bear-standing without hand-authoring fragile scene/prefab files.
    ///
    /// Bears use the <c>ted_low</c> FBX + its Mixamo rig with a real Idle/Walk/Fast-Run/Jump/Throw
    /// Animator (falls back to capsule + procedural clips if the art can't be read). Pillow models
    /// are data-driven: drop <c>pil1_low</c>…<c>pil4_low</c> / <c>pil3_low</c> into Assets/Graphics
    /// and rebuild — no code change. Shared helpers here are reused by MainMenuBuilder / NetworkBuilder.
    /// </summary>
    public static class Day1SceneBuilder
    {
        internal const string PrefabFolder = "Assets/Prefabs";
        internal const string SceneFolder = "Assets/Scenes";
        internal const string ScenePath = SceneFolder + "/Day1_TestArena.unity";
        internal const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        internal const string TedFolder = "Assets/Graphics/ted";
        internal const string TedFbxPath = TedFolder + "/ted_low.fbx";
        internal const string BaseTedMatPath = TedFolder + "/Ted.mat";
        internal const string GeneratedFolder = TedFolder + "/Generated";
        internal const string VariantFolder = TedFolder + "/Variants";
        internal const string ControllerPath = GeneratedFolder + "/Ted.controller";

        internal const string PillowMatPath = "Assets/Graphics/pillowsflashlight/Pillow.mat";

        internal const float BearTargetHeight = 1.8f;
        internal const float ArenaSize = 28f;
        internal const float ArenaWallHeight = 4.5f;

        // Animation FBX candidates by role — first that exists wins. Add filenames here, not code.
        private static readonly string[] IdleFbx = { TedFolder + "/Idle.fbx" };
        private static readonly string[] WalkFbx = { TedFolder + "/Walking.fbx", TedFolder + "/Walk.fbx" };
        private static readonly string[] RunFbx = { TedFolder + "/Fast Run.fbx", TedFolder + "/FastRun.fbx", TedFolder + "/Run.fbx" };
        private static readonly string[] JumpFbx = { TedFolder + "/Jump.fbx", TedFolder + "/Jumping.fbx" };
        private static readonly string[] ThrowFbx = { TedFolder + "/Throw.fbx", TedFolder + "/Throwing.fbx" };

        /// <summary>Index 0 is the human bear; the rest cycle across the AI bears.</summary>
        internal static readonly (string name, Color color)[] BearPalette =
        {
            ("Honey", new Color(0.95f, 0.72f, 0.30f)),
            ("Cocoa", new Color(0.42f, 0.27f, 0.17f)),
            ("Rosewood", new Color(0.82f, 0.34f, 0.40f)),
            ("Mint", new Color(0.46f, 0.78f, 0.60f)),
            ("Blueberry", new Color(0.34f, 0.47f, 0.84f)),
            ("Plum", new Color(0.58f, 0.38f, 0.70f)),
        };

        internal struct BearArt
        {
            public GameObject modelPrefab;            // null => capsule fallback
            public RuntimeAnimatorController controller;
            public Avatar avatar;
            public string report;
            public bool UseCapsule => modelPrefab == null;
        }

        /// <summary>Last art built this session — NetworkBuilder reuses it to avoid a second rig reimport.</summary>
        internal static BearArt LastArt;
        internal static bool LastArtValid;

        [MenuItem("Bearly Standing/Build Day 1 Test Scene", priority = 0)]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(TedFolder);
            EnsureFolder(GeneratedFolder);
            EnsureFolder(VariantFolder);

            BearArt art = BuildBearArt();
            LastArt = art;
            LastArtValid = true;
            Material[] materials = BuildBearMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildPillowFortArena();
            BuildEnvironmentProps();
            var (playerSpawns, pillowSpawns) = BuildSpawnPoints();
            var pillowPrefab = BuildPillowPrefab(out string pillowReport);
            var pickupPrefab = BuildPickupPrefab();
            var (humanPrefab, aiPrefab) = BuildBearPrefabs(art);

            foreach (var spawn in pillowSpawns)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pillowPrefab);
                instance.transform.position = spawn.position;
            }

            var spawnManagerGO = new GameObject("SpawnManager");
            var spawnManager = spawnManagerGO.AddComponent<SpawnManager>();
            spawnManager.playerSpawnPoints = playerSpawns;
            spawnManager.pillowSpawnPoints = pillowSpawns;

            new GameObject("ArenaEffectManager").AddComponent<ArenaEffectManager>();

            var pickupSpawnerGO = new GameObject("PickupSpawner");
            var pickupSpawner = pickupSpawnerGO.AddComponent<PickupSpawner>();
            pickupSpawner.pickupPrefab = pickupPrefab;

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
            gameManager.humanMaterial = materials.Length > 0 ? materials[0] : null;
            gameManager.aiMaterials = materials.Length > 1 ? materials.Skip(1).ToArray() : materials;
            gameManager.bearTints = BearPalette.Select(p => p.color).ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings(ScenePath, prepend: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Bearly Standing: arena scene built at {ScenePath}.\n" +
                      $"Bears: {(art.UseCapsule ? "CAPSULE placeholder" : "ted_low FBX")} — {art.report}\n" +
                      $"Throwable pillow: {pillowReport}\n" +
                      "Enter Play Mode to fight (1 human vs 5 AI). WASD move · Shift run · Space jump · LMB throw · hold RMB to aim · E grab.");
        }

        // ---------------------------------------------------------------------------------------
        //  Bear art: rig setup + real-clip Animator
        // ---------------------------------------------------------------------------------------

        internal static BearArt BuildBearArt()
        {
            var art = new BearArt { report = string.Empty };
            try
            {
                var tedImporter = AssetImporter.GetAtPath(TedFbxPath) as ModelImporter;
                if (tedImporter == null)
                {
                    art.report = "ted_low.fbx not found — capsule bears.";
                    return art;
                }

                ConfigureRig(tedImporter, ModelImporterAvatarSetup.CreateFromThisModel, null, null);
                Avatar tedAvatar = AssetDatabase.LoadAllAssetsAtPath(TedFbxPath).OfType<Avatar>().FirstOrDefault();

                GameObject tedModel = AssetDatabase.LoadAssetAtPath<GameObject>(TedFbxPath);
                if (!HasSkinnedMesh(tedModel))
                {
                    art.report += "ted_low.fbx has no skinned mesh — capsule bears.";
                    return art;
                }

                art.modelPrefab = tedModel;
                art.avatar = tedAvatar;
                art.controller = BuildAnimatorController(tedModel, tedAvatar, out string clipReport);
                art.report += clipReport;
                return art;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Day1SceneBuilder: bear-art pipeline failed ({e.Message})\n{e.StackTrace}\nFalling back to capsule bears.");
                return new BearArt { report = "exception in art pipeline — capsule fallback." };
            }
        }

        private static bool HasSkinnedMesh(GameObject go) =>
            go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

        private static void ConfigureRig(ModelImporter importer, ModelImporterAvatarSetup setup, Avatar source, bool? loop)
        {
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.avatarSetup != setup)
            {
                importer.avatarSetup = setup;
                changed = true;
            }
            if (setup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar != source)
            {
                importer.sourceAvatar = source;
                changed = true;
            }
            if (loop.HasValue)
            {
                var clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    bool clipChanged = false;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        if (clips[i].loopTime != loop.Value) { clips[i].loopTime = loop.Value; clipChanged = true; }
                        if (clips[i].loopPose != loop.Value) { clips[i].loopPose = loop.Value; clipChanged = true; }
                        if (!clips[i].lockRootPositionXZ) { clips[i].lockRootPositionXZ = true; clipChanged = true; }
                        if (!clips[i].lockRootHeightY) { clips[i].lockRootHeightY = true; clipChanged = true; }
                        if (!clips[i].lockRootRotation) { clips[i].lockRootRotation = true; clipChanged = true; }
                    }
                    if (clipChanged)
                    {
                        importer.clipAnimations = clips;
                        changed = true;
                    }
                }
            }
            if (changed) importer.SaveAndReimport();
        }

        private static AnimationClip ResolveClip(string[] candidatePaths, Avatar tedAvatar, bool loop)
        {
            foreach (var path in candidatePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var setup = tedAvatar != null ? ModelImporterAvatarSetup.CopyFromOther : ModelImporterAvatarSetup.CreateFromThisModel;
                ConfigureRig(importer, setup, tedAvatar, loop);

                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
                if (clip != null) return clip;
            }
            return null;
        }

        private static RuntimeAnimatorController BuildAnimatorController(GameObject model, Avatar tedAvatar, out string report)
        {
            // Real Mixamo clips where available.
            AnimationClip realIdle = ResolveClip(IdleFbx, tedAvatar, true);
            AnimationClip realWalk = ResolveClip(WalkFbx, tedAvatar, true);
            AnimationClip realRun = ResolveClip(RunFbx, tedAvatar, true);
            AnimationClip jumpClip = ResolveClip(JumpFbx, tedAvatar, false);
            AnimationClip realThrow = ResolveClip(ThrowFbx, tedAvatar, false);

            // Procedural placeholders (only used to fill any gaps).
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            AnimationClip procIdle, procThrow, procSwing;
            try
            {
                procIdle = BuildIdleClip(temp);
                procThrow = BuildArmActionClip(temp, "Ted_Throw", 0.55f, 0.16f, 0.30f);
                procSwing = BuildArmActionClip(temp, "Ted_Swing", 0.40f, 0.10f, 0.22f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }

            AnimationClip idleClip = realIdle != null ? realIdle : procIdle;
            AnimationClip walkClip = realWalk != null ? realWalk : idleClip;
            AnimationClip runClip = realRun != null ? realRun : walkClip;
            AnimationClip throwClip = realThrow != null ? realThrow : procThrow;
            AnimationClip swingClip = procSwing != null ? procSwing : throwClip;

            string Tag(AnimationClip real) => real != null ? real.name : "placeholder";
            report = $"clips[idle={Tag(realIdle)}, walk={Tag(realWalk)}, run={Tag(realRun)}, " +
                     $"jump={(jumpClip != null ? jumpClip.name : "MISSING")}, throw={Tag(realThrow)}]";

            // Rebuild in place when it already exists so the asset GUID (and every prefab reference
            // to it) survives repeated builds.
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl != null)
            {
                for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
                var sm0 = ctrl.layers[0].stateMachine;
                foreach (var child in sm0.states) sm0.RemoveState(child.state);
                foreach (var t in sm0.anyStateTransitions) sm0.RemoveAnyStateTransition(t);
            }
            else
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Swing", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            var locoState = ctrl.CreateBlendTreeInController("Locomotion", out var loco, 0);
            loco.blendType = BlendTreeType.Simple1D;
            loco.blendParameter = "Speed";
            loco.useAutomaticThresholds = false;
            loco.AddChild(idleClip, 0f);
            loco.AddChild(walkClip, 0.5f);
            loco.AddChild(runClip, 1f);
            sm.defaultState = locoState;

            AnimatorState AddClipState(string name, Motion motion, float exitTime)
            {
                var st = sm.AddState(name);
                st.motion = motion;
                var any = sm.AddAnyStateTransition(st);
                any.duration = 0.06f;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, name);
                var back = st.AddTransition(locoState);
                back.hasExitTime = true;
                back.exitTime = exitTime;
                back.duration = 0.14f;
                return st;
            }

            AddClipState("Jump", jumpClip != null ? jumpClip : procThrow, 0.55f);
            AddClipState("Throw", throwClip, 0.75f);
            AddClipState("Swing", swingClip, 0.8f);
            AddClipState("HitReact", procSwing, 0.6f); // placeholder flinch — swap a real Hit Reaction clip on Day 3

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ---- procedural placeholder clips (mixamorig transform curves) ----

        private static Transform FindBone(GameObject root, params string[] namePartsInPriority)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var part in namePartsInPriority)
                foreach (var t in all)
                    if (t.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
            return null;
        }

        private static void SetLocalRotationKeys(AnimationClip clip, string path, Quaternion bind, (float t, Vector3 delta)[] keys)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            var cw = new AnimationCurve();
            foreach (var (t, delta) in keys)
            {
                Quaternion q = bind * Quaternion.Euler(delta);
                cx.AddKey(t, q.x);
                cy.AddKey(t, q.y);
                cz.AddKey(t, q.z);
                cw.AddKey(t, q.w);
            }
            clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
        }

        private static AnimationClip BuildIdleClip(GameObject temp)
        {
            var clip = new AnimationClip { name = "Ted_Idle" };
            var root = temp.transform;

            var hips = FindBone(temp, "Hips");
            if (hips != null)
            {
                string p = AnimationUtility.CalculateTransformPath(hips, root);
                float y = hips.localPosition.y;
                var bob = new AnimationCurve(new Keyframe(0f, y), new Keyframe(1f, y - 0.015f), new Keyframe(2f, y));
                clip.SetCurve(p, typeof(Transform), "localPosition.y", bob);
                SetLocalRotationKeys(clip, p, hips.localRotation, new[]
                {
                    (0f, Vector3.zero), (1f, new Vector3(1.5f, 0f, 0f)), (2f, Vector3.zero)
                });
            }

            var spine = FindBone(temp, "Spine2", "Spine1", "Spine");
            if (spine != null)
            {
                SetLocalRotationKeys(clip, AnimationUtility.CalculateTransformPath(spine, root), spine.localRotation, new[]
                {
                    (0f, Vector3.zero), (1f, new Vector3(-2f, 0f, 0f)), (2f, Vector3.zero)
                });
            }

            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            CreateOrReplaceAsset(clip, GeneratedFolder + "/Ted_Idle.anim");
            return clip;
        }

        private static AnimationClip BuildArmActionClip(GameObject temp, string assetName, float length, float windup, float release)
        {
            var clip = new AnimationClip { name = assetName };
            var root = temp.transform;

            var upperArm = FindBone(temp, "RightArm", "RightShoulder");
            var foreArm = FindBone(temp, "RightForeArm");
            var spine = FindBone(temp, "Spine1", "Spine");

            if (upperArm != null)
                SetLocalRotationKeys(clip, AnimationUtility.CalculateTransformPath(upperArm, root), upperArm.localRotation, new[]
                {
                    (0f, Vector3.zero), (windup, new Vector3(0f, 0f, -55f)), (release, new Vector3(0f, 0f, 65f)), (length, Vector3.zero)
                });
            if (foreArm != null)
                SetLocalRotationKeys(clip, AnimationUtility.CalculateTransformPath(foreArm, root), foreArm.localRotation, new[]
                {
                    (0f, Vector3.zero), (windup, new Vector3(0f, 0f, -40f)), (release, new Vector3(0f, 0f, 15f)), (length, Vector3.zero)
                });
            if (spine != null)
                SetLocalRotationKeys(clip, AnimationUtility.CalculateTransformPath(spine, root), spine.localRotation, new[]
                {
                    (0f, Vector3.zero), (windup, new Vector3(-8f, 0f, 0f)), (release, new Vector3(12f, 0f, 0f)), (length, Vector3.zero)
                });

            clip.EnsureQuaternionContinuity();
            CreateOrReplaceAsset(clip, GeneratedFolder + "/" + assetName + ".anim");
            return clip;
        }

        private static void CreateOrReplaceAsset(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        // ---------------------------------------------------------------------------------------
        //  Colour-variant materials
        // ---------------------------------------------------------------------------------------

        internal static Material[] BuildBearMaterials()
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseTedMatPath);
            Shader shader = baseMat != null ? baseMat.shader : Shader.Find("Universal Render Pipeline/Lit");
            EnsureFolder(VariantFolder);

            var mats = new Material[BearPalette.Length];
            for (int i = 0; i < BearPalette.Length; i++)
            {
                var (name, color) = BearPalette[i];
                string path = $"{VariantFolder}/Ted_{name}.mat";

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = baseMat != null ? new Material(baseMat) : new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                if (shader != null) mat.shader = shader;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                EditorUtility.SetDirty(mat);
                mats[i] = mat;
            }
            AssetDatabase.SaveAssets();
            return mats;
        }

        // ---------------------------------------------------------------------------------------
        //  Pillow model resolution (data-driven)
        // ---------------------------------------------------------------------------------------

        /// <summary>Preferred throwable pillow: pil3_low (prefab or FBX), else pillow_low, else null (primitive).</summary>
        internal static GameObject ResolveThrowablePillowModel(out string report)
        {
            foreach (var name in new[] { "pil3_low", "pil3", "pillow_low" })
            {
                var go = FindPillowAsset(name);
                if (go != null) { report = $"{name} ({AssetDatabase.GetAssetPath(go)})"; return go; }
            }
            report = "primitive cube (no pil3_low / pillow_low asset found yet)";
            return null;
        }

        /// <summary>Fort cladding pillows: pil1_low..pil4_low (the user's prefabs) first, then pil5/6, then pillow_low.</summary>
        internal static List<GameObject> ResolveFortPillowModels()
        {
            var list = new List<GameObject>();
            foreach (var name in new[] { "pil1_low", "pil2_low", "pil3_low", "pil4_low", "pil5_low", "pil6_low", "pillow_low" })
            {
                var go = FindPillowAsset(name);
                if (go != null && !list.Contains(go)) list.Add(go);
            }
            return list;
        }

        /// <summary>A pillow model by name: a hand-made prefab (Assets/Prefabs first, then anywhere), else the FBX under Assets/Graphics.</summary>
        private static GameObject FindPillowAsset(string name)
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
            if (direct != null) return direct;

            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
            return FindModelByName(name);
        }

        private static GameObject FindModelByName(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Graphics", StringComparison.OrdinalIgnoreCase)) continue;
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) return go;
            }
            return null;
        }

        // ---------------------------------------------------------------------------------------
        //  Scene content
        // ---------------------------------------------------------------------------------------

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        internal static void RegisterSceneInBuildSettings(string scenePath, bool prepend)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (prepend) scenes.Insert(0, entry);
            else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Material SolidUrpMaterial(string name, Color color, float smoothness = 0.15f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EnsureFolder(GeneratedFolder);
            CreateOrReplaceAsset(mat, $"{GeneratedFolder}/Arena_{name}.mat");
            return mat;
        }

        private const float PillowBlockSize = 4f;   // metres, per fort-cladding pillow tile

        /// <summary>A fully enclosed pillow fort: every surface — floor, four walls, ceiling — is tiled
        /// with the user's pil1_low..pil4_low pillow prefabs. Structural primitives keep the collision;
        /// the pillow tiles are pure decoration. The centre is left clear (loose throwable pillows are
        /// placed separately at the pillow spawn points).</summary>
        internal static void BuildPillowFortArena()
        {
            float size = ArenaSize;
            float wallHeight = ArenaWallHeight;
            const float wallThickness = 0.6f;
            float half = size / 2f;

            var arena = new GameObject("PillowFort");

            var floorMat = SolidUrpMaterial("Floor", new Color(0.9f, 0.86f, 0.8f));
            var wallMat = SolidUrpMaterial("Sheet", new Color(0.93f, 0.9f, 0.86f));
            var ceilMat = SolidUrpMaterial("Blanket", new Color(0.86f, 0.72f, 0.74f));

            // --- structural shell (collision + a soft backing colour behind the pillow tiles) ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Collision";
            floor.transform.SetParent(arena.transform);
            floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            void MakeBox(string name, Vector3 pos, Vector3 scale, Material mat)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = name;
                box.transform.SetParent(arena.transform);
                box.transform.position = pos;
                box.transform.localScale = scale;
                if (mat != null) box.GetComponent<Renderer>().sharedMaterial = mat;
            }

            MakeBox("Wall_North", new Vector3(0f, wallHeight / 2f, half), new Vector3(size, wallHeight, wallThickness), wallMat);
            MakeBox("Wall_South", new Vector3(0f, wallHeight / 2f, -half), new Vector3(size, wallHeight, wallThickness), wallMat);
            MakeBox("Wall_East", new Vector3(half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size), wallMat);
            MakeBox("Wall_West", new Vector3(-half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size), wallMat);
            MakeBox("Ceiling", new Vector3(0f, wallHeight, 0f), new Vector3(size, wallThickness, size), ceilMat);

            // --- pillow cladding (visual only) ---
            var pillowMat = AssetDatabase.LoadAssetAtPath<Material>(PillowMatPath);
            var models = ResolveFortPillowModels();
            var cladding = new GameObject("PillowCladding");
            cladding.transform.SetParent(arena.transform);
            int seed = 20260;

            void CladPillow(Transform parent, Vector3 pos, Quaternion rot, float jitterDeg)
            {
                GameObject tile;
                if (models.Count > 0)
                {
                    var src = models[Mathf.Abs(seed++) % models.Count];
                    tile = (GameObject)PrefabUtility.InstantiatePrefab(src); // stays linked to the prefab
                    FitToSize(tile, PillowBlockSize);
                }
                else
                {
                    tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.transform.localScale = new Vector3(PillowBlockSize, PillowBlockSize * 0.32f, PillowBlockSize);
                    if (pillowMat != null) tile.GetComponent<Renderer>().sharedMaterial = pillowMat;
                }
                tile.name = "PillowTile";
                tile.transform.SetParent(parent, true);
                tile.transform.position = pos;
                float j = jitterDeg;
                tile.transform.rotation = rot * Quaternion.Euler(
                    ((seed * 13) % 100 / 100f - 0.5f) * j,
                    ((seed * 29) % 100 / 100f - 0.5f) * 360f,
                    ((seed * 47) % 100 / 100f - 0.5f) * j);

                foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // ~230 tiles — skip shadow passes
            }

            float step = PillowBlockSize * 0.9f;               // slight overlap, no gaps
            int perRow = Mathf.CeilToInt(size / step) + 1;
            float start = -(perRow - 1) / 2f * step;           // centred on the origin

            // floor
            var floorRoot = new GameObject("Floor").transform;
            floorRoot.SetParent(cladding.transform);
            for (int x = 0; x < perRow; x++)
                for (int z = 0; z < perRow; z++)
                    CladPillow(floorRoot, new Vector3(start + x * step, -0.2f, start + z * step), Quaternion.identity, 8f);

            // ceiling (flipped so the seam side faces down)
            var ceilRoot = new GameObject("Ceiling").transform;
            ceilRoot.SetParent(cladding.transform);
            for (int x = 0; x < perRow; x++)
                for (int z = 0; z < perRow; z++)
                    CladPillow(ceilRoot, new Vector3(start + x * step, wallHeight - 0.15f, start + z * step), Quaternion.Euler(180f, 0f, 0f), 8f);

            // walls
            int rows = Mathf.Max(2, Mathf.CeilToInt((wallHeight - 0.4f) / step));
            void CladWall(string name, Vector3 normal)
            {
                var root = new GameObject(name).transform;
                root.SetParent(cladding.transform);
                Vector3 along = Vector3.Cross(Vector3.up, normal).normalized;      // horizontal run of the wall
                Quaternion face = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                for (int i = 0; i < perRow; i++)
                    for (int r = 0; r < rows; r++)
                        CladPillow(root,
                            normal * (half - 0.35f) + along * (start + i * step) + Vector3.up * (0.6f + r * step),
                            face, 6f);
            }
            CladWall("Wall_N", Vector3.forward);
            CladWall("Wall_S", Vector3.back);
            CladWall("Wall_E", Vector3.right);
            CladWall("Wall_W", Vector3.left);

            var sun = new GameObject("Directional Light");
            sun.transform.SetParent(arena.transform);
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.96f, 0.9f);
            sunLight.intensity = 1.1f;
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var lamp = new GameObject("Fort Lamp");
            lamp.transform.SetParent(arena.transform);
            lamp.transform.position = new Vector3(0f, wallHeight - 0.8f, 0f);
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.range = size;
            lampLight.color = new Color(1f, 0.85f, 0.65f);
            lampLight.intensity = 2.2f;
        }

        /// <summary>Soft cover for fights: low pillow-cushion piles in the four corners only — the
        /// centre of the room is kept completely clear.</summary>
        private static void BuildEnvironmentProps()
        {
            var root = new GameObject("CushionPiles");
            var models = ResolveFortPillowModels();
            var pillowMat = AssetDatabase.LoadAssetAtPath<Material>(PillowMatPath);
            int seed = 7777;
            float half = ArenaSize / 2f;
            float inset = 4.5f;

            void Cushion(Vector3 pos, float yaw, float sizeM)
            {
                GameObject go;
                if (models.Count > 0)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(models[Mathf.Abs(seed++) % models.Count]);
                    FitToSize(go, sizeM);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(sizeM, sizeM * 0.35f, sizeM);
                    if (pillowMat != null) go.GetComponent<Renderer>().sharedMaterial = pillowMat;
                }
                go.name = "Cushion";
                go.transform.SetParent(root.transform, true);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(
                    UnityEngine.Random.Range(-6f, 6f), yaw, UnityEngine.Random.Range(-6f, 6f)));

                var existing = go.GetComponentInChildren<BoxCollider>();
                var bc = existing != null ? existing : go.AddComponent<BoxCollider>();
                bc.size = new Vector3(sizeM, sizeM * 0.5f, sizeM);
                bc.center = Vector3.up * (sizeM * 0.25f);
            }

            var corners = new[]
            {
                new Vector3(half - inset, 0f, half - inset),
                new Vector3(-(half - inset), 0f, half - inset),
                new Vector3(half - inset, 0f, -(half - inset)),
                new Vector3(-(half - inset), 0f, -(half - inset)),
            };
            foreach (var c in corners)
            {
                int stack = 2 + (Mathf.Abs(seed++) % 2);
                for (int s = 0; s < stack; s++)
                    Cushion(c + Vector3.up * (0.25f + s * 0.9f) + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f, UnityEngine.Random.Range(-0.6f, 0.6f)),
                            UnityEngine.Random.Range(0f, 360f), 2.6f);
            }
        }

        internal static GameObject BuildPickupPrefab()
        {
            EnsureFolder(GeneratedFolder);
            var go = new GameObject("Pickup");
            go.AddComponent<NetworkIdentity>();

            var nt = go.AddComponent<NetworkTransformUnreliable>();
            nt.target = go.transform;
            nt.syncDirection = SyncDirection.ServerToClient;
            nt.syncScale = false;

            go.AddComponent<Pickup>(); // builds its own orb + light + trigger in Awake

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Pickup.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static void FitToSize(GameObject go, float targetSize)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim > 0.0001f)
            {
                float s = targetSize / maxDim;
                go.transform.localScale *= s;
            }
        }

        private static (Transform[] playerSpawns, Transform[] pillowSpawns) BuildSpawnPoints()
        {
            var spawnRoot = new GameObject("SpawnPoints");

            const int playerCount = 6;
            var playerSpawns = new Transform[playerCount];
            float radius = ArenaSize / 2f - 5f;
            for (int i = 0; i < playerCount; i++)
            {
                float angle = i * Mathf.PI * 2f / playerCount;
                var go = new GameObject($"PlayerSpawn_{i}");
                go.transform.SetParent(spawnRoot.transform);
                go.transform.position = new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius);
                go.transform.LookAt(new Vector3(0f, 1f, 0f));
                playerSpawns[i] = go.transform;
            }

            // Loose throwable pillows (pil3_low) scattered across the floor — NOT part of the fort cladding.
            var pillowPositions = new[]
            {
                new Vector3(0f, 0.5f, 0f),
                new Vector3(4f, 0.5f, 4f), new Vector3(-4f, 0.5f, 4f),
                new Vector3(4f, 0.5f, -4f), new Vector3(-4f, 0.5f, -4f),
                new Vector3(9f, 0.5f, 0f), new Vector3(-9f, 0.5f, 0f),
                new Vector3(0f, 0.5f, 9f), new Vector3(0f, 0.5f, -9f),
                new Vector3(3f, 0.5f, -8f), new Vector3(-3f, 0.5f, 8f),
                new Vector3(7f, 0.5f, 5f), new Vector3(-7f, 0.5f, -5f),
                new Vector3(-6f, 0.5f, 6f), new Vector3(6f, 0.5f, -6f),
                new Vector3(2f, 0.5f, 6f), new Vector3(-2f, 0.5f, -6f),
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

        internal static GameObject BuildPillowPrefab(out string report)
        {
            var model = ResolveThrowablePillowModel(out report);

            // Root holds the physics + gameplay scripts; its origin must sit at the pillow's visual
            // centre because PlayerCombat uses transform.position for the E-pickup range check, the
            // hold-socket parent, and the throw origin. The pil3_low mesh pivot is offset by ~metres,
            // so the visual is nested under a "Model" child that compensates.
            var go = new GameObject("Pillow");

            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Model";
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.transform.SetParent(go.transform, false);
                foreach (var childCol in visual.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(childCol);
                FitToSize(visual, 0.6f);

                var rends = visual.GetComponentsInChildren<Renderer>(true);
                Vector3 size = new Vector3(0.6f, 0.28f, 0.42f);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    visual.transform.position -= (b.center - go.transform.position); // recentre the visual on the root
                    size = b.size;
                }

                var bc = go.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = size;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Model";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = new Vector3(0.6f, 0.3f, 0.42f);
                var pillowMat = AssetDatabase.LoadAssetAtPath<Material>(PillowMatPath);
                if (pillowMat != null) visual.GetComponent<Renderer>().sharedMaterial = pillowMat;

                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.6f, 0.3f, 0.42f);
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.4f;           // settle instead of skating / spinning forever
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // don't tunnel when thrown
            go.AddComponent<Pillow>();
            go.AddComponent<PillowProjectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Pillow.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static (GameObject human, GameObject ai) BuildBearPrefabs(BearArt art)
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            var humanGO = BuildBearRoot("Bear_Human", art);
            var inputHandler = humanGO.AddComponent<PlayerInputHandler>();
            inputHandler.inputActions = inputActions;
            var humanPrefab = PrefabUtility.SaveAsPrefabAsset(humanGO, $"{PrefabFolder}/Bear_Human.prefab");
            UnityEngine.Object.DestroyImmediate(humanGO);

            var aiGO = BuildBearRoot("Bear_AI", art);
            aiGO.AddComponent<TeddyAI>();
            var aiPrefab = PrefabUtility.SaveAsPrefabAsset(aiGO, $"{PrefabFolder}/Bear_AI.prefab");
            UnityEngine.Object.DestroyImmediate(aiGO);

            return (humanPrefab, aiPrefab);
        }

        /// <summary>Builds an un-networked bear root (CharacterController + gameplay scripts + visual).
        /// NetworkBuilder reuses this and layers Mirror components on top.</summary>
        internal static GameObject BuildBearRoot(string name, BearArt art)
        {
            var root = new GameObject(name);

            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var physicsCollider = root.AddComponent<CapsuleCollider>();
            physicsCollider.center = new Vector3(0f, 1f, 0f);
            physicsCollider.height = 2f;
            physicsCollider.radius = 0.4f;
            physicsCollider.enabled = false;

            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerHealth>();

            GameObject visual;
            Renderer primaryRenderer;
            Animator animator = null;

            if (!art.UseCapsule)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(art.modelPrefab);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                FitVisualToHeight(visual);

                animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = art.controller;
                if (art.avatar != null) animator.avatar = art.avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                primaryRenderer = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1f, 0f);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                primaryRenderer = visual.GetComponent<Renderer>();
            }

            var animatorComp = root.AddComponent<PlayerAnimator>();
            animatorComp.visualRenderer = primaryRenderer;
            animatorComp.animator = animator;

            root.AddComponent<BearAppearance>();
            root.AddComponent<DownedPlayerHandle>();
            root.AddComponent<BearBuffs>();

            var holdSocket = new GameObject("HoldSocket");
            holdSocket.transform.SetParent(root.transform, false);
            holdSocket.transform.localPosition = new Vector3(0.35f, 1.15f, 0.55f);

            var hitboxGO = new GameObject("MeleeHitbox");
            hitboxGO.transform.SetParent(root.transform, false);
            hitboxGO.transform.localPosition = new Vector3(0f, 1f, 1f);
            var hitboxCollider = hitboxGO.AddComponent<SphereCollider>();
            hitboxCollider.radius = 1f;
            hitboxCollider.isTrigger = true;
            var hitbox = hitboxGO.AddComponent<Hitbox>();

            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(root.transform, false);
            camTarget.transform.localPosition = new Vector3(0f, 1.1f, 0f); // bear centre, not head — camera frames the whole bear

            var combat = root.AddComponent<PlayerCombat>();
            combat.holdSocket = holdSocket.transform;
            combat.meleeHitbox = hitbox;

            return root;
        }

        private static void FitVisualToHeight(GameObject visual)
        {
            var smr = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null)
            {
                visual.transform.localPosition = Vector3.zero;
                return;
            }

            Bounds b = smr.sharedMesh.bounds;
            float srcHeight = b.size.y;
            float scale = srcHeight > 0.0001f ? BearTargetHeight / srcHeight : 1f;
            visual.transform.localScale = new Vector3(scale, scale, scale);
            visual.transform.localPosition = new Vector3(0f, -b.min.y * scale, 0f);
        }

        private static (Camera camera, CinemachineCamera bearCamera) BuildCamera()
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<CinemachineBrain>();
            camGO.transform.position = new Vector3(0f, 3f, -6f);

            var vcamGO = new GameObject("CM Bear Camera");
            var vcam = vcamGO.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = 55f;   // wider than the old 40 — shows more room, bear reads smaller / less "stuck to the screen"
            vcam.Lens = lens;

            var orbital = vcamGO.AddComponent<CinemachineOrbitalFollow>();
            orbital.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            orbital.TrackerSettings.PositionDamping = new Vector3(0.5f, 0.5f, 0.5f); // smoother, less snappy follow
            orbital.Radius = 6.5f;    // was 5.2 — pulled back, but kept low enough to fit the closed fort so it doesn't fight the deoccluder
            orbital.VerticalAxis.Value = 13f; // low, near-level angle — clears the 4.5m ceiling and shows the room ahead, not the bear's head
            orbital.VerticalAxis.Center = 13f;

            var composer = vcamGO.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.4f, 0.4f);         // ease the aim onto the bear centre
            var composition = composer.Composition;
            composition.ScreenPosition = new Vector2(0f, -0.12f); // bear a touch below centre → more of what's in front is visible
            composer.Composition = composition;

            // The arena is a fully enclosed pillow fort, so the orbit ray hits a wall/ceiling constantly.
            // Left at defaults the deoccluder yanks the camera onto the bear's back — tune it to keep a
            // usable third-person shot even when occluded (never closer than 3m) and ease in/out.
            var deoccluder = vcamGO.AddComponent<CinemachineDeoccluder>();
            deoccluder.MinimumDistanceFromTarget = 3f;
            var avoid = deoccluder.AvoidObstacles;
            avoid.DistanceLimit = 6f;
            avoid.MinimumOcclusionTime = 0.3f;
            avoid.CameraRadius = 0.12f;
            avoid.SmoothingTime = 0.6f;
            avoid.Damping = 0.6f;
            avoid.DampingWhenOccluded = 0.5f;
            deoccluder.AvoidObstacles = avoid;

            vcamGO.AddComponent<CinemachineImpulseListener>(); // receives ScreenShake bumps

            var lookInput = vcamGO.AddComponent<CinemachineLookInput>();
            lookInput.orbital = orbital;

            // Global juice singleton (also self-creates at runtime via JuiceBootstrap, but a scene
            // object means the impulse source exists on frame 0).
            var shakeGO = new GameObject("ScreenShake");
            shakeGO.AddComponent<CinemachineImpulseSource>();
            shakeGO.AddComponent<ScreenShake>();

            return (cam, vcam);
        }

        private static GameHUD BuildHUD()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (actions != null) module.actionsAsset = actions;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Graphics/font/Chunky Sprout.otf")
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("HUD Canvas");
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor align, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = fontSize;
                text.alignment = align;
                text.text = content;
                text.color = color;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
                return text;
            }

            Image MakePanel(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvasGO.transform, false);
                var img = go.AddComponent<Image>();
                img.color = color;
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                var rect = img.rectTransform;
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
                return img;
            }

            Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color bg)
            {
                var go = new GameObject(label + "Button", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();
                img.color = bg;
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                var rect = img.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                var t = MakeText(label + "Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, label, 34, TextAnchor.MiddleCenter, new Color(0.15f, 0.12f, 0.1f));
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.sizeDelta = Vector2.zero;
                return btn;
            }

            var hearts = MakeText("HeartsText", canvasGO.transform, new Vector2(0f, 1f), new Vector2(150f, -50f), new Vector2(520f, 70f), "❤ ❤ ❤", 44, TextAnchor.MiddleLeft, new Color(1f, 0.35f, 0.4f));
            var bearsLeft = MakeText("BearsLeftText", canvasGO.transform, new Vector2(1f, 1f), new Vector2(-180f, -50f), new Vector2(460f, 60f), "BEARS LEFT: 6", 30, TextAnchor.MiddleRight, Color.white);
            var countdown = MakeText("CountdownText", canvasGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(800f, 260f), "3", 180, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
            countdown.fontStyle = FontStyle.Bold;
            countdown.gameObject.SetActive(false);

            // control hints strip
            var hintsPanel = MakePanel("ControlHints", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 70f), new Color(0f, 0f, 0f, 0.5f));
            MakeText("HintText", hintsPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1460f, 60f),
                "WASD move   ·   SHIFT run   ·   SPACE jump   ·   MOUSE look   ·   LMB throw   ·   hold RMB to aim   ·   E grab",
                26, TextAnchor.MiddleCenter, Color.white);

            // end card
            var endCard = MakePanel("EndCard", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 520f), new Color(0.14f, 0.11f, 0.16f, 0.96f));
            var winner = MakeText("WinnerText", endCard.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(820f, 160f), "VICTORY!", 96, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
            winner.fontStyle = FontStyle.Bold;
            var rematch = MakeButton(endCard.transform, "REMATCH", new Vector2(0f, -110f), new Vector2(320f, 96f), new Color(0.46f, 0.78f, 0.6f));
            var menu = MakeButton(endCard.transform, "MENU", new Vector2(0f, -230f), new Vector2(320f, 96f), new Color(0.82f, 0.55f, 0.4f));
            endCard.gameObject.SetActive(false);

            var hud = canvasGO.AddComponent<GameHUD>();
            hud.heartsText = hearts;
            hud.bearsLeftText = bearsLeft;
            hud.countdownText = countdown;
            hud.controlHints = hintsPanel.gameObject;
            hud.endCard = endCard.gameObject;
            hud.winnerText = winner;
            hud.rematchButton = rematch;
            hud.menuButton = menu;
            return hud;
        }
    }
}
