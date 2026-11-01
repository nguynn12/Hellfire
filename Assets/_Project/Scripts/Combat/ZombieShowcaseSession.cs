using Hellfire.AI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Combat
{
    // Isolated local-host session: no singleton from a gameplay prefab is instantiated.
    public sealed class ZombieShowcaseSession : MonoBehaviour
    {
        public GameObject[] EnemyPrefabs;
        public bool AutoStartCombat = true;
        public bool CombatActive { get; private set; }
        private EnemyStateMachine[] enemies;
        private Health player;
        private void Awake()
        {
            Application.targetFrameRate = 30;
            enemies = FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) enemy.enabled = false;
        }
        private void Start()
        {
            var manager = GetComponent<NetworkManager>();
            if (EnemyPrefabs != null) foreach (var prefab in EnemyPrefabs) if (prefab != null) manager.AddNetworkPrefab(prefab);
            // Local test only: choose an available loopback port instead of competing with other Editor sessions.
            ushort port;
            using (var probe = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
                port = (ushort)((System.Net.IPEndPoint)probe.Client.LocalEndPoint).Port;
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            if (!manager.StartHost()) { Debug.LogError("[Showcase] Local host failed"); return; }
            var go = GameObject.Find("Player_Test");
            var network = go.GetComponent<NetworkObject>();
            if (network.IsSpawned) network.Despawn(false);
            network.SpawnAsPlayerObject(manager.LocalClientId);
            player = go.GetComponent<Health>();
            if (AutoStartCombat) BeginCombat();
            Debug.Log("[Showcase] Ready: local FPS player on loopback port " + port + ", combat=" + CombatActive);
        }
        private void Update()
        {
            if (!CombatActive && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) BeginCombat();
        }
        public void BeginCombat()
        {
            CombatActive = true;
            if (enemies == null) enemies = FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None);
            foreach (var enemy in enemies) if (enemy != null) enemy.enabled = true;
            Debug.Log("[Showcase] Combat activated");
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(15, 15, 560, 72), "HELLFIRE / INFECTED CONTAINMENT LAB\nWASD move | Mouse look | LMB fire | R reload | Esc cursor\n" +
                (CombatActive ? "COMBAT ACTIVE" : "OBSERVATION / Press E or click START COMBAT") +
                (player == null ? "" : " | HP " + player.CurrentHealth.Value.ToString("0")));
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 12, 24, 24), "+");
            if (!CombatActive && GUI.Button(new Rect(15, 92, 180, 32), "START COMBAT")) BeginCombat();
        }
    }
}
