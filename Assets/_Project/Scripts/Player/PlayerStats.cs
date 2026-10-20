// Script: PlayerStats.cs
// Mục đích: Quản lý các chỉ số sinh tồn và cơ động của người chơi (Máu cộng thêm, Giáp, Hệ số tốc chạy), có trần cứng (Max Caps).
//           Ghi nhớ lịch sử các Tế đàn (Shrine) đã tương tác (Server-Authoritative).
// Môi trường thực thi: Cả hai (Server tính toán & ghi nhận, Client đọc qua NetworkVariable).

using System;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Player
{
    /// <summary>
    /// Quản lý chỉ số thân thể (Body Stats) của người chơi theo nguyên tắc phân tách trách nhiệm.
    /// Nhân vật không sở hữu chỉ số tấn công (chỉ số tấn công do WeaponData và RuntimeWeapon quản lý).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerStats : NetworkBehaviour
    {
        // ================= GIỚI HẠN TRẦN CHỈ SỐ (HARD CAPS) =================
        public const float MaxBonusHealthCap = 150.0f;       // Trần máu cộng thêm (khi cộng với 100 máu gốc thì tối đa 250 HP)
        public const float MaxArmorCap = 60.0f;              // Trần giáp tối đa (60 giáp ~ giảm khoảng 47% sát thương)
        public const float MaxSpeedMultiplierCap = 1.8f;     // Tốc độ chạy tối đa 180% (tăng tối đa +80%)
        public const float MinSpeedMultiplier = 1.0f;        // Tốc độ cơ bản ban đầu là 1.0 (100%)

        // ================= NETWORK VARIABLES (SERVER-AUTHORITATIVE) =================
        /// <summary>
        /// Lượng máu tối đa cộng thêm từ tế đàn hoặc bùa hộ mệnh (0 đến 150).
        /// </summary>
        public NetworkVariable<float> BonusMaxHealth { get; } = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Chỉ số giáp hiện tại của người chơi (0 đến 60).
        /// </summary>
        public NetworkVariable<float> Armor { get; } = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Hệ số nhân tốc độ di chuyển (1.0 đến 1.8).
        /// </summary>
        public NetworkVariable<float> MoveSpeedMultiplier { get; } = new NetworkVariable<float>(
            MinSpeedMultiplier,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Thuộc tính truy cập nhanh chỉ số hiện tại (hoạt động đồng thời Online và Offline).
        /// </summary>
        public float CurrentBonusMaxHealth => BonusMaxHealth.Value;
        public float CurrentArmor => Armor.Value;
        public float CurrentMoveSpeedMultiplier => MoveSpeedMultiplier.Value;

        // ================= LỊCH SỬ TẾ ĐÀN (SERVER-SIDE TRACKING) =================
        // Lưu trữ các ID tế đàn mà riêng người chơi này đã kích hoạt (chỉ chạy trên Server)
        private readonly HashSet<int> _usedShrineIds = new HashSet<int>();

        // Sự kiện cục bộ để UI hoặc các hệ thống khác lắng nghe khi chỉ số thay đổi
        public event Action<float, float, float> OnStatsChanged; // bonusHealth, armor, speedMultiplier

        private Health _healthComponent;

        private void Awake()
        {
            _healthComponent = GetComponent<Health>();
        }

        private void Start()
        {
            if (GameModeHelper.IsOffline && !IsSpawned)
            {
                NotifyStatsChanged();
            }
        }

        public override void OnNetworkSpawn()
        {
            BonusMaxHealth.OnValueChanged += HandleBonusHealthChanged;
            Armor.OnValueChanged += HandleArmorChanged;
            MoveSpeedMultiplier.OnValueChanged += HandleSpeedChanged;

            // Kích hoạt event ban đầu cho UI
            NotifyStatsChanged();
        }

        public override void OnNetworkDespawn()
        {
            BonusMaxHealth.OnValueChanged -= HandleBonusHealthChanged;
            Armor.OnValueChanged -= HandleArmorChanged;
            MoveSpeedMultiplier.OnValueChanged -= HandleSpeedChanged;

            if (IsServer)
            {
                _usedShrineIds.Clear();
            }
        }

        private void HandleBonusHealthChanged(float prev, float curr) => NotifyStatsChanged();
        private void HandleArmorChanged(float prev, float curr) => NotifyStatsChanged();
        private void HandleSpeedChanged(float prev, float curr) => NotifyStatsChanged();

        private void NotifyStatsChanged()
        {
            OnStatsChanged?.Invoke(BonusMaxHealth.Value, Armor.Value, MoveSpeedMultiplier.Value);
        }

        // ================= QUẢN LÝ LỊCH SỬ TẾ ĐÀN (SHRINE TRACKING) =================

        /// <summary>
        /// Kiểm tra xem người chơi này đã từng dùng tế đàn có ID này chưa (Server-Authoritative).
        /// </summary>
        public bool HasUsedShrine(int shrineId)
        {
            return _usedShrineIds.Contains(shrineId);
        }

        /// <summary>
        /// Ghi nhận tế đàn đã được tiêu thụ bởi người chơi này.
        /// </summary>
        public void RecordShrineUsed(int shrineId)
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                Debug.LogWarning("[PlayerStats] Chỉ Server mới có quyền ghi nhận Shrine!");
                return;
            }

            _usedShrineIds.Add(shrineId);
            Debug.Log($"[PlayerStats] Người chơi {NetworkObjectId} đã dùng Tế đàn #{shrineId}. Tổng số tế đàn đã dùng: {_usedShrineIds.Count}");
        }

        // ================= NÂNG CẤP CHỈ SỐ CƠ THỂ (BODY BUFF) =================

        /// <summary>
        /// Kiểm tra xem người chơi có còn khả năng nhận thêm buff chỉ số nào không (chưa chạm trần).
        /// </summary>
        public bool CanReceiveBodyBuff(float addHealth, float addArmor, float addSpeed)
        {
            bool canAddHp = addHealth > 0f && BonusMaxHealth.Value < MaxBonusHealthCap;
            bool canAddArmor = addArmor > 0f && Armor.Value < MaxArmorCap;
            bool canAddSpeed = addSpeed > 0f && MoveSpeedMultiplier.Value < MaxSpeedMultiplierCap;

            return canAddHp || canAddArmor || canAddSpeed;
        }

        /// <summary>
        /// Thực thi cộng chỉ số cơ thể lên người chơi (Server-Authoritative).
        /// Tự động clamp trong giới hạn trần cứng (Max Caps).
        /// </summary>
        /// <returns>True nếu có ít nhất 1 chỉ số được tăng, False nếu đã đạt trần tối đa.</returns>
        public bool TryApplyBodyBuff(float addHealth, float addArmor, float addSpeed)
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                Debug.LogWarning("[PlayerStats] Chỉ Server mới có quyền tăng chỉ số PlayerStats!");
                return false;
            }

            if (!CanReceiveBodyBuff(addHealth, addArmor, addSpeed))
            {
                return false;
            }

            bool statModified = false;

            // 1. Tăng máu tối đa (kèm hồi lượng máu tương ứng vào Health)
            if (addHealth > 0f && BonusMaxHealth.Value < MaxBonusHealthCap)
            {
                float newBonus = Mathf.Min(MaxBonusHealthCap, BonusMaxHealth.Value + addHealth);
                float actualAdd = newBonus - BonusMaxHealth.Value;
                BonusMaxHealth.Value = newBonus;

                if (_healthComponent != null)
                {
                    _healthComponent.IncreaseMaxHealthServer(actualAdd);
                }
                statModified = true;
            }

            // 2. Tăng Giáp
            if (addArmor > 0f && Armor.Value < MaxArmorCap)
            {
                Armor.Value = Mathf.Min(MaxArmorCap, Armor.Value + addArmor);
                statModified = true;
            }

            // 3. Tăng Tốc độ chạy
            if (addSpeed > 0f && MoveSpeedMultiplier.Value < MaxSpeedMultiplierCap)
            {
                MoveSpeedMultiplier.Value = Mathf.Min(MaxSpeedMultiplierCap, MoveSpeedMultiplier.Value + addSpeed);
                statModified = true;
            }

            if (statModified)
            {
                if (GameModeHelper.IsOffline)
                {
                    NotifyStatsChanged();
                }
                Debug.Log($"[PlayerStats] Đã nâng cấp chỉ số cho Client {OwnerClientId}: +HP={BonusMaxHealth.Value}/{MaxBonusHealthCap}, Armor={Armor.Value}/{MaxArmorCap}, Speed={MoveSpeedMultiplier.Value:F2}x");
            }

            return statModified;
        }
    }
}
