// Script: DungeonMapUI.cs
// Mục đích: Bản đồ hầm ngục toàn màn hình bật/tắt bằng phím Tab (Mục 16.8 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections.Generic;
using Hellfire.Dungeon;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class DungeonMapUI : MonoBehaviour
    {
        public static DungeonMapUI Instance { get; private set; }

        [Header("UI Containers")]
        [SerializeField] private GameObject _mapPanel;
        [SerializeField] private RectTransform _mapContentContainer;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _legendText;

        [Header("Settings (Mục 16.8)")]
        [SerializeField] private bool _revealBossRoom = true; // Mặc định bật để người chơi không bị lạc
        [SerializeField] private float _roomCellSize = 44f;
        [SerializeField] private float _gridSpacing = 56f;

        private readonly HashSet<Vector2Int> _visitedRooms = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _revealedRooms = new HashSet<Vector2Int>();

        private readonly Dictionary<Vector2Int, GameObject> _roomUIObjects = new Dictionary<Vector2Int, GameObject>();
        private readonly List<GameObject> _corridorUIObjects = new List<GameObject>();
        private readonly List<GameObject> _playerMarkerObjects = new List<GameObject>();

        private DungeonGrid _cachedGrid;
        private bool _isMapOpen = false;
        private float _lastUpdateCheck = 0f;

        public bool IsMapOpen => _isMapOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUIHierarchyIfMissing();
        }

        private void Start()
        {
            if (_mapPanel != null)
            {
                _mapPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // Phím Tab bật/tắt bản đồ toàn màn hình (Mục 16.8)
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleMap();
            }

            // Cập nhật các phòng đã đi qua định kỳ mỗi 0.2s
            if (Time.time - _lastUpdateCheck >= 0.2f)
            {
                _lastUpdateCheck = Time.time;
                UpdatePlayerVisitations();
            }

            // Nếu map đang mở thì cập nhật vị trí người chơi và trạng thái phòng
            if (_isMapOpen)
            {
                UpdateMapDisplay();
            }
        }

        public void ToggleMap()
        {
            _isMapOpen = !_isMapOpen;

            if (_mapPanel != null)
            {
                _mapPanel.SetActive(_isMapOpen);
            }

            if (_isMapOpen)
            {
                RefreshMapGeometry();
                UpdatePlayerVisitations();
                UpdateMapDisplay();
            }
        }

        private DungeonGrid GetActiveGrid()
        {
            if (DungeonManager.Instance != null && DungeonManager.Instance.GetComponent<DungeonGenerator>() != null)
            {
                return DungeonManager.Instance.GetComponent<DungeonGenerator>().ActiveGrid;
            }
            return null;
        }

        private void UpdatePlayerVisitations()
        {
            var grid = GetActiveGrid();
            if (grid == null) return;

            // Luôn mở phòng Spawn
            _visitedRooms.Add(grid.SpawnPosition);

            // Duyệt vị trí của mọi người chơi đang có trong phòng (Mục 16.8: mỗi client tự tính từ vị trí người chơi)
            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        Vector3 playerPos = client.PlayerObject.transform.position;

                        foreach (var kvp in grid.Rooms)
                        {
                            var room = kvp.Value;
                            // Nếu người chơi ở gần tâm phòng (bán kính 9m)
                            if (Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(room.WorldCenter.x, 0f, room.WorldCenter.z)) <= 9.0f)
                            {
                                _visitedRooms.Add(room.GridPosition);
                            }
                        }
                    }
                }
            }

            // Tính các phòng liền kề phòng đã đi qua (Revealed)
            _revealedRooms.Clear();
            foreach (var visitedCoord in _visitedRooms)
            {
                if (grid.Rooms.TryGetValue(visitedCoord, out var roomNode))
                {
                    foreach (var neighbor in roomNode.ConnectedNeighbors)
                    {
                        if (!_visitedRooms.Contains(neighbor))
                        {
                            _revealedRooms.Add(neighbor);
                        }
                    }
                }
            }

            // Phòng Boss luôn hiện nếu revealBossRoom bật (Mục 16.8)
            if (_revealBossRoom && grid.BossRoom != null)
            {
                _revealedRooms.Add(grid.BossPosition);
            }
        }

        private void RefreshMapGeometry()
        {
            var grid = GetActiveGrid();
            if (grid == null || grid == _cachedGrid) return;

            _cachedGrid = grid;

            // Dọn dẹp UI cũ
            foreach (var kvp in _roomUIObjects) Destroy(kvp.Value);
            _roomUIObjects.Clear();

            foreach (var obj in _corridorUIObjects) Destroy(obj);
            _corridorUIObjects.Clear();

            if (_mapContentContainer == null) return;

            // 1. Vẽ các hành lang (Corridors)
            foreach (var edge in grid.Corridors)
            {
                Vector2 posA = GridToMapAnchoredPosition(edge.RoomA);
                Vector2 posB = GridToMapAnchoredPosition(edge.RoomB);
                Vector2 mid = (posA + posB) * 0.5f;

                var lineObj = new GameObject($"Corridor_{edge.RoomA}_{edge.RoomB}", typeof(RectTransform), typeof(Image));
                lineObj.transform.SetParent(_mapContentContainer, false);
                var lineRect = lineObj.GetComponent<RectTransform>();
                lineRect.anchoredPosition = mid;

                Vector2 diff = posB - posA;
                float length = diff.magnitude;
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                lineRect.sizeDelta = new Vector2(length, 8f);
                lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

                var img = lineObj.GetComponent<Image>();
                img.color = new Color(0.3f, 0.35f, 0.45f, 0.6f);
                _corridorUIObjects.Add(lineObj);
            }

            // 2. Vẽ các phòng (Rooms)
            foreach (var kvp in grid.Rooms)
            {
                var room = kvp.Value;
                Vector2 pos = GridToMapAnchoredPosition(room.GridPosition);

                var roomObj = new GameObject($"Room_{room.GridPosition.x}_{room.GridPosition.y}", typeof(RectTransform), typeof(Image));
                roomObj.transform.SetParent(_mapContentContainer, false);
                var rRect = roomObj.GetComponent<RectTransform>();
                rRect.anchoredPosition = pos;
                rRect.sizeDelta = new Vector2(_roomCellSize, _roomCellSize);

                // Biểu tượng bên trong phòng (Ví dụ Boss skull hoặc Spawn S)
                var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
                iconObj.transform.SetParent(roomObj.transform, false);
                var iRect = iconObj.GetComponent<RectTransform>();
                iRect.anchorMin = Vector2.zero;
                iRect.anchorMax = Vector2.one;
                iRect.offsetMin = Vector2.zero;
                iRect.offsetMax = Vector2.zero;
                var iconText = iconObj.GetComponent<TextMeshProUGUI>();
                iconText.alignment = TextAlignmentOptions.Center;
                iconText.fontSize = 22;
                iconText.fontStyle = FontStyles.Bold;

                if (room.RoomType == DungeonRoomType.Boss)
                {
                    iconText.text = "💀"; // Biểu tượng đầu lâu phòng boss theo mục 16.8
                    iconText.color = new Color(1f, 0.2f, 0.2f, 1f);
                }
                else if (room.RoomType == DungeonRoomType.Spawn)
                {
                    iconText.text = "S";
                    iconText.color = new Color(0.1f, 1f, 0.6f, 1f);
                }

                _roomUIObjects[room.GridPosition] = roomObj;
            }
        }

        private void UpdateMapDisplay()
        {
            var grid = GetActiveGrid();
            if (grid == null) return;

            // Cập nhật trạng thái từng phòng
            foreach (var kvp in grid.Rooms)
            {
                Vector2Int coord = kvp.Key;
                var room = kvp.Value;

                if (!_roomUIObjects.TryGetValue(coord, out var roomObj)) continue;

                var img = roomObj.GetComponent<Image>();
                var iconText = roomObj.GetComponentInChildren<TextMeshProUGUI>();

                if (_visitedRooms.Contains(coord))
                {
                    // Phòng đã đi qua: Sáng (Mục 16.8)
                    roomObj.SetActive(true);
                    switch (room.RoomType)
                    {
                        case DungeonRoomType.Boss:
                            img.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);
                            break;
                        case DungeonRoomType.Upgrade:
                            img.color = new Color(0.2f, 0.5f, 0.75f, 0.95f);
                            break;
                        case DungeonRoomType.Treasure:
                            img.color = new Color(0.7f, 0.6f, 0.15f, 0.95f);
                            break;
                        case DungeonRoomType.Shop:
                            img.color = new Color(0.75f, 0.45f, 0.15f, 0.95f);
                            break;
                        case DungeonRoomType.Spawn:
                            img.color = new Color(0.15f, 0.65f, 0.45f, 0.95f);
                            break;
                        default:
                            img.color = new Color(0.45f, 0.55f, 0.7f, 0.95f);
                            break;
                    }
                }
                else if (_revealedRooms.Contains(coord))
                {
                    // Phòng kề phòng đã đi qua (hoặc Boss): Mờ (Mục 16.8)
                    roomObj.SetActive(true);
                    switch (room.RoomType)
                    {
                        case DungeonRoomType.Boss:
                            img.color = new Color(0.7f, 0.1f, 0.1f, 0.55f);
                            break;
                        case DungeonRoomType.Upgrade:
                            img.color = new Color(0.15f, 0.3f, 0.45f, 0.5f);
                            break;
                        case DungeonRoomType.Treasure:
                            img.color = new Color(0.4f, 0.35f, 0.1f, 0.5f);
                            break;
                        case DungeonRoomType.Shop:
                            img.color = new Color(0.45f, 0.25f, 0.1f, 0.5f);
                            break;
                        default:
                            img.color = new Color(0.25f, 0.3f, 0.4f, 0.4f);
                            break;
                    }
                }
                else
                {
                    // Phòng khác: Ẩn (Mục 16.8)
                    roomObj.SetActive(false);
                    continue;
                }

                // Cập nhật biểu tượng theo mục 17.9
                if (iconText != null)
                {
                    switch (room.RoomType)
                    {
                        case DungeonRoomType.Boss:
                            iconText.text = "💀";
                            iconText.color = new Color(1f, 0.2f, 0.2f, 1f);
                            break;
                        case DungeonRoomType.Upgrade:
                            iconText.text = "✦"; // Tế đàn (Mục 17.9)
                            iconText.color = new Color(0.35f, 0.9f, 1f, 1f);
                            break;
                        case DungeonRoomType.Treasure:
                            iconText.text = "📦"; // Rương (Mục 17.9)
                            iconText.color = new Color(1f, 0.88f, 0.25f, 1f);
                            break;
                        case DungeonRoomType.Shop:
                            iconText.text = "🪙"; // Đồng xu (Mục 17.9)
                            iconText.color = new Color(1f, 0.75f, 0.15f, 1f);
                            break;
                        case DungeonRoomType.Combat:
                            bool isCleared = DungeonManager.ClearedRoomsClient.Contains(coord);
                            iconText.text = isCleared ? "" : "🔒"; // Ổ khóa nhỏ khi chưa dọn, biến mất khi Cleared (Mục 17.9)
                            iconText.color = new Color(1f, 0.6f, 0.25f, 0.95f);
                            break;
                        case DungeonRoomType.Spawn:
                        default:
                            iconText.text = ""; // Spawn không biểu tượng theo mục 17.9
                            break;
                    }
                }
            }

            // Cập nhật marker người chơi (Đồng đội chấm màu/số, bản thân mũi tên chỉ hướng)
            UpdatePlayerMarkers();
        }

        private void UpdatePlayerMarkers()
        {
            // Xóa hoặc tái sử dụng marker
            int markerIndex = 0;

            if (NetworkManager.Singleton != null)
            {
                var cam = Camera.main;

                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject == null) continue;

                    bool isLocal = (client.ClientId == NetworkManager.Singleton.LocalClientId);
                    Vector3 worldPos = client.PlayerObject.transform.position;
                    Vector2 mapPos = WorldToMapAnchoredPosition(worldPos);

                    GameObject marker;
                    if (markerIndex < _playerMarkerObjects.Count)
                    {
                        marker = _playerMarkerObjects[markerIndex];
                        marker.SetActive(true);
                    }
                    else
                    {
                        marker = CreatePlayerMarkerObject();
                        _playerMarkerObjects.Add(marker);
                    }

                    var rect = marker.GetComponent<RectTransform>();
                    rect.anchoredPosition = mapPos;

                    var identity = client.PlayerObject.GetComponent<PlayerIdentity>();
                    int colorIdx = identity != null ? identity.ColorIndex.Value : (int)(client.ClientId % 4);
                    Color playerColor = PlayerPalette.GetColor(colorIdx);

                    var img = marker.GetComponent<Image>();
                    var txt = marker.GetComponentInChildren<TextMeshProUGUI>();

                    if (isLocal)
                    {
                        // Bản thân: Mũi tên chỉ hướng nhìn (Mục 16.8)
                        img.color = new Color(1f, 1f, 1f, 0.2f);
                        txt.text = "▲";
                        txt.fontSize = 20;
                        txt.color = playerColor;

                        // Xoay mũi tên theo hướng nhìn camera
                        if (cam != null)
                        {
                            float yaw = cam.transform.eulerAngles.y;
                            rect.localRotation = Quaternion.Euler(0f, 0f, -yaw);
                        }
                    }
                    else
                    {
                        // Đồng đội: Chấm đúng màu + số 1-4 theo mục 16.1 & 16.8
                        img.color = playerColor;
                        rect.localRotation = Quaternion.identity;
                        txt.text = $"{colorIdx + 1}";
                        txt.fontSize = 13;
                        txt.color = Color.white;
                    }

                    markerIndex++;
                }
            }

            // Tắt các marker thừa
            for (int i = markerIndex; i < _playerMarkerObjects.Count; i++)
            {
                _playerMarkerObjects[i].SetActive(false);
            }
        }

        private GameObject CreatePlayerMarkerObject()
        {
            var obj = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(_mapContentContainer, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(22f, 22f);

            var txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(obj.transform, false);
            var tRect = txtObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var txt = txtObj.GetComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;

            return obj;
        }

        private Vector2 GridToMapAnchoredPosition(Vector2Int gridPos)
        {
            return new Vector2(gridPos.x * _gridSpacing, gridPos.y * _gridSpacing);
        }

        private Vector2 WorldToMapAnchoredPosition(Vector3 worldPos)
        {
            float gridCellSize = 20f;
            return new Vector2((worldPos.x / gridCellSize) * _gridSpacing, (worldPos.z / gridCellSize) * _gridSpacing);
        }

        private void BuildUIHierarchyIfMissing()
        {
            Transform root = transform;

            if (_mapPanel == null)
            {
                var panelObj = new GameObject("DungeonMapPanel", typeof(RectTransform), typeof(Image));
                panelObj.transform.SetParent(root, false);
                var pRect = panelObj.GetComponent<RectTransform>();
                pRect.anchorMin = Vector2.zero;
                pRect.anchorMax = Vector2.one;
                pRect.offsetMin = Vector2.zero;
                pRect.offsetMax = Vector2.zero;

                var pImg = panelObj.GetComponent<Image>();
                pImg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f); // Nền mờ toàn màn hình
                _mapPanel = panelObj;

                // Title
                var titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(panelObj.transform, false);
                var tRect = titleObj.GetComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0.5f, 1f);
                tRect.anchorMax = new Vector2(0.5f, 1f);
                tRect.anchoredPosition = new Vector2(0f, -40f);
                tRect.sizeDelta = new Vector2(600f, 40f);
                _titleText = titleObj.GetComponent<TextMeshProUGUI>();
                _titleText.text = "🗺️ BẢN ĐỒ HẦM NGỤC (TAB ĐỂ ĐÓNG)";
                _titleText.fontSize = 24;
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = new Color(0.85f, 0.9f, 1f, 1f);

                // Container chứa sơ đồ
                var mapBox = new GameObject("MapContentContainer", typeof(RectTransform));
                mapBox.transform.SetParent(panelObj.transform, false);
                _mapContentContainer = mapBox.GetComponent<RectTransform>();
                _mapContentContainer.anchorMin = new Vector2(0.5f, 0.5f);
                _mapContentContainer.anchorMax = new Vector2(0.5f, 0.5f);
                _mapContentContainer.sizeDelta = new Vector2(800f, 600f);
                _mapContentContainer.anchoredPosition = Vector2.zero;

                // Legend chú thích
                var legendObj = new GameObject("LegendText", typeof(RectTransform), typeof(TextMeshProUGUI));
                legendObj.transform.SetParent(panelObj.transform, false);
                var lRect = legendObj.GetComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0.5f, 0f);
                lRect.anchorMax = new Vector2(0.5f, 0f);
                lRect.anchoredPosition = new Vector2(0f, 35f);
                lRect.sizeDelta = new Vector2(900f, 35f);
                _legendText = legendObj.GetComponent<TextMeshProUGUI>();
                _legendText.text = "<color=#7799CC>[■] Đã Qua</color>   |   <color=#445566>[◽] Liền Kề</color>   |   <color=#FF3333>💀 Boss</color>   |   <color=#55CCFF>✦ Tế Đàn</color>   |   <color=#FFD700>📦 Rương</color>   |   <color=#FFA500>🪙 Shop</color>   |   <color=#FF8844>🔒 Combat</color>   |   ▲ Bản thân";
                _legendText.fontSize = 15;
                _legendText.alignment = TextAlignmentOptions.Center;
                _legendText.color = new Color(0.8f, 0.85f, 0.9f, 0.9f);

                panelObj.SetActive(false);
            }
        }
    }
}
