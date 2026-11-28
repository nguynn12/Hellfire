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
        Cleared = 2,
        Gathering = 3
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

        public bool IsRoomLocked(Vector2Int coord) => _roomLockStates.TryGetValue(coord, out var state) && state == RoomLockState.Locked;
        public DungeonGenerator Generator => _generator;
        public DungeonEnemySpawner EnemySpawner => _enemySpawner;

        private readonly Dictionary<Vector2Int, RoomLockState> _roomLockStates = new Dictionary<Vector2Int, RoomLockState>();
        private readonly HashSet<Vector2Int> _roomsFinishedSpawning = new HashSet<Vector2Int>();
        private Coroutine _roomLockRoutine;
        private Vector2Int? _activeEncounter;
        [Header("Party gathering (Mục 20.5)")]
        [SerializeField] private float _partyTeleportDelay = 0.5f;
        [SerializeField] private float _roomEnemySpawnDelay = 0.8f;
        [SerializeField] private float _partyMinimumSpacing = 1.5f;
        [SerializeField] private float _partyGatherMinRadius = 2f;
        [SerializeField] private float _partyGatherMaxRadius = 4f;

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
#if HELLFIRE_PHASE1_SMOKE
                if (int.TryParse(System.Environment.GetEnvironmentVariable("HELLFIRE_QA_DUNGEON_SEED"), out int qaSeed)
                    && qaSeed > 0) seedToUse = qaSeed;
#endif
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

            // Mục 20.0: kiểm tra trước khi vòng lặp khóa phòng được khởi động.
            var leftovers = UnityEngine.Object.FindObjectsByType<Hellfire.AI.EnemyStateMachine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (leftovers.Length == 0)
                Debug.Log("[SceneAudit] Playing before first lock: EnemyStateMachine=0");
            foreach (var enemy in leftovers)
                Debug.LogError($"[SceneAudit] EnemyStateMachine trước khi khóa phòng: {enemy.name}", enemy);

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

            Vector3 shrinePos = DungeonEnemySpawner.GetValidSpawnPosition(room.WorldCenter, room, "Upgrade");

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
            float spacing = Mathf.Min(2.5f, room.Size.x * 0.5f - 2f);

            for (int i = 0; i < count; i++)
            {
                // Đặt đối xứng quanh tâm phòng (Mục 17.6)
                float xOffset = (i == 0) ? -spacing : spacing;
                Vector3 proposedPos = room.WorldCenter + new Vector3(xOffset, 0f, 0f);
                Vector3 chestPos = DungeonEnemySpawner.GetValidSpawnPosition(proposedPos, room, "Treasure");

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
            Vector3 stationPos = DungeonEnemySpawner.GetValidSpawnPosition(room.WorldCenter, room, "Shop");

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
                            proposedPos, spawnRoom, "Phòng Spawn (Vị trí Player)", 3.0f
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

            _activeEncounter = null;
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
                        var entrant = _activeEncounter == null ? FindRoomEntrant(room) : null;
                        if (entrant != null) LockRoom(room, entrant);
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

        private bool IsCombatActive => GameManager.Instance != null &&
            (GameManager.Instance.CurrentState.Value == GameState.Playing || GameManager.Instance.CurrentState.Value == GameState.BossFight);

        private NetworkObject FindRoomEntrant(DungeonRoomNode room)
        {
            if (NetworkManager.Singleton == null) return null;
            NetworkObject entrant = null;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                var health = player != null ? player.GetComponent<Health>() : null;
                if (health == null || health.IsDead.Value || health.IsDowned.Value || !room.Contains(player.transform.position, 1.5f)) continue;
                if (entrant == null || player.OwnerClientId < entrant.OwnerClientId) entrant = player;
            }
            return entrant;
        }

        private void LockRoom(DungeonRoomNode room, NetworkObject entrant)
        {
            if (!IsServer || _activeEncounter != null || _roomLockStates[room.GridPosition] != RoomLockState.Idle) return;
            _activeEncounter = room.GridPosition;
            _roomLockStates[room.GridPosition] = RoomLockState.Gathering;
            StartCoroutine(GatherPartyAndLock(room, entrant));
        }

        private bool NeedsGathering(DungeonRoomNode room, Vector3 position)
        {
            if (!room.Contains(position)) return true;
            // A player in a doorway must be moved clear of the closing gate too.
            foreach (var dir in DungeonGrid.CardinalDirections)
            {
                if (!room.HasConnection(room.GridPosition + dir)) continue;
                Vector3 local = position - room.WorldCenter;
                if (dir.x != 0 && Mathf.Abs(local.x - dir.x * room.Size.x * .5f) <= 1.5f && Mathf.Abs(local.z) <= _generator.CorridorWidth * .5f) return true;
                if (dir.y != 0 && Mathf.Abs(local.z - dir.y * room.Size.y * .5f) <= 1.5f && Mathf.Abs(local.x) <= _generator.CorridorWidth * .5f) return true;
            }
            return false;
        }

        private bool TryGatherPosition(DungeonRoomNode room, Vector3 entrant, List<Vector3> occupied, out Vector3 target)
        {
            // Deterministic rings; validate after sampling, never accept a point across a wall.
            for (int ring = 0; ring < 5; ring++)
                for (int step = 0; step < 48; step++)
                {
                    float radius = Mathf.Lerp(_partyGatherMinRadius, _partyGatherMaxRadius, ring / 4f);
                    float angle = step * Mathf.PI * 2f / 48;
                    Vector3 proposed = entrant + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(proposed, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    if (!room.Contains(hit.position, 1.5f) || Mathf.Abs(hit.position.y - room.WorldCenter.y) > .5f) continue;
                    Vector3 flat = hit.position - entrant; flat.y = 0;
                    if (flat.magnitude < _partyGatherMinRadius || flat.magnitude > _partyGatherMaxRadius) continue;
                    bool blocked = false;
                    foreach (var point in occupied)
                    {
                        Vector3 delta = point - hit.position; delta.y = 0;
                        if (delta.sqrMagnitude < _partyMinimumSpacing * _partyMinimumSpacing) { blocked = true; break; }
                    }
                    foreach (var obstacle in room.ObstacleBounds)
                        if (RoomThemeBuilder.DistanceXZ(obstacle, new Bounds(hit.position, Vector3.zero)) < .5f) { blocked = true; break; }
                    if (blocked) continue;
                    target = hit.position;
                    return true;
                }
            target = default;
            return false;
        }

        private IEnumerator GatherPartyAndLock(DungeonRoomNode room, NetworkObject entrant)
        {
            var occupied = new List<Vector3>();
            var pending = new List<NetworkObject>();
            var destinations = new List<Vector3>();
            var clients = new List<NetworkClient>(NetworkManager.Singleton.ConnectedClientsList);
            clients.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
            foreach (var client in clients)
            {
                var player = client.PlayerObject;
                var health = player != null ? player.GetComponent<Health>() : null;
                if (health == null || health.IsDead.Value) continue; // Downed remains in the party.
                if (NeedsGathering(room, player.transform.position)) pending.Add(player);
                else occupied.Add(player.transform.position);
            }
            // Reserve the whole party before sending any RPC; retain valid spacing.
            foreach (var player in pending)
            {
                if (!TryGatherPosition(room, entrant.transform.position, occupied, out var target))
                {
                    Debug.LogWarning($"[PartyTeleport] No valid gathering slot in {room.GridPosition}; gates remain open.");
                    _roomLockStates[room.GridPosition] = RoomLockState.Idle;
                    _activeEncounter = null;
                    yield break;
                }
                destinations.Add(target); occupied.Add(target);
            }
            for (int i = 0; i < pending.Count; i++)
            {
                var player = pending[i];
                var movement = player.GetComponent<PlayerMovement>();
                if (movement == null)
                {
                    Debug.LogError($"[PartyTeleport] Missing PlayerMovement on {player.name}");
                    _roomLockStates[room.GridPosition] = RoomLockState.Idle; _activeEncounter = null;
                    yield break;
                }
                movement.TeleportOwnerClientRpc(destinations[i], player.transform.rotation,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { player.OwnerClientId } } });
                Debug.Log($"[PartyTeleport] room={room.GridPosition} entrant={entrant.OwnerClientId} target={player.OwnerClientId} downed={player.GetComponent<Health>().IsDowned.Value} destination={destinations[i]}");
            }
            Debug.Log($"[PartyTeleport] room={room.GridPosition} entrant={entrant.OwnerClientId} gathered={pending.Count}; waiting {_partyTeleportDelay:F1}s before gates");
            yield return new WaitForSeconds(_partyTeleportDelay);
            if (!IsServer || !IsCombatActive) yield break;
            // Owner transforms must have arrived. Never lock someone outside under packet delay.
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                var health = player != null ? player.GetComponent<Health>() : null;
                if (health != null && !health.IsDead.Value && NeedsGathering(room, player.transform.position))
                {
                    Debug.LogWarning($"[PartyTeleport] Awaiting owner {client.ClientId}; retry gathering with gates open.");
                    _roomLockStates[room.GridPosition] = RoomLockState.Idle; _activeEncounter = null;
                    yield break;
                }
            }
            _roomLockStates[room.GridPosition] = RoomLockState.Locked;
            SetRoomGatesClientRpc(room.GridPosition, true);
            Debug.Log($"[PartyTeleport] room={room.GridPosition} Locked; spawning after {_roomEnemySpawnDelay:F1}s");
            yield return DelayedRoomSpawnRoutine(room);
        }

        private IEnumerator DelayedRoomSpawnRoutine(DungeonRoomNode room)
        {
            // Chờ 0.8 giây cho cổng hạ xong (Mục 17.4)
            yield return new WaitForSeconds(_roomEnemySpawnDelay);

            if (!IsServer || !IsCombatActive) yield break;

            if (room.RoomType == DungeonRoomType.Combat)
            {
                if (_enemySpawner != null)
                {
                    yield return _enemySpawner.SpawnEnemiesForRoom(room);
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
                    yield return _enemySpawner.SpawnBossForRoom(room);
                }
            }

            if (!IsServer || !IsCombatActive) yield break;
            // Đặt cờ đã spawn xong cho phòng này (chỉ Server)
            _roomsFinishedSpawning.Add(room.GridPosition);
            Debug.Log($"[DungeonManager] Đã đặt cờ phòng {room.GridPosition} ({room.RoomType}) đã spawn xong.");
        }

        private void ClearRoom(DungeonRoomNode room)
        {
            _roomLockStates[room.GridPosition] = RoomLockState.Cleared;
            if (_activeEncounter == room.GridPosition) _activeEncounter = null;
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
