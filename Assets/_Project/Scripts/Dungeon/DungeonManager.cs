// Script: DungeonManager.cs
// Mục đích: Quản lý vòng đời hầm ngục, đồng bộ Seed mạng qua NetworkVariable<int>, điều phối sinh map và bake NavMesh (Mục 2.3, 4.1, 4.2 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server gán Seed, mọi Client nhận và tự dựng map + bake NavMesh cục bộ).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Networking;
using Hellfire.Player;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class DungeonManager : NetworkBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private DungeonGenerator _generator;
        [SerializeField] private RuntimeNavMeshBaker _navMeshBaker;

        [Header("Debug / Fallback Seed")]
        [Tooltip("Nếu > 0, Server sẽ dùng seed này để test bản đồ cố định")]
        [SerializeField] private int _overrideSeed = 0;

        [Header("Loading Gate (Mục 15.1)")]
        [Tooltip("Thời gian chờ tối đa cho các client sẵn sàng hầm ngục (giây)")]
        [SerializeField] private float _readyTimeoutSeconds = 30f;

        /// <summary>
        /// Seed hầm ngục đồng bộ qua mạng (Mục 2.3: chỉ Server ghi, mọi client đọc).
        /// </summary>
        public NetworkVariable<int> DungeonSeed { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Số client đã sẵn sàng (đã dựng hình học và bake NavMesh) (Mục 15.1: chỉ Server ghi, mọi client đọc).
        /// </summary>
        public NetworkVariable<int> ReadyCount { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsDungeonReady { get; private set; }

        public event Action<int> OnDungeonReady;
        public event Action<int> OnAllClientsReady;

        private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();
        private Coroutine _loadingGateTimeoutCoroutine;
        private bool _gateCompleted = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (_generator == null)
            {
                _generator = GetComponent<DungeonGenerator>() ?? GetComponentInChildren<DungeonGenerator>();
            }

            if (_navMeshBaker == null)
            {
                _navMeshBaker = GetComponent<RuntimeNavMeshBaker>() ?? GetComponentInChildren<RuntimeNavMeshBaker>();
            }
        }

        public override void OnNetworkSpawn()
        {
            DungeonSeed.OnValueChanged += HandleSeedChanged;

            if (IsServer)
            {
                _gateCompleted = false;
                _readyClients.Clear();
                ReadyCount.Value = 0;

                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
                }

                _loadingGateTimeoutCoroutine = StartCoroutine(LoadingGateTimeoutRoutine());

                // Host sinh seed ngẫu nhiên (hoặc dùng overrideSeed nếu thiết lập)
                int seedToUse = _overrideSeed > 0 ? _overrideSeed : UnityEngine.Random.Range(100000, 999999);
                DungeonSeed.Value = seedToUse;
            }
            else
            {
                // Nếu là Client và Seed đã có giá trị sẵn
                if (DungeonSeed.Value != 0)
                {
                    StartCoroutine(BuildDungeonSequence(DungeonSeed.Value));
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            DungeonSeed.OnValueChanged -= HandleSeedChanged;

            if (IsServer)
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                }

                if (_loadingGateTimeoutCoroutine != null)
                {
                    StopCoroutine(_loadingGateTimeoutCoroutine);
                    _loadingGateTimeoutCoroutine = null;
                }
            }
        }

        private void HandleSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
            {
                StartCoroutine(BuildDungeonSequence(newValue));
            }
        }

        private IEnumerator BuildDungeonSequence(int seed)
        {
            IsDungeonReady = false;

            // 1. Dựng hình học bản đồ từ Seed
            if (_generator != null)
            {
                _generator.GenerateDungeon(seed);
            }

            // Chờ 1 frame cho Engine cập nhật GameObject hierarchy và Colliders
            yield return null;

            // 2. Bake NavMesh tại runtime cục bộ trên máy
            bool navMeshFinished = false;
            if (_navMeshBaker != null)
            {
                _navMeshBaker.BakeNavMesh(() => navMeshFinished = true);

                while (!navMeshFinished)
                {
                    yield return null;
                }
            }
            else
            {
                yield return null;
            }

            IsDungeonReady = true;
            OnDungeonReady?.Invoke(seed);

            // 3. Báo lên Server đã dựng xong bản đồ và bake xong NavMesh (Mục 15.1)
            if (IsServer)
            {
                RegisterClientReady(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            }
            else
            {
                DungeonReadyServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DungeonReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            RegisterClientReady(senderClientId);
        }

        private void RegisterClientReady(ulong clientId)
        {
            if (!IsServer || _gateCompleted) return;

            if (_readyClients.Add(clientId))
            {
                ReadyCount.Value = _readyClients.Count;
                Debug.Log($"[DungeonManager] Client {clientId} đã sẵn sàng hầm ngục ({ReadyCount.Value}/{GetConnectedClientCount()}).");
                CheckAllClientsReady();
            }
        }

        private int GetConnectedClientCount()
        {
            return NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 0;
        }

        private void CheckAllClientsReady()
        {
            if (!IsServer || _gateCompleted) return;

            int connectedCount = GetConnectedClientCount();
            if (connectedCount > 0 && _readyClients.Count >= connectedCount)
            {
                CompleteLoadingGate();
            }
        }

        private void CompleteLoadingGate()
        {
            if (!IsServer || _gateCompleted) return;
            _gateCompleted = true;

            if (_loadingGateTimeoutCoroutine != null)
            {
                StopCoroutine(_loadingGateTimeoutCoroutine);
                _loadingGateTimeoutCoroutine = null;
            }

            Debug.Log($"[DungeonManager] Đủ toàn bộ {_readyClients.Count} client! Mở cổng hầm ngục theo thứ tự (a) -> (b) -> (c).");

            // (a) Dịch chuyển người chơi về phòng spawn (Mục 2.1.1)
            PositionPlayersAtSpawn();

            // (b) Spawn quái / rương / boss (Mục 15.1)
            OnAllClientsReady?.Invoke(DungeonSeed.Value);

            // (c) Chuyển CurrentState = Playing
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }
        }

        private IEnumerator LoadingGateTimeoutRoutine()
        {
            yield return new WaitForSeconds(_readyTimeoutSeconds);

            if (!_gateCompleted && IsServer)
            {
                Debug.LogWarning($"[DungeonManager] Cổng chờ hết thời gian ({_readyTimeoutSeconds}s)! Kiểm tra các client chưa sẵn sàng...");

                if (NetworkManager.Singleton != null)
                {
                    var unreadyClients = new List<ulong>();
                    foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        if (!_readyClients.Contains(client.ClientId) && client.ClientId != NetworkManager.Singleton.LocalClientId)
                        {
                            unreadyClients.Add(client.ClientId);
                        }
                    }

                    foreach (var unreadyId in unreadyClients)
                    {
                        Debug.LogWarning($"[DungeonManager] Ngắt kết nối Client {unreadyId}: Tải bản đồ quá lâu.");
                        NetworkManager.Singleton.DisconnectClient(unreadyId);
                    }
                }

                CompleteLoadingGate();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer || _gateCompleted) return;

            if (_readyClients.Remove(clientId))
            {
                ReadyCount.Value = _readyClients.Count;
            }

            CheckAllClientsReady();
        }

        private void PositionPlayersAtSpawn()
        {
            if (_generator == null || _generator.ActiveGrid == null) return;

            var spawnRoom = _generator.ActiveGrid.SpawnRoom;
            if (spawnRoom == null) return;

            Vector3 spawnCenter = spawnRoom.WorldCenter + Vector3.up * 1.0f;

            // Dời các player hiện có trong NetworkManager về phòng Spawn an toàn theo Mục 2.1.1
            if (NetworkManager.Singleton != null)
            {
                int index = 0;
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        // Xếp so le vị trí các người chơi trong phòng spawn và kiểm tra NavMesh (Mục 15.2)
                        Vector3 offset = new Vector3((index % 2 == 0 ? 1f : -1f) * 2f, 0f, (index > 1 ? 2f : -2f));
                        Vector3 proposedPos = spawnCenter + offset;
                        Vector3 targetPos = DungeonEnemySpawner.GetValidSpawnPosition(
                            proposedPos, spawnCenter, "Phòng Spawn (Vị trí Player)", 3.0f
                        );

                        var movement = client.PlayerObject.GetComponent<PlayerMovement>();
                        if (movement != null)
                        {
                            var rpcParams = new ClientRpcParams
                            {
                                Send = new ClientRpcSendParams
                                {
                                    TargetClientIds = new[] { client.ClientId }
                                }
                            };
                            movement.TeleportOwnerClientRpc(targetPos, Quaternion.identity, rpcParams);
                        }
                        index++;
                    }
                }
            }
        }

        public Vector3 GetSpawnRoomCenter()
        {
            if (_generator != null && _generator.ActiveGrid != null && _generator.ActiveGrid.SpawnRoom != null)
            {
                return _generator.ActiveGrid.SpawnRoom.WorldCenter;
            }
            return Vector3.zero;
        }

        public Vector3 GetBossRoomCenter()
        {
            if (_generator != null && _generator.ActiveGrid != null && _generator.ActiveGrid.BossRoom != null)
            {
                return _generator.ActiveGrid.BossRoom.WorldCenter;
            }
            return Vector3.zero;
        }
    }
}
