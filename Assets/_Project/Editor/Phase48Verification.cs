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
                EditorApplication.update -= Tick;
            }
        }
    }
}
