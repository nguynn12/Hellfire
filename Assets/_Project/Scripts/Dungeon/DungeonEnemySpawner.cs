// Script: DungeonEnemySpawner.cs
// Mục đích: Quản lý việc sinh quái vật và Boss theo phòng khi phòng bị khóa (Mục 17.4 & 17.5 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative spawn).

using System.Collections.Generic;
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
        public static DungeonEnemySpawner Instance { get; private set; }

        [Header("Enemy Prefabs (Mục 6.2)")]
        [SerializeField] private GameObject _impPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private GameObject _brutePrefab;

        [Header("Boss Prefab (Mục 5.2)")]
        [SerializeField] private GameObject _bossPrefab;

        [Header("Spawn Balancing (Mục 6.2 & 17.5)")]
        [SerializeField] private int _minEnemiesPerNormalRoom = 2;
        [SerializeField] private int _maxEnemiesPerNormalRoom = 4;

        [Header("Spawn Validation (Mục 15.2 & 17.5)")]
        [Tooltip("Khoảng cách tìm kiếm tối đa trên NavMesh khi spawn đối tượng (m)")]
        [SerializeField] private float _maxNavMeshSampleDistance = 3.0f;

        [Header("Difficulty Scaling (Mục 15.3)")]
        [Tooltip("Cấu hình cân bằng số lượng quái và máu Boss theo số người chơi")]
        [SerializeField] private DifficultyScalingData _scalingData;

        private DungeonManager _dungeonManager;
        private readonly Dictionary<Vector2Int, HashSet<GameObject>> _roomAliveEnemies = new Dictionary<Vector2Int, HashSet<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _dungeonManager = GetComponent<DungeonManager>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _roomAliveEnemies.Clear();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                _roomAliveEnemies.Clear();
            }
        }

        /// <summary>
        /// Sinh quái vật cho phòng Combat khi phòng chuyển sang Locked (Mục 17.4 & 17.5).
        /// </summary>
        public void SpawnEnemiesForRoom(DungeonRoomNode room)
        {
            if (!IsServer || room == null) return;

            int seed = _dungeonManager != null ? _dungeonManager.DungeonSeed.Value : 12345;
            int roomRngSeed = seed ^ (room.GridPosition.x * 73856093) ^ (room.GridPosition.y * 19349663) ^ 0x175;
            var rng = new System.Random(roomRngSeed);

            int rolledCount = rng.Next(_minEnemiesPerNormalRoom, _maxEnemiesPerNormalRoom + 1);

            // Cân bằng số lượng quái theo số người chơi chốt khi vào Generating (Mục 15.3 & 17.5)
            int playerCount = (GameManager.Instance != null)
                ? GameManager.Instance.LockedPlayerCount.Value
                : 1;
            float enemyMultiplier = _scalingData != null
                ? _scalingData.GetEnemyCountMultiplier(playerCount)
                : GetDefaultEnemyCountMultiplier(playerCount);
            int enemyCount = Mathf.Max(1, Mathf.RoundToInt(rolledCount * enemyMultiplier));

            if (!_roomAliveEnemies.ContainsKey(room.GridPosition))
            {
                _roomAliveEnemies[room.GridPosition] = new HashSet<GameObject>();
            }

            var alivePlayerPositions = GetAlivePlayerPositions();

            for (int i = 0; i < enemyCount; i++)
            {
                // Chọn ngẫu nhiên loại quái: 50% Imp, 30% Archer, 20% Brute (Mục 6.2 & 17.5)
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

                // Vị trí spawn: cách tường tối thiểu 2m, không trùng cột, tránh bán kính 4m quanh người chơi nếu còn chỗ (Mục 17.5)
                Vector3 spawnPos = FindValidCombatSpawnPosition(room, rng, alivePlayerPositions);

                var enemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.Euler(0f, rng.Next(360), 0f));
                var netObj = enemyObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    _roomAliveEnemies[room.GridPosition].Add(enemyObj);
                }
            }

            Debug.Log($"[DungeonEnemySpawner] Đã spawn {enemyCount} quái tại phòng Combat {room.GridPosition} (Hệ số: x{enemyMultiplier:F2}).");
        }

        /// <summary>
        /// Sinh Boss tại phòng Boss khi phòng Boss chuyển sang Locked (Mục 17.4, 17.5, 15.2, 15.3).
        /// </summary>
        public void SpawnBossForRoom(DungeonRoomNode bossRoom)
        {
            if (!IsServer || bossRoom == null || _bossPrefab == null) return;

            if (!_roomAliveEnemies.ContainsKey(bossRoom.GridPosition))
            {
                _roomAliveEnemies[bossRoom.GridPosition] = new HashSet<GameObject>();
            }

            Vector3 bossProposed = bossRoom.WorldCenter + Vector3.up * 0.5f;
            Vector3 bossSpawnPos = GetValidSpawnPosition(bossProposed, bossRoom.WorldCenter, "Phòng Boss", _maxNavMeshSampleDistance);
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
                _roomAliveEnemies[bossRoom.GridPosition].Add(bossObj);
            }

            if (bossHealth != null)
            {
                bossHealth.SetMaxHealth(scaledBossMaxHp);
            }

            Debug.Log($"[DungeonEnemySpawner] Đã spawn Boss tại {bossSpawnPos} với {scaledBossMaxHp} HP (Số người: {playerCount}, hệ số x{bossHpMultiplier:F2}).");
        }

        /// <summary>
        /// Đăng ký quái vật do Boss triệu hồi vào danh sách quái sống của phòng Boss (Mục 17.4).
        /// </summary>
        public void RegisterBossSummon(GameObject minion)
        {
            if (!IsServer || minion == null) return;

            var grid = _dungeonManager != null && _dungeonManager.Generator != null
                ? _dungeonManager.Generator.ActiveGrid
                : null;
            if (grid == null || grid.BossRoom == null) return;

            Vector2Int bossPos = grid.BossRoom.GridPosition;
            if (!_roomAliveEnemies.ContainsKey(bossPos))
            {
                _roomAliveEnemies[bossPos] = new HashSet<GameObject>();
            }
            _roomAliveEnemies[bossPos].Add(minion);
        }

        /// <summary>
        /// Đếm số quái còn sống trong phòng, đồng thời loại bỏ quái chết, quái rơi ra ngoài bản đồ y < -10 (Mục 17.5).
        /// </summary>
        public int GetAliveEnemyCountInRoom(Vector2Int roomCoord)
        {
            if (!_roomAliveEnemies.TryGetValue(roomCoord, out var enemies)) return 0;

            var deadOrDespawned = new List<GameObject>();

            foreach (var enemy in enemies)
            {
                if (enemy == null)
                {
                    deadOrDespawned.Add(enemy);
                    continue;
                }

                var health = enemy.GetComponent<Health>();
                if (health != null && health.IsDead.Value)
                {
                    deadOrDespawned.Add(enemy);
                    continue;
                }

                // Quái rơi ra ngoài bản đồ (y < -10) coi như đã chết để phòng không bị kẹt khóa (Mục 17.5)
                if (enemy.transform.position.y < -10f)
                {
                    deadOrDespawned.Add(enemy);
                    var netObj = enemy.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(enemy);
                    }
                }
            }

            foreach (var rem in deadOrDespawned)
            {
                enemies.Remove(rem);
            }

            return enemies.Count;
        }

        /// <summary>
        /// Hạ và dọn sạch mọi quái vật còn sống trong phòng (dùng khi Boss chết theo Mục 17.4).
        /// </summary>
        public void ClearAllEnemiesInRoom(Vector2Int roomCoord)
        {
            if (!_roomAliveEnemies.TryGetValue(roomCoord, out var enemies)) return;

            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    var netObj = enemy.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(enemy);
                    }
                }
            }

            enemies.Clear();
        }

        private Vector3 FindValidCombatSpawnPosition(DungeonRoomNode room, System.Random rng, List<Vector3> alivePlayers)
        {
            // Phòng 14m x 14m: cách tường tối thiểu 2m -> phạm vi offset x, z trong [-5, 5]
            Vector3 chosenPos = room.WorldCenter;
            bool found = false;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float ox = (float)(rng.NextDouble() * 10.0 - 5.0); // [-5, 5]
                float oz = (float)(rng.NextDouble() * 10.0 - 5.0); // [-5, 5]

                // Không trùng 4 cột ở góc (±4, 0, ±4): bán kính cột 1.5m
                if (Mathf.Abs(Mathf.Abs(ox) - 4f) < 1.5f && Mathf.Abs(Mathf.Abs(oz) - 4f) < 1.5f)
                {
                    continue;
                }

                Vector3 candidate = room.WorldCenter + new Vector3(ox, 0.5f, oz);

                // NÊN tránh bán kính 4m quanh người chơi nếu còn chỗ (Mục 17.5)
                bool nearPlayer = false;
                if (attempt < 8)
                {
                    foreach (var pPos in alivePlayers)
                    {
                        if ((candidate - pPos).sqrMagnitude < 16f) // 4m squared = 16
                        {
                            nearPlayer = true;
                            break;
                        }
                    }
                }

                if (nearPlayer) continue;

                if (NavMesh.SamplePosition(candidate, out var hit, 2.0f, NavMesh.AllAreas))
                {
                    chosenPos = hit.position;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                chosenPos = GetValidSpawnPosition(room.WorldCenter + Vector3.up * 0.5f, room.WorldCenter, $"Phòng {room.GridPosition}", _maxNavMeshSampleDistance);
            }

            return chosenPos;
        }

        private List<Vector3> GetAlivePlayerPositions()
        {
            var positions = new List<Vector3>();
            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var health = client.PlayerObject.GetComponent<Health>();
                        if (health != null && !health.IsDead.Value && !health.IsDowned.Value)
                        {
                            positions.Add(client.PlayerObject.transform.position);
                        }
                    }
                }
            }
            return positions;
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
