#if HELLFIRE_PHASE1_SMOKE
using System;
using System.IO;
using System.Linq;
using System.Collections;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Hellfire.Items;
using Hellfire.Player;
using Hellfire.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Hellfire.Networking
{
    // Compiled only into the QA build. Both players use the real scene and Netcode APIs.
    internal sealed class PhaseOneLoopbackSmoke : MonoBehaviour
    {
        private const ushort Port = 17781;
        private const string Tag = "[PHASE1]";
        private const string SyncFile = "/private/tmp/hf_phase1/loopback-host-ready";
        private bool _host;
        private bool _testGameOver;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetUp()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-hfPhase1Host") && !args.Contains("-hfPhase1Client")) return;
            Application.runInBackground = true;
            var root = new GameObject("Phase one QA");
            DontDestroyOnLoad(root);
            var smoke = root.AddComponent<PhaseOneLoopbackSmoke>();
            smoke._host = args.Contains("-hfPhase1Host");
            smoke._testGameOver = args.Contains("-hfPhase1GameOver");
            smoke.StartCoroutine(smoke.Run());
        }

        private IEnumerator Run()
        {
            yield return WaitFor(() => NetworkConnectManager.Instance != null, "MainMenu network bootstrap");
            if (_finished) yield break;
            Debug.Log(Tag + " MAIN_MENU " + SceneManager.GetActiveScene().name);

            if (!_host)
                yield return WaitFor(() => File.Exists(SyncFile), "host readiness");
            if (_finished) yield break;
            var connection = NetworkConnectManager.Instance;
            bool ok = _host ? connection.StartHost("Phase1QA", Port) : connection.StartClient("127.0.0.1", Port);
            if (!ok) { Fail("Netcode startup"); yield break; }

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" &&
                NetworkManager.Singleton?.LocalClient?.PlayerObject != null, "Lobby and local player");
            if (_finished) yield break;
            var lobby = FindFirstObjectByType<LobbyUI>();
            if (lobby == null) { Fail("LobbyUI missing"); yield break; }
            Debug.Log(Tag + " LOBBY " + (_host ? "Host" : "Client"));

            if (_host)
            {
                File.WriteAllText(SyncFile, "Lobby");
                yield return WaitFor(() => NetworkManager.Singleton.ConnectedClientsList.Count == 2 &&
                    NetworkManager.Singleton.ConnectedClientsList.All(client => client.PlayerObject != null) &&
                    NetworkManager.Singleton.ConnectedClientsList.Where(client => client.ClientId != NetworkManager.ServerClientId)
                        .All(client => client.PlayerObject.GetComponent<PlayerIdentity>()?.IsReady.Value == true), "both players ready");
                if (_finished) yield break;
                lobby.OnClickStartGame();
            }
            else
                lobby.OnClickToggleReady();

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Gameplay" &&
                GameManager.Instance?.CurrentState.Value == GameState.Playing &&
                DungeonManager.Instance?.Generator?.ActiveGrid != null, "Gameplay generating and ready");
            if (_finished) yield break;
            var dungeon = DungeonManager.Instance;
            var grid = dungeon.Generator.ActiveGrid;
            string signature = string.Join(";", grid.Rooms.Values
                .OrderBy(room => room.GridPosition.x).ThenBy(room => room.GridPosition.y)
                .Select(room => $"{room.GridPosition}:{room.RoomType}:{room.Theme?.name}:{room.Size}:{room.ObstacleBounds.Count}"));
            Debug.Log($"{Tag} PLAYING seed={dungeon.DungeonSeed.Value} rooms={grid.Rooms.Count} signature={signature}");

            if (_host)
            {
                var manager = NetworkManager.Singleton;
                var local = manager.LocalClient.PlayerObject;
                var keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return new WaitForSecondsRealtime(.4f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (local.GetComponent<PlayerMovement>().StaminaFraction >= .995f)
                { Fail("Sprint did not consume stamina"); yield break; }
                Debug.Log(Tag + " STAMINA_CONSUMED");

                if (_testGameOver)
                {
                    yield return CheckReviveAndGameOver(local, grid.SpawnRoom, keyboard);
                    if (_finished) yield break;
                }
                else
                {
                    yield return CheckVictoryRoute(dungeon, grid, local, keyboard);
                    if (_finished) yield break;
                }
            }

            GameState expected = _testGameOver ? GameState.GameOver : GameState.Victory;
            yield return WaitFor(() => GameManager.Instance?.CurrentState.Value == expected, $"replicated {expected}");
            if (_finished) yield break;
            Debug.Log(Tag + " " + expected.ToString().ToUpperInvariant() + " " + (_host ? "Host" : "Client"));
            if (_host) yield return new WaitForSecondsRealtime(1.5f);
            _finished = true;
            Application.Quit(0);
        }

        private IEnumerator CheckReviveAndGameOver(NetworkObject hostPlayer, DungeonRoomNode spawn, Keyboard keyboard)
        {
            var players = NetworkManager.Singleton.ConnectedClientsList;
            var client = players.First(player => player.ClientId != NetworkManager.ServerClientId).PlayerObject;
            Vector3 origin = DungeonEnemySpawner.GetValidSpawnPosition(spawn.WorldCenter, spawn, "Phase 1 QA revive");
            hostPlayer.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(origin, Quaternion.identity);
            client.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(origin + Vector3.right, Quaternion.identity);
            yield return WaitFor(() => Vector3.Distance(hostPlayer.transform.position, client.transform.position) < 1.7f, "revive distance");
            if (_finished) yield break;
            var health = client.GetComponent<Health>();
            health.TakeDamage(10000f, HitboxType.Torso, ulong.MaxValue);
            yield return WaitFor(() => health.IsDowned.Value, "client downed");
            if (_finished) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return WaitFor(() => !health.IsDowned.Value && health.CurrentHealth.Value > 0f, "revive completed by player input");
            if (_finished) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log(Tag + " REVIVE_CLIENT");
            foreach (var player in players)
                player.PlayerObject.GetComponent<Health>().TakeDamage(10000f, HitboxType.Torso, ulong.MaxValue);
        }

        private IEnumerator CheckVictoryRoute(DungeonManager dungeon, DungeonGrid grid, NetworkObject local, Keyboard keyboard)
        {
            var chest = FindObjectsByType<LootChest>().FirstOrDefault(item => item.IsSpawned && !item.IsBossChest);
            if (chest != null)
            {
                var target = chest.transform.position + Vector3.right * 0.75f;
                local.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(target, Quaternion.identity);
                yield return new WaitForSecondsRealtime(.25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return WaitFor(() => chest.IsOpened.Value, "interact with network loot chest");
                if (_finished) yield break;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Debug.Log(Tag + " LOOT_CHEST_OPEN");
            }

            var combat = grid.Rooms.Values.First(room => room.RoomType == DungeonRoomType.Combat);
            Teleport(local, combat);
            yield return WaitFor(() => dungeon.IsRoomLocked(combat.GridPosition) &&
                dungeon.EnemySpawner.GetAliveEnemyCountInRoom(combat.GridPosition) > 0, "combat gate and enemy spawn");
            if (_finished) yield break;
            var party = NetworkManager.Singleton.ConnectedClientsList;
            if (!party.All(client => combat.Contains(client.PlayerObject.transform.position)))
            { Fail("Party not inside the locked combat room"); yield break; }
            Debug.Log($"{Tag} COMBAT party={party.Count} enemies={dungeon.EnemySpawner.GetAliveEnemyCountInRoom(combat.GridPosition)}");
            yield return new WaitForSecondsRealtime(1f);
            DamageEnemies(combat);
            yield return WaitFor(() => !dungeon.IsRoomLocked(combat.GridPosition), "combat clear and gate reopen");
            if (_finished) yield break;

            var boss = grid.Rooms.Values.First(room => room.RoomType == DungeonRoomType.Boss);
            Teleport(local, boss);
            yield return WaitFor(() => GameManager.Instance.CurrentState.Value == GameState.BossFight &&
                FindObjectsByType<BossStateMachine>().Any(enemy => enemy.IsSpawned), "boss spawn");
            if (_finished) yield break;
            Debug.Log(Tag + " BOSS_FIGHT");
            // Drive server combat through the damage API, keeping actual transitions and death callbacks.
            for (int step = 0; step < 8 && GameManager.Instance.CurrentState.Value == GameState.BossFight; step++)
            {
                DamageEnemies(boss);
                yield return new WaitForSecondsRealtime(2.25f);
            }
        }

        private static void Teleport(NetworkObject player, DungeonRoomNode room)
        {
            Vector3 target = DungeonEnemySpawner.GetValidSpawnPosition(room.WorldCenter, room, "Phase 1 QA entrant");
            player.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(target, Quaternion.identity);
        }

        private static void DamageEnemies(DungeonRoomNode room)
        {
            foreach (var enemy in FindObjectsByType<EnemyStateMachine>())
            {
                if (!enemy.IsSpawned || !room.Contains(enemy.transform.position)) continue;
                var health = enemy.GetComponent<Health>();
                if (health != null && !health.IsDead.Value)
                    health.TakeDamage(10000f, HitboxType.Torso, NetworkManager.ServerClientId);
            }
        }

        private IEnumerator WaitFor(Func<bool> ready, string step)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!ready()) Fail(step);
        }

        private void Fail(string step)
        {
            _finished = true;
            Debug.LogError(Tag + " FAIL " + step);
            Application.Quit(1);
        }
    }
}
#endif
