// Script: Health.cs
// Mục đích: Quản lý lượng máu, giáp, trạng thái Gục (Downed) và Chết (Dead) đồng bộ qua NetworkVariable (Mục 2.4 & 3.3).
//           Tính toán sát thương theo công thức giáp chuẩn: DamageFactor = e^(-0.01053605 * EffectiveArmor).
//           Hỗ trợ chế độ Offline mượt mà (biến fallback _rawCurrentHealth) và miễn nhiễm sát thương nếu bật GodMode từ OfflineManager.
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

        // Fallback variables nội bộ khi chạy Offline (chưa spawn mạng)
        private float _rawCurrentHealth = 100f;
        private float _rawMaxHealth = 100f;
        private bool _rawIsDowned = false;
        private bool _rawIsDead = false;

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

        // Safe Accessors (hỗ trợ đọc an toàn cả Online lẫn Offline)
        public float CurrentHealthValue => IsSpawned ? CurrentHealth.Value : _rawCurrentHealth;
        public float MaxHealthValue => IsSpawned ? MaxHealthNet.Value : _rawMaxHealth;
        public bool IsDownedValue => IsSpawned ? IsDowned.Value : _rawIsDowned;
        public bool IsDeadValue => IsSpawned ? IsDead.Value : _rawIsDead;

        // IDamageable implementation
        bool IDamageable.IsDead => IsDeadValue;
        bool IDamageable.IsDowned => IsDownedValue;
        float IDamageable.CurrentHealthValue => CurrentHealthValue;
        float IDamageable.MaxHealthValue => MaxHealthValue;
        bool IDamageable.IsPlayerTarget => _isPlayer;

        public float MaxHealth => MaxHealthValue;
        public float BaseArmor => _baseArmor;
        public bool IsPlayer => _isPlayer;

        // Local Events
        public event Action<float, float> OnHealthChanged;           // current, max
        public event Action<bool> OnDownedStateChanged;              // isDowned
        public event Action<ulong> OnDied;                           // attackerClientId
        public event Action OnRevived;
        public event Action<float, HitboxType, ulong> OnDamageTaken; // damage, hitbox, attacker

        private Collider[] _colliders;
        private PlayerStats _playerStats;

        private void Awake()
        {
            _rawMaxHealth = _baseMaxHealth;
            _rawCurrentHealth = _baseMaxHealth;
            _colliders = GetComponentsInChildren<Collider>();
            _playerStats = GetComponent<PlayerStats>();
        }

        private void Start()
        {
            if (GameModeHelper.IsOffline && !IsSpawned)
            {
                _rawMaxHealth = _baseMaxHealth;
                _rawCurrentHealth = _baseMaxHealth;
                _rawIsDowned = false;
                _rawIsDead = false;
                OnHealthChanged?.Invoke(_rawCurrentHealth, _rawMaxHealth);
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

            _rawMaxHealth = MaxHealthNet.Value;
            _rawCurrentHealth = CurrentHealth.Value;
            _rawIsDowned = IsDowned.Value;
            _rawIsDead = IsDead.Value;

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
            if ((IsServer || GameModeHelper.IsOffline) && _isPlayer && IsDownedValue && !IsDeadValue)
            {
                float newTimer = DownedTimer.Value - Time.deltaTime;
                if (IsSpawned)
                {
                    DownedTimer.Value = Mathf.Max(0f, newTimer);
                }

                if (newTimer <= 0f)
                {
                    if (IsSpawned)
                    {
                        IsDead.Value = true;
                    }
                    else
                    {
                        _rawIsDead = true;
                    }
                    HandleDeathServer(0);
                }
            }
        }

        private void HandleHealthChanged(float prev, float curr)
        {
            _rawCurrentHealth = curr;
            OnHealthChanged?.Invoke(curr, MaxHealthNet.Value);
        }

        private void HandleMaxHealthChanged(float prev, float curr)
        {
            _rawMaxHealth = curr;
            OnHealthChanged?.Invoke(CurrentHealth.Value, curr);
        }

        private void HandleDownedChanged(bool prev, bool curr)
        {
            _rawIsDowned = curr;
            OnDownedStateChanged?.Invoke(curr);
        }

        private void HandleDeadChanged(bool prev, bool curr)
        {
            _rawIsDead = curr;
            if (curr)
            {
                DisableColliders();
            }
        }

        /// <summary>
        /// Cho phép tạm thời bất tử (GodMode hoặc test).
        /// </summary>
        public bool IsInvulnerable { get; set; } = false;

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

            // Kiểm tra GodMode từ OfflineManager khi test trong scene
            if (_isPlayer && OfflineManager.Instance != null && OfflineManager.Instance.GodMode)
            {
                return;
            }

            if (IsInvulnerable || IsDeadValue || payload.RawDamage <= 0f)
            {
                return;
            }

            // Nếu người chơi đang trong trạng thái Downed, không trừ thêm máu vào Health mà tiếp tục duy trì Downed
            if (_isPlayer && IsDownedValue)
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
                    targetArmor = _playerStats.CurrentArmor;
                }
            }

            // 2. Tính giáp hiệu dụng: EffectiveArmor = TargetArmor - ArmorPenetration (Mục I.3)
            float effectiveArmor = targetArmor - payload.ArmorPenetration;

            // 3. Hệ số giảm thương chính xác: DamageFactor = e^(-0.01053605 * EffectiveArmor) ~ (0.9)^(EffectiveArmor / 10)
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

            if (IsSpawned)
            {
                float newHealth = Mathf.Max(0f, CurrentHealth.Value - finalDamage);
                CurrentHealth.Value = newHealth;
                _rawCurrentHealth = newHealth;
            }
            else
            {
                _rawCurrentHealth = Mathf.Max(0f, _rawCurrentHealth - finalDamage);
            }

            // Báo feedback cho mọi client hoặc cập nhật trực tiếp khi Offline
            if (GameModeHelper.IsOffline)
            {
                OnDamageTaken?.Invoke(finalDamage, payload.Hitbox, payload.AttackerClientId);
                OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
            }
            else
            {
                NotifyDamageClientRpc(finalDamage, payload.Hitbox, payload.AttackerClientId);
            }

            if (CurrentHealthValue <= 0f)
            {
                if (_isPlayer)
                {
                    // Người chơi chuyển sang trạng thái Downed (Mục 3.3)
                    if (IsSpawned)
                    {
                        IsDowned.Value = true;
                        DownedTimer.Value = _downedDuration;
                    }
                    else
                    {
                        _rawIsDowned = true;
                    }

                    if (GameModeHelper.IsOffline)
                    {
                        OnDownedStateChanged?.Invoke(true);
                    }
                    else
                    {
                        NotifyDownedClientRpc();
                    }
                    Debug.Log($"[Health] Người chơi {NetworkObjectId} đã gục! Bắt đầu đếm ngược {_downedDuration}s để cứu.");
                }
                else
                {
                    // Quái vật chết ngay khi Health <= 0
                    if (IsSpawned)
                    {
                        IsDead.Value = true;
                    }
                    else
                    {
                        _rawIsDead = true;
                    }
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
                DisableColliders();
                OnDied?.Invoke(attackerClientId);
            }
            else
            {
                NotifyDiedClientRpc(attackerClientId);
            }
        }

        /// <summary>
        /// Tăng máu tối đa cho người chơi trên Server và đồng thời khôi phục lượng máu tương ứng.
        /// </summary>
        public void IncreaseMaxHealthServer(float bonusAmount)
        {
            if (!GameModeHelper.IsServerOrOffline || bonusAmount <= 0f) return;

            if (IsSpawned)
            {
                MaxHealthNet.Value += bonusAmount;
                CurrentHealth.Value = Mathf.Min(MaxHealthNet.Value, CurrentHealth.Value + bonusAmount);
                _rawMaxHealth = MaxHealthNet.Value;
                _rawCurrentHealth = CurrentHealth.Value;
            }
            else
            {
                _rawMaxHealth += bonusAmount;
                _rawCurrentHealth = Mathf.Min(_rawMaxHealth, _rawCurrentHealth + bonusAmount);
            }

            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
            }
            Debug.Log($"[Health] Đã tăng MaxHealth cho {gameObject.name}: {MaxHealthValue} HP.");
        }

        /// <summary>
        /// Tăng máu tối đa và hồi đầy máu ngay khi nhặt bùa (Mục 6.2).
        /// </summary>
        public void IncreaseMaxHealthAndHeal(float bonusAmount)
        {
            if (!GameModeHelper.IsServerOrOffline || bonusAmount <= 0f) return;

            if (IsSpawned)
            {
                MaxHealthNet.Value += bonusAmount;
                CurrentHealth.Value = MaxHealthNet.Value;
                _rawMaxHealth = MaxHealthNet.Value;
                _rawCurrentHealth = CurrentHealth.Value;
            }
            else
            {
                _rawMaxHealth += bonusAmount;
                _rawCurrentHealth = _rawMaxHealth;
            }

            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
            }
            Debug.Log($"[Health] Đã tăng MaxHealth và hồi đầy máu cho {gameObject.name}: {MaxHealthValue} HP.");
        }

        /// <summary>
        /// Hồi máu cho thực thể trên Server hoặc Offline.
        /// </summary>
        public void Heal(float amount)
        {
            if (!GameModeHelper.IsServerOrOffline || amount <= 0f || IsDeadValue) return;

            if (IsSpawned)
            {
                CurrentHealth.Value = Mathf.Min(MaxHealthNet.Value, CurrentHealth.Value + amount);
                _rawCurrentHealth = CurrentHealth.Value;
            }
            else
            {
                _rawCurrentHealth = Mathf.Min(_rawMaxHealth, _rawCurrentHealth + amount);
            }

            if (GameModeHelper.IsOffline)
            {
                OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
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

            if (!IsDownedValue || IsDeadValue)
            {
                return;
            }

            if (IsSpawned)
            {
                IsDowned.Value = false;
                DownedTimer.Value = 0f;
                CurrentHealth.Value = MaxHealthNet.Value * Mathf.Clamp01(_reviveHealthPercent);
                _rawCurrentHealth = CurrentHealth.Value;
                _rawIsDowned = false;
            }
            else
            {
                _rawIsDowned = false;
                _rawCurrentHealth = _rawMaxHealth * Mathf.Clamp01(_reviveHealthPercent);
            }

            if (GameModeHelper.IsOffline)
            {
                OnRevived?.Invoke();
                OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
                OnDownedStateChanged?.Invoke(false);
            }
            else
            {
                NotifyRevivedClientRpc();
            }
            Debug.Log($"[Health] Người chơi {NetworkObjectId} đã được hồi sinh với {CurrentHealthValue}/{MaxHealthValue} HP!");
        }

        public void SetMaxHealth(float maxHealth)
        {
            _baseMaxHealth = Mathf.Max(1f, maxHealth);
            if (GameModeHelper.IsServerOrOffline)
            {
                if (IsSpawned)
                {
                    MaxHealthNet.Value = _baseMaxHealth;
                    CurrentHealth.Value = _baseMaxHealth;
                }
                _rawMaxHealth = _baseMaxHealth;
                _rawCurrentHealth = _baseMaxHealth;

                if (GameModeHelper.IsOffline)
                {
                    OnHealthChanged?.Invoke(CurrentHealthValue, MaxHealthValue);
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
