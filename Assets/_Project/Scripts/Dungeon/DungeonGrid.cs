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
        public DungeonRoomType RoomType = DungeonRoomType.Normal;
        public int DistanceFromSpawn = 0;
        public List<Vector2Int> ConnectedNeighbors = new List<Vector2Int>();
        public Vector3 WorldCenter;

        public DungeonRoomNode(Vector2Int gridPos, Vector3 worldCenter, DungeonRoomType roomType = DungeonRoomType.Normal)
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
    }
}
