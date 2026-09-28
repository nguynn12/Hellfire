// Script: Health.cs
// Mục đích: Quản lý lượng máu, trạng thái Gục (Downed) và Chết (Dead) đồng bộ qua NetworkVariable (Mục 2.4 & 3.3).
// Môi trường thực thi: Cả hai (Server-Authoritative tính toán & trừ máu, Client nhận event cập nhật UI).

using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class Health : NetworkBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private bool _isPlayer = false;

        [Header("Player Downed & Revive Settings (Mục 3.3)")]
        [Tooltip("Thời gian chờ cứu khi gục trước khi chết hẳn (giây)")]
        [SerializeField] private float _downedDuration = 60.0f;

        [Tooltip("Tỷ lệ % máu tối đa nhận được sau khi hồi sinh")]
        [SerializeField] private float _reviveHealthPercent = 0.3f;

        [Header("Enemy Death Settings")]
        [Tooltip("Thời gian trễ trước khi despawn GameObject quái vật sau khi chết")]
        [SerializeField] private float _despawnDelay = 3.0f;

        // NetworkVariables (Server-Authoritative)
        public NetworkVariable<float> CurrentHealth { get; } = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsDowned { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsDead { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> DownedTimer { get; } = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // IDamageable implementation
        bool IDamageable.IsDead => IsDead.Value;
        bool IDamageable.IsDowned => IsDowned.Value;
        float IDamageable.CurrentHealthValue => CurrentHealth.Value;
        float IDamageable.MaxHealthValue => _maxHealth;
        bool IDamageable.IsPlayerTarget => _isPlayer;

        public float MaxHealth => _maxHealth;
        public bool IsPlayer => _isPlayer;

        // Local Events
        public event Action<float, float> OnHealthChanged;      // current, max
        public event Action<bool> OnDownedStateChanged;         // isDowned
        public event Action<ulong> OnDied;                      // attackerClientId
        public event Action OnRevived;
        public event Action<float, HitboxType, ulong> OnDamageTaken; // damage, hitbox, attacker

        private Collider[] _colliders;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentHealth.Value = _maxHealth;
                IsDowned.Value = false;
                IsDead.Value = false;
                DownedTimer.Value = 0f;
            }

            CurrentHealth.OnValueChanged += HandleHealthChanged;
            IsDowned.OnValueChanged += HandleDownedChanged;
            IsDead.OnValueChanged += HandleDeadChanged;

            // Trigger initial UI update
            OnHealthChanged?.Invoke(CurrentHealth.Value, _maxHealth);
            OnDownedStateChanged?.Invoke(IsDowned.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthChanged;
            IsDowned.OnValueChanged -= HandleDownedChanged;
            IsDead.OnValueChanged -= HandleDeadChanged;
        }

        private void Update()
        {
            // Chỉ Server mới đếm ngược thời gian gục (Mục 3.3)
            if (IsServer && _isPlayer && IsDowned.Value && !IsDead.Value)
            {
                DownedTimer.Value -= Time.deltaTime;
                if (DownedTimer.Value <= 0f)
                {
                    DownedTimer.Value = 0f;
                    IsDowned.Value = false;
                    IsDead.Value = true;
                    HandleDeathServer(0);
                }
            }
        }

        private void HandleHealthChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(newValue, _maxHealth);
        }

        private void HandleDownedChanged(bool previousValue, bool newValue)
        {
            OnDownedStateChanged?.Invoke(newValue);
        }

        private void HandleDeadChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                DisableColliders();
            }
        }

        /// <summary>
        /// Cờ bất tử tạm thời (Mục 5.2: Boss chuyển phase; Mục 6.2: Khiên tạm thời).
        /// </summary>
        public bool IsInvulnerable { get; set; }

        /// <summary>
        /// Gây sát thương lên thực thể (Server-Authoritative theo Mục 3.1 & 3.2).
        /// </summary>
        public void TakeDamage(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[Health] Chỉ Server mới có quyền trừ máu!");
                return;
            }

            if (IsInvulnerable || IsDead.Value || damage <= 0f)
            {
                return;
            }

            // Nếu người chơi đang trong trạng thái Downed, không trừ thêm máu vào Health mà tiếp tục duy trì Downed
            if (_isPlayer && IsDowned.Value)
            {
                return;
            }

            float newHealth = Mathf.Max(0f, CurrentHealth.Value - damage);
            CurrentHealth.Value = newHealth;

            // Báo feedback cho mọi client
            NotifyDamageClientRpc(damage, hitboxType, attackerClientId);

            if (CurrentHealth.Value <= 0f)
            {
                if (_isPlayer)
                {
                    // Người chơi chuyển sang trạng thái Downed (Mục 3.3)
                    IsDowned.Value = true;
                    DownedTimer.Value = _downedDuration;
                    NotifyDownedClientRpc();
                    Debug.Log($"[Health] Người chơi {NetworkObjectId} đã gục! Bắt đầu đếm ngược {_downedDuration}s để cứu.");
                }
                else
                {
                    // Quái vật chết ngay khi Health <= 0
                    IsDead.Value = true;
                    HandleDeathServer(attackerClientId);
                }
            }
        }

        private void HandleDeathServer(ulong attackerClientId)
        {
            Debug.Log($"[Health] Thực thể {gameObject.name} (NetId: {NetworkObjectId}) đã chết bởi Client {attackerClientId}!");
            NotifyDiedClientRpc(attackerClientId);

            if (!_isPlayer)
            {
                StartCoroutine(DespawnAfterDelayRoutine());
            }
        }

        private IEnumerator DespawnAfterDelayRoutine()
        {
            yield return new WaitForSeconds(_despawnDelay);
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>
        /// Server hồi sinh người chơi gục ngã (Mục 3.3).
        /// </summary>
        public void ServerRevive()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[Health] Chỉ Server mới có quyền thực thi hồi sinh!");
                return;
            }

            if (!IsDowned.Value || IsDead.Value)
            {
                return;
            }

            IsDowned.Value = false;
            DownedTimer.Value = 0f;
            CurrentHealth.Value = _maxHealth * Mathf.Clamp01(_reviveHealthPercent);

            NotifyRevivedClientRpc();
            Debug.Log($"[Health] Người chơi {NetworkObjectId} đã được hồi sinh với {CurrentHealth.Value}/{_maxHealth} HP!");
        }

        public void SetMaxHealth(float maxHealth)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
            if (IsServer)
            {
                CurrentHealth.Value = _maxHealth;
            }
        }

        /// <summary>
        /// Tăng máu tối đa và hồi đầy máu ngay lập tức (Mục 6.2: Bùa Trái tim máu lớn).
        /// </summary>
        public void IncreaseMaxHealthAndHeal(float bonus)
        {
            if (!IsServer) return;
            _maxHealth += Mathf.Max(0f, bonus);
            CurrentHealth.Value = _maxHealth;
        }

        private void DisableColliders()
        {
            if (_colliders != null)
            {
                foreach (var col in _colliders)
                {
                    if (col != null) col.enabled = false;
                }
            }
        }

        // ================= CLIENT RPC NOTIFICATIONS =================

        [ClientRpc]
        private void NotifyDamageClientRpc(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            OnDamageTaken?.Invoke(damage, hitboxType, attackerClientId);
        }

        [ClientRpc]
        private void NotifyDownedClientRpc()
        {
            OnDownedStateChanged?.Invoke(true);
        }

        [ClientRpc]
        private void NotifyDiedClientRpc(ulong attackerClientId)
        {
            DisableColliders();
            OnDied?.Invoke(attackerClientId);
        }

        [ClientRpc]
        private void NotifyRevivedClientRpc()
        {
            OnRevived?.Invoke();
            OnDownedStateChanged?.Invoke(false);
        }
    }
}
