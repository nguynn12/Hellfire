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
        [Tooltip("Khoảng cách giữa các tâm ô phòng trên lưới (mục 17.2: mặc định 26m)")]
        [SerializeField] private float _gridCellSize = 26f;

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

        [Header("Door Frame & Corridor Visuals (Mục 17.2)")]
        [Tooltip("Vật liệu riêng cho sàn hành lang (tối hơn sàn phòng)")]
        [SerializeField] private Material _corridorFloorMaterial;
        [Tooltip("Vật liệu khung cửa phòng")]
        [SerializeField] private Material _doorFrameMaterial;
        [SerializeField] private float _doorFramePostWidth = 0.35f;
        [SerializeField] private float _doorFramePostDepth = 1.2f;
        [SerializeField] private float _doorFrameLintelHeight = 0.4f;

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
                        var newRoom = new DungeonRoomNode(candidateCoord, candWorldPos, DungeonRoomType.Combat);

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

            // Phân bổ loại phòng tất định theo Mục 17.1 (Spawn, Combat, Upgrade, Treasure, Shop, Boss)
            _activeGrid.AllocateRoomTypes(dungeonSeed);

            // 6. Dựng hình học thế giới thực (Instantiate hình học phòng và hành lang)
            BuildDungeonGeometry(_activeGrid, dungeonSeed);

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

        private void BuildDungeonGeometry(DungeonGrid grid, int dungeonSeed)
        {
            if (_dungeonRoot == null)
            {
                var newRoot = new GameObject("DungeonGeometryRoot");
                _dungeonRoot = newRoot.transform;
            }

            // Dựng các phòng
            foreach (var kvp in grid.Rooms)
            {
                BuildRoomGeometry(kvp.Value, dungeonSeed);
            }

            // Dựng các hành lang kết nối
            foreach (var corridor in grid.Corridors)
            {
                BuildCorridorGeometry(corridor);
            }
        }

        private void BuildRoomGeometry(DungeonRoomNode room, int dungeonSeed)
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
            // Nếu có kết nối hành lang theo hướng đó thì tạo cửa mở (doorway) kèm khung cửa (Mục 17.2)
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

            // 3. Cột trong phòng Combat (Mục 17.3: 0–4 cột ở 4 ô góc ±4, 0, ±4 dựng trước khi bake NavMesh)
            if (room.RoomType == DungeonRoomType.Combat)
            {
                BuildCombatRoomPillars(roomObj.transform, room.GridPosition, dungeonSeed);
            }
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

                // Dựng khung cửa (2 trụ + 1 dầm) ở mỗi cửa phòng (Mục 17.2)
                BuildDoorFrame(parent, dir, wallPos);
            }
        }

        private void BuildDoorFrame(Transform parent, Vector2Int dir, Vector3 wallPos)
        {
            var frameObj = new GameObject($"DoorFrame_{dir}");
            frameObj.transform.SetParent(parent);

            bool isZAxisWall = (dir.y != 0);

            if (isZAxisWall)
            {
                float halfCorridor = _corridorWidth * 0.5f;
                float postCenterOffset = halfCorridor - (_doorFramePostWidth * 0.5f);

                // Trụ trái (-X)
                var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftPost.name = "Post_Left";
                leftPost.transform.SetParent(frameObj.transform);
                leftPost.transform.localPosition = new Vector3(-postCenterOffset, _wallHeight * 0.5f, wallPos.z);
                leftPost.transform.localScale = new Vector3(_doorFramePostWidth, _wallHeight, _doorFramePostDepth);
                ApplyDoorFrameMaterial(leftPost);

                // Trụ phải (+X)
                var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightPost.name = "Post_Right";
                rightPost.transform.SetParent(frameObj.transform);
                rightPost.transform.localPosition = new Vector3(postCenterOffset, _wallHeight * 0.5f, wallPos.z);
                rightPost.transform.localScale = new Vector3(_doorFramePostWidth, _wallHeight, _doorFramePostDepth);
                ApplyDoorFrameMaterial(rightPost);

                // Dầm trên (Lintel)
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Lintel";
                lintel.transform.SetParent(frameObj.transform);
                lintel.transform.localPosition = new Vector3(0f, _wallHeight - (_doorFrameLintelHeight * 0.5f), wallPos.z);
                lintel.transform.localScale = new Vector3(_corridorWidth, _doorFrameLintelHeight, _doorFramePostDepth);
                ApplyDoorFrameMaterial(lintel);
            }
            else
            {
                float halfCorridor = _corridorWidth * 0.5f;
                float postCenterOffset = halfCorridor - (_doorFramePostWidth * 0.5f);

                // Trụ trên (+Z)
                var topPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topPost.name = "Post_Top";
                topPost.transform.SetParent(frameObj.transform);
                topPost.transform.localPosition = new Vector3(wallPos.x, _wallHeight * 0.5f, postCenterOffset);
                topPost.transform.localScale = new Vector3(_doorFramePostDepth, _wallHeight, _doorFramePostWidth);
                ApplyDoorFrameMaterial(topPost);

                // Trụ dưới (-Z)
                var bottomPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bottomPost.name = "Post_Bottom";
                bottomPost.transform.SetParent(frameObj.transform);
                bottomPost.transform.localPosition = new Vector3(wallPos.x, _wallHeight * 0.5f, -postCenterOffset);
                bottomPost.transform.localScale = new Vector3(_doorFramePostDepth, _wallHeight, _doorFramePostWidth);
                ApplyDoorFrameMaterial(bottomPost);

                // Dầm trên (Lintel)
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Lintel";
                lintel.transform.SetParent(frameObj.transform);
                lintel.transform.localPosition = new Vector3(wallPos.x, _wallHeight - (_doorFrameLintelHeight * 0.5f), 0f);
                lintel.transform.localScale = new Vector3(_doorFramePostDepth, _doorFrameLintelHeight, _corridorWidth);
                ApplyDoorFrameMaterial(lintel);
            }
        }

        private void ApplyDoorFrameMaterial(GameObject obj)
        {
            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (_doorFrameMaterial != null)
                {
                    renderer.sharedMaterial = _doorFrameMaterial;
                }
                else if (_wallMaterial != null)
                {
                    renderer.sharedMaterial = _wallMaterial;
                }
            }
        }

        private void BuildCombatRoomPillars(Transform parent, Vector2Int gridPos, int dungeonSeed)
        {
            // RNG riêng cho cột theo mục 17.3, bảo đảm kết quả giống hệt trên mọi máy
            int roomSeed = dungeonSeed ^ (gridPos.x * 73856093) ^ (gridPos.y * 19349663) ^ 0x5C01;
            var rng = new System.Random(roomSeed);
            int pillarCount = rng.Next(0, 5); // 0 đến 4 cột

            if (pillarCount == 0) return;

            var cornerOffsets = new List<Vector3>
            {
                new Vector3(-4f, 2f, -4f),
                new Vector3(-4f, 2f, 4f),
                new Vector3(4f, 2f, -4f),
                new Vector3(4f, 2f, 4f)
            };

            for (int i = cornerOffsets.Count - 1; i > 0; i--)
            {
                int swapIdx = rng.Next(i + 1);
                var temp = cornerOffsets[i];
                cornerOffsets[i] = cornerOffsets[swapIdx];
                cornerOffsets[swapIdx] = temp;
            }

            for (int i = 0; i < pillarCount; i++)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Pillar_{i + 1}";
                pillar.transform.SetParent(parent);
                pillar.transform.localPosition = cornerOffsets[i];
                pillar.transform.localScale = new Vector3(2f, 4f, 2f);
                ApplyWallMaterial(pillar);
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
            if (renderer != null)
            {
                if (_corridorFloorMaterial != null)
                {
                    renderer.sharedMaterial = _corridorFloorMaterial;
                }
                else if (_floorMaterial != null)
                {
                    renderer.sharedMaterial = _floorMaterial;
                }
            }

            // 2 đèn điểm ở 1/3 và 2/3 chiều dài, nháy nhẹ theo mục 9.1 và 17.2
            Vector3 light1Pos = Vector3.Lerp(corridor.WorldStart, corridor.WorldEnd, 1f / 3f);
            light1Pos.y = _wallHeight - 1.2f;
            CreateCorridorLight(corridorObj.transform, light1Pos, "CorridorLight_1");

            Vector3 light2Pos = Vector3.Lerp(corridor.WorldStart, corridor.WorldEnd, 2f / 3f);
            light2Pos.y = _wallHeight - 1.2f;
            CreateCorridorLight(corridorObj.transform, light2Pos, "CorridorLight_2");
        }

        private void CreateCorridorLight(Transform parent, Vector3 worldPos, string name)
        {
            var lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent);
            lightObj.transform.position = worldPos;

            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.color = new Color(1.0f, 0.72f, 0.45f);
            light.intensity = 1.2f;

            lightObj.AddComponent<CorridorLightFlicker>();
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
