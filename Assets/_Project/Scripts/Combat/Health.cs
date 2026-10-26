// Script: Health.cs
// Mục đích: Quản lý lượng máu, giáp, trạng thái Gục (Downed) và Chết (Dead) đồng bộ qua NetworkVariable (Mục 2.4 & 3.3).
//           Tính toán sát thương theo công thức giáp chuẩn: DamageFactor = e^(-0.01053605 * EffectiveArmor).
// Môi trường thực thi: Cả hai (Server-Authoritative tính toán & trừ máu, Client nhận event cập nhật UI).

using System;
using System.Collections;
using Hellfire.Player;
using Hellfire.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class Health : NetworkBehaviour, IDamageable
    {
        [Header("Health Settings (Mục I.2 & II.2)")]
        [Tooltip("Lượng máu tối đa ban đầu (Cơ bản là 100)")]
        [SerializeField] private float _baseMaxHealth = 100f;
        [SerializeField] private bool _isPlayer = false;

        [Header("Armor Settings")]
        [Tooltip("Chỉ số giáp cơ bản (dành cho Quái vật/Boss. Người chơi sẽ ưu tiên lấy từ PlayerStats)")]
        [SerializeField] private float _baseArmor = 0f;

        [Header("Player Downed & Revive Settings (Mục 3.3)")]
        [Tooltip("Thời gian chờ cứu khi gục trước khi chết hẳn (giây)")]
        [SerializeField] private float _downedDuration = 60.0f;

        [Tooltip("Tỷ lệ % máu tối đa nhận được sau khi hồi sinh")]
        [SerializeField] private float _reviveHealthPercent = 0.3f;


        // NetworkVariables (Server-Authoritative)
        public NetworkVariable<float> CurrentHealth { get; } = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> MaxHealthNet { get; } = new NetworkVariable<float>(
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
        float IDamageable.MaxHealthValue => MaxHealthNet.Value;
        bool IDamageable.IsPlayerTarget => _isPlayer;

        public float MaxHealth => MaxHealthNet.Value;
        public float BaseArmor => _baseArmor;
        public bool IsPlayer => _isPlayer;

        // Local Events
        public event Action<float, float> OnHealthChanged;           // current, max
        public event Action<bool> OnDownedStateChanged;              // isDowned
        public event Action<ulong> OnDied;                           // attackerClientId
        public event Action OnRevived;
        public event Action<float, HitboxType, ulong> OnDamageTaken; // damage, hitbox, attacker
        public event Action<float, Vector3> OnPlayerAttackedBy;      // damage, attackerPosition (Mục 16.7)

        private Collider[] _colliders;
        private PlayerStats _playerStats;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>();
            _playerStats = GetComponent<PlayerStats>();
        }

        private void Start()
        {
            if (GameModeHelper.IsOffline && !IsSpawned)
            {
                MaxHealthNet.Value = _baseMaxHealth;
                CurrentHealth.Value = _baseMaxHealth;
                IsDowned.Value = false;
                IsDead.Value = false;
                DownedTimer.Value = 0f;
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
                OnDownedStateChanged?.Invoke(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                MaxHealthNet.Value = _baseMaxHealth;
                CurrentHealth.Value = _baseMaxHealth;
                IsDowned.Value = false;
                IsDead.Value = false;
                DownedTimer.Value = 0f;
            }

            CurrentHealth.OnValueChanged += HandleHealthChanged;
            MaxHealthNet.OnValueChanged += HandleMaxHealthChanged;
            IsDowned.OnValueChanged += HandleDownedChanged;
            IsDead.OnValueChanged += HandleDeadChanged;

            // Trigger initial UI update
            OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
            OnDownedStateChanged?.Invoke(IsDowned.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthChanged;
            MaxHealthNet.OnValueChanged -= HandleMaxHealthChanged;
            IsDowned.OnValueChanged -= HandleDownedChanged;
            IsDead.OnValueChanged -= HandleDeadChanged;
        }

        private void Update()
        {
            // Chỉ Server hoặc Offline mới đếm ngược thời gian gục (Mục 3.3)
            if ((IsServer || GameModeHelper.IsOffline) && _isPlayer && IsDowned.Value && !IsDead.Value)
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
            OnHealthChanged?.Invoke(newValue, MaxHealthNet.Value);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(CurrentHealth.Value, newValue);
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
        /// Gây sát thương lên thực thể (Server-Authoritative theo Mục I.3).
        /// Áp dụng công thức: EffectiveArmor = TargetArmor - ArmorPenetration,
        /// Hệ số giảm thương: DamageFactor = e^(-0.01053605 * EffectiveArmor).
        /// </summary>
        /// <param name="payload">Gói dữ liệu sát thương bất biến Zero-GC</param>
        public void TakeDamage(DamagePayload payload)
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                Debug.LogWarning("[Health] Chỉ Server mới có quyền trừ máu!");
                return;
            }

            if (IsInvulnerable || IsDead.Value || payload.RawDamage <= 0f)
            {
                return;
            }

            // Nếu người chơi đang trong trạng thái Downed, không trừ thêm máu vào Health mà tiếp tục duy trì Downed
            if (_isPlayer && IsDowned.Value)
            {
                return;
            }

            // 1. Xác định giáp mục tiêu
            float targetArmor = _baseArmor;
            if (_isPlayer)
            {
                if (_playerStats == null) _playerStats = GetComponent<PlayerStats>();
                if (_playerStats != null)
                {
                    targetArmor = _playerStats.Armor.Value;
                }
            }

            // 2. Tính giáp hiệu dụng: EffectiveArmor = TargetArmor - ArmorPenetration (Mục I.3)
            float effectiveArmor = targetArmor - payload.ArmorPenetration;

            // 3. Hệ số giảm thương chính xác: DamageFactor = e^(-0.01053605 * EffectiveArmor) ~ (0.9)^(EffectiveArmor / 10)
            // Khi EffectiveArmor âm (xuyên giáp vượt quá giáp): tự động khuếch đại sát thương nhận vào (> 1.0)
            float damageFactor = Mathf.Exp(-0.01053605f * effectiveArmor);

            // 4. Hệ số nhân vị trí trúng đòn Hitbox
            float hitboxMultiplier = 1.0f;
            switch (payload.Hitbox)
            {
                case HitboxType.Head:
                    hitboxMultiplier = 2.0f;
                    break;
                case HitboxType.Torso:
                    hitboxMultiplier = 1.0f;
                    break;
                case HitboxType.Limb:
                    hitboxMultiplier = 0.75f;
                    break;
            }

            // 5. Tính sát thương cuối cùng: làm tròn số nguyên gần nhất (Mathf.Round), tối thiểu 1 sát thương
            float calculatedDamage = payload.RawDamage * damageFactor * hitboxMultiplier;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(calculatedDamage));

            float newHealth = Mathf.Max(0f, CurrentHealth.Value - finalDamage);
            CurrentHealth.Value = newHealth;

            // Báo feedback cho mọi client hoặc cập nhật trực tiếp khi Offline
            if (GameModeHelper.IsOffline)
            {
                OnDamageTaken?.Invoke(finalDamage, payload.Hitbox, payload.AttackerClientId);
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
            }
            else
            {
                NotifyDamageClientRpc(finalDamage, payload.Hitbox, payload.AttackerClientId);
            }

            // Báo hướng kẻ tấn công cho riêng nạn nhân (Mục 16.7)
            if (_isPlayer)
            {
                Vector3 attackerPos = Vector3.zero;
                if (NetworkManager.Singleton != null)
                {
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(payload.AttackerClientId, out var client) && client.PlayerObject != null)
                    {
                        attackerPos = client.PlayerObject.transform.position;
                    }
                    else if (NetworkManager.Singleton.SpawnManager != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(payload.AttackerClientId, out var netObj))
                    {
                        attackerPos = netObj.transform.position;
                    }
                }
                NotifyPlayerAttackedClientRpc(finalDamage, attackerPos);
            }

            if (CurrentHealth.Value <= 0f)
            {
                if (_isPlayer)
                {
                    // Người chơi chuyển sang trạng thái Downed (Mục 3.3)
                    IsDowned.Value = true;
                    DownedTimer.Value = _downedDuration;
                    if (GameModeHelper.IsOffline)
                    {
                        OnDownedStateChanged?.Invoke(true);
                    }
                    else
                    {
                        NotifyDownedClientRpc();
                    }
                    Networking.MatchSummaryManager.RecordDowned(OwnerClientId);
                    Debug.Log($"[Health] Người chơi {NetworkObjectId} đã gục! Bắt đầu đếm ngược {_downedDuration}s để cứu.");
                }
                else
                {
                    // Quái vật chết ngay khi Health <= 0
                    IsDead.Value = true;
                    HandleDeathServer(payload.AttackerClientId);
                }
            }
        }

        /// <summary>
        /// Phương thức hỗ trợ gọi nhanh tương thích ngược (tự động đóng gói DamagePayload).
        /// </summary>
        public void TakeDamage(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            TakeDamage(new DamagePayload(damage, 0f, hitboxType, attackerClientId));
        }

        private void HandleDeathServer(ulong attackerClientId)
        {
            Debug.Log($"[Health] Thực thể {gameObject.name} (NetId: {NetworkObjectId}) đã chết bởi Client {attackerClientId}!");
            if (GameModeHelper.IsOffline)
            {
                OnDied?.Invoke(attackerClientId);
            }
            else
            {
                NotifyDiedClientRpc(attackerClientId);
            }

            if (!_isPlayer)
            {
                // Toàn bộ trách nhiệm despawn quái vật sau 3 giây được quy về EnemyStateMachine.DespawnRoutine() (Lỗi 02)
            }
        }

        /// <summary>
        /// Tăng máu tối đa cho người chơi trên Server và đồng thời khôi phục lượng máu tương ứng.
        /// </summary>
        public void IncreaseMaxHealthServer(float bonusAmount)
        {
            if (!GameModeHelper.IsServerOrOffline || bonusAmount <= 0f) return;

            MaxHealthNet.Value += bonusAmount;
            CurrentHealth.Value = Mathf.Min(MaxHealthNet.Value, CurrentHealth.Value + bonusAmount);
            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
            }
            Debug.Log($"[Health] Đã tăng MaxHealth cho {gameObject.name}: {MaxHealthNet.Value} HP.");
        }

        /// <summary>
        /// Tăng máu tối đa và hồi đầy máu ngay khi nhặt bùa (Mục 6.2).
        /// </summary>
        public void IncreaseMaxHealthAndHeal(float bonusAmount)
        {
            if (!GameModeHelper.IsServerOrOffline || bonusAmount <= 0f) return;

            MaxHealthNet.Value += bonusAmount;
            CurrentHealth.Value = MaxHealthNet.Value;
            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
            }
            Debug.Log($"[Health] Đã tăng MaxHealth và hồi đầy máu cho {gameObject.name}: {MaxHealthNet.Value} HP.");
        }

        /// <summary>
        /// Hồi máu cho thực thể trên Server.
        /// </summary>
        public void Heal(float amount)
        {
            if (!GameModeHelper.IsServerOrOffline || amount <= 0f || IsDead.Value) return;

            CurrentHealth.Value = Mathf.Min(MaxHealthNet.Value, CurrentHealth.Value + amount);
            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
            }
        }

        /// <summary>
        /// Server hồi sinh người chơi gục ngã (Mục 3.3).
        /// </summary>
        public void ServerRevive()
        {
            if (!GameModeHelper.IsServerOrOffline)
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
            CurrentHealth.Value = MaxHealthNet.Value * Mathf.Clamp01(_reviveHealthPercent);

            if (GameModeHelper.IsOffline)
            {
                OnRevived?.Invoke();
                OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
                OnDownedStateChanged?.Invoke(false);
            }
            else
            {
                NotifyRevivedClientRpc();
            }
            Debug.Log($"[Health] Người chơi {NetworkObjectId} đã được hồi sinh với {CurrentHealth.Value}/{MaxHealthNet.Value} HP!");
        }

        public void SetMaxHealth(float maxHealth)
        {
            _baseMaxHealth = Mathf.Max(1f, maxHealth);
            if (GameModeHelper.IsServerOrOffline)
            {
                MaxHealthNet.Value = _baseMaxHealth;
                CurrentHealth.Value = _baseMaxHealth;
                if (GameModeHelper.IsOffline)
                {
                    OnHealthChanged?.Invoke(CurrentHealth.Value, MaxHealthNet.Value);
                }
            }
        }

        public void SetBaseArmor(float armor)
        {
            _baseArmor = Mathf.Max(0f, armor);
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
        private void NotifyPlayerAttackedClientRpc(float damage, Vector3 attackerPosition)
        {
            if (IsOwner)
            {
                OnPlayerAttackedBy?.Invoke(damage, attackerPosition);
            }
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
