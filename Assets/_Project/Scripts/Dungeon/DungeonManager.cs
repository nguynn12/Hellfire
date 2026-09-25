// Script: DungeonManager.cs
// Mục đích: Quản lý vòng đời hầm ngục, đồng bộ Seed mạng qua NetworkVariable<int>, điều phối sinh map và bake NavMesh (Mục 2.3, 4.1, 4.2 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server gán Seed, mọi Client nhận và tự dựng map + bake NavMesh cục bộ).

using System;
using System.Collections;
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

        /// <summary>
        /// Seed hầm ngục đồng bộ qua mạng (Mục 2.3: chỉ Server ghi, mọi client đọc).
        /// </summary>
        public NetworkVariable<int> DungeonSeed { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsDungeonReady { get; private set; }

        public event Action<int> OnDungeonReady;

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

            // 3. Nếu là Server, sau khi NavMesh sẵn sàng, định vị người chơi về Spawn Room
            if (IsServer)
            {
                PositionPlayersAtSpawn();
            }
        }

        private void PositionPlayersAtSpawn()
        {
            if (_generator == null || _generator.ActiveGrid == null) return;

            var spawnRoom = _generator.ActiveGrid.SpawnRoom;
            if (spawnRoom == null) return;

            Vector3 spawnCenter = spawnRoom.WorldCenter + Vector3.up * 1.0f;

            // Dời các player hiện có trong NetworkManager về phòng Spawn
            if (NetworkManager.Singleton != null)
            {
                int index = 0;
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        // Xếp so le vị trí các người chơi trong phòng spawn
                        Vector3 offset = new Vector3((index % 2 == 0 ? 1f : -1f) * 2f, 0f, (index > 1 ? 2f : -2f));
                        client.PlayerObject.transform.position = spawnCenter + offset;
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
