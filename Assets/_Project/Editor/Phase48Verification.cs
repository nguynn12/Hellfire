// Script: Phase48Verification.cs
// Purpose: Explicitly invoked smoke checks for section 20; never runs on editor startup.
// Execution: Editor only; runtime checks use the real Host/Lobby/Gameplay flow.
using System;
using System.IO;
using Hellfire.AI;
using Hellfire.Dungeon;
using Hellfire.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellfire.EditorTools
{
    public static class Phase48Verification
    {
        private static double _deadline;
        private static bool _started;
        [MenuItem("Hellfire/Phase 4.8/1 Prepare smoke test #&1")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                { Debug.LogWarning("[Phase48QA] Unsaved scene: smoke test cancelled."); return; }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Hellfire/Phase 4.8/2 Run Host smoke test #&2")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || NetworkConnectManager.Instance == null) return;
            _started = false;
            _deadline = EditorApplication.timeSinceStartup + 65;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            NetworkConnectManager.Instance.StartHost("Phase48QA", 17777);
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup > _deadline)
            { Debug.LogError("[Phase48QA] Timeout awaiting Playing."); EditorApplication.update -= Tick; return; }
            var gm = GameManager.Instance;
            if (!_started && gm != null && SceneManager.GetActiveScene().name == "Lobby")
            {
                var lobby = UnityEngine.Object.FindFirstObjectByType<Hellfire.UI.LobbyUI>();
                if (lobby != null) { _started = true; lobby.OnClickStartGame(); }
            }
            if (gm != null && gm.CurrentState.Value == GameState.Playing)
            {
                int enemies = UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                Debug.Log($"[Phase48QA] Real Host flow reached Playing: enemies={enemies}; ready={DungeonManager.Instance.ReadyCount.Value}; seed={DungeonManager.Instance.DungeonSeed.Value}");
                if (enemies != 0) Debug.LogError("[Phase48QA] Unexpected enemies before first room lock.");
                VerifyRectangles();
                EditorApplication.update -= Tick;
            }
        }
        [MenuItem("Hellfire/Phase 4.8/3 Inspect Combat geometry #&3")]
        public static void InspectCombat()
        {
            var gen = DungeonManager.Instance?.Generator;
            if (gen?.ActiveGrid == null) return;
            foreach (var room in gen.ActiveGrid.Rooms.Values)
                if (room.RoomType == DungeonRoomType.Combat)
                {
                    var view = SceneView.lastActiveSceneView;
                    if (view != null) { view.LookAt(room.WorldCenter, Quaternion.Euler(75, 0, 0), 18f); view.Focus(); }
                    Debug.Log($"[Phase48QA] Inspect room {room.GridPosition}, theme={room.Theme?.ThemeName}");
                    break;
                }
        }
        [MenuItem("Hellfire/Phase 4.8/4 Enter first Combat (Host) #&4")]
        public static void EnterCombat() => EnterRoom(DungeonRoomType.Combat);

        [MenuItem("Hellfire/Phase 4.8/6 Enter Boss (Host) #&6")]
        public static void EnterBoss() => EnterRoom(DungeonRoomType.Boss);

        private static void EnterRoom(DungeonRoomType type)
        {
            var dm = DungeonManager.Instance;
            if (!EditorApplication.isPlaying || dm == null || !dm.IsServer) return;
            foreach (var room in dm.Generator.ActiveGrid.Rooms.Values)
                if (room.RoomType == type)
                {
                    var player = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject;
                    Vector3 target = DungeonEnemySpawner.GetValidSpawnPosition(room.WorldCenter, room, "QA entrant");
                    player.GetComponent<Hellfire.Player.PlayerMovement>().TeleportOwnerClientRpc(target, Quaternion.identity);
                    Debug.Log($"[Phase48QA] Host entered {type} {room.GridPosition}; observing normal server lock sequence.");
                    return;
                }
        }
        [MenuItem("Hellfire/Phase 4.8/5 Check determinism and scaling #&5")]
        public static void CheckDeterminismAndScaling()
        {
            var dm = DungeonManager.Instance;
            if (!EditorApplication.isPlaying || dm == null) return;
            var spawner = dm.EnemySpawner;
            for (int floor = 1; floor <= 3; floor++)
            {
                spawner.GetBaseEnemyRange(floor, out int min, out int max);
                if (min != 5 + 2 * (floor - 1) || max != 7 + 2 * (floor - 1))
                    throw new InvalidOperationException("Floor enemy range mismatch.");
                Debug.Log($"[Phase48QA] Floor formula {floor}: {min}..{max} (CurrentFloor was not changed)");
            }
            for (int players = 1; players <= 4; players++)
                Debug.Log($"[Phase48QA] Floor1 players={players}: rolls5/6/7 => {spawner.ScaleEnemyCount(5, players)}/{spawner.ScaleEnemyCount(6, players)}/{spawner.ScaleEnemyCount(7, players)}; high-floor cap={spawner.ScaleEnemyCount(1000, players)}");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var obj = new GameObject("Phase48DeterminismOnly");
                SceneManager.MoveGameObjectToScene(obj, preview);
                var gen = obj.AddComponent<DungeonGenerator>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(dm.Generator), gen);
                var root = new GameObject("PreviewGeometryOnly");
                root.transform.SetParent(obj.transform, false);
                var so = new SerializedObject(gen);
                so.FindProperty("_dungeonRoot").objectReferenceValue = root.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                int[] seeds = {1, 42, 12345, 770316, 649984, 999999};
                foreach (int seed in seeds)
                {
                    string first = Signature(gen.GenerateDungeon(seed));
                    string second = Signature(gen.GenerateDungeon(seed));
                    if (first != second) throw new InvalidOperationException($"Non-deterministic geometry for seed {seed}");
                }
                Debug.Log("[Phase48QA] Determinism PASS: 6 seeds x 2 independent generations; room type/size/theme/obstacle bounds identical.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static string Signature(DungeonGrid grid)
        {
            var rooms = new System.Collections.Generic.List<DungeonRoomNode>(grid.Rooms.Values);
            rooms.Sort((a, b) => a.GridPosition.x != b.GridPosition.x ? a.GridPosition.x.CompareTo(b.GridPosition.x) : a.GridPosition.y.CompareTo(b.GridPosition.y));
            var text = new System.Text.StringBuilder();
            foreach (var room in rooms)
            {
                text.Append(room.GridPosition).Append(room.RoomType).Append(room.Size.ToString("F6")).Append(room.Theme?.name);
                foreach (var bounds in room.ObstacleBounds) text.Append(bounds.center.ToString("F6")).Append(bounds.size.ToString("F6"));
            }
            return text.ToString();
        }
        private static void VerifyRectangles()
        {
            var gen = DungeonManager.Instance.Generator;
            var grid = gen.ActiveGrid;
            float shortest = float.MaxValue;
            foreach (var edge in grid.Corridors)
            {
                var a = grid.Rooms[edge.RoomA]; var b = grid.Rooms[edge.RoomB];
                bool alongX = edge.RoomA.x != edge.RoomB.x;
                float length = gen.GridCellSize - ((alongX ? a.Size.x : a.Size.y) + (alongX ? b.Size.x : b.Size.y)) * 0.5f;
                shortest = Mathf.Min(shortest, length);
                if (length < 10f) Debug.LogError($"[Phase48QA] Corridor too short: {length}");
            }
            foreach (var room in grid.Rooms.Values)
            {
                if (room.ObstacleBounds.Count > 6) Debug.LogError("[Phase48QA] Obstacle count > 6.");
                foreach (var bounds in room.ObstacleBounds)
                    if (!RoomThemeBuilder.Allowed(room, bounds)) Debug.LogError($"[Phase48QA] Invalid obstacle in {room.GridPosition}.");
                if (room.RoomType != DungeonRoomType.Combat && room.ObstacleBounds.Count > 0)
                    Debug.LogError("[Phase48QA] Obstacles outside Combat.");
            }
            Debug.Log($"[Phase48QA] Rooms={grid.Rooms.Count}; shortest corridor={shortest:F2}m; grid={gen.GridCellSize}m; obstacle bounds checked");
        }
    }
}
