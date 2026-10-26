using UnityEngine;
using Unity.Netcode;

namespace Hellfire.Combat
{
    /// <summary>
    /// Component hỗ trợ tự động khởi động Local Host trong Scene Test (TestCombatJuice.unity).
    /// Giúp Player nhận quyền sở hữu (IsOwner = true) và thực thi trọn vẹn quy trình Netcode gốc của đồng đội.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalTestHostBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _autoStartHost = true;

        private void Start()
        {
            if (_autoStartHost && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartHost();
                Debug.Log($"[LocalTestHostBootstrap] Khởi tạo Local Host tự động cho Scene Test: {success}");

                if (success && NetworkManager.Singleton.IsServer)
                {
                    // 1. Spawn Player trong Scene làm PlayerObject cho Local Host nếu có
                    var playerObj = GameObject.Find("Player_Test");
                    if (playerObj != null)
                    {
                        var playerNet = playerObj.GetComponent<NetworkObject>();
                        if (playerNet != null && !playerNet.IsSpawned)
                        {
                            playerNet.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
                            Debug.Log("[LocalTestHostBootstrap] Đã Spawn Player_Test làm PlayerObject.");
                        }
                    }

                    // 2. Spawn tất cả NetworkObject khác trong Scene (như Archer Zombie)
                    var netObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                    foreach (var no in netObjects)
                    {
                        if (no != null && !no.IsSpawned && no.gameObject.scene.isLoaded)
                        {
                            no.Spawn(true);
                            Debug.Log($"[LocalTestHostBootstrap] Đã tự động Network Spawn: {no.name}");
                        }
                    }
                }
            }
        }
    }
}
