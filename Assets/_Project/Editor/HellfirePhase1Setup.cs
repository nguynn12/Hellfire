// Script: HellfirePhase1Setup.cs
// Mục đích: Editor Utility tự động thiết lập toàn bộ Prefab, Scene, và Build Settings cho Giai đoạn 1 trên Unity 6 (6000.5.6f1).
// Môi trường thực thi: Editor-only.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Hellfire.Networking;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hellfire.Editor
{
    public static class HellfirePhase1Setup
    {
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string GameManagerPrefabPath = "Assets/_Project/Prefabs/GameManager.prefab";
        private const string NetworkManagerPrefabPath = "Assets/_Project/Prefabs/NetworkManager.prefab";

        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";

        [MenuItem("Hellfire/Phase 1/Setup All (Prefabs, Scenes, Build Settings)", false, 1)]
        public static void SetupAll()
        {
            EnsureDirectories();
            var playerPrefab = CreatePlayerPrefabInternal();
            var gameManagerPrefab = CreateGameManagerPrefabInternal();
            var networkManagerPrefab = CreateNetworkManagerPrefabInternal(playerPrefab, gameManagerPrefab);
            CreateScenesInternal(networkManagerPrefab);
            UpdateBuildSettingsInternal();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Hellfire - Thiết lập Giai đoạn 1 (Unity 6 Standard)",
                "Đã hoàn thành rà soát & thiết lập toàn diện:\n\n" +
                "1. Prefab Player (FPS Camera, CharacterController, NetworkTransform, Culling Mask)\n" +
                "2. Prefab GameManager (NetworkObject, NetworkVariable<GameState>)\n" +
                "3. Prefab NetworkManager (UTP, LanDiscovery, NetworkConnectManager, ConnectionApproval, SceneManagement)\n" +
                "4. Scene MainMenu (Đầy đủ Main, Host, Join IP, Scan LAN, Settings, Disconnect Modal, EventSystem + InputSystemUIInputModule)\n" +
                "5. Scene Lobby (4 Slots người chơi, Host Start, Leave button, EventSystem)\n" +
                "6. Scene Gameplay (Arena, Obstacles, Crosshair HUD, Pause Menu, Player Info)\n" +
                "7. Cập nhật Build Settings cho cả 3 Scenes.",
                "OK"
            );
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        }

        [MenuItem("Hellfire/Phase 1/1. Create Player Prefab", false, 10)]
        public static void MenuItemCreatePlayerPrefab()
        {
            CreatePlayerPrefabInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Hellfire/Phase 1/2. Create GameManager Prefab", false, 11)]
        public static void MenuItemCreateGameManagerPrefab()
        {
            CreateGameManagerPrefabInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Hellfire/Phase 1/3. Create NetworkManager Prefab", false, 12)]
        public static void MenuItemCreateNetworkManagerPrefab()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var gameManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath);
            CreateNetworkManagerPrefabInternal(playerPrefab, gameManagerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Hellfire/Phase 1/4. Create All Scenes", false, 13)]
        public static void MenuItemCreateAllScenes()
        {
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            CreateScenesInternal(nmPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Hellfire/Phase 1/5. Update Build Settings", false, 14)]
        public static void MenuItemUpdateBuildSettings()
        {
            UpdateBuildSettingsInternal();
        }

        public static GameObject CreatePlayerPrefabInternal()
        {
            EnsureDirectories();

            var playerRoot = new GameObject("Player");
            playerRoot.tag = "Player";

            // 1. CharacterController (Mục 1.3)
            var cc = playerRoot.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;

            // 2. Netcode components
            var netObj = playerRoot.AddComponent<NetworkObject>();
            playerRoot.AddComponent<NetworkTransform>();

            // 3. CameraPivot & PlayerCamera (Mục 1.2)
            var cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(playerRoot.transform);
            cameraPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot.transform.localRotation = Quaternion.identity;

            var playerCamObj = new GameObject("PlayerCamera");
            playerCamObj.transform.SetParent(cameraPivot.transform);
            playerCamObj.transform.localPosition = Vector3.zero;
            playerCamObj.transform.localRotation = Quaternion.identity;

            var cam = playerCamObj.AddComponent<Camera>();
            cam.fieldOfView = 90f;
            cam.nearClipPlane = 0.05f;
            var audioListener = playerCamObj.AddComponent<AudioListener>();

            // Viewmodel Placeholder
            var viewmodelObj = new GameObject("WeaponViewmodel");
            viewmodelObj.transform.SetParent(playerCamObj.transform);
            viewmodelObj.transform.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            int viewmodelLayer = LayerMask.NameToLayer("Viewmodel");
            if (viewmodelLayer >= 0) viewmodelObj.layer = viewmodelLayer;

            var gunPlaceholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gunPlaceholder.name = "GunMesh";
            gunPlaceholder.transform.SetParent(viewmodelObj.transform);
            gunPlaceholder.transform.localPosition = Vector3.zero;
            gunPlaceholder.transform.localScale = new Vector3(0.1f, 0.1f, 0.4f);
            if (viewmodelLayer >= 0) gunPlaceholder.layer = viewmodelLayer;
            var gunCol = gunPlaceholder.GetComponent<Collider>();
            if (gunCol != null) Object.DestroyImmediate(gunCol);

            // 4. PlayerBodyMesh (Mục 1.2 & 1.1)
            var bodyMeshObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyMeshObj.name = "PlayerBodyMesh";
            bodyMeshObj.transform.SetParent(playerRoot.transform);
            bodyMeshObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            bodyMeshObj.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            int bodyLayer = LayerMask.NameToLayer("PlayerBody");
            if (bodyLayer >= 0) bodyMeshObj.layer = bodyLayer;
            var bodyCol = bodyMeshObj.GetComponent<Collider>();
            if (bodyCol != null) Object.DestroyImmediate(bodyCol);

            // 5. PlayerMovement Script
            var movement = playerRoot.AddComponent<PlayerMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("_characterController").objectReferenceValue = cc;
            so.FindProperty("_cameraPivot").objectReferenceValue = cameraPivot.transform;
            so.FindProperty("_playerCamera").objectReferenceValue = cam;
            so.FindProperty("_audioListener").objectReferenceValue = audioListener;
            so.FindProperty("_playerBodyMesh").objectReferenceValue = bodyMeshObj;
            so.FindProperty("_weaponViewmodel").objectReferenceValue = viewmodelObj;
            so.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            Object.DestroyImmediate(playerRoot);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo Player Prefab tại: {PlayerPrefabPath}");
            return prefab;
        }

        public static GameObject CreateGameManagerPrefabInternal()
        {
            EnsureDirectories();

            var gmObj = new GameObject("GameManager");
            gmObj.AddComponent<NetworkObject>();
            gmObj.AddComponent<GameManager>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(gmObj, GameManagerPrefabPath);
            Object.DestroyImmediate(gmObj);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo GameManager Prefab tại: {GameManagerPrefabPath}");
            return prefab;
        }

        public static GameObject CreateNetworkManagerPrefabInternal(GameObject playerPrefab = null, GameObject gameManagerPrefab = null)
        {
            EnsureDirectories();

            if (playerPrefab == null)
            {
                playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            }

            if (gameManagerPrefab == null)
            {
                gameManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath);
            }

            var nmObj = new GameObject("NetworkManager");
            var nm = nmObj.AddComponent<NetworkManager>();
            var utp = nmObj.AddComponent<UnityTransport>();
            utp.SetConnectionData("0.0.0.0", 7777);

            var netConnectMgr = nmObj.AddComponent<NetworkConnectManager>();
            nmObj.AddComponent<LanDiscovery>();

            // Cấu hình NetworkConfig chuẩn NGO 2.x cho Unity 6
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.TickRate = 30;

            if (playerPrefab != null)
            {
                nm.NetworkConfig.PlayerPrefab = playerPrefab;
            }

            if (gameManagerPrefab != null)
            {
                if (!nm.NetworkConfig.Prefabs.Contains(gameManagerPrefab))
                {
                    nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = gameManagerPrefab });
                }

                var soConnect = new SerializedObject(netConnectMgr);
                soConnect.FindProperty("_gameManagerPrefab").objectReferenceValue = gameManagerPrefab;
                soConnect.ApplyModifiedProperties();
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(nmObj, NetworkManagerPrefabPath);
            Object.DestroyImmediate(nmObj);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo NetworkManager Prefab tại: {NetworkManagerPrefabPath}");
            return prefab;
        }

        public static void CreateScenesInternal(GameObject networkManagerPrefab = null)
        {
            EnsureDirectories();

            if (networkManagerPrefab == null)
            {
                networkManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            }

            CreateMainMenuScene(networkManagerPrefab);
            CreateLobbyScene();
            CreateGameplayScene();
        }

        private static void CreateMainMenuScene(GameObject networkManagerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camObj = new GameObject("Main Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.12f);
            camObj.AddComponent<AudioListener>();

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.85f, 0.7f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // NetworkManager instance in scene
            if (networkManagerPrefab != null)
            {
                PrefabUtility.InstantiatePrefab(networkManagerPrefab);
            }

            // EventSystem & InputSystemUIInputModule (Unity 6 Standard)
            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            var inputModule = esObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            // Canvas
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // MainMenuUI Component
            var menuObj = new GameObject("MainMenuUI");
            menuObj.transform.SetParent(canvasObj.transform, false);
            var menuUI = menuObj.AddComponent<MainMenuUI>();

            // 1. Main Panel
            var mainPanel = CreateUIPanel("MainPanel", canvasObj.transform, new Color(0.08f, 0.05f, 0.08f, 0.95f));
            var titleText = CreateTextMeshPro("TitleText", mainPanel.transform, "HELLFIRE", 64, FontStyles.Bold, new Vector2(0f, 220f));
            titleText.color = new Color(1f, 0.3f, 0.1f);
            var subTitleText = CreateTextMeshPro("SubTitleText", mainPanel.transform, "LAN Co-op First-Person Roguelite", 22, FontStyles.Italic, new Vector2(0f, 160f));
            subTitleText.color = new Color(0.85f, 0.85f, 0.85f);

            var hostBtn = CreateUIButton("HostButton", mainPanel.transform, "TẠO PHÒNG (HOST)", new Vector2(0f, 60f), new Vector2(320f, 50f));
            var joinIpBtn = CreateUIButton("JoinIpButton", mainPanel.transform, "NHẬP IP VÀO PHÒNG", new Vector2(0f, -5f), new Vector2(320f, 50f));
            var scanLanBtn = CreateUIButton("ScanLanButton", mainPanel.transform, "QUÉT PHÒNG LAN", new Vector2(0f, -70f), new Vector2(320f, 50f));
            var settingsBtn = CreateUIButton("SettingsButton", mainPanel.transform, "CÀI ĐẶT", new Vector2(0f, -135f), new Vector2(320f, 50f));
            var quitBtn = CreateUIButton("QuitButton", mainPanel.transform, "THOÁT GAME", new Vector2(0f, -200f), new Vector2(320f, 50f));

            // 2. Host Panel
            var hostPanel = CreateUIPanel("HostPanel", canvasObj.transform, new Color(0.08f, 0.05f, 0.08f, 0.95f));
            CreateTextMeshPro("HostTitle", hostPanel.transform, "CẤU HÌNH TẠO PHÒNG", 36, FontStyles.Bold, new Vector2(0f, 200f));
            CreateTextMeshPro("HostRoomLabel", hostPanel.transform, "Tên phòng:", 18, FontStyles.Normal, new Vector2(-150f, 100f), new Vector2(200f, 40f));
            var hostRoomNameInput = CreateTMPInputField("HostRoomNameInput", hostPanel.transform, "Nhập tên phòng...", "Hellfire LAN Match", new Vector2(80f, 100f), new Vector2(280f, 45f));
            CreateTextMeshPro("HostPortLabel", hostPanel.transform, "Cổng mạng (Port):", 18, FontStyles.Normal, new Vector2(-150f, 30f), new Vector2(200f, 40f));
            var hostPortInput = CreateTMPInputField("HostPortInput", hostPanel.transform, "Cổng mạng...", "7777", new Vector2(80f, 30f), new Vector2(280f, 45f));
            var hostStartBtn = CreateUIButton("HostStartBtn", hostPanel.transform, "BẮT ĐẦU HOST", new Vector2(0f, -60f), new Vector2(300f, 50f));
            var hostBackBtn = CreateUIButton("HostBackBtn", hostPanel.transform, "QUAY LẠI", new Vector2(0f, -130f), new Vector2(300f, 50f));
            hostPanel.SetActive(false);

            // 3. Join IP Panel
            var joinIpPanel = CreateUIPanel("JoinIpPanel", canvasObj.transform, new Color(0.08f, 0.05f, 0.08f, 0.95f));
            CreateTextMeshPro("JoinTitle", joinIpPanel.transform, "KẾT NỐI QUA IP", 36, FontStyles.Bold, new Vector2(0f, 200f));
            CreateTextMeshPro("JoinIpLabel", joinIpPanel.transform, "Địa chỉ IP Host:", 18, FontStyles.Normal, new Vector2(-150f, 100f), new Vector2(200f, 40f));
            var joinIpInput = CreateTMPInputField("JoinIpInput", joinIpPanel.transform, "127.0.0.1", "127.0.0.1", new Vector2(80f, 100f), new Vector2(280f, 45f));
            CreateTextMeshPro("JoinPortLabel", joinIpPanel.transform, "Cổng mạng (Port):", 18, FontStyles.Normal, new Vector2(-150f, 30f), new Vector2(200f, 40f));
            var joinPortInput = CreateTMPInputField("JoinPortInput", joinIpPanel.transform, "7777", "7777", new Vector2(80f, 30f), new Vector2(280f, 45f));
            var joinConnectBtn = CreateUIButton("JoinConnectBtn", joinIpPanel.transform, "KẾT NỐI NGAY", new Vector2(0f, -60f), new Vector2(300f, 50f));
            var joinBackBtn = CreateUIButton("JoinIpBackBtn", joinIpPanel.transform, "QUAY LẠI", new Vector2(0f, -130f), new Vector2(300f, 50f));
            joinIpPanel.SetActive(false);

            // 4. LAN Scan Panel
            var lanScanPanel = CreateUIPanel("LanScanPanel", canvasObj.transform, new Color(0.08f, 0.05f, 0.08f, 0.95f));
            CreateTextMeshPro("ScanTitle", lanScanPanel.transform, "DANH SÁCH PHÒNG LAN", 36, FontStyles.Bold, new Vector2(0f, 230f));
            var scanStatusText = CreateTextMeshPro("ScanStatus", lanScanPanel.transform, "Đang quét...", 18, FontStyles.Normal, new Vector2(0f, 180f));

            var scrollObj = new GameObject("RoomScrollContent");
            scrollObj.transform.SetParent(lanScanPanel.transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(600f, 260f);
            scrollRect.anchoredPosition = new Vector2(0f, 15f);

            var scanRefreshBtn = CreateUIButton("ScanRefreshBtn", lanScanPanel.transform, "LÀM MỚI QUÉT", new Vector2(-160f, -180f), new Vector2(220f, 45f));
            var scanBackBtn = CreateUIButton("ScanBackBtn", lanScanPanel.transform, "QUAY LẠI", new Vector2(160f, -180f), new Vector2(220f, 45f));
            lanScanPanel.SetActive(false);

            // 5. Settings Panel
            var settingsPanel = CreateUIPanel("SettingsPanel", canvasObj.transform, new Color(0.08f, 0.05f, 0.08f, 0.95f));
            CreateTextMeshPro("SettingsTitle", settingsPanel.transform, "CÀI ĐẶT GAME", 36, FontStyles.Bold, new Vector2(0f, 200f));
            CreateTextMeshPro("SensitivityLabel", settingsPanel.transform, "Độ nhạy chuột (Mouse Sensitivity):", 18, FontStyles.Normal, new Vector2(0f, 100f));
            var sensSlider = CreateSlider("SensitivitySlider", settingsPanel.transform, 0.1f, 10.0f, 2.0f, new Vector2(0f, 50f), new Vector2(360f, 30f));
            var sensValueText = CreateTextMeshPro("SensitivityValue", settingsPanel.transform, "2.0", 18, FontStyles.Bold, new Vector2(0f, 10f));
            sensValueText.color = Color.yellow;
            var settingsBackBtn = CreateUIButton("SettingsBackBtn", settingsPanel.transform, "QUAY LẠI", new Vector2(0f, -100f), new Vector2(300f, 50f));
            settingsPanel.SetActive(false);

            // 6. Disconnect Modal Panel
            var disconnectModal = CreateUIPanel("DisconnectModal", canvasObj.transform, new Color(0f, 0f, 0f, 0.75f));
            var modalBox = CreateUIPanel("ModalBox", disconnectModal.transform, new Color(0.18f, 0.1f, 0.12f, 0.98f));
            var modalBoxRect = modalBox.GetComponent<RectTransform>();
            modalBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalBoxRect.sizeDelta = new Vector2(500f, 260f);
            modalBoxRect.anchoredPosition = Vector2.zero;

            CreateTextMeshPro("DisconnectTitle", modalBox.transform, "THÔNG BÁO MẤT KẾT NỐI", 24, FontStyles.Bold, new Vector2(0f, 75f));
            var disconnectReasonText = CreateTextMeshPro("DisconnectReason", modalBox.transform, "Mất kết nối với máy chủ.", 16, FontStyles.Normal, new Vector2(0f, 15f), new Vector2(450f, 60f));
            var disconnectOkBtn = CreateUIButton("DisconnectOkBtn", modalBox.transform, "ĐÃ HIỂU", new Vector2(0f, -65f), new Vector2(200f, 45f));
            disconnectModal.SetActive(false);

            // 7. Status message text
            var statusText = CreateTextMeshPro("StatusText", canvasObj.transform, "", 16, FontStyles.Italic, new Vector2(0f, -250f));
            statusText.color = Color.yellow;

            // Wire Serialized Properties
            var so = new SerializedObject(menuUI);
            so.FindProperty("_mainPanel").objectReferenceValue = mainPanel;
            so.FindProperty("_hostPanel").objectReferenceValue = hostPanel;
            so.FindProperty("_joinIpPanel").objectReferenceValue = joinIpPanel;
            so.FindProperty("_lanScanPanel").objectReferenceValue = lanScanPanel;
            so.FindProperty("_settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("_disconnectModal").objectReferenceValue = disconnectModal;

            so.FindProperty("_mainHostButton").objectReferenceValue = hostBtn;
            so.FindProperty("_mainJoinIpButton").objectReferenceValue = joinIpBtn;
            so.FindProperty("_mainScanLanButton").objectReferenceValue = scanLanBtn;
            so.FindProperty("_mainSettingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("_mainQuitButton").objectReferenceValue = quitBtn;

            so.FindProperty("_hostRoomNameInput").objectReferenceValue = hostRoomNameInput;
            so.FindProperty("_hostPortInput").objectReferenceValue = hostPortInput;
            so.FindProperty("_hostStartButton").objectReferenceValue = hostStartBtn;
            so.FindProperty("_hostBackButton").objectReferenceValue = hostBackBtn;

            so.FindProperty("_joinIpInput").objectReferenceValue = joinIpInput;
            so.FindProperty("_joinPortInput").objectReferenceValue = joinPortInput;
            so.FindProperty("_joinConnectButton").objectReferenceValue = joinConnectBtn;
            so.FindProperty("_joinIpBackButton").objectReferenceValue = joinBackBtn;

            so.FindProperty("_scanRefreshButton").objectReferenceValue = scanRefreshBtn;
            so.FindProperty("_scanBackButton").objectReferenceValue = scanBackBtn;
            so.FindProperty("_scanStatusText").objectReferenceValue = scanStatusText;
            so.FindProperty("_roomListContent").objectReferenceValue = scrollObj.transform;

            so.FindProperty("_mouseSensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("_mouseSensitivityValueText").objectReferenceValue = sensValueText;
            so.FindProperty("_settingsBackButton").objectReferenceValue = settingsBackBtn;

            so.FindProperty("_disconnectReasonText").objectReferenceValue = disconnectReasonText;
            so.FindProperty("_disconnectModalOkButton").objectReferenceValue = disconnectOkBtn;

            so.FindProperty("_statusMessageText").objectReferenceValue = statusText;
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo Scene: {MainMenuScenePath}");
        }

        private static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObj = new GameObject("Main Camera");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.08f, 0.1f);

            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            var inputModule = esObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            var lobbyObj = new GameObject("LobbyUI");
            lobbyObj.transform.SetParent(canvasObj.transform, false);
            var lobbyUI = lobbyObj.AddComponent<LobbyUI>();

            var bgObj = CreateUIPanel("LobbyPanel", canvasObj.transform, new Color(0.08f, 0.06f, 0.08f, 0.95f));
            var title = CreateTextMeshPro("LobbyTitle", bgObj.transform, "PHÒNG CHỜ (LOBBY)", 40, FontStyles.Bold, new Vector2(0f, 220f));
            var connInfo = CreateTextMeshPro("ConnectionInfo", bgObj.transform, "Đang làm Host (Cổng 7777)", 18, FontStyles.Italic, new Vector2(0f, 175f));
            connInfo.color = new Color(0.8f, 0.8f, 0.8f);
            var pCount = CreateTextMeshPro("PlayerCount", bgObj.transform, "Người chơi: 1/4", 20, FontStyles.Bold, new Vector2(0f, 135f));
            pCount.color = Color.yellow;

            var slotsList = new List<TextMeshProUGUI>();
            for (int i = 0; i < 4; i++)
            {
                var slot = CreateTextMeshPro($"PlayerSlot_{i + 1}", bgObj.transform, $"Slot {i + 1}: [Trống]", 22, FontStyles.Normal, new Vector2(0f, 70f - i * 50f));
                slotsList.Add(slot);
            }

            var startBtn = CreateUIButton("StartGameButton", bgObj.transform, "BẮT ĐẦU GAME (HOST)", new Vector2(0f, -160f), new Vector2(300f, 50f));
            var waitingText = CreateTextMeshPro("WaitingHostText", bgObj.transform, "Đang chờ chủ phòng bắt đầu trận đấu...", 18, FontStyles.Italic, new Vector2(0f, -160f));
            waitingText.color = Color.cyan;
            waitingText.gameObject.SetActive(false);

            var leaveBtn = CreateUIButton("LeaveButton", bgObj.transform, "RỜI PHÒNG", new Vector2(0f, -230f), new Vector2(300f, 50f));

            var so = new SerializedObject(lobbyUI);
            so.FindProperty("_roomTitleText").objectReferenceValue = title;
            so.FindProperty("_connectionInfoText").objectReferenceValue = connInfo;
            so.FindProperty("_playerCountText").objectReferenceValue = pCount;
            so.FindProperty("_startGameButton").objectReferenceValue = startBtn;
            so.FindProperty("_waitingHostText").objectReferenceValue = waitingText;
            so.FindProperty("_leaveRoomButton").objectReferenceValue = leaveBtn;

            var slotsProp = so.FindProperty("_playerSlotTexts");
            slotsProp.arraySize = slotsList.Count;
            for (int i = 0; i < slotsList.Count; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotsList[i];
            }
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo Scene: {LobbyScenePath}");
        }

        private static void CreateGameplayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.8f, 0.65f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Test Arena Floor (50m x 50m)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);

            // Obstacles / Pillars for testing first person movement & collisions
            for (int x = -15; x <= 15; x += 10)
            {
                for (int z = -15; z <= 15; z += 10)
                {
                    if (x == 0 && z == 0) continue;
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = $"Pillar_{x}_{z}";
                    pillar.transform.position = new Vector3(x, 2f, z);
                    pillar.transform.localScale = new Vector3(2f, 4f, 2f);
                }
            }

            // EventSystem & HUD Canvas
            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            var inputModule = esObj.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            var canvasObj = new GameObject("HUDCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            var hudObj = new GameObject("GameplayHUD");
            hudObj.transform.SetParent(canvasObj.transform, false);
            var hud = hudObj.AddComponent<GameplayHUD>();

            // Crosshair (Mục 7)
            var crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(canvasObj.transform, false);
            var crosshairImg = crosshairObj.AddComponent<Image>();
            crosshairImg.color = new Color(1f, 1f, 1f, 0.85f);
            var rect = crosshairObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(6f, 6f);
            rect.anchoredPosition = Vector2.zero;

            // Player Info Text
            var pInfoText = CreateTextMeshPro("PlayerInfoText", canvasObj.transform, "Player #0", 18, FontStyles.Normal, new Vector2(-800f, 480f), new Vector2(300f, 40f));
            pInfoText.alignment = TextAlignmentOptions.MidlineLeft;

            // Pause Menu Panel
            var pausePanel = CreateUIPanel("PauseMenuPanel", canvasObj.transform, new Color(0f, 0f, 0f, 0.75f));
            var pauseBox = CreateUIPanel("PauseBox", pausePanel.transform, new Color(0.15f, 0.1f, 0.12f, 0.95f));
            var pauseBoxRect = pauseBox.GetComponent<RectTransform>();
            pauseBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            pauseBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            pauseBoxRect.sizeDelta = new Vector2(400f, 300f);
            pauseBoxRect.anchoredPosition = Vector2.zero;

            CreateTextMeshPro("PauseTitle", pauseBox.transform, "TẠM DỪNG (PAUSE)", 28, FontStyles.Bold, new Vector2(0f, 80f));
            var resumeBtn = CreateUIButton("ResumeButton", pauseBox.transform, "TIẾP TỤC", new Vector2(0f, 10f), new Vector2(250f, 45f));
            var leaveBtn = CreateUIButton("LeaveGameButton", pauseBox.transform, "RỜI TRẬN ĐẤU", new Vector2(0f, -60f), new Vector2(250f, 45f));
            pausePanel.SetActive(false);

            // Wire Serialized Properties
            var so = new SerializedObject(hud);
            so.FindProperty("_crosshairObject").objectReferenceValue = crosshairObj;
            so.FindProperty("_pauseMenuPanel").objectReferenceValue = pausePanel;
            so.FindProperty("_resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("_leaveGameButton").objectReferenceValue = leaveBtn;
            so.FindProperty("_playerInfoText").objectReferenceValue = pInfoText;
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            Debug.Log($"[HellfirePhase1Setup] Đã tạo Scene: {GameplayScenePath}");
        }

        public static void UpdateBuildSettingsInternal()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("[HellfirePhase1Setup] Đã cập nhật 3 Scenes vào Build Settings.");
        }

        private static GameObject CreateUIPanel(string name, Transform parent, Color color)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent, false);
            var img = panelObj.AddComponent<Image>();
            img.color = color;
            var rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panelObj;
        }

        private static TextMeshProUGUI CreateTextMeshPro(string name, Transform parent, string text, float fontSize, FontStyles style, Vector2 pos, Vector2? size = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size ?? new Vector2(600f, 60f);
            rect.anchoredPosition = pos;
            return tmp;
        }

        private static Button CreateUIButton(string name, Transform parent, string text, Vector2 pos, Vector2 size)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.15f, 0.15f, 0.95f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.55f, 0.2f, 0.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.1f, 0.1f, 1f);
            colors.selectedColor = new Color(0.55f, 0.2f, 0.15f, 1f);
            btn.colors = colors;

            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // Tránh chặn raycast của Button cha
            var txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            return btn;
        }

        private static TMP_InputField CreateTMPInputField(string name, Transform parent, string placeholderText, string defaultText, Vector2 pos, Vector2 size)
        {
            var inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);
            var img = inputObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.12f, 0.15f, 0.95f);

            var rect = inputObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            // Text area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            var areaRect = textArea.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10, 5);
            areaRect.offsetMax = new Vector2(-10, -5);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textArea.transform, false);
            var phTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholderText;
            phTmp.fontSize = 16;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.raycastTarget = false;
            var phRect = placeholderObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);
            var textTmp = textObj.AddComponent<TextMeshProUGUI>();
            textTmp.text = defaultText;
            textTmp.fontSize = 16;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;
            textTmp.raycastTarget = false;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.textViewport = areaRect;
            inputField.textComponent = textTmp;
            inputField.placeholder = phTmp;
            inputField.text = defaultText;

            return inputField;
        }

        private static Slider CreateSlider(string name, Transform parent, float min, float max, float defaultValue, Vector2 pos, Vector2 size)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = size;
            sliderRect.anchoredPosition = pos;

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.4f, 0.1f, 1f);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle Slide Area
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleArea.transform, false);
            var handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 20f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }
    }
}
#endif
