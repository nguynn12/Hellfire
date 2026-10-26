// Script: OfflineManager.cs
// Mục đích: Quản lý chế độ kiểm thử ngoại tuyến (Scene Test Harness) cục bộ, độc lập cho từng scene test.
//           Tự động kích hoạt trạng thái Offline và cung cấp các tiện ích Debug (GodMode, InfiniteAmmo, InitialWeapon).
// Môi trường thực thi: Client / Editor / Offline Test Scene (Tự hủy nếu xuất hiện trong Scene chính thức "Gameplay").

using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Hellfire.Networking
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class OfflineManager : MonoBehaviour
    {
        public static OfflineManager Instance { get; private set; }

        [Header("Testing & Debug Options")]
        [Tooltip("Bất tử máu khi test trong scene")]
        [SerializeField] private bool _godMode = false;

        [Tooltip("Vô hạn đạn khi test trong scene (không trừ đạn khi bắn)")]
        [SerializeField] private bool _infiniteAmmo = false;

        [Tooltip("Chỉ số vũ khí trang bị sẵn khi bắt đầu (0: Mặc định, 1: Shotgun, 2: Rifle, 3: Melee...)")]
        [SerializeField] private int _initialWeaponIndex = 0;

        public bool GodMode => _godMode;
        public bool InfiniteAmmo => _infiniteAmmo;
        public int InitialWeaponIndex => _initialWeaponIndex;

        private void Awake()
        {
            // Kiểm tra an toàn: Nếu là Scene chính thức "Gameplay", tự hủy ngay để tránh phá vỡ cơ chế Multiplayer
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == "Gameplay")
            {
                Debug.LogWarning("[OfflineManager] Phát hiện OfflineManager trong scene 'Gameplay'! Tự hủy để đảm bảo tính toàn vẹn của Multiplayer.");
                Destroy(gameObject);
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[OfflineManager] Đã tồn tại một OfflineManager khác trong Scene. Hủy bản sao trên GameObject '{gameObject.name}'.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GameModeHelper.ForceOfflineMode = true;

            // Nếu NetworkManager đang chạy dở hoặc lắng nghe mạng thử nghiệm, chủ động ngắt
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[OfflineManager] Chủ động gọi NetworkManager.Singleton.Shutdown() để ngắt socket thử nghiệm.");
                NetworkManager.Singleton.Shutdown();
            }

            Debug.Log($"<color=#00FFAA>[OfflineManager] Chế độ Offline Test đã kích hoạt trong Scene '{activeSceneName}'. (GodMode: {_godMode}, InfiniteAmmo: {_infiniteAmmo}, WeaponIndex: {_initialWeaponIndex})</color>");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                GameModeHelper.ForceOfflineMode = false;
                Instance = null;
                Debug.Log("[OfflineManager] Đã giải phóng OfflineManager và reset ForceOfflineMode về false.");
            }
        }
    }
}
