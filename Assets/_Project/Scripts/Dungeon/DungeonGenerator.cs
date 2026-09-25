// Script: DungeonGenerator.cs
// Mục đích: Thuật toán sinh bản đồ Room-and-Corridor tất định bằng System.Random(seed) (Mục 4.1 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server và mọi Client cùng chạy cục bộ từ cùng seed, kết quả hình học đồng nhất 100%).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Grid & Room Settings")]
        [Tooltip("Khoảng cách giữa các tâm ô phòng trên lưới (mục 4.1: ví dụ 20m x 20m)")]
        [SerializeField] private float _gridCellSize = 20f;

        [Tooltip("Kích thước phòng (rộng x dài)")]
        [SerializeField] private float _roomSize = 14f;

        [Tooltip("Chiều cao tường phòng")]
        [SerializeField] private float _wallHeight = 4.5f;

        [Tooltip("Chiều rộng hành lang")]
        [SerializeField] private float _corridorWidth = 4f;

        [Header("Room Count Limits (Mục 4.1: 8–14 phòng)")]
        [SerializeField] private int _minRoomCount = 8;
        [SerializeField] private int _maxRoomCount = 14;

        [Header("Prefabs / Materials")]
        [SerializeField] private Material _floorMaterial;
        [SerializeField] private Material _wallMaterial;
        [SerializeField] private Material _spawnFloorMaterial;
        [SerializeField] private Material _bossFloorMaterial;

        [Header("Hierarchy Root")]
        [SerializeField] private Transform _dungeonRoot;

        private DungeonGrid _activeGrid;

        public DungeonGrid ActiveGrid => _activeGrid;
        public float GridCellSize => _gridCellSize;
        public float RoomSize => _roomSize;

        public event Action<DungeonGrid> OnDungeonGenerated;

        /// <summary>
        /// Sinh sơ đồ logic 2D và dựng hình học thế giới thực từ seed (Mục 4.1).
        /// TUYỆT ĐỐI dùng System.Random, không dùng UnityEngine.Random cho logic.
        /// </summary>
        public DungeonGrid GenerateDungeon(int dungeonSeed)
        {
            ClearPreviousDungeon();

            // 1. Khởi tạo bộ sinh số ngẫu nhiên System.Random với seed đầu vào
            var rng = new System.Random(dungeonSeed);

            // 2. Xác định số lượng phòng trong khoảng [minRoomCount, maxRoomCount]
            int targetRoomCount = rng.Next(_minRoomCount, _maxRoomCount + 1);

            _activeGrid = new DungeonGrid();

            // 3. Đặt phòng Spawn tại gốc tọa độ (0, 0)
            Vector2Int spawnCoord = Vector2Int.zero;
            Vector3 spawnWorldPos = GridToWorld(spawnCoord);
            var spawnRoom = new DungeonRoomNode(spawnCoord, spawnWorldPos, DungeonRoomType.Spawn);
            _activeGrid.AddRoom(spawnRoom);
            _activeGrid.SetSpawnRoom(spawnCoord);

            var placedRoomCoords = new List<Vector2Int> { spawnCoord };

            // 4. Lặp đặt phòng liền kề cho đến khi đủ số lượng phòng
            while (placedRoomCoords.Count < targetRoomCount)
            {
                // Chọn ngẫu nhiên 1 phòng đã đặt
                int chosenIndex = rng.Next(placedRoomCoords.Count);
                Vector2Int fromCoord = placedRoomCoords[chosenIndex];

                // Thử 4 hướng ngẫu nhiên
                var shuffledDirs = GetShuffledDirections(rng);
                bool added = false;

                foreach (var dir in shuffledDirs)
                {
                    Vector2Int candidateCoord = fromCoord + dir;

                    if (!_activeGrid.ContainsRoom(candidateCoord))
                    {
                        Vector3 candWorldPos = GridToWorld(candidateCoord);
                        var newRoom = new DungeonRoomNode(candidateCoord, candWorldPos, DungeonRoomType.Normal);

                        _activeGrid.AddRoom(newRoom);
                        _activeGrid.ConnectRooms(fromCoord, candidateCoord, GridToWorld(fromCoord), candWorldPos);

                        placedRoomCoords.Add(candidateCoord);
                        added = true;
                        break;
                    }
                }

                // Nếu phòng được chọn bị bao vây cả 4 hướng, tiếp tục vòng lặp để chọn phòng khác
                if (!added && placedRoomCoords.Count > 1)
                {
                    continue;
                }
            }

            // 5. Tính toán khoảng cách bằng BFS và chọn phòng xa nhất làm Boss Room (Mục 4.1 bước 6)
            _activeGrid.CalculateDistancesAndDesignateBossRoom();

            // 6. Dựng hình học thế giới thực (Instantiate hình học phòng và hành lang)
            BuildDungeonGeometry(_activeGrid);

            OnDungeonGenerated?.Invoke(_activeGrid);
            return _activeGrid;
        }

        private Vector2Int[] GetShuffledDirections(System.Random rng)
        {
            var dirs = (Vector2Int[])DungeonGrid.CardinalDirections.Clone();
            for (int i = dirs.Length - 1; i > 0; i--)
            {
                int swapIdx = rng.Next(i + 1);
                var temp = dirs[i];
                dirs[i] = dirs[swapIdx];
                dirs[swapIdx] = temp;
            }
            return dirs;
        }

        public Vector3 GridToWorld(Vector2Int gridCoord)
        {
            return new Vector3(gridCoord.x * _gridCellSize, 0f, gridCoord.y * _gridCellSize);
        }

        public void ClearPreviousDungeon()
        {
            if (_dungeonRoot == null)
            {
                var existingRoot = GameObject.Find("DungeonGeometryRoot");
                if (existingRoot != null)
                {
                    _dungeonRoot = existingRoot.transform;
                }
                else
                {
                    var newRoot = new GameObject("DungeonGeometryRoot");
                    _dungeonRoot = newRoot.transform;
                }
            }

            // Xóa sạch các child geometry cũ
            for (int i = _dungeonRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_dungeonRoot.GetChild(i).gameObject);
            }
        }

        private void BuildDungeonGeometry(DungeonGrid grid)
        {
            if (_dungeonRoot == null)
            {
                var newRoot = new GameObject("DungeonGeometryRoot");
                _dungeonRoot = newRoot.transform;
            }

            // Dựng các phòng
            foreach (var kvp in grid.Rooms)
            {
                BuildRoomGeometry(kvp.Value);
            }

            // Dựng các hành lang kết nối
            foreach (var corridor in grid.Corridors)
            {
                BuildCorridorGeometry(corridor);
            }
        }

        private void BuildRoomGeometry(DungeonRoomNode room)
        {
            var roomObj = new GameObject($"Room_{room.GridPosition.x}_{room.GridPosition.y}_{room.RoomType}");
            roomObj.transform.SetParent(_dungeonRoot);
            roomObj.transform.position = room.WorldCenter;

            // 1. Sàn phòng (Floor)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(roomObj.transform);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(_roomSize, 1f, _roomSize);

            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (room.RoomType == DungeonRoomType.Spawn && _spawnFloorMaterial != null)
                    renderer.sharedMaterial = _spawnFloorMaterial;
                else if (room.RoomType == DungeonRoomType.Boss && _bossFloorMaterial != null)
                    renderer.sharedMaterial = _bossFloorMaterial;
                else if (_floorMaterial != null)
                    renderer.sharedMaterial = _floorMaterial;
            }

            // 2. Bốn bức tường xung quanh phòng (Bắc, Nam, Đông, Tây)
            // Nếu có kết nối hành lang theo hướng đó thì tạo cửa mở (doorway)
            float halfRoom = _roomSize * 0.5f;
            float wallThickness = 1f;

            // Hướng Bắc (+Z)
            BuildRoomWall(roomObj.transform, room, new Vector2Int(0, 1),
                new Vector3(0f, _wallHeight * 0.5f, halfRoom),
                new Vector3(_roomSize, _wallHeight, wallThickness));

            // Hướng Nam (-Z)
            BuildRoomWall(roomObj.transform, room, new Vector2Int(0, -1),
                new Vector3(0f, _wallHeight * 0.5f, -halfRoom),
                new Vector3(_roomSize, _wallHeight, wallThickness));

            // Hướng Đông (+X)
            BuildRoomWall(roomObj.transform, room, new Vector2Int(1, 0),
                new Vector3(halfRoom, _wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, _wallHeight, _roomSize));

            // Hướng Tây (-X)
            BuildRoomWall(roomObj.transform, room, new Vector2Int(-1, 0),
                new Vector3(-halfRoom, _wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, _wallHeight, _roomSize));
        }

        private void BuildRoomWall(Transform parent, DungeonRoomNode room, Vector2Int dir, Vector3 wallPos, Vector3 wallScale)
        {
            Vector2Int neighborCoord = room.GridPosition + dir;
            bool hasDoorway = room.HasConnection(neighborCoord);

            if (!hasDoorway)
            {
                // Tường kín hoàn toàn
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{dir}";
                wall.transform.SetParent(parent);
                wall.transform.localPosition = wallPos;
                wall.transform.localScale = wallScale;

                ApplyWallMaterial(wall);
            }
            else
            {
                // Tường có cửa mở: chia làm 2 đoạn hai bên để chừa lối đi ở giữa bằng _corridorWidth
                bool isZAxisWall = (dir.y != 0); // Tường Bắc hoặc Nam -> đoạn tường trải theo trục X

                if (isZAxisWall)
                {
                    float sideWidth = (_roomSize - _corridorWidth) * 0.5f;
                    float offset = (_corridorWidth + sideWidth) * 0.5f;

                    // Đoạn trái
                    var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leftWall.name = $"Wall_{dir}_Left";
                    leftWall.transform.SetParent(parent);
                    leftWall.transform.localPosition = new Vector3(-offset, wallPos.y, wallPos.z);
                    leftWall.transform.localScale = new Vector3(sideWidth, _wallHeight, wallScale.z);
                    ApplyWallMaterial(leftWall);

                    // Đoạn phải
                    var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightWall.name = $"Wall_{dir}_Right";
                    rightWall.transform.SetParent(parent);
                    rightWall.transform.localPosition = new Vector3(offset, wallPos.y, wallPos.z);
                    rightWall.transform.localScale = new Vector3(sideWidth, _wallHeight, wallScale.z);
                    ApplyWallMaterial(rightWall);
                }
                else
                {
                    float sideLength = (_roomSize - _corridorWidth) * 0.5f;
                    float offset = (_corridorWidth + sideLength) * 0.5f;

                    // Đoạn trên (+Z)
                    var topWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    topWall.name = $"Wall_{dir}_Top";
                    topWall.transform.SetParent(parent);
                    topWall.transform.localPosition = new Vector3(wallPos.x, wallPos.y, offset);
                    topWall.transform.localScale = new Vector3(wallScale.x, _wallHeight, sideLength);
                    ApplyWallMaterial(topWall);

                    // Đoạn dưới (-Z)
                    var btmWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    btmWall.name = $"Wall_{dir}_Bottom";
                    btmWall.transform.SetParent(parent);
                    btmWall.transform.localPosition = new Vector3(wallPos.x, wallPos.y, -offset);
                    btmWall.transform.localScale = new Vector3(wallScale.x, _wallHeight, sideLength);
                    ApplyWallMaterial(btmWall);
                }
            }
        }

        private void BuildCorridorGeometry(DungeonCorridorEdge corridor)
        {
            var corridorObj = new GameObject($"Corridor_{corridor.RoomA}_to_{corridor.RoomB}");
            corridorObj.transform.SetParent(_dungeonRoot);

            Vector3 midPoint = (corridor.WorldStart + corridor.WorldEnd) * 0.5f;
            Vector3 diff = corridor.WorldEnd - corridor.WorldStart;
            float length = diff.magnitude - _roomSize + 1f; // chiều dài đoạn nối ngoài phòng
            if (length <= 0f) length = 2f;

            bool isHorizontal = Mathf.Abs(diff.x) > Mathf.Abs(diff.z);

            // Sàn hành lang
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CorridorFloor";
            floor.transform.SetParent(corridorObj.transform);
            floor.transform.position = new Vector3(midPoint.x, -0.5f, midPoint.z);

            if (isHorizontal)
            {
                floor.transform.localScale = new Vector3(length, 1f, _corridorWidth);

                // 2 tường 2 bên hành lang
                BuildCorridorWall(corridorObj.transform,
                    new Vector3(midPoint.x, _wallHeight * 0.5f, midPoint.z + _corridorWidth * 0.5f),
                    new Vector3(length, _wallHeight, 1f));

                BuildCorridorWall(corridorObj.transform,
                    new Vector3(midPoint.x, _wallHeight * 0.5f, midPoint.z - _corridorWidth * 0.5f),
                    new Vector3(length, _wallHeight, 1f));
            }
            else
            {
                floor.transform.localScale = new Vector3(_corridorWidth, 1f, length);

                // 2 tường 2 bên hành lang
                BuildCorridorWall(corridorObj.transform,
                    new Vector3(midPoint.x + _corridorWidth * 0.5f, _wallHeight * 0.5f, midPoint.z),
                    new Vector3(1f, _wallHeight, length));

                BuildCorridorWall(corridorObj.transform,
                    new Vector3(midPoint.x - _corridorWidth * 0.5f, _wallHeight * 0.5f, midPoint.z),
                    new Vector3(1f, _wallHeight, length));
            }

            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer != null && _floorMaterial != null)
            {
                renderer.sharedMaterial = _floorMaterial;
            }
        }

        private void BuildCorridorWall(Transform parent, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CorridorWall";
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            ApplyWallMaterial(wall);
        }

        private void ApplyWallMaterial(GameObject obj)
        {
            if (_wallMaterial != null)
            {
                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _wallMaterial;
                }
            }
        }
    }
}
