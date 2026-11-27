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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Hellfire.Networking
{
    // Compiled only into the QA build. Both players use the real scene and Netcode APIs.
    internal sealed class PhaseOneLoopbackSmoke : MonoBehaviour
    {
        private const ushort Port = 17781;
        private const string Tag = "[PHASE1]";
        private static string SyncFile => Path.Combine(Path.GetTempPath(), "hellfire-qa-" + Port + "-lobby");
        private bool _host;
        private bool _testGameOver;
        private bool _finished;
        private string _captureDirectory;

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
            smoke._captureDirectory = Environment.GetEnvironmentVariable("HELLFIRE_QA_CAPTURE_DIR");
            smoke.StartCoroutine(smoke.Run());
        }

        private IEnumerator Run()
        {
            yield return WaitFor(() => NetworkConnectManager.Instance != null, "MainMenu network bootstrap");
            if (_finished) yield break;
            Debug.Log(Tag + " MAIN_MENU " + SceneManager.GetActiveScene().name);
            if (_host && !string.IsNullOrEmpty(_captureDirectory))
            {
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("MainMenu");
                var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<Button>();
                if (selected == null || selected.name != "HostButton")
                { Fail("MainMenu keyboard focus"); yield break; }
                var move = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down };
                ExecuteEvents.Execute(selected.gameObject, move, ExecuteEvents.moveHandler);
                if (EventSystem.current.currentSelectedGameObject?.name != "JoinIpButton")
                { Fail("MainMenu keyboard navigation"); yield break; }
                if (!Click("SettingsButton")) { Fail("Settings button"); yield break; }
                yield return Capture("Settings");
                if (!CheckSettings()) { Fail("Settings backends"); yield break; }
                var shake = GameObject.Find("ScreenShakeToggle").GetComponent<Toggle>();
                bool originalShake = shake.isOn;
                shake.isOn = true;
                yield return new WaitForSecondsRealtime(.18f);
                yield return Capture("Settings-Enabled");
                shake.isOn = false;
                yield return new WaitForSecondsRealtime(.18f);
                yield return Capture("Settings-Disabled");
                shake.isOn = originalShake;
                if (!Click("SettingsBackBtn")) { Fail("Settings back"); yield break; }
            }

            if (!_host)
                yield return WaitFor(() => File.Exists(SyncFile), "host readiness");
            if (_finished) yield break;
            string entry = _host ? "HostButton" : "JoinIpButton";
            if (!Click(entry)) { Fail("MainMenu action missing: " + entry); yield break; }
            string addressField = _host ? "HostRoomNameInput" : "JoinIpInput";
            string portField = _host ? "HostPortInput" : "JoinPortInput";
            if (!Fill(addressField, _host ? "Phase1QA" : "127.0.0.1") || !Fill(portField, Port.ToString()) ||
                !Click(_host ? "HostStartBtn" : "JoinConnectBtn"))
            { Fail("MainMenu connection form"); yield break; }

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" &&
                NetworkManager.Singleton?.LocalClient?.PlayerObject != null, "Lobby and local player");
            if (_finished) yield break;
            Debug.Log(Tag + " LOBBY " + (_host ? "Host" : "Client"));
            if (!string.IsNullOrEmpty(_captureDirectory))
            {
                yield return WaitFor(() => GameObject.Find("LobbyTitle")?.GetComponent<TMP_Text>()?.text == "Phase1QA",
                    "replicated room name");
                if (_finished) yield break;
            }

            if (_host)
            {
                File.WriteAllText(SyncFile, "Lobby");
                if (!string.IsNullOrEmpty(_captureDirectory))
                {
                    yield return WaitFor(() => NetworkManager.Singleton.ConnectedClientsIds.Count == 2, "two Lobby players");
                    if (_finished) yield break;
                    var copy = GameObject.Find("CopyIpButton")?.GetComponent<Button>();
                    var pointer = PointerAt(copy?.transform as RectTransform, Vector2.zero);
                    if (pointer == null || copy.targetGraphic is not Image copyImage || copyImage.sprite == null)
                    { Fail("Lobby Copy IP graphic or hitbox"); yield break; }
                    ExecuteEvents.Execute(copy.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.15f);
                    Color hover = copyImage.canvasRenderer.GetColor();
                    ExecuteEvents.Execute(copy.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(.15f);
                    if (copyImage.canvasRenderer.GetColor() == hover)
                    { Fail("Lobby Copy IP pressed feedback"); yield break; }
                    ExecuteEvents.Execute(copy.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(copy.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    yield return new WaitForSecondsRealtime(.15f);
                    Debug.Log(Tag + " COPY_IP_POINTER_STATES");
                    yield return Capture("Lobby");
                    StartCoroutine(CaptureLoading());
                }
                yield return WaitFor(() => NetworkManager.Singleton.ConnectedClientsList.Count == 2 &&
                    NetworkManager.Singleton.ConnectedClientsList.All(client => client.PlayerObject != null) &&
                    NetworkManager.Singleton.ConnectedClientsList.Where(client => client.ClientId != NetworkManager.ServerClientId)
                        .All(client => client.PlayerObject.GetComponent<PlayerIdentity>()?.IsReady.Value == true), "both players ready");
                if (_finished) yield break;
                yield return WaitFor(() => Click("StartGameButton"), "Lobby Start Game button");
            }
            else
                yield return WaitFor(() => Click("ReadyButton"), "Lobby Ready button");
            if (_finished) yield break;

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
            if (_host && !string.IsNullOrEmpty(_captureDirectory))
            {
                yield return new WaitForSecondsRealtime(.65f);
                var health = GameObject.Find("HealthText")?.GetComponent<TMP_Text>();
                var stamina = GameObject.Find("StaminaText")?.GetComponent<TMP_Text>();
                if (health == null || string.IsNullOrEmpty(health.text) || stamina == null ||
                    !stamina.text.Contains("%") || GameObject.Find("FloorText") == null || GameObject.Find("DungeonMinimap") == null)
                { Fail("Health, stamina or floor HUD not displaying gameplay data"); yield break; }
                yield return Capture("Gameplay");
                var menuKeyboard = InputSystem.AddDevice<Keyboard>();
                yield return Press(menuKeyboard, Key.Escape);
                if (GameObject.Find("PauseMenuPanel") == null)
                { Fail("Pause overlay through Escape"); yield break; }
                yield return Capture("Pause");
                var pausedPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
                var pausedWeapon = pausedPlayer.GetComponent<WeaponController>();
                int ammoWhilePaused = pausedWeapon.CurrentAmmo;
                var menuMouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(menuMouse, new MouseState().WithButton(MouseButton.Left));
                yield return new WaitForSecondsRealtime(.15f);
                InputSystem.RemoveDevice(menuMouse);
                if (pausedWeapon.CurrentAmmo != ammoWhilePaused)
                { Fail("Pause mouse click fired the weapon"); yield break; }
                if (!Click("ResumeButton")) { Fail("Pause resume"); yield break; }
                yield return Press(menuKeyboard, Key.Tab);
                if (GameObject.Find("DungeonMapPanel") != null)
                {
                    yield return Capture("Map");
                    yield return Press(menuKeyboard, Key.Tab);
                }
                InputSystem.RemoveDevice(menuKeyboard);
            }

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
                    yield return CheckVictoryRoute(dungeon, grid, local, keyboard, InputSystem.AddDevice<Mouse>());
                    if (_finished) yield break;
                }
            }

            if (!_host && !_testGameOver)
            {
                yield return CheckClientLoot();
                if (_finished) yield break;
                yield return CheckBossFight(NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>(),
                    Keyboard.current, InputSystem.AddDevice<Mouse>(), grid.BossRoom);
                if (_finished) yield break;
            }

            GameState expected = _testGameOver ? GameState.GameOver : GameState.Victory;
            yield return WaitFor(() => GameManager.Instance?.CurrentState.Value == expected, $"replicated {expected}");
            if (_finished) yield break;
            Debug.Log(Tag + " " + expected.ToString().ToUpperInvariant() + " " + (_host ? "Host" : "Client"));
            string prefix = _testGameOver ? "GameOver" : "Victory";
            yield return WaitFor(() => GameObject.Find(prefix + "OverlayPanel") != null, "visible result panel");
            if (_finished) yield break;
            if (_host)
            {
                yield return new WaitForSecondsRealtime(.5f);
                yield return Capture(prefix);
                yield return new WaitForSecondsRealtime(1f);
                var button = GameObject.Find(prefix + "ReturnButton")?.GetComponent<Button>();
                if (button == null || !button.IsInteractable())
                { Fail("Host result button missing"); yield break; }
                button.onClick.Invoke();
            }
            else
            {
                if (GameObject.Find(prefix + "WaitingText") == null || GameObject.Find(prefix + "ReturnButton") != null)
                { Fail("Client result view"); yield break; }
                yield return Capture(prefix);
            }

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" &&
                GameManager.Instance?.CurrentState.Value == GameState.Lobby, "return to Lobby for both players");
            if (_finished) yield break;
            Debug.Log(Tag + " RETURN_LOBBY " + (_host ? "Host" : "Client"));
            if (_host) yield return new WaitForSecondsRealtime(1.5f);
            _finished = true;
            Application.Quit(0);
        }

        private IEnumerator CaptureLoading()
        {
            yield return WaitFor(() => GameObject.Find("LoadingPanel") != null, "loading overlay");
            if (!_finished) yield return Capture("Loading");
        }

        private IEnumerator Capture(string screen)
        {
            if (string.IsNullOrEmpty(_captureDirectory)) yield break;
            yield return new WaitForEndOfFrame();
            var snapshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (snapshot == null) { Fail("Screenshot " + screen); yield break; }
            Directory.CreateDirectory(_captureDirectory);
            string role = _host ? "Host" : "Client";
            File.WriteAllBytes(Path.Combine(_captureDirectory, screen + "-" + role + ".png"), snapshot.EncodeToPNG());
            Debug.Log($"{Tag} CAPTURE {screen} {role} {snapshot.width}x{snapshot.height}");
            Destroy(snapshot);
        }

        private static PointerEventData PointerAt(RectTransform target, Vector2 localOffset)
        {
            if (target == null || EventSystem.current == null) return null;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(localOffset)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(target) ? pointer : null;
        }

        private static bool CheckSettings()
        {
            var fov = GameObject.Find("FovSlider")?.GetComponent<Slider>();
            var volume = GameObject.Find("VolumeSlider")?.GetComponent<Slider>();
            var shake = GameObject.Find("ScreenShakeToggle")?.GetComponent<Toggle>();
            if (fov == null || volume == null || shake == null) return false;
            float originalFov = fov.value;
            float originalVolume = volume.value;
            bool originalShake = shake.isOn;
            fov.value = originalFov < 100f ? 105f : 80f;
            volume.value = originalVolume < .5f ? .8f : .35f;
            var pointer = PointerAt(shake.transform as RectTransform, new Vector2(-190f, 0f));
            if (pointer != null) ExecuteEvents.Execute(shake.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            var state = GameObject.Find("ShakeStateText")?.GetComponent<TMP_Text>();
            bool connected = Mathf.Approximately(GameSettings.FieldOfView, fov.value) &&
                Mathf.Approximately(AudioListener.volume, volume.value) &&
                GameSettings.ReduceScreenShake == shake.isOn && shake.isOn != originalShake &&
                state?.text == (shake.isOn ? "BẬT" : "TẮT");
            fov.value = originalFov;
            volume.value = originalVolume;
            shake.isOn = originalShake;
            if (connected) Debug.Log(Tag + " SETTINGS_CHANGE_AND_RESTORE");
            return connected;
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
            if (!string.IsNullOrEmpty(_captureDirectory))
            {
                yield return WaitFor(() => GameObject.Find("DownedOverlay")?.GetComponentInChildren<TMP_Text>()?.text.StartsWith("GỤC") == true,
                    "teammate downed HUD");
                if (_finished) yield break;
                yield return Capture("TeammateDowned");
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return WaitFor(() => !health.IsDowned.Value && health.CurrentHealth.Value > 0f, "revive completed by player input");
            if (_finished) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Debug.Log(Tag + " REVIVE_CLIENT");
            if (!string.IsNullOrEmpty(_captureDirectory))
            {
                var teammate = GameObject.Find("TeammateSlot")?.transform;
                var healthFill = teammate?.Find("HealthBar/Fill") as RectTransform;
                if (healthFill == null ||
                    !Mathf.Approximately(healthFill.anchorMax.x, health.CurrentHealth.Value / health.MaxHealth))
                { Fail("teammate HUD health fill after revive"); yield break; }
                Debug.Log(Tag + " TEAMMATE_HUD_HEALTH");
            }
            foreach (var player in players)
                player.PlayerObject.GetComponent<Health>().TakeDamage(10000f, HitboxType.Torso, ulong.MaxValue);
        }

        private IEnumerator CheckVictoryRoute(DungeonManager dungeon, DungeonGrid grid, NetworkObject local,
            Keyboard keyboard, Mouse mouse)
        {
            var movement = local.GetComponent<PlayerMovement>();
            var weapon = local.GetComponent<WeaponController>();
            if (weapon.AvailableWeapons.Count < 4 || weapon.AvailableWeapons.Any(data => data == null))
            { Fail("Player loadout references"); yield break; }

            int initial = weapon.CurrentAmmo;
            var pistol = weapon.CurrentWeaponData;
            if (pistol.FireSound == null) { Fail("Pistol fire clip missing"); yield break; }
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return WaitFor(() => weapon.CurrentAmmo == initial - 1, "Pistol input and ammo");
            if (_finished) yield break;
            InputSystem.QueueStateEvent(mouse, new MouseState());

            yield return Press(keyboard, Key.Digit4);
            yield return WaitFor(() => weapon.CurrentWeaponData != pistol, "Rifle selection");
            if (_finished) yield break;
            var rifle = weapon.CurrentWeaponData;
            if (rifle.FireSound == null) { Fail("Rifle fire clip missing"); yield break; }
            yield return Press(keyboard, Key.Digit1);
            if (weapon.CurrentWeaponData != pistol || weapon.CurrentAmmo != initial - 1)
            { Fail("Pistol magazine did not survive weapon switch"); yield break; }

            yield return Press(keyboard, Key.R);
            yield return WaitFor(() => weapon.IsReloading, "reload starts via R");
            if (_finished) yield break;
            yield return WaitFor(() => !weapon.IsReloading && weapon.CurrentAmmo == initial, "reload fills magazine");
            if (_finished) yield break;
            var ammoText = GameObject.Find("AmmoText")?.GetComponent<TextMeshProUGUI>();
            if (ammoText == null || ammoText.text != $"{initial} / {pistol.MagSize}")
            { Fail("Ammo HUD did not reflect reload"); yield break; }

            yield return Press(keyboard, Key.Digit4);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return WaitFor(() => weapon.IsAimingDownSights && movement.PlayerCamera.fieldOfView < 75f, "ADS input and FOV");
            if (_finished) yield break;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return WaitFor(() => !weapon.IsAimingDownSights, "ADS release");
            if (_finished) yield break;
            Debug.Log(Tag + " LOADOUT_PISTOL_RIFLE_RELOAD_ADS");

            var chest = FindObjectsByType<LootChest>().FirstOrDefault(item => item.IsSpawned && !item.IsBossChest);
            if (chest == null) { Fail("spawned treasure chest missing"); yield break; }
            var awayFromDrop = chest.transform.position - chest.transform.forward * 2.35f;
            movement.TeleportOwnerClientRpc(awayFromDrop, Quaternion.identity);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Press(keyboard, Key.E);
            yield return WaitFor(() => chest.IsOpened.Value, "interact with network loot chest");
            if (_finished) yield break;

            ItemPickup loot = FindObjectsByType<ItemPickup>().FirstOrDefault(item => item.IsSpawned);
            if (loot == null)
            {
                yield return WaitFor(() => (loot = FindObjectsByType<ItemPickup>().FirstOrDefault(item => item.IsSpawned)) != null, "chest item spawn");
                if (_finished) yield break;
            }
            var remote = NetworkManager.Singleton.ConnectedClientsList.First(client => client.ClientId != NetworkManager.ServerClientId).PlayerObject;
            var lootPoint = loot.transform.position;
            Vector3 approach = lootPoint + chest.transform.right * 1.75f;
            approach.y = chest.transform.position.y;
            Vector3 toward = lootPoint - approach;
            toward.y = 0f;
            Debug.Log($"{Tag} SEND_PICKUP_APPROACH player={remote.OwnerClientId} item={loot.NetworkObjectId} point={lootPoint} approach={approach}");
            remote.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(approach, Quaternion.LookRotation(toward),
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { remote.OwnerClientId } } });
            yield return WaitFor(() => loot == null || !loot.IsSpawned, "loot consumed on server");
            if (_finished) yield break;
            Debug.Log(Tag + " HOST_OBSERVED_LOOT_DESPAWN");

            var combat = grid.Rooms.Values.First(room => room.RoomType == DungeonRoomType.Combat);
            Teleport(local, combat);
            yield return WaitFor(() => dungeon.IsRoomLocked(combat.GridPosition) &&
                dungeon.EnemySpawner.GetAliveEnemyCountInRoom(combat.GridPosition) > 0, "combat gate and enemy spawn");
            if (_finished) yield break;
            if (!NetworkManager.Singleton.ConnectedClientsList.All(client => combat.Contains(client.PlayerObject.transform.position)))
            { Fail("Party not inside the locked combat room"); yield break; }
            Debug.Log(Tag + " COMBAT_ROOM_LOCKED");

            int hits = 0;
            void CountHit(bool _) => hits++;
            weapon.OnHitTargetConfirmed += CountHit;
            try
            {
                yield return FightThroughInput(movement, keyboard, mouse, combat,
                    () => !dungeon.IsRoomLocked(combat.GridPosition), 90f);
                if (_finished) yield break;
                if (hits == 0) { Fail("No hits confirmed through real input"); yield break; }
                Debug.Log($"{Tag} COMBAT_CLEARED_WITH_INPUT hits={hits}");

                var boss = grid.Rooms.Values.First(room => room.RoomType == DungeonRoomType.Boss);
                Teleport(local, boss);
                yield return CheckBossFight(movement, keyboard, mouse, boss);
            }
            finally
            {
                weapon.OnHitTargetConfirmed -= CountHit;
            }
        }

        private IEnumerator CheckBossFight(PlayerMovement movement, Keyboard keyboard, Mouse mouse, DungeonRoomNode room)
        {
            var weapon = movement.GetComponent<WeaponController>();
            yield return WaitFor(() => !weapon.IsReloading, "ready to switch loadout for Boss");
            if (_finished) yield break;
            yield return Press(keyboard, Key.Digit2);
            if (weapon.CurrentWeaponData != weapon.AvailableWeapons[1])
            { Fail("Boss loadout switch did not register"); yield break; }
            yield return WaitFor(() => BossStateMachine.ActiveBoss != null &&
                GameManager.Instance.CurrentState.Value == GameState.BossFight && room.Contains(movement.transform.position),
                "boss and local player in the encounter");
            if (_finished) yield break;
            var boss = BossStateMachine.ActiveBoss.GetComponent<Health>();
            if (GameObject.Find("BossHealthBarRoot") == null)
            { Fail("Boss health HUD missing during encounter"); yield break; }
            if (_host) yield return Capture("Boss");
            float startingHealth = boss.CurrentHealth.Value;
            int hits = 0;
            void CountHit(bool _) => hits++;
            weapon.OnHitTargetConfirmed += CountHit;
            try
            {
                Debug.Log($"{Tag} BOSS_INPUT role={(_host ? "Host" : "Client")} weapon={weapon.CurrentWeaponData.name} startingBossHealth={startingHealth}");
                yield return FightThroughInput(movement, keyboard, mouse, room,
                    () => GameManager.Instance.CurrentState.Value == GameState.Victory, 150f);
                if (_finished) yield break;
                if (_host && (hits == 0 || startingHealth <= 0f || (boss != null && boss.CurrentHealth.Value > 0f)))
                { Fail("Boss defeat did not follow confirmed input hits"); yield break; }
                Debug.Log($"{Tag} BOSS_DEFEATED_WITH_INPUT role={(_host ? "Host" : "Client")} hits={hits}");
            }
            finally
            {
                weapon.OnHitTargetConfirmed -= CountHit;
            }
        }

        private IEnumerator CheckClientLoot()
        {
            ItemPickup loot = null;
            yield return WaitFor(() => (loot = FindObjectsByType<ItemPickup>().FirstOrDefault(item =>
                item.IsSpawned && (item.Weapon != null || item.PowerUp != null))) != null, "client received item identity");
            if (_finished) yield break;
            var local = NetworkManager.Singleton.LocalClient.PlayerObject;
            Debug.Log($"{Tag} CLIENT_SAW_PICKUP item={loot.NetworkObjectId} point={loot.transform.position} player={local.transform.position}");
            var buff = local.GetComponent<PlayerBuffManager>();
            var weapon = local.GetComponent<WeaponController>();
            var expectedWeapon = loot.Weapon;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            if (expectedWeapon != null && weapon.CurrentWeaponData == expectedWeapon)
                yield return Press(keyboard, Key.Digit2);
            int rewards = 0;
            void BuffApplied(PowerUpType _, float value) => rewards++;
            void WeaponChanged(string name)
            {
                if (expectedWeapon != null && weapon.CurrentWeaponData == expectedWeapon) rewards++;
            }
            buff.OnPowerUpApplied += BuffApplied;
            weapon.OnWeaponChanged += WeaponChanged;
            try
            {
                yield return WaitFor(() => loot != null &&
                    Vector3.ProjectOnPlane(local.transform.position - loot.transform.position, Vector3.up).magnitude <= 2.25f,
                    "client approaches drop");
                if (_finished) yield break;
                Debug.Log($"{Tag} CLIENT_WALKS_TO_PICKUP point={loot.transform.position} player={local.transform.position}");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return WaitFor(() => (loot == null || !loot.IsSpawned) && rewards > 0, "client receives one loot reward");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (_finished) yield break;
                yield return new WaitForSecondsRealtime(.25f);
                if (rewards != 1) { Fail("Loot reward duplicated"); yield break; }
                Debug.Log(Tag + " CLIENT_PICKUP_ONCE " + (expectedWeapon == null ? "POWERUP" : "WEAPON"));
            }
            finally
            {
                buff.OnPowerUpApplied -= BuffApplied;
                weapon.OnWeaponChanged -= WeaponChanged;
            }
        }

        private IEnumerator FightThroughInput(PlayerMovement movement, Keyboard keyboard, Mouse mouse,
            DungeonRoomNode room, Func<bool> completed, float timeout)
        {
            var health = movement.GetComponent<Health>();
            var camera = movement.PlayerCamera;
            bool bossRoom = room.RoomType == DungeonRoomType.Boss;
            var weapon = movement.GetComponent<WeaponController>();
            float deadline = Time.realtimeSinceStartup + timeout;
            float mouseGain = .1f * Mathf.Max(.1f, PlayerPrefs.GetFloat("MouseSensitivity", 2f));
            while (!completed() && Time.realtimeSinceStartup < deadline && GameManager.Instance.CurrentState.Value != GameState.GameOver)
            {
                if (health.IsDowned.Value || health.IsDead.Value)
                {
                    if (!bossRoom) break;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return new WaitForSecondsRealtime(.2f);
                    continue;
                }
                var origin = camera.transform.position;
                if (bossRoom && BossStateMachine.ActiveBoss is { } boss &&
                    Vector3.Distance(boss.transform.position, origin) < 4.8f &&
                    TryChooseEscapeDirection(room, origin, boss.transform.position, out var escape))
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = AimDelta(camera, escape, mouseGain) });
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                }
                else if (TryFindVisibleTarget(room, origin, out Vector3 point))
                {
                    Vector3 direction = (point - origin).normalized;
                    Vector2 aimDelta = AimDelta(camera, direction, mouseGain);
                    if (bossRoom)
                    {
                        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                        Key strafe = room.Contains(origin + right * 2f, 1f) ? Key.D : Key.A;
                        if (_host && room.Contains(origin - right * 2f, 1f)) strafe = Key.A;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(strafe));
                    }
                    else if (Vector3.Distance(point, origin) < 5f)
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    else
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (bossRoom && weapon.CurrentWeaponData.IsAutomatic)
                    {
                        InputSystem.QueueStateEvent(mouse, new MouseState { delta = aimDelta }.WithButton(MouseButton.Left));
                    }
                    else
                    {
                        InputSystem.QueueDeltaStateEvent(mouse.delta, aimDelta);
                        yield return null;
                        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                        yield return new WaitForSecondsRealtime(.12f);
                        InputSystem.QueueStateEvent(mouse, new MouseState());
                    }
                }
                else
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    yield return new WaitForSecondsRealtime(.10f);
                }
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (!completed()) Fail($"real-input fight did not finish in {room.RoomType}; playerHealth={health.CurrentHealth.Value}, " +
                $"bossHealth={BossStateMachine.ActiveBoss?.GetComponent<Health>().CurrentHealth.Value}");
        }

        private static Vector2 AimDelta(Camera camera, Vector3 direction, float mouseGain)
        {
            Vector3 desired = Quaternion.LookRotation(direction).eulerAngles;
            Vector3 current = camera.transform.rotation.eulerAngles;
            return new Vector2(
                Mathf.DeltaAngle(current.y, desired.y) / mouseGain,
                -Mathf.DeltaAngle(current.x, desired.x) / mouseGain);
        }

        private static bool TryChooseEscapeDirection(DungeonRoomNode room, Vector3 origin, Vector3 threat, out Vector3 direction)
        {
            direction = default;
            float bestDistance = 0f;
            for (int i = 0; i < 8; i++)
            {
                var step = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 destination = origin + step * 3f;
                if (!room.Contains(destination, .75f) ||
                    Physics.Raycast(origin, step, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                float distance = (destination - threat).sqrMagnitude;
                if (distance <= bestDistance) continue;
                direction = step;
                bestDistance = distance;
            }
            return bestDistance > 0f;
        }

        private static bool TryFindVisibleTarget(DungeonRoomNode room, Vector3 origin, out Vector3 point)
        {
            point = default;
            float bestDistance = float.PositiveInfinity;
            foreach (var enemy in FindObjectsByType<EnemyStateMachine>())
            {
                if (!enemy.IsSpawned || !room.Contains(enemy.transform.position)) continue;
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead.Value) continue;
                foreach (var body in enemy.GetComponentsInChildren<Collider>())
                {
                    if (!body.enabled || body.isTrigger) continue;
                    Vector3 delta = body.bounds.center - origin;
                    float distance = delta.magnitude;
                    if (distance <= .01f) continue;
                    if (Physics.Raycast(origin, delta / distance, out var impact, distance + .05f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        impact.collider.GetComponentInParent<Health>() == health)
                    {
                        float score = distance;
                        if (enemy is BossStateMachine)
                            score -= impact.collider.GetComponent<HitboxIdentifier>()?.Type == HitboxType.Head ? 200f : 100f;
                        if (score >= bestDistance) continue;
                        bestDistance = score;
                        point = body.bounds.center;
                    }
                }
            }
            return !float.IsInfinity(bestDistance);
        }

        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static bool Click(string name)
        {
            var button = GameObject.Find(name)?.GetComponent<Button>();
            if (button == null || !button.IsInteractable()) return false;
            button.onClick.Invoke();
            return true;
        }

        private static bool Fill(string name, string value)
        {
            var input = GameObject.Find(name)?.GetComponent<TMP_InputField>();
            if (input == null) return false;
            input.text = value;
            return true;
        }

        private static void Teleport(NetworkObject player, DungeonRoomNode room)
        {
            Vector3 target = DungeonEnemySpawner.GetValidSpawnPosition(room.WorldCenter, room, "Phase 1 QA entrant");
            player.GetComponent<PlayerMovement>().TeleportOwnerClientRpc(target, Quaternion.identity);
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
