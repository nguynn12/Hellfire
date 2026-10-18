// Script: DungeonEnemySpawner.cs
// Mục đích: Quản lý việc sinh quái vật và Boss tại các phòng sau khi bản đồ và NavMesh đã sẵn sàng (Mục 4.2, 5.1, 5.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative spawn).

using System.Collections.Generic;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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

        [Header("Spawn Validation (Mục 15.2)")]
        [Tooltip("Khoảng cách tìm kiếm tối đa trên NavMesh khi spawn đối tượng (m)")]
        [SerializeField] private float _maxNavMeshSampleDistance = 3.0f;

        [Header("Difficulty Scaling (Mục 15.3)")]
        [Tooltip("Cấu hình cân bằng số lượng quái và máu Boss theo số người chơi")]
        [SerializeField] private DifficultyScalingData _scalingData;

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
                _dungeonManager.OnAllClientsReady += HandleDungeonReady;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _dungeonManager != null)
            {
                _dungeonManager.OnAllClientsReady -= HandleDungeonReady;
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

                    // Sinh 1 rương báu thường trong mỗi phòng Normal (Mục 15.2: kiểm tra NavMesh)
                    if (_lootChestPrefab != null)
                    {
                        Vector3 chestProposed = room.WorldCenter + new Vector3(2.5f, 0f, 2.5f);
                        Vector3 chestSpawnPos = GetValidSpawnPosition(chestProposed, room.WorldCenter, $"Phòng {room.GridPosition}", _maxNavMeshSampleDistance);
                        var chestObj = Instantiate(_lootChestPrefab, chestSpawnPos, Quaternion.identity);
                        var netObj = chestObj.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn(true);
                        }
                    }
                }
            }

            // 2. Sinh Boss tại phòng Boss xa nhất (Mục 4.1, 5.2, 15.2, 15.3)
            if (grid.BossRoom != null && _bossPrefab != null)
            {
                Vector3 bossProposed = grid.BossRoom.WorldCenter + Vector3.up * 0.5f;
                Vector3 bossSpawnPos = GetValidSpawnPosition(bossProposed, grid.BossRoom.WorldCenter, "Phòng Boss", _maxNavMeshSampleDistance);
                var bossObj = Instantiate(_bossPrefab, bossSpawnPos, Quaternion.identity);

                // Tính máu tối đa của Boss theo hệ số độ khó (Mục 15.3: 800 * hệ số)
                int playerCount = (GameManager.Instance != null)
                    ? GameManager.Instance.LockedPlayerCount.Value
                    : 1;
                float bossHpMultiplier = _scalingData != null
                    ? _scalingData.GetBossHealthMultiplier(playerCount)
                    : GetDefaultBossHpMultiplier(playerCount);
                float scaledBossMaxHp = Mathf.Round(800f * bossHpMultiplier);

                var bossHealth = bossObj.GetComponent<Health>();
                if (bossHealth != null)
                {
                    bossHealth.SetMaxHealth(scaledBossMaxHp);
                }

                var netObj = bossObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    _spawnedEnemies.Add(bossObj);
                }

                if (bossHealth != null)
                {
                    bossHealth.SetMaxHealth(scaledBossMaxHp);
                }

                Debug.Log($"[DungeonEnemySpawner] Đã spawn Boss tại {bossSpawnPos} với {scaledBossMaxHp} HP (Số người chốt: {playerCount}, hệ số x{bossHpMultiplier:F2}).");
            }
        }

        private void SpawnEnemiesInRoom(DungeonRoomNode room, System.Random rng)
        {
            int rolledCount = rng.Next(_minEnemiesPerNormalRoom, _maxEnemiesPerNormalRoom + 1);

            // Cân bằng số lượng quái theo số người chơi chốt khi vào Generating (Mục 15.3)
            int playerCount = (GameManager.Instance != null)
                ? GameManager.Instance.LockedPlayerCount.Value
                : 1;
            float enemyMultiplier = _scalingData != null
                ? _scalingData.GetEnemyCountMultiplier(playerCount)
                : GetDefaultEnemyCountMultiplier(playerCount);
            int enemyCount = Mathf.Max(1, Mathf.RoundToInt(rolledCount * enemyMultiplier));

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

                // Vị trí spawn trong phòng (Mục 15.2: kiểm tra NavMesh hợp lệ)
                float offsetX = (float)(rng.NextDouble() * 8.0 - 4.0);
                float offsetZ = (float)(rng.NextDouble() * 8.0 - 4.0);
                Vector3 proposedPos = room.WorldCenter + new Vector3(offsetX, 0.5f, offsetZ);
                Vector3 spawnPos = GetValidSpawnPosition(proposedPos, room.WorldCenter, $"Phòng {room.GridPosition}", _maxNavMeshSampleDistance);

                var enemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.Euler(0f, rng.Next(360), 0f));
                var netObj = enemyObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    _spawnedEnemies.Add(enemyObj);
                }
            }
        }

        /// <summary>
        /// Xác thực vị trí spawn hợp lệ qua NavMesh.SamplePosition, có fallback về tâm phòng (Mục 15.2).
        /// </summary>
        public static Vector3 GetValidSpawnPosition(Vector3 proposedPos, Vector3 roomCenter, string roomName, float maxDistance = 3.0f)
        {
            // 1. Thử sample vị trí đề xuất trên NavMesh
            if (NavMesh.SamplePosition(proposedPos, out var hit, maxDistance, NavMesh.AllAreas))
            {
                return hit.position;
            }

            // 2. Thất bại: thử sample tại tâm phòng
            if (NavMesh.SamplePosition(roomCenter, out var centerHit, maxDistance, NavMesh.AllAreas))
            {
                return centerHit.position;
            }

            // 3. Tâm phòng cũng thất bại: cảnh báo và fallback về tâm phòng (không được bỏ qua)
            Debug.LogWarning($"[SpawnValidation] Không thể tìm vị trí NavMesh hợp lệ tại '{roomName}' (đề xuất: {proposedPos}, tâm: {roomCenter}). Dùng tâm phòng làm fallback.");
            return roomCenter;
        }

        private float GetDefaultEnemyCountMultiplier(int playerCount)
        {
            switch (playerCount)
            {
                case 1: return 0.7f;
                case 2: return 1.0f;
                case 3: return 1.4f;
                case 4: return 1.8f;
                default: return 1.0f;
            }
        }

        private float GetDefaultBossHpMultiplier(int playerCount)
        {
            switch (playerCount)
            {
                case 1: return 0.6f;
                case 2: return 1.0f;
                case 3: return 1.5f;
                case 4: return 2.0f;
                default: return 1.0f;
            }
        }
    }
}
