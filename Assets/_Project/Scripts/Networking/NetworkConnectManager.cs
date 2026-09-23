// Script: NetworkConnectManager.cs
// Mục đích: Quản lý vòng đời kết nối mạng LAN, Connection Approval giới hạn 4 người, và xử lý ngắt kết nối theo mục 2.2 & 2.2.1.
// Môi trường thực thi: Cả hai (Server quản lý phê duyệt và client kết nối/ngắt kết nối).

using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellfire.Networking
{
    [DisallowMultipleComponent]
    public class NetworkConnectManager : MonoBehaviour
    {
        public static NetworkConnectManager Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private ushort _defaultPort = 7777;
        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private string _lobbySceneName = "Lobby";
        [SerializeField] private string _gameplaySceneName = "Gameplay";
        [SerializeField] private GameObject _gameManagerPrefab;

        public int MaxPlayers => _maxPlayers;
        public ushort CurrentPort => _currentPort;
        public string CurrentRoomName => _currentRoomName;
        public string GameplaySceneName => _gameplaySceneName;
        public string DisconnectReason { get; private set; } = string.Empty;

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;
        public event Action<string> OnDisconnectedWithReason;

        private ushort _currentPort;
        private string _currentRoomName = "Hellfire Room";
        private bool _isShuttingDownIntentionally;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _currentPort = _defaultPort;
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                SubscribeToNetworkEvents();
            }
        }

        private void SubscribeToNetworkEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
        }

        public bool StartHost(string roomName = "Hellfire Room", ushort port = 7777)
        {
            _isShuttingDownIntentionally = false;
            DisconnectReason = string.Empty;
            _currentRoomName = string.IsNullOrWhiteSpace(roomName) ? "Hellfire Room" : roomName;
            _currentPort = port > 0 ? port : _defaultPort;

            ConfigureTransport("0.0.0.0", _currentPort);

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkConnectManager] Không tìm thấy NetworkManager!");
                return false;
            }

            // Thiết lập Connection Approval Callback để giới hạn tối đa 4 người (Mục 2.2)
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = ConnectionApprovalCheck;

            bool success = nm.StartHost();
            if (success)
            {
                Debug.Log($"[NetworkConnectManager] Đã khởi tạo Host thành công trên cổng {_currentPort}");
                if (LanDiscovery.Instance != null)
                {
                    LanDiscovery.Instance.StartBroadcasting(_currentRoomName, _currentPort, _maxPlayers, 1);
                }

                // Tải Lobby Scene qua NetworkSceneManager (Mục 10.1)
                if (nm.SceneManager != null)
                {
                    nm.SceneManager.LoadScene(_lobbySceneName, LoadSceneMode.Single);
                }
            }
            else
            {
                Debug.LogError("[NetworkConnectManager] Khởi tạo Host thất bại!");
            }

            return success;
        }

        public bool StartClient(string targetIp, ushort port = 7777)
        {
            _isShuttingDownIntentionally = false;
            DisconnectReason = string.Empty;
            _currentPort = port > 0 ? port : _defaultPort;

            string ipToUse = string.IsNullOrWhiteSpace(targetIp) ? "127.0.0.1" : targetIp.Trim();
            ConfigureTransport(ipToUse, _currentPort);

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkConnectManager] Không tìm thấy NetworkManager!");
                return false;
            }

            bool success = nm.StartClient();
            if (success)
            {
                Debug.Log($"[NetworkConnectManager] Đang kết nối tới Host: {ipToUse}:{_currentPort}");
                if (LanDiscovery.Instance != null)
                {
                    LanDiscovery.Instance.StopListening();
                }
            }
            else
            {
                Debug.LogError($"[NetworkConnectManager] Không thể bắt đầu Client kết nối tới {ipToUse}:{_currentPort}!");
            }

            return success;
        }

        public void Disconnect()
        {
            _isShuttingDownIntentionally = true;

            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.StopBroadcasting();
                LanDiscovery.Instance.StopListening();
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsHost || nm.IsServer || nm.IsClient))
            {
                nm.Shutdown();
            }

            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void ConfigureTransport(string ipAddress, ushort port)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null)
            {
                if (nm.NetworkConfig.NetworkTransport == null)
                {
                    nm.NetworkConfig.NetworkTransport = utp;
                }
                utp.SetConnectionData(ipAddress, port);
            }
            else
            {
                Debug.LogError("[NetworkConnectManager] Không tìm thấy UnityTransport trên NetworkManager!");
            }
        }

        /// <summary>
        /// Phê duyệt kết nối: Từ chối người chơi thứ 5 trở lên (BẮT BUỘC theo mục 2.2).
        /// </summary>
        private void ConnectionApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                response.Approved = false;
                response.Reason = "NetworkManager không hợp lệ.";
                return;
            }

            int currentConnected = nm.ConnectedClientsIds.Count;
            if (currentConnected >= _maxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Phòng đã đầy (tối đa {_maxPlayers} người).";
                Debug.LogWarning($"[NetworkConnectManager] Từ chối kết nối Client {request.ClientNetworkId} vì phòng đã đủ {_maxPlayers} người.");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            Debug.Log($"[NetworkConnectManager] Chấp thuận Client {request.ClientNetworkId}. Số người hiện tại: {currentConnected + 1}/{_maxPlayers}");
        }

        private void HandleServerStarted()
        {
            Debug.Log("[NetworkConnectManager] Server đã khởi động.");

            if (_gameManagerPrefab != null && GameManager.Instance == null)
            {
                var gmObj = Instantiate(_gameManagerPrefab);
                var netObj = gmObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
        }

        private void HandleServerStopped(bool wasHost)
        {
            Debug.Log($"[NetworkConnectManager] Server đã dừng. (WasHost: {wasHost})");
            if (!_isShuttingDownIntentionally)
            {
                TriggerDisconnectNotice("Mất kết nối với máy chủ.");
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkConnectManager] Client {clientId} đã kết nối.");
            OnPlayerConnected?.Invoke(clientId);

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.UpdatePlayerCount(nm.ConnectedClientsIds.Count);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            Debug.Log($"[NetworkConnectManager] Client {clientId} đã ngắt kết nối.");

            OnPlayerDisconnected?.Invoke(clientId);

            if (nm != null && nm.IsServer && LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.UpdatePlayerCount(nm.ConnectedClientsIds.Count);
            }

            // Xử lý mất kết nối theo Mục 2.2.1:
            // Nếu chính Client này bị mất kết nối tới Server (và không phải do chủ động bấm thoát):
            if (nm != null && !nm.IsServer && clientId == nm.LocalClientId)
            {
                if (!_isShuttingDownIntentionally)
                {
                    string reason = !string.IsNullOrEmpty(nm.DisconnectReason)
                        ? nm.DisconnectReason
                        : "Mất kết nối với chủ phòng.";

                    TriggerDisconnectNotice(reason);
                }
            }
        }

        private void TriggerDisconnectNotice(string reason)
        {
            DisconnectReason = reason;
            Debug.LogWarning($"[NetworkConnectManager] Điều hướng về Main Menu: {reason}");

            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.StopBroadcasting();
                LanDiscovery.Instance.StopListening();
            }

            OnDisconnectedWithReason?.Invoke(reason);
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }
    }
}
