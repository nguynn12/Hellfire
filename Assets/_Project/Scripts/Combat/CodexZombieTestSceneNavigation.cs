using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace Hellfire.Combat
{
    // Attach only to isolated zombie test scenes. No global navigation settings or baked assets change.
    [DefaultExecutionOrder(-10000)]
    public sealed class CodexZombieTestSceneNavigation : MonoBehaviour
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private void Awake()
        {
            // Scene contains Player_Test. Prevent NGO also spawning its configured player prefab.
            // Configure at runtime before LocalTestHostBootstrap.Start; prefab defaults stay intact.
            var manager = GetComponent<NetworkManager>();
            if (manager != null && !manager.IsListening) manager.NetworkConfig.PlayerPrefab = null;
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var health in FindObjectsByType<Health>(FindObjectsSortMode.None))
                markups.Add(new NavMeshBuildMarkup { root = health.transform, ignoreFromBuild = true });
            NavMeshBuilder.CollectSources(null, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Vector3.zero, new Vector3(160, 40, 160)), Vector3.zero, Quaternion.identity);
            if (data != null) instance = NavMesh.AddNavMeshData(data);
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                if (agent.enabled && NavMesh.SamplePosition(agent.transform.position, out var hit, 3, agent.areaMask))
                    agent.Warp(hit.position);
            Debug.Log("[CodexZombieQA] Runtime test-scene NavMesh built: " + instance.valid);
        }
        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }
    }
}
