// Script: LobbyUI.cs
// Mục đích: Quản lý giao diện phòng chờ Lobby, hiển thị danh sách 2-4 người chơi, nút Start game cho Host (Mục 10.1 & 10.2).
// Môi trường thực thi: Cả hai (Hiển thị cho mọi client, Host có thêm quyền Start game).

using System.Collections.Generic;
using Hellfire.Networking;
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

        [Header("Player Slots (Max 4)")]
        [SerializeField] private List<TextMeshProUGUI> _playerSlotTexts = new List<TextMeshProUGUI>();

        [Header("Controls")]
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _leaveRoomButton;
        [SerializeField] private TextMeshProUGUI _waitingHostText;

        [Header("Scenes")]
        [SerializeField] private string _gameplaySceneName = "Gameplay";

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

            if (_leaveRoomButton != null)
            {
                _leaveRoomButton.onClick.RemoveAllListeners();
                _leaveRoomButton.onClick.AddListener(OnClickLeaveRoom);
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
        }

        private void Update()
        {
            UpdatePlayerSlots();
        }

        private void HandleClientListChanged(ulong clientId)
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

            if (_connectionInfoText != null && NetworkConnectManager.Instance != null)
            {
                _connectionInfoText.text = isHost
                    ? $"Đang làm Host (Cổng {NetworkConnectManager.Instance.CurrentPort})"
                    : "Đã kết nối với Host";
            }

            // Nút Bắt đầu chỉ hiển thị cho Host (Mục 10.1)
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
            }

            if (_waitingHostText != null)
            {
                _waitingHostText.gameObject.SetActive(!isHost);
            }

            UpdatePlayerSlots();
        }

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

                    string roleTag = isSlotHost ? " [Chủ phòng]" : "";
                    string youTag = isLocal ? " (Bạn)" : "";

                    _playerSlotTexts[i].text = $"Slot {i + 1}: Player #{id}{roleTag}{youTag}";
                    _playerSlotTexts[i].color = isLocal ? Color.green : Color.white;
                }
                else
                {
                    _playerSlotTexts[i].text = $"Slot {i + 1}: [Trống]";
                    _playerSlotTexts[i].color = Color.gray;
                }
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

            // Chuyển trạng thái GameManager sang Playing
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGameFromLobby();
            }

            // BẮT BUỘC dùng NetworkManager.SceneManager để đồng bộ chuyển Scene trên mọi máy (Mục 10.1)
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
    }
}
