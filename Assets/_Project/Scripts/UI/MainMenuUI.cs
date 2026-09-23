// Script: MainMenuUI.cs
// Mục đích: Quản lý giao diện Main Menu (uGUI Canvas), bao gồm Tạo phòng (Host), Vào phòng (Join IP / Quét LAN), và Cài đặt (Mục 7 & 10.1).
// Môi trường thực thi: Client-only.

using System.Collections.Generic;
using Hellfire.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Panel Buttons")]
        [SerializeField] private Button _mainHostButton;
        [SerializeField] private Button _mainJoinIpButton;
        [SerializeField] private Button _mainScanLanButton;
        [SerializeField] private Button _mainSettingsButton;
        [SerializeField] private Button _mainQuitButton;

        [Header("Panels")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _hostPanel;
        [SerializeField] private GameObject _joinIpPanel;
        [SerializeField] private GameObject _lanScanPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _disconnectModal;

        [Header("Host Panel Inputs & Buttons")]
        [SerializeField] private TMP_InputField _hostRoomNameInput;
        [SerializeField] private TMP_InputField _hostPortInput;
        [SerializeField] private Button _hostStartButton;
        [SerializeField] private Button _hostBackButton;

        [Header("Join IP Panel Inputs & Buttons")]
        [SerializeField] private TMP_InputField _joinIpInput;
        [SerializeField] private TMP_InputField _joinPortInput;
        [SerializeField] private Button _joinConnectButton;
        [SerializeField] private Button _joinIpBackButton;

        [Header("LAN Scan Panel")]
        [SerializeField] private Transform _roomListContent;
        [SerializeField] private GameObject _roomItemPrefab;
        [SerializeField] private Button _scanRefreshButton;
        [SerializeField] private Button _scanBackButton;
        [SerializeField] private TextMeshProUGUI _scanStatusText;

        [Header("Settings Panel")]
        [SerializeField] private Slider _mouseSensitivitySlider;
        [SerializeField] private TextMeshProUGUI _mouseSensitivityValueText;
        [SerializeField] private Button _settingsBackButton;

        [Header("Disconnect Modal")]
        [SerializeField] private TextMeshProUGUI _disconnectReasonText;
        [SerializeField] private Button _disconnectModalOkButton;

        [Header("Status Feedback")]
        [SerializeField] private TextMeshProUGUI _statusMessageText;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            AutoResolveReferences();
            RegisterButtonListeners();
            ShowMainPanel();
            CheckForDisconnectNotice();
            LoadSettings();

            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.OnRoomListUpdated += UpdateLanRoomList;
            }
        }

        private void AutoResolveReferences()
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform.root;

            // Panels
            if (_mainPanel == null) _mainPanel = FindInHierarchy(root, "MainPanel");
            if (_hostPanel == null) _hostPanel = FindInHierarchy(root, "HostPanel");
            if (_joinIpPanel == null) _joinIpPanel = FindInHierarchy(root, "JoinIpPanel");
            if (_lanScanPanel == null) _lanScanPanel = FindInHierarchy(root, "LanScanPanel");
            if (_settingsPanel == null) _settingsPanel = FindInHierarchy(root, "SettingsPanel");
            if (_disconnectModal == null) _disconnectModal = FindInHierarchy(root, "DisconnectModal");

            // Main Buttons
            if (_mainHostButton == null) _mainHostButton = FindComponentInHierarchy<Button>(root, "HostButton");
            if (_mainJoinIpButton == null) _mainJoinIpButton = FindComponentInHierarchy<Button>(root, "JoinIpButton");
            if (_mainScanLanButton == null) _mainScanLanButton = FindComponentInHierarchy<Button>(root, "ScanLanButton");
            if (_mainSettingsButton == null) _mainSettingsButton = FindComponentInHierarchy<Button>(root, "SettingsButton");
            if (_mainQuitButton == null) _mainQuitButton = FindComponentInHierarchy<Button>(root, "QuitButton");

            // Host Panel
            if (_hostStartButton == null) _hostStartButton = FindComponentInHierarchy<Button>(root, "HostStartBtn");
            if (_hostBackButton == null) _hostBackButton = FindComponentInHierarchy<Button>(root, "HostBackBtn");
            if (_hostRoomNameInput == null) _hostRoomNameInput = FindComponentInHierarchy<TMP_InputField>(root, "HostRoomNameInput");
            if (_hostPortInput == null) _hostPortInput = FindComponentInHierarchy<TMP_InputField>(root, "HostPortInput");

            // Join IP Panel
            if (_joinConnectButton == null) _joinConnectButton = FindComponentInHierarchy<Button>(root, "JoinConnectBtn");
            if (_joinIpBackButton == null) _joinIpBackButton = FindComponentInHierarchy<Button>(root, "JoinIpBackBtn");
            if (_joinIpInput == null) _joinIpInput = FindComponentInHierarchy<TMP_InputField>(root, "JoinIpInput");
            if (_joinPortInput == null) _joinPortInput = FindComponentInHierarchy<TMP_InputField>(root, "JoinPortInput");

            // LAN Scan Panel
            if (_scanRefreshButton == null) _scanRefreshButton = FindComponentInHierarchy<Button>(root, "ScanRefreshBtn");
            if (_scanBackButton == null) _scanBackButton = FindComponentInHierarchy<Button>(root, "ScanBackBtn");
            if (_scanStatusText == null) _scanStatusText = FindComponentInHierarchy<TextMeshProUGUI>(root, "ScanStatus");
            if (_roomListContent == null) _roomListContent = FindInHierarchy(root, "RoomScrollContent")?.transform;

            // Settings & Modals
            if (_settingsBackButton == null) _settingsBackButton = FindComponentInHierarchy<Button>(root, "SettingsBackBtn");
            if (_mouseSensitivitySlider == null) _mouseSensitivitySlider = FindComponentInHierarchy<Slider>(root, "SensitivitySlider");
            if (_mouseSensitivityValueText == null) _mouseSensitivityValueText = FindComponentInHierarchy<TextMeshProUGUI>(root, "SensitivityValue");
            if (_disconnectReasonText == null) _disconnectReasonText = FindComponentInHierarchy<TextMeshProUGUI>(root, "DisconnectReason");
            if (_disconnectModalOkButton == null) _disconnectModalOkButton = FindComponentInHierarchy<Button>(root, "DisconnectOkBtn");

            // Status
            if (_statusMessageText == null) _statusMessageText = FindComponentInHierarchy<TextMeshProUGUI>(root, "StatusText");
        }

        private GameObject FindInHierarchy(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent.gameObject;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindInHierarchy(parent.GetChild(i), targetName);
                if (result != null) return result;
            }
            return null;
        }

        private T FindComponentInHierarchy<T>(Transform parent, string targetName) where T : Component
        {
            var obj = FindInHierarchy(parent, targetName);
            return obj != null ? obj.GetComponent<T>() : null;
        }

        private void RegisterButtonListeners()
        {
            // Main Panel
            if (_mainHostButton != null)
            {
                _mainHostButton.onClick.RemoveAllListeners();
                _mainHostButton.onClick.AddListener(ShowHostPanel);
            }
            if (_mainJoinIpButton != null)
            {
                _mainJoinIpButton.onClick.RemoveAllListeners();
                _mainJoinIpButton.onClick.AddListener(ShowJoinIpPanel);
            }
            if (_mainScanLanButton != null)
            {
                _mainScanLanButton.onClick.RemoveAllListeners();
                _mainScanLanButton.onClick.AddListener(ShowLanScanPanel);
            }
            if (_mainSettingsButton != null)
            {
                _mainSettingsButton.onClick.RemoveAllListeners();
                _mainSettingsButton.onClick.AddListener(ShowSettingsPanel);
            }
            if (_mainQuitButton != null)
            {
                _mainQuitButton.onClick.RemoveAllListeners();
                _mainQuitButton.onClick.AddListener(OnClickQuit);
            }

            // Host Panel
            if (_hostStartButton != null)
            {
                _hostStartButton.onClick.RemoveAllListeners();
                _hostStartButton.onClick.AddListener(OnClickHost);
            }
            if (_hostBackButton != null)
            {
                _hostBackButton.onClick.RemoveAllListeners();
                _hostBackButton.onClick.AddListener(ShowMainPanel);
            }

            // Join IP Panel
            if (_joinConnectButton != null)
            {
                _joinConnectButton.onClick.RemoveAllListeners();
                _joinConnectButton.onClick.AddListener(OnClickJoinDirectIp);
            }
            if (_joinIpBackButton != null)
            {
                _joinIpBackButton.onClick.RemoveAllListeners();
                _joinIpBackButton.onClick.AddListener(ShowMainPanel);
            }

            // LAN Scan Panel
            if (_scanRefreshButton != null)
            {
                _scanRefreshButton.onClick.RemoveAllListeners();
                _scanRefreshButton.onClick.AddListener(RefreshLanScan);
            }
            if (_scanBackButton != null)
            {
                _scanBackButton.onClick.RemoveAllListeners();
                _scanBackButton.onClick.AddListener(ShowMainPanel);
            }

            // Settings Panel
            if (_settingsBackButton != null)
            {
                _settingsBackButton.onClick.RemoveAllListeners();
                _settingsBackButton.onClick.AddListener(ShowMainPanel);
            }
            if (_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
                _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            }

            // Disconnect Modal
            if (_disconnectModalOkButton != null)
            {
                _disconnectModalOkButton.onClick.RemoveAllListeners();
                _disconnectModalOkButton.onClick.AddListener(CloseDisconnectModal);
            }
        }

        private void OnDestroy()
        {
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.OnRoomListUpdated -= UpdateLanRoomList;
                LanDiscovery.Instance.StopListening();
            }
        }

        public void ShowMainPanel()
        {
            SetPanelActive(_mainPanel);
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.StopListening();
            }
        }

        public void ShowHostPanel()
        {
            SetPanelActive(_hostPanel);
            if (_hostRoomNameInput != null && string.IsNullOrEmpty(_hostRoomNameInput.text))
            {
                _hostRoomNameInput.text = "Hellfire LAN Match";
            }
            if (_hostPortInput != null && string.IsNullOrEmpty(_hostPortInput.text))
            {
                _hostPortInput.text = "7777";
            }
        }

        public void ShowJoinIpPanel()
        {
            SetPanelActive(_joinIpPanel);
            if (_joinIpInput != null && string.IsNullOrEmpty(_joinIpInput.text))
            {
                _joinIpInput.text = "127.0.0.1";
            }
            if (_joinPortInput != null && string.IsNullOrEmpty(_joinPortInput.text))
            {
                _joinPortInput.text = "7777";
            }
        }

        public void ShowLanScanPanel()
        {
            SetPanelActive(_lanScanPanel);
            RefreshLanScan();
        }

        public void ShowSettingsPanel()
        {
            SetPanelActive(_settingsPanel);
        }

        private void SetPanelActive(GameObject targetPanel)
        {
            if (_mainPanel != null) _mainPanel.SetActive(_mainPanel == targetPanel);
            if (_hostPanel != null) _hostPanel.SetActive(_hostPanel == targetPanel);
            if (_joinIpPanel != null) _joinIpPanel.SetActive(_joinIpPanel == targetPanel);
            if (_lanScanPanel != null) _lanScanPanel.SetActive(_lanScanPanel == targetPanel);
            if (_settingsPanel != null) _settingsPanel.SetActive(_settingsPanel == targetPanel);
        }

        public void OnClickHost()
        {
            string roomName = _hostRoomNameInput != null ? _hostRoomNameInput.text : "Hellfire LAN Match";
            ushort port = 7777;
            if (_hostPortInput != null && ushort.TryParse(_hostPortInput.text, out var p))
            {
                port = p;
            }

            SetStatus("Đang khởi tạo Host...");
            if (NetworkConnectManager.Instance != null)
            {
                bool ok = NetworkConnectManager.Instance.StartHost(roomName, port);
                if (!ok)
                {
                    SetStatus("Khởi tạo Host thất bại! Vui lòng kiểm tra lại cổng mạng.");
                }
            }
            else
            {
                Debug.LogError("[MainMenuUI] NetworkConnectManager.Instance is null!");
            }
        }

        public void OnClickJoinDirectIp()
        {
            string ip = _joinIpInput != null ? _joinIpInput.text : "127.0.0.1";
            ushort port = 7777;
            if (_joinPortInput != null && ushort.TryParse(_joinPortInput.text, out var p))
            {
                port = p;
            }

            SetStatus($"Đang kết nối tới {ip}:{port}...");
            if (NetworkConnectManager.Instance != null)
            {
                bool ok = NetworkConnectManager.Instance.StartClient(ip, port);
                if (!ok)
                {
                    SetStatus("Không thể kết nối tới máy chủ.");
                }
            }
        }

        public void RefreshLanScan()
        {
            if (LanDiscovery.Instance != null)
            {
                SetScanStatus("Đang quét các phòng trong mạng LAN...");
                ClearRoomList();
                LanDiscovery.Instance.StartListening();
            }
        }

        private void UpdateLanRoomList(IReadOnlyList<LanRoomInfo> rooms)
        {
            ClearRoomList();

            if (rooms == null || rooms.Count == 0)
            {
                SetScanStatus("Chưa tìm thấy phòng nào trong mạng LAN.");
                return;
            }

            SetScanStatus($"Tìm thấy {rooms.Count} phòng trong mạng LAN:");

            foreach (var room in rooms)
            {
                if (_roomListContent == null) continue;

                var roomItemObj = new GameObject($"Room_{room.HostIp}_{room.Port}");
                roomItemObj.transform.SetParent(_roomListContent, false);

                var img = roomItemObj.AddComponent<Image>();
                img.color = new Color(0.18f, 0.12f, 0.15f, 0.9f);

                var btn = roomItemObj.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.4f, 0.2f, 0.2f, 1f);
                colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
                btn.colors = colors;

                var rect = roomItemObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(500f, 45f);

                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(roomItemObj.transform, false);
                var tmp = txtObj.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{room.RoomName} ({room.HostIp}:{room.Port}) [{room.CurrentPlayers}/{room.MaxPlayers}]";
                tmp.fontSize = 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                var txtRect = txtObj.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;

                var capturedRoom = room;
                btn.onClick.AddListener(() =>
                {
                    SetStatus($"Đang tham gia phòng {capturedRoom.RoomName}...");
                    if (NetworkConnectManager.Instance != null)
                    {
                        NetworkConnectManager.Instance.StartClient(capturedRoom.HostIp, capturedRoom.Port);
                    }
                });
            }
        }

        private void ClearRoomList()
        {
            if (_roomListContent == null) return;
            foreach (Transform child in _roomListContent)
            {
                Destroy(child.gameObject);
            }
        }

        public void OnMouseSensitivityChanged(float value)
        {
            PlayerPrefs.SetFloat("MouseSensitivity", value);
            if (_mouseSensitivityValueText != null)
            {
                _mouseSensitivityValueText.text = value.ToString("F1");
            }
        }

        private void LoadSettings()
        {
            float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
            if (_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.value = sensitivity;
            }
            if (_mouseSensitivityValueText != null)
            {
                _mouseSensitivityValueText.text = sensitivity.ToString("F1");
            }
        }

        private void CheckForDisconnectNotice()
        {
            if (NetworkConnectManager.Instance != null && !string.IsNullOrEmpty(NetworkConnectManager.Instance.DisconnectReason))
            {
                ShowDisconnectModal(NetworkConnectManager.Instance.DisconnectReason);
            }
        }

        public void ShowDisconnectModal(string reason)
        {
            if (_disconnectModal != null)
            {
                _disconnectModal.SetActive(true);
                if (_disconnectReasonText != null)
                {
                    _disconnectReasonText.text = reason;
                }
            }
        }

        public void CloseDisconnectModal()
        {
            if (_disconnectModal != null)
            {
                _disconnectModal.SetActive(false);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = message;
            }
        }

        private void SetScanStatus(string message)
        {
            if (_scanStatusText != null)
            {
                _scanStatusText.text = message;
            }
        }

        public void OnClickQuit()
        {
            Application.Quit();
        }
    }
}
