using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BearlyStanding.EditorTools
{
    /// <summary>
    /// Builds a cute main-menu scene: title, Host / Join / Practice / Quit, a room-code panel with a
    /// copy button, and a slowly turning Ted for flavour. Also drops in the BearlyNetworkManager
    /// (+ KcpTransport) that the menu and the arena share. NetworkBuilder fills in its prefab refs.
    /// </summary>
    public static class MainMenuBuilder
    {
        internal const string MenuScenePath = Day1SceneBuilder.SceneFolder + "/MainMenu.unity";
        private const string ChunkyFontPath = "Assets/Graphics/font/Chunky Sprout.otf";

        private static readonly Color Cream = new Color(0.98f, 0.94f, 0.88f);
        private static readonly Color Berry = new Color(0.82f, 0.34f, 0.40f);
        private static readonly Color Ink = new Color(0.24f, 0.18f, 0.16f);

        [MenuItem("Bearly Standing/Build Main Menu", priority = 20)]
        public static void Build()
        {
            Day1SceneBuilder.EnsureFolder(Day1SceneBuilder.SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            BuildNetworkManager();
            BuildCanvas();
            BuildSpinnerTed();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Day1SceneBuilder.RegisterSceneInBuildSettings(MenuScenePath, prepend: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Bearly Standing: main menu built at {MenuScenePath} (startup scene). " +
                      "Run 'Build Networking' next so the manager knows its prefabs.");
        }

        private static void BuildEnvironment()
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.12f, 0.16f);
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0f, 1.4f, -3f);
            camGO.transform.rotation = Quaternion.Euler(6f, 0f, 0f);

            var key = new GameObject("Key Light");
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.92f, 0.82f);
            keyLight.intensity = 1.3f;
            key.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            var rim = new GameObject("Rim Light");
            var rimLight = rim.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.color = Berry;
            rimLight.range = 12f;
            rimLight.intensity = 3f;
            rim.transform.position = new Vector3(-2f, 2.5f, -1.5f);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(0f, 0f, 0f);
            pedestal.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
            pedestal.GetComponent<Renderer>().sharedMaterial = SolidMaterial("Menu_Pedestal", Berry);
        }

        private static GameObject BuildNetworkManager()
        {
            var go = new GameObject("BearlyNetworkManager");
            var kcp = go.AddComponent<KcpTransport>();
            var nm = go.AddComponent<BearlyNetworkManager>();
            nm.transport = kcp;
            nm.offlineScene = MenuScenePath;
            nm.onlineScene = Day1SceneBuilder.ScenePath;
            nm.autoCreatePlayer = true;
            nm.targetBearCount = 6;
            nm.botFillDelay = 4f;
            nm.bearMaterials = LoadVariantMaterials();
            return go;
        }

        private static void BuildCanvas()
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            var uiModule = esGO.AddComponent<InputSystemUIInputModule>();
            var uiActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Day1SceneBuilder.InputActionsPath);
            if (uiActions != null) uiModule.actionsAsset = uiActions; // auto-binds Point/Click/Navigate/Submit by name

            var canvasGO = new GameObject("Menu Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var font = AssetDatabase.LoadAssetAtPath<Font>(ChunkyFontPath)
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // vignette backdrop
            var bg = MakeImage("Backdrop", canvasGO.transform, new Color(0.16f, 0.13f, 0.17f, 0.35f));
            Stretch(bg.rectTransform);

            var title = MakeText("Title", canvasGO.transform, "BEARLY\nSTANDING", font, 120, Cream, TextAnchor.UpperCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1100f, 320f));
            title.fontStyle = FontStyle.Bold;
            title.lineSpacing = 0.85f;

            var subtitle = MakeText("Subtitle", canvasGO.transform, "six teddies · one room · infinite pillow violence", font, 34, Berry, TextAnchor.UpperCenter);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(1400f, 60f));

            // --- main button column ---
            var column = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(canvasGO.transform, false);
            Anchor(column, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(560f, 520f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Button hostBtn = MakeButton(column, "HostButton", "HOST A GAME", font, Berry, Cream);
            Button joinBtn = MakeButton(column, "JoinButton", "JOIN A FRIEND", font, new Color(0.34f, 0.47f, 0.84f), Cream);
            Button practiceBtn = MakeButton(column, "PracticeButton", "PRACTICE (vs BOTS)", font, new Color(0.46f, 0.78f, 0.60f), Ink);
            Button quitBtn = MakeButton(column, "QuitButton", "QUIT", font, new Color(0.30f, 0.24f, 0.24f), Cream);

            var status = MakeText("Status", canvasGO.transform, "", font, 30, Cream, TextAnchor.MiddleCenter);
            Anchor(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 50f));

            // --- join panel ---
            var joinPanel = MakePanel("JoinPanel", canvasGO.transform, out RectTransform joinBody);
            var joinTitle = MakeText("JoinTitle", joinBody, "ENTER ROOM CODE", font, 44, Cream, TextAnchor.UpperCenter);
            Anchor(joinTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 60f));
            var codeInput = MakeInputField(joinBody, "CodeInput", font);
            Anchor(codeInput.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(520f, 90f));
            Button joinConfirm = MakeButton(joinBody, "JoinConfirm", "JOIN", font, new Color(0.34f, 0.47f, 0.84f), Cream);
            Anchor(joinConfirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-140f, 40f), new Vector2(220f, 80f));
            Button joinBack = MakeButton(joinBody, "JoinBack", "BACK", font, new Color(0.30f, 0.24f, 0.24f), Cream);
            Anchor(joinBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(140f, 40f), new Vector2(220f, 80f));

            // --- room-code panel ---
            var codePanel = MakePanel("CodePanel", canvasGO.transform, out RectTransform codeBody);
            var codeLabel = MakeText("CodeLabel", codeBody, "YOUR ROOM CODE", font, 40, Cream, TextAnchor.UpperCenter);
            Anchor(codeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 60f));
            var codeValue = MakeText("CodeValue", codeBody, "--------", font, 84, Berry, TextAnchor.MiddleCenter);
            Anchor(codeValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(600f, 120f));
            Button copyBtn = MakeButton(codeBody, "CopyButton", "COPY", font, new Color(0.46f, 0.78f, 0.60f), Ink);
            Anchor(copyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(240f, 80f));

            var ui = canvasGO.AddComponent<MainMenuUI>();
            ui.hostButton = hostBtn;
            ui.openJoinButton = joinBtn;
            ui.joinConfirmButton = joinConfirm;
            ui.joinBackButton = joinBack;
            ui.practiceButton = practiceBtn;
            ui.quitButton = quitBtn;
            ui.copyCodeButton = copyBtn;
            ui.joinPanel = joinPanel;
            ui.codePanel = codePanel;
            ui.codeInput = codeInput;
            ui.statusText = status;
            ui.roomCodeText = codeValue;
            ui.practiceScene = "Day1_TestArena";
        }

        private static void BuildSpinnerTed()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Day1SceneBuilder.TedFbxPath);
            GameObject ted;
            if (model != null)
            {
                ted = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(ted, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var smr = ted.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && smr.sharedMesh != null)
                {
                    float h = smr.sharedMesh.bounds.size.y;
                    float s = h > 0.001f ? 1.7f / h : 1f;
                    ted.transform.localScale = new Vector3(s, s, s);
                }
            }
            else
            {
                ted = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }
            ted.name = "Menu Ted";
            ted.transform.position = new Vector3(0f, 0.15f, 0f);
            ted.transform.rotation = Quaternion.Euler(0f, 155f, 0f);

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Day1SceneBuilder.ControllerPath);
            var animator = ted.GetComponentInChildren<Animator>();
            if (animator != null && controller != null) animator.runtimeAnimatorController = controller;

            ted.AddComponent<MenuSpin>();
        }

        // ---------------------------------------------------------------- UI helpers

        private static Text MakeText(string name, Transform parent, string content, Font font, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            return img;
        }

        private static Button MakeButton(Transform parent, string name, string label, Font font, Color bg, Color fg)
        {
            var img = MakeImage(name, parent, bg);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 92f;
            le.preferredWidth = 520f;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(bg, Color.black, 0.15f);
            button.colors = colors;

            var text = MakeText(name + "Label", img.transform, label, font, 40, fg, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static InputField MakeInputField(Transform parent, string name, Font font)
        {
            var img = MakeImage(name, parent, Cream);
            var field = img.gameObject.AddComponent<InputField>();

            var text = MakeText(name + "Text", img.transform, "", font, 44, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 16f);
            var placeholder = MakeText(name + "Placeholder", img.transform, "code / ip:port", font, 40, new Color(0.4f, 0.35f, 0.33f, 0.7f), TextAnchor.MiddleCenter);
            Stretch(placeholder.rectTransform, 16f);
            placeholder.fontStyle = FontStyle.Italic;

            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 32;
            field.textComponent.supportRichText = false;
            return field;
        }

        private static GameObject MakePanel(string name, Transform parent, out RectTransform body)
        {
            var dim = MakeImage(name, parent, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            var card = MakeImage(name + "Card", dim.transform, new Color(0.20f, 0.16f, 0.20f, 0.98f));
            Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 480f));
            body = card.rectTransform;
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private static Material SolidMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            Day1SceneBuilder.EnsureFolder(Day1SceneBuilder.GeneratedFolder);
            string path = $"{Day1SceneBuilder.GeneratedFolder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material[] LoadVariantMaterials()
        {
            var list = new System.Collections.Generic.List<Material>();
            foreach (var (name, _) in Day1SceneBuilder.BearPalette)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{Day1SceneBuilder.VariantFolder}/Ted_{name}.mat");
                if (m != null) list.Add(m);
            }
            return list.ToArray();
        }

        private static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
