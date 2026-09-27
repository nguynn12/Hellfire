// Script: GameManager.cs
// Mục đích: Quản lý trạng thái vòng lặp game (GameState) và đồng bộ giữa Server và Client qua NGO (Mục 10.1 & 10.2).
// Môi trường thực thi: Cả hai (Server-authoritative cập nhật state, Client quan sát).

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellfire.Networking
{
    /// <summary>
    /// Các trạng thái vòng lặp trò chơi theo mục 10.2 đặc tả kỹ thuật.
    /// Thứ tự chuyển trạng thái một chiều: Lobby -> Generating -> Playing -> BossFight -> Victory/GameOver -> Lobby.
    /// </summary>
    public enum GameState
    {
        Lobby,
        Generating,
        Playing,
        BossFight,
        Victory,
        GameOver
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState _initialState = GameState.Lobby;
        [SerializeField] private string _lobbySceneName = "Lobby";
        [SerializeField] private string _gameplaySceneName = "Gameplay";

        public NetworkVariable<GameState> CurrentState { get; } = new NetworkVariable<GameState>(
            GameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public event Action<GameState, GameState> OnGameStateChanged;

        private float _checkGameOverTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += HandleStateChanged;

            if (IsServer)
            {
                CurrentState.Value = _initialState;
            }
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState previousState, GameState newState)
        {
            Debug.Log($"[GameManager] Chuyển trạng thái game: {previousState} -> {newState}");
            OnGameStateChanged?.Invoke(previousState, newState);
        }

        private void Update()
        {
            if (!IsServer) return;

            // Kiểm tra điều kiện thất bại (GameOver) định kỳ mỗi 0.5s (Mục 3.3 & 10.2)
            if (CurrentState.Value == GameState.Playing || CurrentState.Value == GameState.BossFight)
            {
                _checkGameOverTimer += Time.deltaTime;
                if (_checkGameOverTimer >= 0.5f)
                {
                    _checkGameOverTimer = 0f;
                    CheckGameOverCondition();
                }
            }
        }

        private void CheckGameOverCondition()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClients.Count == 0)
            {
                return;
            }

            int totalPlayers = 0;
            int defeatedPlayers = 0;

            foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            {
                var client = kvp.Value;
                if (client != null && client.PlayerObject != null)
                {
                    totalPlayers++;
                    var health = client.PlayerObject.GetComponent<Health>();
                    if (health != null)
                    {
                        if (health.IsDead.Value || health.IsDowned.Value)
                        {
                            defeatedPlayers++;
                        }
                    }
                }
            }

            // Toàn bộ người chơi trong phòng cùng gục hoặc chết -> Game Over (Mục 3.3)
            if (totalPlayers > 0 && defeatedPlayers == totalPlayers)
            {
                Debug.Log("[GameManager] Toàn bộ người chơi đã gục ngã! Kích hoạt GameOver.");
                SetState(GameState.GameOver);
            }
        }

        /// <summary>
        /// Chỉ Server mới có quyền thay đổi trạng thái Game (Server-Authoritative).
        /// </summary>
        public void SetState(GameState newState)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameManager] Chỉ Server mới có quyền thay đổi GameState!");
                return;
            }

            if (CurrentState.Value == newState)
            {
                return;
            }

            CurrentState.Value = newState;
        }

        /// <summary>
        /// Kích hoạt khi Boss bị tiêu diệt (Mục 5.2 & 10.2).
        /// </summary>
        public void TriggerVictory()
        {
            if (!IsServer) return;
            Debug.Log("[GameManager] Chúa quỷ đã bị hạ gục! Kích hoạt Chiến Thắng (Victory).");
            SetState(GameState.Victory);
        }

        /// <summary>
        /// Bắt đầu lượt chơi từ Lobby sang Playing.
        /// </summary>
        public void StartGameFromLobby()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameManager] Chỉ Host mới có quyền bắt đầu game!");
                return;
            }

            SetState(GameState.Playing);
        }

        /// <summary>
        /// Quay về Sảnh Lobby cho lượt chơi mới (Mục 2.3 & 10.2).
        /// </summary>
        public void ReturnToLobby()
        {
            if (!IsServer)
            {
                ReturnToLobbyServerRpc();
                return;
            }

            SetState(GameState.Lobby);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                Debug.Log($"[GameManager] Host đang chuyển tất cả người chơi về Sảnh ({_lobbySceneName})...");
                NetworkManager.Singleton.SceneManager.LoadScene(_lobbySceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(_lobbySceneName);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
        {
            // Chỉ Host/Server xử lý yêu cầu chuyển scene
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId)
            {
                ReturnToLobby();
            }
        }

        public bool CanPlayerMove()
        {
            return CurrentState.Value == GameState.Playing || CurrentState.Value == GameState.BossFight;
        }
    }
}
