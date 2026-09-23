// Script: GameManager.cs
// Mục đích: Quản lý trạng thái vòng lặp game (GameState) và đồng bộ giữa Server và Client qua NGO.
// Môi trường thực thi: Cả hai (Server-authoritative cập nhật state, Client quan sát).

using System;
using Unity.Netcode;
using UnityEngine;

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

        public NetworkVariable<GameState> CurrentState { get; } = new NetworkVariable<GameState>(
            GameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public event Action<GameState, GameState> OnGameStateChanged;

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
        /// Bắt đầu lượt chơi từ Lobby sang Playing (Giai đoạn 1 testbed).
        /// </summary>
        public void StartGameFromLobby()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameManager] Chỉ Host mới có quyền bắt đầu game!");
                return;
            }

            // Giai đoạn 1: chuyển thẳng từ Lobby sang Playing để test controller & LAN.
            // (Giai đoạn 3 sẽ chèn bước Generating để sinh bản đồ & bake NavMesh).
            SetState(GameState.Playing);
        }

        public bool CanPlayerMove()
        {
            return CurrentState.Value == GameState.Playing || CurrentState.Value == GameState.BossFight;
        }
    }
}
