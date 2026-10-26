// Script: DungeonManager.cs
// Mục đích: Quản lý vòng đời hầm ngục, đồng bộ Seed mạng qua NetworkVariable<int>, điều phối sinh map và bake NavMesh (Mục 2.3, 4.1, 4.2 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server gán Seed, mọi Client nhận và tự dựng map + bake NavMesh cục bộ).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Networking;
using Hellfire.Player;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Dungeon
{
    public enum RoomLockState
    {
        Idle = 0,
        Locked = 1,
        Cleared = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class DungeonManager : NetworkBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private DungeonGenerator _generator;
        [SerializeField] private RuntimeNavMeshBaker _navMeshBaker;
        [SerializeField] private DungeonEnemySpawner _enemySpawner;

        public DungeonGenerator Generator => _generator;
        public DungeonEnemySpawner EnemySpawner => _enemySpawner;

        private readonly Dictionary<Vector2Int, RoomLockState> _roomLockStates = new Dictionary<Vector2Int, RoomLockState>();
        private readonly HashSet<Vector2Int> _roomsFinishedSpawning = new HashSet<Vector2Int>();
        private Coroutine _roomLockRoutine;

        [Header("Room Rewards (Mục 17.7)")]
        [Tooltip("Thưởng Linh hồn cho mỗi người chơi còn sống khi phòng Combat Cleared (Mục 17.7)")]
        [SerializeField] private int _combatRoomClearSoulsBonus = 20;

        [Header("Special Rooms (Mục 17.6)")]
        [SerializeField] private GameObject _shrinePrefab;
        [SerializeField] private GameObject _lootChestPrefab;
        [SerializeField] private int _treasureChestCount = 2;

        [Header("Shop Room (Mục 17.8)")]
        [SerializeField] private GameObject _shopStationPrefab;

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

            if (_enemySpawner == null)
            {
                _enemySpawner = GetComponent<DungeonEnemySpawner>() ?? GetComponentInChildren<DungeonEnemySpawner>();
            }
        }

        public override void OnNetworkSpawn()
        {
            ClearedRoomsClient.Clear();
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

                if (_roomLockRoutine != null)
                {
                    StopCoroutine(_roomLockRoutine);
                    _roomLockRoutine = null;
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

            // (b) Spawn phòng đặc biệt: Upgrade (InteractiveShrine) & Treasure (LootChest) (Mục 17.6)
            SpawnSpecialRoomsContents();

            // (c) Spawn quái / rương / boss (Mục 15.1)
            OnAllClientsReady?.Invoke(DungeonSeed.Value);

            // (d) Chuyển CurrentState = Playing
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            // (e) Khởi tạo trạng thái phòng và cổng sắt (Mục 17.4)
            InitializeRoomGatesAndLockStates();
        }

        /// <summary>
        /// Sinh nội dung cho các phòng đặc biệt (Upgrade, Treasure) theo mục 17.6.
        /// </summary>
        private void SpawnSpecialRoomsContents()
        {
            if (!IsServer || _generator == null || _generator.ActiveGrid == null) return;

            var grid = _generator.ActiveGrid;

            foreach (var kvp in grid.Rooms)
            {
                var room = kvp.Value;
                if (room == null) continue;

                if (room.RoomType == DungeonRoomType.Upgrade)
                {
                    SpawnUpgradeRoomShrine(room);
                }
                else if (room.RoomType == DungeonRoomType.Treasure)
                {
                    SpawnTreasureRoomChests(room);
                }
                else if (room.RoomType == DungeonRoomType.Shop)
                {
                    SpawnShopStation(room);
                }
            }
        }

        private void SpawnUpgradeRoomShrine(DungeonRoomNode room)
        {
            if (_shrinePrefab == null)
            {
                Debug.LogWarning("[DungeonManager] Thiếu _shrinePrefab khi tạo phòng Upgrade!");
                return;
            }

            Vector3 shrinePos = room.WorldCenter;
            if (UnityEngine.AI.NavMesh.SamplePosition(shrinePos, out var hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                shrinePos = hit.position;
            }

            var shrineObj = Instantiate(_shrinePrefab, shrinePos, Quaternion.identity);
            int shrineId = room.GridPosition.x * 1000 + room.GridPosition.y;

            var shrine = shrineObj.GetComponent<Hellfire.Items.InteractiveShrine>();
            if (shrine != null)
            {
                shrine.SetShrineId(shrineId);
            }

            var netObj = shrineObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }

            Debug.Log($"[DungeonManager] Đã spawn InteractiveShrine (ID: {shrineId}) tại tâm phòng Upgrade {room.GridPosition} ({shrinePos})");
        }

        private void SpawnTreasureRoomChests(DungeonRoomNode room)
        {
            if (_lootChestPrefab == null)
            {
                Debug.LogWarning("[DungeonManager] Thiếu _lootChestPrefab khi tạo phòng Treasure!");
                return;
            }

            int count = Mathf.Max(1, _treasureChestCount);
            float spacing = 2.5f;

            for (int i = 0; i < count; i++)
            {
                // Đặt đối xứng quanh tâm phòng (Mục 17.6)
                float xOffset = (i == 0) ? -spacing : spacing;
                Vector3 proposedPos = room.WorldCenter + new Vector3(xOffset, 0f, 0f);
                Vector3 chestPos = proposedPos;

                if (UnityEngine.AI.NavMesh.SamplePosition(proposedPos, out var hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    chestPos = hit.position;
                }

                Vector3 lookDir = (room.WorldCenter - chestPos);
                lookDir.y = 0;
                Quaternion rot = (lookDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                var chestObj = Instantiate(_lootChestPrefab, chestPos, rot);
                var chest = chestObj.GetComponent<Hellfire.Items.LootChest>();
                if (chest != null)
                {
                    chest.IsBossChest = false;
                }

                var netObj = chestObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }

                Debug.Log($"[DungeonManager] Đã spawn LootChest {i + 1}/{count} tại phòng Treasure {room.GridPosition} ({chestPos})");
            }
        }

        private void SpawnShopStation(DungeonRoomNode room)
        {
            Vector3 stationPos = room.WorldCenter;
            if (UnityEngine.AI.NavMesh.SamplePosition(stationPos, out var hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                stationPos = hit.position;
            }

            GameObject stationObj = null;
            if (_shopStationPrefab != null)
            {
                stationObj = Instantiate(_shopStationPrefab, stationPos, Quaternion.identity);
            }
            else
            {
                stationObj = CreateDefaultShopStationObject(stationPos);
            }

            var netObj = stationObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }

            Debug.Log($"[DungeonManager] Đã spawn ShopStation tại tâm phòng Shop {room.GridPosition} ({stationPos})");
        }

        private GameObject CreateDefaultShopStationObject(Vector3 pos)
        {
            var root = new GameObject("ShopStation");
            root.transform.position = pos;
            root.AddComponent<NetworkObject>();

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.0f, 0f);
            col.size = new Vector3(2.5f, 2.0f, 2.5f);

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;

            // Pedestal Base
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "ShopCounterBase";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            baseObj.transform.localScale = new Vector3(1.6f, 0.4f, 1.6f);
            Destroy(baseObj.GetComponent<Collider>());
            var renderer = baseObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.18f, 0.18f, 0.22f);
                renderer.material = mat;
            }

            // Spinning Gold Coin Visual
            var coinObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinObj.name = "SpinningCoin";
            coinObj.transform.SetParent(root.transform, false);
            coinObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            coinObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coinObj.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
            Destroy(coinObj.GetComponent<Collider>());
            var coinRenderer = coinObj.GetComponent<Renderer>();
            if (coinRenderer != null)
            {
                var coinMat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
                coinMat.color = new Color(1.0f, 0.8f, 0.1f);
                coinMat.EnableKeyword("_EMISSION");
                coinMat.SetColor("_EmissionColor", new Color(1.0f, 0.8f, 0.1f) * 1.5f);
                coinRenderer.material = coinMat;
            }

            // Warm Gold Light
            var lightObj = new GameObject("ShopLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6.0f;
            light.intensity = 2.5f;
            light.color = new Color(1.0f, 0.85f, 0.3f);

            // 3D Prompt Text
            var promptObj = new GameObject("PromptText");
            promptObj.transform.SetParent(root.transform, false);
            promptObj.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            var promptTmp = promptObj.AddComponent<TMPro.TextMeshPro>();
            promptTmp.text = "[E] Mở Cửa Hàng (Shop)";
            promptTmp.fontSize = 4f;
            promptTmp.alignment = TMPro.TextAlignmentOptions.Center;
            promptTmp.color = new Color(1f, 0.85f, 0.2f);
            promptObj.SetActive(false);

            var shopStation = root.AddComponent<Hellfire.Items.ShopStation>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(Hellfire.Items.ShopStation).GetField("_promptText", flags)?.SetValue(shopStation, promptTmp);
            typeof(Hellfire.Items.ShopStation).GetField("_stationLight", flags)?.SetValue(shopStation, light);
            typeof(Hellfire.Items.ShopStation).GetField("_spinningCoin", flags)?.SetValue(shopStation, coinObj.transform);
            typeof(Hellfire.Items.ShopStation).GetField("_audioSource", flags)?.SetValue(shopStation, audioSource);

            return root;
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
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
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

                        var movement = client.PlayerObject.GetComponent<Hellfire.Player.PlayerMovement>();
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
                        else
                        {
                            Debug.LogWarning($"[DungeonManager] Thiếu PlayerMovement trên Player của Client {client.ClientId}!");
                        }
                        index++;
                    }
                }
            }
            else
            {
                // Hỗ trợ chế độ Offline: dịch chuyển Player cục bộ về phòng spawn
                if (Hellfire.Player.PlayerMovement.LocalInstance != null)
                {
                    Hellfire.Player.PlayerMovement.LocalInstance.TeleportOwnerClientRpc(spawnCenter, Quaternion.identity);
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

        #region Room Locking & Gates (Mục 17.4 & 17.5)

        private void InitializeRoomGatesAndLockStates()
        {
            if (!IsServer) return;

            _roomLockStates.Clear();
            _roomsFinishedSpawning.Clear();
            if (_generator != null && _generator.ActiveGrid != null)
            {
                foreach (var kvp in _generator.ActiveGrid.Rooms)
                {
                    var room = kvp.Value;
                    if (room.RoomType == DungeonRoomType.Combat || room.RoomType == DungeonRoomType.Boss)
                    {
                        _roomLockStates[room.GridPosition] = RoomLockState.Idle;
                    }
                }
            }

            if (_roomLockRoutine != null)
            {
                StopCoroutine(_roomLockRoutine);
            }
            _roomLockRoutine = StartCoroutine(RoomLockEvaluationRoutine());
        }

        private IEnumerator RoomLockEvaluationRoutine()
        {
            var wait = new WaitForSeconds(0.2f);

            while (true)
            {
                yield return wait;

                if (!IsServer || _generator == null || _generator.ActiveGrid == null) continue;

                var state = GameManager.Instance != null ? GameManager.Instance.CurrentState.Value : GameState.Generating;
                if (state != GameState.Playing && state != GameState.BossFight) continue;

                // Duyệt qua các phòng Combat và Boss
                foreach (var kvp in _generator.ActiveGrid.Rooms)
                {
                    var room = kvp.Value;
                    if (room.RoomType != DungeonRoomType.Combat && room.RoomType != DungeonRoomType.Boss) continue;

                    if (!_roomLockStates.TryGetValue(room.GridPosition, out var lockState))
                    {
                        _roomLockStates[room.GridPosition] = RoomLockState.Idle;
                        lockState = RoomLockState.Idle;
                    }

                    if (lockState == RoomLockState.Idle)
                    {
                        // 1. Server mỗi 0.2s kiểm tra các phòng đang Idle: nếu có ít nhất 1 người chơi còn sống
                        // và không Downed có vị trí nằm trong vùng "lõi" của phòng (roomSize - 3) thì khóa (Mục 17.4)
                        if (IsAnyAlivePlayerInRoomCore(room))
                        {
                            LockRoom(room);
                        }
                    }
                    else if (lockState == RoomLockState.Locked)
                    {
                        // 3. Điều kiện mở: mọi quái thuộc phòng đó đã chết (Mục 17.4 & 17.5)
                        // Chỉ cho phép ClearRoom khi phòng đó ĐÃ spawn xong để tránh race condition trong 0.8s đầu
                        if (_roomsFinishedSpawning.Contains(room.GridPosition))
                        {
                            int aliveCount = _enemySpawner != null ? _enemySpawner.GetAliveEnemyCountInRoom(room.GridPosition) : 0;
                            if (aliveCount == 0)
                            {
                                ClearRoom(room);
                            }
                        }
                    }
                }
            }
        }

        private bool IsAnyAlivePlayerInRoomCore(DungeonRoomNode room)
        {
            if (NetworkManager.Singleton == null || _generator == null) return false;

            float halfCore = (_generator.RoomSize - 3f) * 0.5f; // thụt vào 1.5m mỗi phía (Mục 17.4)
            Vector3 roomCenter = room.WorldCenter;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var player = client.PlayerObject;
                var health = player.GetComponent<Health>();
                if (health == null || health.IsDead.Value || health.IsDowned.Value) continue;

                Vector3 pPos = player.transform.position;
                if (Mathf.Abs(pPos.x - roomCenter.x) <= halfCore && Mathf.Abs(pPos.z - roomCenter.z) <= halfCore)
                {
                    return true;
                }
            }

            return false;
        }

        private void LockRoom(DungeonRoomNode room)
        {
            // (a) Đặt Locked
            _roomLockStates[room.GridPosition] = RoomLockState.Locked;
            Debug.Log($"[DungeonManager] Kích hoạt khóa phòng {room.GridPosition} ({room.RoomType})!");

            // (b) Dịch chuyển vào trong phòng 2m mọi người chơi đang đứng trong vùng ngưỡng cửa (Mục 17.4)
            TeleportThresholdPlayersIntoRoom(room);

            // (c) Gọi SetRoomGatesClientRpc(roomGrid, true)
            SetRoomGatesClientRpc(room.GridPosition, true);

            // (d) Sau 0.8 giây (cổng đã hạ xong) spawn quái hoặc boss (Mục 17.4 & 17.5)
            StartCoroutine(DelayedRoomSpawnRoutine(room));
        }

        private void TeleportThresholdPlayersIntoRoom(DungeonRoomNode room)
        {
            if (NetworkManager.Singleton == null || _generator == null) return;

            float halfRoom = _generator.RoomSize * 0.5f;
            float halfCorridor = 4f * 0.5f; // Bề rộng cửa bằng _corridorWidth (4m)

            foreach (var dir in DungeonGrid.CardinalDirections)
            {
                Vector2Int neighborCoord = room.GridPosition + dir;
                if (!room.HasConnection(neighborCoord)) continue;

                Vector3 wallCenter = room.WorldCenter + new Vector3(dir.x * halfRoom, 0f, dir.y * halfRoom);
                bool isZAxisWall = (dir.y != 0);

                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject == null) continue;
                    var player = client.PlayerObject;
                    var health = player.GetComponent<Health>();
                    if (health == null || health.IsDead.Value || health.IsDowned.Value) continue;

                    Vector3 pPos = player.transform.position;
                    bool inThreshold = false;
                    Vector3 targetPos = pPos;

                    if (isZAxisWall)
                    {
                        // Ngưỡng cửa trục Z: khoảng cách tới mặt tường <= 1.5m, bề rộng cửa theo trục X <= 2.0m
                        if (Mathf.Abs(pPos.x - wallCenter.x) <= halfCorridor && Mathf.Abs(pPos.z - wallCenter.z) <= 1.5f)
                        {
                            inThreshold = true;
                            // Dịch chuyển vào trong phòng 2m (hướng ngược lại với dir)
                            targetPos = new Vector3(pPos.x, pPos.y, wallCenter.z - Mathf.Sign(dir.y) * 2f);
                        }
                    }
                    else
                    {
                        // Ngưỡng cửa trục X: khoảng cách tới mặt tường <= 1.5m, bề rộng cửa theo trục Z <= 2.0m
                        if (Mathf.Abs(pPos.z - wallCenter.z) <= halfCorridor && Mathf.Abs(pPos.x - wallCenter.x) <= 1.5f)
                        {
                            inThreshold = true;
                            // Dịch chuyển vào trong phòng 2m (hướng ngược lại với dir)
                            targetPos = new Vector3(wallCenter.x - Mathf.Sign(dir.x) * 2f, pPos.y, pPos.z);
                        }
                    }

                    if (inThreshold)
                    {
                        Vector3 validatedPos = DungeonEnemySpawner.GetValidSpawnPosition(
                            targetPos, room.WorldCenter, $"Ngưỡng cửa phòng {room.GridPosition}", 3f
                        );
                        var movement = player.GetComponent<PlayerMovement>();
                        if (movement != null)
                        {
                            var rpcParams = new ClientRpcParams
                            {
                                Send = new ClientRpcSendParams
                                {
                                    TargetClientIds = new[] { client.ClientId }
                                }
                            };
                            movement.TeleportOwnerClientRpc(validatedPos, player.transform.rotation, rpcParams);
                            Debug.Log($"[DungeonManager] Đã dịch chuyển người chơi {client.ClientId} vào trong phòng {room.GridPosition} 2m để tránh kẹt cổng (Mục 17.4).");
                        }
                    }
                }
            }
        }

        private IEnumerator DelayedRoomSpawnRoutine(DungeonRoomNode room)
        {
            // Chờ 0.8 giây cho cổng hạ xong (Mục 17.4)
            yield return new WaitForSeconds(0.8f);

            if (!IsServer) yield break;

            if (room.RoomType == DungeonRoomType.Combat)
            {
                if (_enemySpawner != null)
                {
                    _enemySpawner.SpawnEnemiesForRoom(room);
                }
            }
            else if (room.RoomType == DungeonRoomType.Boss)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetState(GameState.BossFight);
                }

                if (_enemySpawner != null)
                {
                    _enemySpawner.SpawnBossForRoom(room);
                }
            }

            // Đặt cờ đã spawn xong cho phòng này (chỉ Server)
            _roomsFinishedSpawning.Add(room.GridPosition);
            Debug.Log($"[DungeonManager] Đã đặt cờ phòng {room.GridPosition} ({room.RoomType}) đã spawn xong.");
        }

        private void ClearRoom(DungeonRoomNode room)
        {
            _roomLockStates[room.GridPosition] = RoomLockState.Cleared;
            SetRoomGatesClientRpc(room.GridPosition, false);
            Debug.Log($"[DungeonManager] [Cleared] Phòng {room.GridPosition} ({room.RoomType}) đã dọn sạch quái (Cleared)! Cổng nâng lên.");

            if (room.RoomType == DungeonRoomType.Combat)
            {
                Hellfire.Player.PlayerWallet.DistributeSoulsToLivingPlayers(_combatRoomClearSoulsBonus);
                Debug.Log($"[DungeonManager] [Cleared] Phòng Combat {room.GridPosition} đã Cleared! Thưởng +{_combatRoomClearSoulsBonus} Linh hồn cho mỗi người chơi còn sống.");
            }
            else if (room.RoomType == DungeonRoomType.Boss)
            {
                if (_enemySpawner != null)
                {
                    _enemySpawner.ClearAllEnemiesInRoom(room.GridPosition);
                }
                Debug.Log($"[DungeonManager] [Cleared] Phòng Boss {room.GridPosition} đã Cleared! Toàn bộ quái còn lại đã dọn sạch.");
            }
        }

        /// <summary>
        /// Được gọi khi Boss chết để mở cổng phòng Boss và dọn sạch quái triệu hồi còn sót (Mục 17.4).
        /// </summary>
        public void NotifyBossDied()
        {
            if (!IsServer) return;

            if (_generator != null && _generator.ActiveGrid != null && _generator.ActiveGrid.BossRoom != null)
            {
                var bossRoom = _generator.ActiveGrid.BossRoom;
                if (_roomLockStates.TryGetValue(bossRoom.GridPosition, out var state) && state != RoomLockState.Cleared)
                {
                    ClearRoom(bossRoom);
                }
            }
        }

        public static readonly HashSet<Vector2Int> ClearedRoomsClient = new HashSet<Vector2Int>();

        [ClientRpc]
        public void SetRoomGatesClientRpc(Vector2Int roomGrid, bool closed)
        {
            if (_generator != null)
            {
                _generator.SetRoomGates(roomGrid, closed);
            }

            if (!closed)
            {
                ClearedRoomsClient.Add(roomGrid);
            }
        }

        #endregion
    }
}
