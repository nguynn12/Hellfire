// Script: DungeonEnemySpawner.cs
// Mục đích: Quản lý việc sinh quái vật và Boss tại các phòng sau khi bản đồ và NavMesh đã sẵn sàng (Mục 4.2, 5.1, 5.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative spawn).

using System.Collections.Generic;
using Hellfire.AI;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DungeonManager))]
    public class DungeonEnemySpawner : NetworkBehaviour
    {
        [Header("Enemy Prefabs (Mục 6.2)")]
        [SerializeField] private GameObject _impPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private GameObject _brutePrefab;

        [Header("Boss Prefab (Mục 5.2)")]
        [SerializeField] private GameObject _bossPrefab;

        [Header("Loot Chest Prefab (Mục 6.3)")]
        [SerializeField] private GameObject _lootChestPrefab;

        [Header("Spawn Balancing (Mục 6.2)")]
        [SerializeField] private int _minEnemiesPerNormalRoom = 2;
        [SerializeField] private int _maxEnemiesPerNormalRoom = 4;

        private DungeonManager _dungeonManager;
        private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();

        private void Awake()
        {
            _dungeonManager = GetComponent<DungeonManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _dungeonManager != null)
            {
                _dungeonManager.OnDungeonReady += HandleDungeonReady;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _dungeonManager != null)
            {
                _dungeonManager.OnDungeonReady -= HandleDungeonReady;
            }
        }

        private void HandleDungeonReady(int seed)
        {
            if (!IsServer) return;

            SpawnEnemiesForDungeon(seed);
        }

        private void SpawnEnemiesForDungeon(int seed)
        {
            var grid = DungeonManager.Instance != null && DungeonManager.Instance.GetComponent<DungeonGenerator>() != null
                ? DungeonManager.Instance.GetComponent<DungeonGenerator>().ActiveGrid
                : null;

            if (grid == null) return;

            // Dùng seed + 100 để random việc bố trí quái vật trong các phòng một cách tất định
            var rng = new System.Random(seed + 100);

            // 1. Sinh quái vật và Rương báu tại các phòng Normal (Mục 6.3)
            foreach (var kvp in grid.Rooms)
            {
                var room = kvp.Value;
                if (room.RoomType == DungeonRoomType.Normal)
                {
                    SpawnEnemiesInRoom(room, rng);

                    // Sinh 1 rương báu thường trong mỗi phòng Normal
                    if (_lootChestPrefab != null)
                    {
                        Vector3 chestSpawnPos = room.WorldCenter + new Vector3(2.5f, 0f, 2.5f);
                        var chestObj = Instantiate(_lootChestPrefab, chestSpawnPos, Quaternion.identity);
                        var netObj = chestObj.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn(true);
                        }
                    }
                }
            }

            // 2. Sinh Boss tại phòng Boss xa nhất (Mục 4.1 & 5.2)
            if (grid.BossRoom != null && _bossPrefab != null)
            {
                Vector3 bossSpawnPos = grid.BossRoom.WorldCenter + Vector3.up * 0.5f;
                var bossObj = Instantiate(_bossPrefab, bossSpawnPos, Quaternion.identity);
                var netObj = bossObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    _spawnedEnemies.Add(bossObj);
                }
            }
        }

        private void SpawnEnemiesInRoom(DungeonRoomNode room, System.Random rng)
        {
            int enemyCount = rng.Next(_minEnemiesPerNormalRoom, _maxEnemiesPerNormalRoom + 1);

            for (int i = 0; i < enemyCount; i++)
            {
                // Chọn ngẫu nhiên loại quái: 50% Imp, 30% Archer, 20% Brute
                int roll = rng.Next(100);
                GameObject prefabToSpawn = _impPrefab;

                if (roll >= 50 && roll < 80 && _archerPrefab != null)
                {
                    prefabToSpawn = _archerPrefab;
                }
                else if (roll >= 80 && _brutePrefab != null)
                {
                    prefabToSpawn = _brutePrefab;
                }

                if (prefabToSpawn == null) prefabToSpawn = _impPrefab;
                if (prefabToSpawn == null) continue;

                // Vị trí spawn trong phòng
                float offsetX = (float)(rng.NextDouble() * 8.0 - 4.0);
                float offsetZ = (float)(rng.NextDouble() * 8.0 - 4.0);
                Vector3 spawnPos = room.WorldCenter + new Vector3(offsetX, 0.5f, offsetZ);

                var enemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.Euler(0f, rng.Next(360), 0f));
                var netObj = enemyObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    _spawnedEnemies.Add(enemyObj);
                }
            }
        }
    }
}
