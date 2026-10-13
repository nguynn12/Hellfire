// Script: LobbyUI.cs
// Mục đích: Quản lý giao diện phòng chờ Lobby, hiển thị danh sách người chơi kèm chip màu/số/tên, hệ thống Ready-up, và hiển thị IP LAN kèm nút Sao chép (Mục 16.1 & 16.10 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Hiển thị cho mọi client, Host có thêm quyền Start game).

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Hellfire.Networking;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class LobbyUI : MonoBehaviour
    {
        [Header("Room Info")]
        [SerializeField] private TextMeshProUGUI _roomTitleText;
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private TextMeshProUGUI _connectionInfoText;
        [SerializeField] private Button _copyIpButton;
        [SerializeField] private TextMeshProUGUI _copyFeedbackText;

        [Header("Player Slots (Max 4 - Mục 16.1)")]
        [SerializeField] private List<TextMeshProUGUI> _playerSlotTexts = new List<TextMeshProUGUI>();

        [Header("Controls (Mục 16.10)")]
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _startGameButtonText;
        [SerializeField] private Button _readyButton;
        [SerializeField] private TextMeshProUGUI _readyButtonText;
        [SerializeField] private Button _leaveRoomButton;
        [SerializeField] private TextMeshProUGUI _waitingHostText;

        [Header("Scenes")]
        [SerializeField] private string _gameplaySceneName = "Gameplay";

        private string _primaryHostIp = "127.0.0.1";

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            AutoResolveReferences();
            RegisterButtonListeners();
            UpdateUI();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientListChanged;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientListChanged;
            }

            PlayerIdentity.OnAnyPlayerIdentityChanged += HandleIdentityChanged;
        }

        private void AutoResolveReferences()
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform.root;

            if (_roomTitleText == null) _roomTitleText = FindComponentInHierarchy<TextMeshProUGUI>(root, "LobbyTitle");
            if (_connectionInfoText == null) _connectionInfoText = FindComponentInHierarchy<TextMeshProUGUI>(root, "ConnectionInfo");
            if (_playerCountText == null) _playerCountText = FindComponentInHierarchy<TextMeshProUGUI>(root, "PlayerCount");
            if (_waitingHostText == null) _waitingHostText = FindComponentInHierarchy<TextMeshProUGUI>(root, "WaitingHostText");
            if (_startGameButton == null) _startGameButton = FindComponentInHierarchy<Button>(root, "StartGameButton");
            if (_leaveRoomButton == null) _leaveRoomButton = FindComponentInHierarchy<Button>(root, "LeaveButton");

            if (_startGameButton != null && _startGameButtonText == null)
            {
                _startGameButtonText = _startGameButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Tự động tìm hoặc tạo nút Sẵn sàng (Ready) cho Client (Mục 16.10)
            if (_readyButton == null) _readyButton = FindComponentInHierarchy<Button>(root, "ReadyButton");
            if (_readyButton == null && _leaveRoomButton != null)
            {
                var readyObj = Instantiate(_leaveRoomButton.gameObject, _leaveRoomButton.transform.parent);
                readyObj.name = "ReadyButton";
                _readyButton = readyObj.GetComponent<Button>();
                _readyButtonText = readyObj.GetComponentInChildren<TextMeshProUGUI>();

                var rRect = readyObj.GetComponent<RectTransform>();
                var lRect = _leaveRoomButton.GetComponent<RectTransform>();
                if (rRect != null && lRect != null)
                {
                    rRect.anchoredPosition = lRect.anchoredPosition + new Vector2(0f, 55f);
                }

                var img = readyObj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(0.1f, 0.45f, 0.2f, 1f);
                }
            }
            else if (_readyButton != null && _readyButtonText == null)
            {
                _readyButtonText = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Tự động tìm hoặc tạo nút Sao chép IP (Mục 16.10)
            if (_copyIpButton == null) _copyIpButton = FindComponentInHierarchy<Button>(root, "CopyIpButton");
            if (_copyIpButton == null && _connectionInfoText != null)
            {
                var copyObj = new GameObject("CopyIpButton", typeof(RectTransform), typeof(Image), typeof(Button));
                copyObj.transform.SetParent(_connectionInfoText.transform.parent, false);

                var cRect = copyObj.GetComponent<RectTransform>();
                cRect.anchorMin = new Vector2(0.5f, 1f);
                cRect.anchorMax = new Vector2(0.5f, 1f);
                cRect.pivot = new Vector2(0.5f, 1f);
                cRect.anchoredPosition = new Vector2(210f, -85f);
                cRect.sizeDelta = new Vector2(100f, 28f);

                var img = copyObj.GetComponent<Image>();
                img.color = new Color(0.2f, 0.35f, 0.5f, 0.95f);

                _copyIpButton = copyObj.GetComponent<Button>();

                var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(copyObj.transform, false);
                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.text = "Sao chép IP";
                tmp.fontSize = 12;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                var tRect = textObj.GetComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.offsetMin = Vector2.zero;
                tRect.offsetMax = Vector2.zero;
            }

            if (_playerSlotTexts == null || _playerSlotTexts.Count == 0)
            {
                _playerSlotTexts = new List<TextMeshProUGUI>();
                for (int i = 1; i <= 4; i++)
                {
                    var slot = FindComponentInHierarchy<TextMeshProUGUI>(root, $"PlayerSlot_{i}");
                    if (slot != null) _playerSlotTexts.Add(slot);
                }
            }
        }

        private void RegisterButtonListeners()
        {
            if (_startGameButton != null)
            {
                _startGameButton.onClick.RemoveAllListeners();
                _startGameButton.onClick.AddListener(OnClickStartGame);
            }

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveAllListeners();
                _readyButton.onClick.AddListener(OnClickToggleReady);
            }

            if (_leaveRoomButton != null)
            {
                _leaveRoomButton.onClick.RemoveAllListeners();
                _leaveRoomButton.onClick.AddListener(OnClickLeaveRoom);
            }

            if (_copyIpButton != null)
            {
                _copyIpButton.onClick.RemoveAllListeners();
                _copyIpButton.onClick.AddListener(OnClickCopyIp);
            }
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

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientListChanged;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientListChanged;
            }

            PlayerIdentity.OnAnyPlayerIdentityChanged -= HandleIdentityChanged;
        }

        private void Update()
        {
            UpdatePlayerSlots();
            UpdateHostStartButtonState();
            UpdateClientReadyButtonState();
        }

        private void HandleClientListChanged(ulong clientId)
        {
            UpdateUI();
        }

        private void HandleIdentityChanged()
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            bool isHost = nm.IsServer;

            if (_roomTitleText != null && NetworkConnectManager.Instance != null)
            {
                _roomTitleText.text = NetworkConnectManager.Instance.CurrentRoomName;
            }

            // Hiển thị IP LAN của Host kèm nút sao chép (Mục 16.10)
            if (isHost)
            {
                var ipList = GetLocalLanIPv4Addresses();
                _primaryHostIp = ipList.Count > 0 ? ipList[0] : "127.0.0.1";
                ushort port = NetworkConnectManager.Instance != null ? NetworkConnectManager.Instance.CurrentPort : (ushort)7777;

                string ipDisplay = $"IP LAN: {_primaryHostIp}:{port}";
                if (ipList.Count > 1)
                {
                    ipDisplay += $" (Phụ: {string.Join(", ", ipList.GetRange(1, Mathf.Min(2, ipList.Count - 1)))})";
                }

                if (_connectionInfoText != null)
                {
                    _connectionInfoText.text = ipDisplay;
                }

                if (_copyIpButton != null)
                {
                    _copyIpButton.gameObject.SetActive(true);
                }
            }
            else
            {
                if (_connectionInfoText != null)
                {
                    _connectionInfoText.text = "Đã kết nối với Host";
                }

                if (_copyIpButton != null)
                {
                    _copyIpButton.gameObject.SetActive(false);
                }
            }

            // Nút Bắt đầu chỉ hiển thị cho Host (Mục 10.1 & 16.10)
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
            }

            // Nút Sẵn sàng chỉ hiển thị cho Client (Mục 16.10: "Host mặc định coi như sẵn sàng")
            if (_readyButton != null)
            {
                _readyButton.gameObject.SetActive(!isHost);
            }

            if (_waitingHostText != null)
            {
                _waitingHostText.gameObject.SetActive(!isHost);
            }

            UpdatePlayerSlots();
            UpdateHostStartButtonState();
            UpdateClientReadyButtonState();
        }

        /// <summary>
        /// Host chỉ bấm được Bắt đầu khi mọi người chơi khác đã sẵn sàng (Mục 16.10).
        /// Cho phép bắt đầu khi chỉ có một mình Host (số người = 1).
        /// </summary>
        private void UpdateHostStartButtonState()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || _startGameButton == null) return;

            int connectedCount = nm.ConnectedClientsIds.Count;

            // Nếu chỉ có 1 mình Host -> Cho phép bắt đầu ngay
            if (connectedCount <= 1)
            {
                _startGameButton.interactable = true;
                if (_startGameButtonText != null)
                {
                    _startGameButtonText.text = "BẮT ĐẦU GAME (CHƠI ĐƠN)";
                }
                return;
            }

            // Kiểm tra xem mọi client khác đã sẵn sàng chưa
            bool allOthersReady = true;
            foreach (ulong clientId in nm.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId) continue;

                var identity = PlayerIdentity.GetByClientId(clientId);
                if (identity == null || !identity.IsReady.Value)
                {
                    allOthersReady = false;
                    break;
                }
            }

            _startGameButton.interactable = allOthersReady;
            if (_startGameButtonText != null)
            {
                _startGameButtonText.text = allOthersReady
                    ? "BẮT ĐẦU GAME (HOST)"
                    : "CHỜ NGƯỜI CHƠI SẴN SÀNG...";
            }
        }

        /// <summary>
        /// Cập nhật hiển thị nút Sẵn sàng của Client.
        /// </summary>
        private void UpdateClientReadyButtonState()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || _readyButton == null) return;

            var localIdentity = PlayerIdentity.LocalIdentity;
            bool isReady = localIdentity != null && localIdentity.IsReady.Value;

            if (_readyButtonText != null)
            {
                _readyButtonText.text = isReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
            }

            var btnImg = _readyButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = isReady ? new Color(0.7f, 0.25f, 0.1f, 1f) : new Color(0.1f, 0.5f, 0.25f, 1f);
            }
        }

        /// <summary>
        /// Cập nhật danh sách slot người chơi hiển thị chip màu + số + tên + dấu sẵn sàng (Mục 16.1 & 16.10).
        /// </summary>
        private void UpdatePlayerSlots()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            int connectedCount = nm.ConnectedClientsIds.Count;
            int maxPlayers = NetworkConnectManager.Instance != null ? NetworkConnectManager.Instance.MaxPlayers : 4;

            if (_playerCountText != null)
            {
                _playerCountText.text = $"Người chơi: {connectedCount}/{maxPlayers}";
            }

            var clientIds = new List<ulong>(nm.ConnectedClientsIds);

            for (int i = 0; i < _playerSlotTexts.Count; i++)
            {
                if (_playerSlotTexts[i] == null) continue;

                if (i < clientIds.Count)
                {
                    ulong id = clientIds[i];
                    bool isSlotHost = id == NetworkManager.ServerClientId;
                    bool isLocal = id == nm.LocalClientId;

                    var identity = PlayerIdentity.GetByClientId(id);
                    if (identity != null)
                    {
                        int colorIndex = identity.ColorIndex.Value;
                        int number = PlayerPalette.GetPlayerNumber(colorIndex);
                        string hex = PlayerPalette.GetColorHex(colorIndex);
                        string playerName = identity.PlayerName.Value.ToString();
                        bool isReady = identity.IsReady.Value;

                        string statusTag = isSlotHost
                            ? "<color=#00FF00>[✓ Chủ phòng]</color>"
                            : (isReady ? "<color=#00FF00>[✓ Sẵn sàng]</color>" : "<color=#FFA500>[Chưa sẵn sàng]</color>");

                        string youTag = isLocal ? " (Bạn)" : "";

                        // Format chuẩn: Slot N: [Số] Tên [Trạng thái] (Mục 16.1)
                        _playerSlotTexts[i].text = $"Slot {i + 1}: <color={hex}><b>[{number}] {playerName}</b></color> {statusTag}{youTag}";
                        _playerSlotTexts[i].color = Color.white;
                    }
                    else
                    {
                        string roleTag = isSlotHost ? " [Chủ phòng]" : "";
                        string youTag = isLocal ? " (Bạn)" : "";
                        _playerSlotTexts[i].text = $"Slot {i + 1}: Player #{id}{roleTag}{youTag} <color=#FFA500>[Đang kết nối...]</color>";
                        _playerSlotTexts[i].color = isLocal ? Color.green : Color.white;
                    }
                }
                else
                {
                    _playerSlotTexts[i].text = $"Slot {i + 1}: [Trống]";
                    _playerSlotTexts[i].color = Color.gray;
                }
            }
        }

        public void OnClickToggleReady()
        {
            var localIdentity = PlayerIdentity.LocalIdentity;
            if (localIdentity != null)
            {
                bool currentReady = localIdentity.IsReady.Value;
                localIdentity.SetReadyServerRpc(!currentReady);
            }
        }

        public void OnClickCopyIp()
        {
            ushort port = NetworkConnectManager.Instance != null ? NetworkConnectManager.Instance.CurrentPort : (ushort)7777;
            string fullIp = $"{_primaryHostIp}:{port}";
            GUIUtility.systemCopyBuffer = fullIp;

            if (_connectionInfoText != null)
            {
                _connectionInfoText.text = $"<color=#00FF00>Đã sao chép: {fullIp}</color>";
            }
        }

        public void OnClickStartGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                Debug.LogWarning("[LobbyUI] Chỉ Host mới có quyền bắt đầu game!");
                return;
            }

            // Kiểm tra lại tính sẵn sàng trước khi cho phép load scene
            int connectedCount = nm.ConnectedClientsIds.Count;
            if (connectedCount > 1)
            {
                foreach (ulong clientId in nm.ConnectedClientsIds)
                {
                    if (clientId == NetworkManager.ServerClientId) continue;
                    var identity = PlayerIdentity.GetByClientId(clientId);
                    if (identity == null || !identity.IsReady.Value)
                    {
                        Debug.LogWarning("[LobbyUI] Vẫn còn người chơi chưa sẵn sàng!");
                        return;
                    }
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGameFromLobby();
            }

            if (nm.SceneManager != null)
            {
                Debug.Log($"[LobbyUI] Host bắt đầu chuyển scene sang {_gameplaySceneName} qua NetworkSceneManager...");
                nm.SceneManager.LoadScene(_gameplaySceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[LobbyUI] NetworkSceneManager không khả dụng!");
            }
        }

        public void OnClickLeaveRoom()
        {
            if (NetworkConnectManager.Instance != null)
            {
                NetworkConnectManager.Instance.Disconnect();
            }
        }

        /// <summary>
        /// Lấy tối đa 3 địa chỉ IPv4 LAN thuộc dải riêng (192.168.x.x, 10.x.x.x, 172.16-31.x.x) theo mục 16.10.
        /// </summary>
        public static List<string> GetLocalLanIPv4Addresses()
        {
            var results = new List<string>();
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        byte[] b = ip.GetAddressBytes();
                        // 10.x.x.x
                        if (b[0] == 10)
                        {
                            results.Add(ip.ToString());
                        }
                        // 172.16.x.x - 172.31.x.x
                        else if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                        {
                            results.Add(ip.ToString());
                        }
                        // 192.168.x.x
                        else if (b[0] == 192 && b[1] == 168)
                        {
                            results.Add(ip.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyUI] Không thể đọc danh sách IP LAN: {ex.Message}");
            }

            if (results.Count == 0)
            {
                results.Add("127.0.0.1");
            }

            if (results.Count > 3)
            {
                results = results.GetRange(0, 3);
            }

            return results;
        }
    }
}
