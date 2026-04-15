#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using KotORUnity.Core;
using KotORUnity.Bootstrap;

namespace KotORUnity.Editor
{
    /// <summary>
    /// PlaytestSceneBuilder  —  one-click tool to create a fully playable test
    /// scene from scratch. Run from:
    ///   Unity menu → KotOR-Unity → Build Playtest Scene
    ///
    /// What it creates:
    ///   1. A new scene "KotOR_Playtest" and saves it
    ///   2. Core manager GameObjects (GameBootstrap, GameManager, EventBus …)
    ///   3. A simple test environment (ground plane, walls, lighting)
    ///   4. Player prefab with all required components
    ///   5. Two companion NPC stubs
    ///   6. One enemy NPC stub
    ///   7. Two interactable objects (door + container)
    ///   8. One NPC with a dialogue stub
    ///   9. HUD Canvas wiring
    ///  10. Camera rig (Action + RTS cameras)
    ///
    /// After running this tool, hit Play in Unity and the scene will boot
    /// into the main game loop exactly as KotOR does.
    /// </summary>
    public static class PlaytestSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/KotOR_Playtest.unity";

        [MenuItem("KotOR-Unity/🎮 Build Playtest Scene", false, 1)]
        public static void BuildPlaytestScene()
        {
            if (!EditorUtility.DisplayDialog(
                "Build Playtest Scene",
                $"This will create / overwrite the scene at:\n{SCENE_PATH}\n\nProceed?",
                "Build", "Cancel"))
                return;

            // ── 1. Create new scene ───────────────────────────────────────────
            EnsureSceneDirectory();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 2. Lighting ───────────────────────────────────────────────────
            SetupLighting();

            // ── 3. Ground ─────────────────────────────────────────────────────
            var ground = CreatePrimitive("Ground", PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial("GroundMat", new Color(0.22f, 0.2f, 0.18f));
            SetLayer(ground, "Default");

            // ── 4. Walkmesh layer helper ──────────────────────────────────────
            EnsureLayer("Walkmesh");
            EnsureLayer("Interactable");

            // ── 5. Core Managers ──────────────────────────────────────────────
            var bootstrap = CreateManager("GameBootstrap");
            AddComponentSafe<KotORUnity.Bootstrap.SceneBootstrapper>(bootstrap);

            var gameManagerGO = CreateManager("GameManager");
            AddComponentSafe<KotORUnity.Core.GameManager>(gameManagerGO);

            var inventoryGO = CreateManager("InventoryManager");
            AddComponentSafe<KotORUnity.Inventory.InventoryManager>(inventoryGO);

            var partyGO = CreateManager("PartyManager");
            AddComponentSafe<KotORUnity.Party.PartyManager>(partyGO);

            var audioGO = CreateManager("AudioManager");
            AddComponentSafe<KotORUnity.Audio.AudioManager>(audioGO);

            var vfxGO = CreateManager("VFXManager");
            AddComponentSafe<KotORUnity.VFX.VFXManager>(vfxGO);

            var achieveGO = CreateManager("AchievementSystem");
            AddComponentSafe<KotORUnity.Core.AchievementSystem>(achieveGO);

            var combatGO = CreateManager("CombatManager");
            AddComponentSafe<KotORUnity.Combat.CombatManager>(combatGO);

            var encounterGO = CreateManager("EncounterManager");
            AddComponentSafe<KotORUnity.Encounter.EncounterManager>(encounterGO);

            var barkGO = CreateManager("BarkSystem");
            AddComponentSafe<KotORUnity.Dialogue.BarkSystem>(barkGO);

            // ── 6. Player ─────────────────────────────────────────────────────
            var player = CreatePlayer();

            // Set player ref in PartyManager
            var pm = partyGO.GetComponent<KotORUnity.Party.PartyManager>();
            if (pm != null) pm.SetPlayerTransform(player.transform);

            // ── 7. Camera Rig ─────────────────────────────────────────────────
            CreateCameraRig(player.transform);

            // ── 8. Companions ─────────────────────────────────────────────────
            CreateCompanion("Carth Onasi",  new Vector3(-2f, 0f,  1.5f), "c_carth");
            CreateCompanion("Bastila Shan", new Vector3( 2f, 0f,  1.5f), "c_bastila");

            // ── 9. Enemy NPC ──────────────────────────────────────────────────
            CreateEnemy("Sith Soldier", new Vector3(0f, 0f, 12f), "n_sithsoldier");

            // ── 10. Interactables ─────────────────────────────────────────────
            CreateDoor(new Vector3(0f, 0f, 5f));
            CreateContainer(new Vector3(3f, 0f, 3f));
            CreateQuestNPC("Mysterious Merchant", new Vector3(-4f, 0f, 4f), "n_merchant01");

            // ── 11. HUD Canvas ────────────────────────────────────────────────
            CreateHUDCanvas(player.transform);

            // ── 12. Dialogue Canvas ───────────────────────────────────────────
            CreateDialogueCanvas();

            // ── 13. Interaction Prompt ────────────────────────────────────────
            CreateInteractionPrompt();

            // ── 14. Save scene ────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            EditorUtility.DisplayDialog("Done!",
                $"Playtest scene saved to:\n{SCENE_PATH}\n\nAdd it to Build Settings and press Play to test.",
                "OK");

            Debug.Log($"[PlaytestSceneBuilder] Scene built: {SCENE_PATH}");
        }

        // ── ENVIRONMENT ───────────────────────────────────────────────────────

        private static void SetupLighting()
        {
            // Remove default directional light, add a warm KotOR-style one
            var existing = GameObject.Find("Directional Light");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var lightGO = new GameObject("Sun");
            var light = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.color     = new Color(1.0f, 0.95f, 0.85f);
            light.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ambient
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.16f, 0.22f);
        }

        // ── PLAYER ────────────────────────────────────────────────────────────

        private static GameObject CreatePlayer()
        {
            var player = CreatePrimitive("Player", PrimitiveType.Capsule);
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.tag = "Player";

            player.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial("PlayerMat", new Color(0.2f, 0.4f, 0.8f));

            // Remove capsule collider — CharacterController provides collision
            UnityEngine.Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

            AddComponentSafe<CharacterController>(player);
            AddComponentSafe<KotORUnity.Player.ActionPlayerController>(player);
            AddComponentSafe<KotORUnity.Player.InputHandler>(player);
            AddComponentSafe<KotORUnity.Player.PlayerStatsBehaviour>(player);
            AddComponentSafe<KotORUnity.Player.StaminaSystem>(player);
            AddComponentSafe<KotORUnity.Inventory.InventoryManager>(player);
            AddComponentSafe<KotORUnity.Combat.ForcePowerManager>(player);
            AddComponentSafe<KotORUnity.World.InteractionController>(player);
            AddComponentSafe<KotORUnity.Combat.CombatInitiator>(player);

            // Camera target bone helper
            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(player.transform);
            camTarget.transform.localPosition = new Vector3(0f, 0.8f, 0f);

            return player;
        }

        // ── CAMERA RIG ────────────────────────────────────────────────────────

        private static void CreateCameraRig(Transform playerTransform)
        {
            var rig = new GameObject("CameraRig");

            // Action camera
            var actionCamGO = new GameObject("ActionCamera");
            actionCamGO.transform.SetParent(rig.transform);
            var actionCam = actionCamGO.AddComponent<UnityEngine.Camera>();
            actionCam.fieldOfView = 70f;
            actionCam.tag = "MainCamera";
            actionCamGO.AddComponent<KotORUnity.Camera.ActionCamera>();

            // RTS camera
            var rtsCamGO = new GameObject("RTSCamera");
            rtsCamGO.transform.SetParent(rig.transform);
            var rtsCam = rtsCamGO.AddComponent<UnityEngine.Camera>();
            rtsCam.fieldOfView = 55f;
            rtsCam.enabled = false;
            rtsCamGO.AddComponent<KotORUnity.Camera.RTSCamera>();

            // Transition controller
            var transCtrl = rig.AddComponent<KotORUnity.Camera.CameraTransitionController>();

            // Dialogue camera
            var dlgCamGO = new GameObject("DialogueCamera");
            dlgCamGO.transform.SetParent(rig.transform);
            var dlgCam = dlgCamGO.AddComponent<UnityEngine.Camera>();
            dlgCam.fieldOfView = 55f;
            dlgCam.enabled = false;
        }

        // ── NPCs ──────────────────────────────────────────────────────────────

        private static void CreateCompanion(string name, Vector3 position, string resRef)
        {
            var go = CreatePrimitive(name, PrimitiveType.Capsule);
            go.transform.position = position;
            go.tag = "Companion";

            go.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial(name + "Mat", new Color(0.4f, 0.7f, 0.4f));

            UnityEngine.Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            go.AddComponent<CapsuleCollider>();

            var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius  = 0.35f;
            agent.height  = 1.8f;
            agent.speed   = 4f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = 1.5f;

            AddComponentSafe<KotORUnity.Player.PlayerStatsBehaviour>(go);
            AddComponentSafe<KotORUnity.AI.Companion.CompanionAI>(go);
            AddComponentSafe<KotORUnity.World.AnimatorBridge>(go);

            var kcd = go.AddComponent<KotORUnity.World.KotorCreatureData>();
            kcd.ResRef      = resRef;
            kcd.FirstName   = name;
            kcd.MaxHP       = 40;
            kcd.CurrentHP   = 40;
            kcd.IsHostile   = false;
        }

        private static void CreateEnemy(string name, Vector3 position, string resRef)
        {
            var go = CreatePrimitive(name, PrimitiveType.Capsule);
            go.transform.position = position;
            go.tag = "Enemy";

            go.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial(name + "Mat", new Color(0.7f, 0.2f, 0.2f));

            UnityEngine.Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            go.AddComponent<CapsuleCollider>();

            var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius  = 0.35f;
            agent.height  = 1.8f;
            agent.speed   = 3.5f;
            agent.stoppingDistance = 1.5f;

            AddComponentSafe<KotORUnity.Player.PlayerStatsBehaviour>(go);
            AddComponentSafe<KotORUnity.AI.Enemy.EnemyAI>(go);
            AddComponentSafe<KotORUnity.World.AnimatorBridge>(go);

            var kcd = go.AddComponent<KotORUnity.World.KotorCreatureData>();
            kcd.ResRef      = resRef;
            kcd.FirstName   = name;
            kcd.MaxHP       = 25;
            kcd.CurrentHP   = 25;
            kcd.IsHostile   = true;
            kcd.DetectionRadius = 12f;
        }

        private static void CreateQuestNPC(string name, Vector3 position, string resRef)
        {
            var go = CreatePrimitive(name, PrimitiveType.Capsule);
            go.transform.position = position;

            go.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial(name + "Mat", new Color(0.8f, 0.7f, 0.3f));

            var interactable = go.AddComponent<KotORUnity.World.Interactable>();
            interactable.DisplayName = name;
            interactable.ResRef      = resRef;
            interactable.DlgResRef   = resRef;
            interactable.Type        = KotORUnity.World.InteractableType.NPC;

            SetLayer(go, "Interactable");
        }

        // ── INTERACTABLES ─────────────────────────────────────────────────────

        private static void CreateDoor(Vector3 position)
        {
            var go = CreatePrimitive("Door_Test", PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.5f, 2.5f, 0.2f);

            go.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial("DoorMat", new Color(0.4f, 0.3f, 0.2f));

            var interactable = go.AddComponent<KotORUnity.World.Interactable>();
            interactable.DisplayName = "Sealed Door";
            interactable.ResRef      = "door_test01";
            interactable.Type        = KotORUnity.World.InteractableType.Door;
            interactable.IsLocked    = false;

            SetLayer(go, "Interactable");
        }

        private static void CreateContainer(Vector3 position)
        {
            var go = CreatePrimitive("Container_Footlocker", PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.7f, 0.5f, 0.5f);

            go.GetComponent<Renderer>().sharedMaterial =
                CreateColorMaterial("ChestMat", new Color(0.5f, 0.45f, 0.3f));

            var interactable = go.AddComponent<KotORUnity.World.Interactable>();
            interactable.DisplayName = "Footlocker";
            interactable.ResRef      = "container_foot01";
            interactable.Type        = KotORUnity.World.InteractableType.Container;
            interactable.ContainerItems.Add("g_i_credits");
            interactable.ContainerItems.Add("g_w_blstrpstl01");

            SetLayer(go, "Interactable");
        }

        // ── HUD CANVAS ────────────────────────────────────────────────────────

        private static void CreateHUDCanvas(Transform player)
        {
            var canvasGO = new GameObject("HUDCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var hud = canvasGO.AddComponent<KotORUnity.UI.HUDManager>();

            // Health bar panel (minimal setup — full wiring in Inspector)
            var healthPanel = CreateUIPanel(canvasGO.transform, "HealthPanel",
                new Vector2(-350f, -40f), new Vector2(250f, 20f), TextAnchor.LowerLeft);

            var healthBarGO = CreateUISlider(healthPanel.transform, "HealthBar");
            SetRectFull(healthBarGO);

            // Mode label top-center
            var modeLabel = CreateUIText(canvasGO.transform, "ModeLabel", "ACTION",
                new Vector2(0f, -20f), new Vector2(150f, 30f));

            // Pause panel (hidden by default)
            var pausePanel = CreateUIPanel(canvasGO.transform, "PausePanel",
                Vector2.zero, new Vector2(200f, 60f), TextAnchor.MiddleCenter);
            pausePanel.SetActive(false);
            CreateUIText(pausePanel.transform, "PauseLabel", "PAUSED",
                Vector2.zero, new Vector2(160f, 50f));

            // Crosshair
            var crosshairGO = new GameObject("Crosshair");
            crosshairGO.transform.SetParent(canvasGO.transform, false);
            var crosshairImg = crosshairGO.AddComponent<UnityEngine.UI.Image>();
            crosshairImg.color = new Color(1f, 1f, 1f, 0.7f);
            var crosshairRT = crosshairGO.GetComponent<RectTransform>();
            crosshairRT.sizeDelta = new Vector2(4f, 4f);
            crosshairRT.anchoredPosition = Vector2.zero;

            // ── MinimapSystem ──────────────────────────────────────────────
            var minimapPanelGO = CreateUIPanel(canvasGO.transform, "MinimapPanel",
                new Vector2(350f, -80f), new Vector2(160f, 160f), TextAnchor.LowerRight);
            minimapPanelGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var minimapDisplayGO = new GameObject("MinimapDisplay");
            minimapDisplayGO.transform.SetParent(minimapPanelGO.transform, false);
            var mmRawImg = minimapDisplayGO.AddComponent<UnityEngine.UI.RawImage>();
            SetRectFull(minimapDisplayGO);
            var mmSys = canvasGO.AddComponent<KotORUnity.UI.MinimapSystem>();
            Debug.Log("[PlaytestSceneBuilder] MinimapSystem added to HUDCanvas.");

            // ── CombatLog ──────────────────────────────────────────────────
            var combatLogPanelGO = CreateUIPanel(canvasGO.transform, "CombatLogPanel",
                new Vector2(-350f, 80f), new Vector2(320f, 120f), TextAnchor.LowerLeft);
            combatLogPanelGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);
            combatLogPanelGO.AddComponent<KotORUnity.UI.CombatLogUI>();

            // ── Inventory Canvas (separate overlay) ───────────────────────
            var invCanvasGO = new GameObject("InventoryCanvas");
            var invCanvas   = invCanvasGO.AddComponent<Canvas>();
            invCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            invCanvas.sortingOrder = 30;
            invCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            invCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var invPanel = CreateUIPanel(invCanvasGO.transform, "InventoryPanel",
                Vector2.zero, new Vector2(900f, 600f), TextAnchor.MiddleCenter);
            invPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            invPanel.AddComponent<KotORUnity.UI.Inventory.InventoryUI>();

            // ── SaveLoad Canvas ───────────────────────────────────────────
            var slCanvasGO = new GameObject("SaveLoadCanvas");
            var slCanvas   = slCanvasGO.AddComponent<Canvas>();
            slCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            slCanvas.sortingOrder = 35;
            slCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            slCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var slPanel = CreateUIPanel(slCanvasGO.transform, "SaveLoadPanel",
                Vector2.zero, new Vector2(860f, 640f), TextAnchor.MiddleCenter);
            slPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
            slPanel.AddComponent<KotORUnity.UI.SaveLoadUI>();
        }

        private static void CreateDialogueCanvas()
        {
            var canvasGO = new GameObject("DialogueCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Dialogue panel (bottom third)
            var panel = CreateUIPanel(canvasGO.transform, "DialoguePanel",
                new Vector2(0f, -230f), new Vector2(900f, 240f), TextAnchor.LowerCenter);
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var speakerText = CreateUIText(panel.transform, "SpeakerName", "Speaker",
                new Vector2(0f, 100f), new Vector2(400f, 30f));

            var bodyText = CreateUIText(panel.transform, "DialogueBody",
                "Dialogue text will appear here...",
                new Vector2(0f, 30f), new Vector2(850f, 120f));

            var replyContainer = new GameObject("ReplyContainer");
            replyContainer.transform.SetParent(panel.transform, false);
            var replyRT = replyContainer.AddComponent<RectTransform>();
            replyRT.sizeDelta        = new Vector2(860f, 100f);
            replyRT.anchoredPosition = new Vector2(0f, -95f);
            var vl = replyContainer.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vl.spacing  = 4f;
            vl.childControlHeight = true;
            vl.childControlWidth  = true;

            var continueBtn = CreateUIButton(panel.transform, "ContinueButton",
                "[ SPACE to continue ]", new Vector2(0f, -100f), new Vector2(240f, 30f));

            // Attach DialogueUI
            var dlgUI = canvasGO.AddComponent<KotORUnity.Dialogue.DialogueUI>();

            // Dialogue Manager
            var dlgMgrGO   = new GameObject("DialogueManager");
            var dlgMgrComp = dlgMgrGO.AddComponent<KotORUnity.Dialogue.DialogueManager>();
        }

        private static void CreateInteractionPrompt()
        {
            var promptGO = new GameObject("InteractionPrompt");
            var canvas   = promptGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            var cg = promptGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var panel = CreateUIPanel(promptGO.transform, "PromptPanel",
                new Vector2(0f, -180f), new Vector2(300f, 50f), TextAnchor.LowerCenter);
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

            CreateUIText(panel.transform, "PromptVerb",  "[E] Talk",
                new Vector2(0f, 10f),  new Vector2(280f, 24f));
            CreateUIText(panel.transform, "PromptName",  "NPC Name",
                new Vector2(0f, -12f), new Vector2(280f, 20f));

            promptGO.AddComponent<KotORUnity.World.InteractionPromptUI>();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static GameObject CreatePrimitive(string name, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            return go;
        }

        private static GameObject CreateManager(string name)
        {
            var go = new GameObject(name);
            return go;
        }

        private static T AddComponentSafe<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static void SetLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        private static void EnsureSceneDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        private static void EnsureLayer(string name)
        {
            // Layers must exist in ProjectSettings/TagManager.asset
            // We log a reminder rather than modify that file programmatically
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
                Debug.LogWarning($"[PlaytestSceneBuilder] Layer '{name}' does not exist. " +
                                 $"Add it in Project Settings → Tags & Layers.");
        }

        private static Material CreateColorMaterial(string name, Color color)
        {
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            mat.color = color;
            mat.name  = name;
            return mat;
        }

        // ── UI HELPERS ────────────────────────────────────────────────────────

        private static GameObject CreateUIPanel(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;
            SetRectAnchor(rt, anchor);
            return go;
        }

        private static GameObject CreateUISlider(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            go.AddComponent<UnityEngine.UI.Slider>();
            return go;
        }

        private static GameObject CreateUIText(Transform parent, string name,
            string defaultText, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;

            // Prefer TMP if available
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = defaultText;
            tmp.fontSize  = 16;
            tmp.color     = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;

            return go;
        }

        private static GameObject CreateUIButton(Transform parent, string name,
            string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            go.AddComponent<UnityEngine.UI.Button>();
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;

            CreateUIText(go.transform, "Label", label, Vector2.zero, size);
            return go;
        }

        private static void SetRectFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetRectAnchor(RectTransform rt, TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.LowerLeft:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); break;
                case TextAnchor.LowerCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); break;
                case TextAnchor.MiddleCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); break;
                case TextAnchor.UpperLeft:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); break;
                case TextAnchor.UpperCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); break;
                default:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); break;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCENE VALIDATOR  —  checks the playtest scene for common issues
    // ══════════════════════════════════════════════════════════════════════════

    public static class SceneValidator
    {
        [MenuItem("KotOR-Unity/🔍 Validate Playtest Scene", false, 2)]
        public static void ValidateScene()
        {
            int issues = 0;

            void Warn(string msg) { Debug.LogWarning("[SceneValidator] ⚠ " + msg); issues++; }
            void Ok(string msg)   { Debug.Log("[SceneValidator] ✓ " + msg); }

            // Player
            var player = GameObject.FindWithTag("Player");
            if (player == null) Warn("No GameObject tagged 'Player'.");
            else
            {
                Ok("Player found: " + player.name);
                if (player.GetComponent<Player.ActionPlayerController>() == null)
                    Warn("Player missing ActionPlayerController.");
                if (player.GetComponent<CharacterController>() == null)
                    Warn("Player missing CharacterController.");
                if (player.GetComponent<Player.PlayerStatsBehaviour>() == null)
                    Warn("Player missing PlayerStatsBehaviour.");
            }

            // Managers
            CheckSingleton<KotORUnity.Core.GameManager>("GameManager");
            CheckSingleton<KotORUnity.Inventory.InventoryManager>("InventoryManager");
            CheckSingleton<KotORUnity.Party.PartyManager>("PartyManager");
            CheckSingleton<KotORUnity.Dialogue.DialogueManager>("DialogueManager");
            CheckSingleton<KotORUnity.Combat.CombatManager>("CombatManager");

            // Camera
            if (UnityEngine.Camera.main == null)
                Warn("No main camera (Camera tagged 'MainCamera') in scene.");
            else
                Ok("Main camera found.");

            // Canvas
            if (UnityEngine.Object.FindObjectOfType<KotORUnity.UI.HUDManager>() == null)
                Warn("HUDManager not found — HUD will not display.");
            else
                Ok("HUDManager found.");

            Debug.Log($"[SceneValidator] Validation complete. Issues: {issues}");
        }

        private static void CheckSingleton<T>(string label) where T : UnityEngine.MonoBehaviour
        {
            var obj = UnityEngine.Object.FindObjectOfType<T>();
            if (obj == null)
                Debug.LogWarning($"[SceneValidator] ⚠ {label} ({typeof(T).Name}) not found in scene.");
            else
                Debug.Log($"[SceneValidator] ✓ {label} found.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  QUICK WIRING HELPER  —  connects serialized fields automatically
    // ══════════════════════════════════════════════════════════════════════════

    public static class QuickWiring
    {
        [MenuItem("KotOR-Unity/⚡ Auto-Wire Scene References", false, 3)]
        public static void AutoWireScene()
        {
            int wired = 0;

            // Wire HUDManager → PlayerStatsBehaviour
            var hud = UnityEngine.Object.FindObjectOfType<KotORUnity.UI.HUDManager>();
            if (hud != null)
            {
                Debug.Log("[AutoWire] HUDManager found — update inspector references manually.");
                wired++;
            }

            // Wire InteractionController → InteractionPromptUI
            var ic = UnityEngine.Object.FindObjectOfType<KotORUnity.World.InteractionController>();
            var ip = UnityEngine.Object.FindObjectOfType<KotORUnity.World.InteractionPromptUI>();
            if (ic != null && ip != null)
            {
                // SerializedObject approach for the field
                var so = new SerializedObject(ic);
                var promptProp = so.FindProperty("interactPrompt");
                if (promptProp != null)
                {
                    promptProp.objectReferenceValue = ip;
                    so.ApplyModifiedProperties();
                    Debug.Log("[AutoWire] Wired InteractionController → InteractionPromptUI.");
                    wired++;
                }
            }

            // Wire DialogueManager → DialogueUI
            var dlgMgr = UnityEngine.Object.FindObjectOfType<KotORUnity.Dialogue.DialogueManager>();
            var dlgUI  = UnityEngine.Object.FindObjectOfType<KotORUnity.Dialogue.DialogueUI>();
            if (dlgMgr != null && dlgUI != null)
            {
                var so   = new SerializedObject(dlgMgr);
                var prop = so.FindProperty("dialogueUI");
                if (prop != null)
                {
                    prop.objectReferenceValue = dlgUI;
                    so.ApplyModifiedProperties();
                    Debug.Log("[AutoWire] Wired DialogueManager → DialogueUI.");
                    wired++;
                }
            }

            EditorUtility.DisplayDialog("Auto-Wire Complete",
                $"Wired {wired} reference(s).\n\nRemaining references must be wired in the Inspector.",
                "OK");
        }
    }
}
#endif
