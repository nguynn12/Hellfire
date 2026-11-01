// Script: GameplayNoiseEmitter.cs
// Mục đích: Phát sự kiện tiếng động gameplay (tiếng súng) độc lập với AudioSource client.
// Môi trường: Client phát hiện qua OnWeaponFired -> gửi ServerRpc có rate-limit; Server xác thực và kích hoạt ZombieHearing.

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class GameplayNoiseEmitter : NetworkBehaviour
    {
        [Header("Noise Settings")]
        [Tooltip("Bán kính phát tiếng súng mặc định (m)")]
        [SerializeField] private float _gunfireNoiseRadius = 20f;

        [Tooltip("Thời gian tối thiểu giữa 2 lần phát sự kiện tiếng động (chống spam RPC)")]
        [SerializeField] private float _minNoiseInterval = 0.08f;

        [Header("Debug")]
        [SerializeField] private bool _debugLog = true;

        private WeaponController _weaponController;
        private float _lastClientNoiseTime;
        private float _lastServerNoiseTime;

        // Static event để các hệ thống/kiểm thử có thể theo dõi sự kiện tiếng động trên Server
        public static event Action<Vector3, float, Transform> OnServerNoiseEmitted;

        public float GunfireNoiseRadius => _gunfireNoiseRadius;

        private void Awake()
        {
            _weaponController = GetComponent<WeaponController>() ?? GetComponentInParent<WeaponController>();
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired += HandleWeaponFired;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired -= HandleWeaponFired;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (_weaponController == null)
            {
                _weaponController = GetComponent<WeaponController>() ?? GetComponentInParent<WeaponController>();
                if (_weaponController != null)
                {
                    _weaponController.OnWeaponFired -= HandleWeaponFired;
                    _weaponController.OnWeaponFired += HandleWeaponFired;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired -= HandleWeaponFired;
            }
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Xử lý khi vũ khí khai hỏa (OnWeaponFired từ WeaponController).
        /// </summary>
        private void HandleWeaponFired()
        {
            // Chỉ chủ sở hữu (Owner) hoặc chạy offline mới kích hoạt gửi sự kiện tiếng động
            if (IsSpawned && !IsOwner) return;

            // Chống spam phía Client
            if (Time.time - _lastClientNoiseTime < _minNoiseInterval) return;
            _lastClientNoiseTime = Time.time;

            Vector3 noisePos = transform.position;

            // Nếu đang là Server (Host hoặc Offline test) -> Thực thi trực tiếp trên Server
            if (!IsSpawned || IsServer)
            {
                EmitNoiseInternal(noisePos, _gunfireNoiseRadius);
            }
            else
            {
                // Nếu là Client thuần -> Gửi ServerRpc an toàn
                EmitNoiseServerRpc(noisePos);
            }
        }

        /// <summary>
        /// ServerRpc nhận yêu cầu phát tiếng động từ Client sở hữu.
        /// Bảo mật: Kiểm tra SenderClientId == OwnerClientId, chống spam và xác thực tọa độ trên Server.
        /// </summary>
        [ServerRpc]
        private void EmitNoiseServerRpc(Vector3 clientReportedPos, ServerRpcParams rpcParams = default)
        {
            // 1. Xác thực quyền sở hữu: Người gửi RPC phải đúng là Owner của NetworkObject này
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning($"[GameplayNoiseEmitter] Bị từ chối: SenderClientId {rpcParams.Receive.SenderClientId} không phải Owner {OwnerClientId}!");
                return;
            }

            // 2. Chống spam trên Server (Rate Limiting)
            if (Time.time - _lastServerNoiseTime < _minNoiseInterval)
            {
                return;
            }
            _lastServerNoiseTime = Time.time;

            // 3. Xác thực vị trí: Không tin tưởng tọa độ tùy ý từ Client.
            // Nếu tọa độ Client báo quá xa vị trí Player thực tế trên Server (> 3m) -> Lấy vị trí Server.
            Vector3 verifiedPos = transform.position;
            if (Vector3.Distance(clientReportedPos, transform.position) <= 3.0f)
            {
                verifiedPos = clientReportedPos;
            }

            EmitNoiseInternal(verifiedPos, _gunfireNoiseRadius);
        }

        /// <summary>
        /// Phát sự kiện tiếng động trực tiếp trên Server tới toàn bộ ZombieHearing.
        /// </summary>
        public void EmitNoiseInternal(Vector3 position, float radius)
        {
            if (_debugLog && RangedZombieAI.IsDebugLoggingEnabled())
            {
                Debug.Log($"[GameplayNoiseEmitter] Phát tiếng súng gameplay tại {position} (Bán kính: {radius}m, Người phát: {name}).");
            }

            // Kích hoạt event static Server
            OnServerNoiseEmitted?.Invoke(position, radius, transform);

            // Phân phối tới tất cả Zombie có ZombieHearing trong Scene
            ZombieHearing.BroadcastNoise(position, radius, transform);
        }

        /// <summary>
        /// Hàm phát tiếng động thủ công (dành cho kiểm thử hoặc âm thanh môi trường).
        /// </summary>
        public static void EmitNoiseDirect(Vector3 position, float radius, Transform source = null)
        {
            OnServerNoiseEmitted?.Invoke(position, radius, source);
            ZombieHearing.BroadcastNoise(position, radius, source);
        }
    }
}
