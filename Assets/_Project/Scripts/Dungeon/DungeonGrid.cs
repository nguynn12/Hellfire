// Script: DungeonGrid.cs
// Mục đích: Cấu trúc dữ liệu sơ đồ ngục 2D và thuật toán BFS tìm phòng Boss xa nhất (Mục 4.1 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server và Client tính toán độc lập từ cùng seed).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hellfire.Dungeon
{
    [Serializable]
    public class DungeonRoomNode
    {
        public Vector2Int GridPosition;
        public DungeonRoomType RoomType = DungeonRoomType.Combat;
        public int DistanceFromSpawn = 0;
        public List<Vector2Int> ConnectedNeighbors = new List<Vector2Int>();
        public Vector3 WorldCenter;

        public DungeonRoomNode(Vector2Int gridPos, Vector3 worldCenter, DungeonRoomType roomType = DungeonRoomType.Combat)
        {
            GridPosition = gridPos;
            WorldCenter = worldCenter;
            RoomType = roomType;
        }

        public bool HasConnection(Vector2Int neighborPos)
        {
            return ConnectedNeighbors.Contains(neighborPos);
        }

        public void AddConnection(Vector2Int neighborPos)
        {
            if (!ConnectedNeighbors.Contains(neighborPos))
            {
                ConnectedNeighbors.Add(neighborPos);
            }
        }
    }

    [Serializable]
    public class DungeonCorridorEdge
    {
        public Vector2Int RoomA;
        public Vector2Int RoomB;
        public Vector3 WorldStart;
        public Vector3 WorldEnd;

        public DungeonCorridorEdge(Vector2Int roomA, Vector2Int roomB, Vector3 start, Vector3 end)
        {
            RoomA = roomA;
            RoomB = roomB;
            WorldStart = start;
            WorldEnd = end;
        }
    }

    public class DungeonGrid
    {
        public static readonly Vector2Int[] CardinalDirections = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // North (+Z)
            new Vector2Int(0, -1),  // South (-Z)
            new Vector2Int(1, 0),   // East  (+X)
            new Vector2Int(-1, 0)   // West  (-X)
        };

        private readonly Dictionary<Vector2Int, DungeonRoomNode> _rooms = new Dictionary<Vector2Int, DungeonRoomNode>();
        private readonly List<DungeonCorridorEdge> _corridors = new List<DungeonCorridorEdge>();
        private Vector2Int _spawnPos;
        private Vector2Int _bossPos;

        public IReadOnlyDictionary<Vector2Int, DungeonRoomNode> Rooms => _rooms;
        public IReadOnlyList<DungeonCorridorEdge> Corridors => _corridors;
        public Vector2Int SpawnPosition => _spawnPos;
        public Vector2Int BossPosition => _bossPos;

        public DungeonRoomNode SpawnRoom => _rooms.TryGetValue(_spawnPos, out var room) ? room : null;
        public DungeonRoomNode BossRoom => _rooms.TryGetValue(_bossPos, out var room) ? room : null;

        public bool ContainsRoom(Vector2Int gridPos) => _rooms.ContainsKey(gridPos);

        public void AddRoom(DungeonRoomNode room)
        {
            _rooms[room.GridPosition] = room;
        }

        public void ConnectRooms(Vector2Int posA, Vector2Int posB, Vector3 worldA, Vector3 worldB)
        {
            if (_rooms.TryGetValue(posA, out var roomA) && _rooms.TryGetValue(posB, out var roomB))
            {
                roomA.AddConnection(posB);
                roomB.AddConnection(posA);
                _corridors.Add(new DungeonCorridorEdge(posA, posB, worldA, worldB));
            }
        }

        public void SetSpawnRoom(Vector2Int spawnPos)
        {
            _spawnPos = spawnPos;
            if (_rooms.TryGetValue(spawnPos, out var room))
            {
                room.RoomType = DungeonRoomType.Spawn;
                room.DistanceFromSpawn = 0;
            }
        }

        /// <summary>
        /// Thuật toán BFS tính khoảng cách ngắn nhất từ Spawn Room tới mọi phòng,
        /// và chọn phòng xa nhất có khoảng cách lớn nhất làm Boss Room (Mục 4.1 bước 6).
        /// </summary>
        public Vector2Int CalculateDistancesAndDesignateBossRoom()
        {
            if (!_rooms.ContainsKey(_spawnPos))
            {
                return Vector2Int.zero;
            }

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();

            queue.Enqueue(_spawnPos);
            visited.Add(_spawnPos);

            Vector2Int furthestRoom = _spawnPos;
            int maxDistance = -1;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                DungeonRoomNode currentNode = _rooms[current];

                if (currentNode.DistanceFromSpawn > maxDistance)
                {
                    maxDistance = currentNode.DistanceFromSpawn;
                    furthestRoom = current;
                }

                foreach (var neighbor in currentNode.ConnectedNeighbors)
                {
                    if (!visited.Contains(neighbor) && _rooms.TryGetValue(neighbor, out var neighborNode))
                    {
                        visited.Add(neighbor);
                        neighborNode.DistanceFromSpawn = currentNode.DistanceFromSpawn + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            _bossPos = furthestRoom;
            if (_rooms.TryGetValue(_bossPos, out var bossRoomNode))
            {
                bossRoomNode.RoomType = DungeonRoomType.Boss;
            }

            return _bossPos;
        }

        /// <summary>
        /// Tìm đường đi ngắn nhất từ start tới end theo thuật toán BFS (Mục 17.1).
        /// </summary>
        public List<Vector2Int> FindShortestPath(Vector2Int start, Vector2Int end)
        {
            var path = new List<Vector2Int>();
            if (!_rooms.ContainsKey(start) || !_rooms.ContainsKey(end)) return path;

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var parent = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(start);
            visited.Add(start);

            bool found = false;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == end)
                {
                    found = true;
                    break;
                }

                if (_rooms.TryGetValue(current, out var node))
                {
                    foreach (var neighbor in node.ConnectedNeighbors)
                    {
                        if (!visited.Contains(neighbor) && _rooms.ContainsKey(neighbor))
                        {
                            visited.Add(neighbor);
                            parent[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (found)
            {
                var curr = end;
                while (curr != start)
                {
                    path.Add(curr);
                    curr = parent[curr];
                }
                path.Add(start);
                path.Reverse();
            }

            return path;
        }

        /// <summary>
        /// Phân bổ loại phòng tất định bằng System.Random(dungeonSeed ^ 0x2F6B) theo Mục 17.1.
        /// Gán 1 Upgrade, 1 Treasure, 1 Shop ưu tiên ở nhánh cụt ngoài đường chính; phần còn lại là Combat.
        /// </summary>
        public void AllocateRoomTypes(int dungeonSeed)
        {
            // BẮT BUỘC dùng một System.Random riêng dựng từ seed (dungeonSeed ^ 0x2F6B)
            var rng = new System.Random(dungeonSeed ^ 0x2F6B);

            // 1. Lấy "đường chính" = đường đi ngắn nhất Spawn -> Boss
            var mainPathList = FindShortestPath(_spawnPos, _bossPos);
            var mainPathSet = new HashSet<Vector2Int>(mainPathList);

            // Thu thập các nhóm ứng viên không phải Spawn và Boss
            var p1Candidates = new List<DungeonRoomNode>(); // Ưu tiên 1: phòng cụt (1 kết nối), ngoài đường chính
            var p2Candidates = new List<DungeonRoomNode>(); // Ưu tiên 2: phòng ngoài đường chính (>1 kết nối)
            var p3Candidates = new List<DungeonRoomNode>(); // Ưu tiên 3: phòng nằm trên đường chính

            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;
                if (room.GridPosition == _spawnPos || room.GridPosition == _bossPos)
                    continue;

                if (!mainPathSet.Contains(room.GridPosition))
                {
                    if (room.ConnectedNeighbors.Count == 1)
                    {
                        p1Candidates.Add(room);
                    }
                    else
                    {
                        p2Candidates.Add(room);
                    }
                }
                else
                {
                    p3Candidates.Add(room);
                }
            }

            var chosenCandidates = new List<DungeonRoomNode>();

            // 2. Ứng viên ưu tiên 1: phòng cụt, ngoài đường chính
            chosenCandidates.AddRange(p1Candidates);

            // 3. Nếu chưa đủ 3 ứng viên: thêm ưu tiên 2 (phòng ngoài đường chính kể cả không cụt)
            if (chosenCandidates.Count < 3)
            {
                chosenCandidates.AddRange(p2Candidates);
            }

            // 4. Nếu vẫn chưa đủ: lấy phòng bất kỳ còn lại không phải Spawn/Boss, ưu tiên phòng xa Spawn nhất; ghi Debug.LogWarning
            if (chosenCandidates.Count < 3)
            {
                p3Candidates.Sort((a, b) => b.DistanceFromSpawn.CompareTo(a.DistanceFromSpawn));
                int needed = 3 - chosenCandidates.Count;
                for (int i = 0; i < needed && i < p3Candidates.Count; i++)
                {
                    chosenCandidates.Add(p3Candidates[i]);
                }

                Debug.LogWarning("[DungeonGrid] Không đủ phòng ngoài đường chính cho Upgrade/Treasure/Shop; phải chọn phòng trên đường chính.");
            }

            // 5. Xáo thứ tự ứng viên bằng rng riêng, lần lượt gán Upgrade, Treasure, Shop. Mọi phòng còn lại là Combat.
            for (int i = chosenCandidates.Count - 1; i > 0; i--)
            {
                int swapIdx = rng.Next(i + 1);
                var temp = chosenCandidates[i];
                chosenCandidates[i] = chosenCandidates[swapIdx];
                chosenCandidates[swapIdx] = temp;
            }

            var specialRooms = new HashSet<Vector2Int>();
            if (chosenCandidates.Count > 0)
            {
                chosenCandidates[0].RoomType = DungeonRoomType.Upgrade;
                specialRooms.Add(chosenCandidates[0].GridPosition);
            }
            if (chosenCandidates.Count > 1)
            {
                chosenCandidates[1].RoomType = DungeonRoomType.Treasure;
                specialRooms.Add(chosenCandidates[1].GridPosition);
            }
            if (chosenCandidates.Count > 2)
            {
                chosenCandidates[2].RoomType = DungeonRoomType.Shop;
                specialRooms.Add(chosenCandidates[2].GridPosition);
            }

            // Gán mọi phòng còn lại thành Combat (ngoại trừ Spawn và Boss)
            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;
                if (room.GridPosition == _spawnPos)
                {
                    room.RoomType = DungeonRoomType.Spawn;
                }
                else if (room.GridPosition == _bossPos)
                {
                    room.RoomType = DungeonRoomType.Boss;
                }
                else if (!specialRooms.Contains(room.GridPosition))
                {
                    room.RoomType = DungeonRoomType.Combat;
                }
            }
        }
    }
}
