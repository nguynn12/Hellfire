// Script: PlayerStats.cs
// Mục đích: Quản lý các chỉ số sinh tồn và cơ động của người chơi (Máu cộng thêm, Giáp, Hệ số tốc chạy), có trần cứng (Max Caps).
//           Ghi nhớ lịch sử các Tế đàn (Shrine) đã tương tác (Server-Authoritative).
//           Hỗ trợ đầy đủ chế độ Offline Test thông qua biến dự phòng khi chưa spawn mạng.
// Môi trường thực thi: Cả hai (Server tính toán & ghi nhận, Client đọc qua NetworkVariable).

using System;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Items;
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

        // Biến nội bộ dự phòng khi chạy Offline (chưa spawn mạng)
        private float _rawBonusMaxHealth = 0f;
        private float _rawArmor = 0f;
        private float _rawMoveSpeedMultiplier = MinSpeedMultiplier;
        private int _rawRedCrystals = 10;
        private int _rawGreenCrystals = 10;

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
        /// Pha Lê Đỏ (Dùng cho Tế Đàn Thân Thể / StatsAltar).
        /// </summary>
        public NetworkVariable<int> RedCrystals { get; } = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Pha Lê Xanh (Dùng cho Bàn Cường Hóa Vũ Khí / WeaponWorkbench).
        /// </summary>
        public NetworkVariable<int> GreenCrystals { get; } = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Thuộc tính truy cập nhanh chỉ số hiện tại (hoạt động đồng thời Online và Offline).
        /// </summary>
        public float CurrentBonusMaxHealth => IsSpawned ? BonusMaxHealth.Value : _rawBonusMaxHealth;
        public float CurrentArmor => IsSpawned ? Armor.Value : _rawArmor;
        public float CurrentMoveSpeedMultiplier => IsSpawned ? MoveSpeedMultiplier.Value : _rawMoveSpeedMultiplier;
        public int CurrentRedCrystals => IsSpawned ? RedCrystals.Value : _rawRedCrystals;
        public int CurrentGreenCrystals => IsSpawned ? GreenCrystals.Value : _rawGreenCrystals;

        public event Action<int, int> OnCrystalsChanged; // redCrystals, greenCrystals

        /// <summary>
        /// Trả về hệ số tốc độ cơ bản/hiện tại cho PlayerMovement khi chưa spawn hoặc offline.
        /// </summary>
        public float GetBaseSpeedMultiplier() => CurrentMoveSpeedMultiplier;

        // ================= LỊCH SỬ TẾ ĐÀN (SERVER-SIDE TRACKING) =================
        // Lưu trữ các ID tế đàn mà riêng người chơi này đã kích hoạt (chỉ chạy trên Server hoặc Offline)
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
            _rawBonusMaxHealth = BonusMaxHealth.Value;
            _rawArmor = Armor.Value;
            _rawMoveSpeedMultiplier = MoveSpeedMultiplier.Value;
            _rawRedCrystals = RedCrystals.Value;
            _rawGreenCrystals = GreenCrystals.Value;

            BonusMaxHealth.OnValueChanged += HandleBonusHealthChanged;
            Armor.OnValueChanged += HandleArmorChanged;
            MoveSpeedMultiplier.OnValueChanged += HandleSpeedChanged;
            RedCrystals.OnValueChanged += HandleRedCrystalsChanged;
            GreenCrystals.OnValueChanged += HandleGreenCrystalsChanged;

            // Kích hoạt event ban đầu cho UI
            NotifyStatsChanged();
            NotifyCrystalsChanged();
        }

        public override void OnNetworkDespawn()
        {
            BonusMaxHealth.OnValueChanged -= HandleBonusHealthChanged;
            Armor.OnValueChanged -= HandleArmorChanged;
            MoveSpeedMultiplier.OnValueChanged -= HandleSpeedChanged;
            RedCrystals.OnValueChanged -= HandleRedCrystalsChanged;
            GreenCrystals.OnValueChanged -= HandleGreenCrystalsChanged;

            if (IsServer)
            {
                _usedShrineIds.Clear();
            }
        }

        private void HandleBonusHealthChanged(float prev, float curr)
        {
            _rawBonusMaxHealth = curr;
            NotifyStatsChanged();
        }

        private void HandleArmorChanged(float prev, float curr)
        {
            _rawArmor = curr;
            NotifyStatsChanged();
        }

        private void HandleSpeedChanged(float prev, float curr)
        {
            _rawMoveSpeedMultiplier = curr;
            NotifyStatsChanged();
        }

        private void HandleRedCrystalsChanged(int prev, int curr)
        {
            _rawRedCrystals = curr;
            NotifyCrystalsChanged();
        }

        private void HandleGreenCrystalsChanged(int prev, int curr)
        {
            _rawGreenCrystals = curr;
            NotifyCrystalsChanged();
        }

        private void NotifyStatsChanged()
        {
            OnStatsChanged?.Invoke(CurrentBonusMaxHealth, CurrentArmor, CurrentMoveSpeedMultiplier);
        }

        public void NotifyCrystalsChanged()
        {
            OnCrystalsChanged?.Invoke(CurrentRedCrystals, CurrentGreenCrystals);
        }

        // ================= QUẢN LÝ TIÊU HAO PHA LÊ (CRYSTAL SYSTEM) =================
        public int GetCrystals(ShrineType shrineType)
        {
            return shrineType == ShrineType.BodyShrine ? CurrentRedCrystals : CurrentGreenCrystals;
        }

        public bool CanAfford(ShrineType shrineType, int cost)
        {
            return GetCrystals(shrineType) >= cost;
        }

        public bool TrySpendCrystals(ShrineType shrineType, int cost)
        {
            if (!GameModeHelper.IsServerOrOffline) return false;
            if (!CanAfford(shrineType, cost)) return false;

            if (shrineType == ShrineType.BodyShrine)
            {
                int next = CurrentRedCrystals - cost;
                _rawRedCrystals = next;
                if (IsSpawned) RedCrystals.Value = next;
            }
            else
            {
                int next = CurrentGreenCrystals - cost;
                _rawGreenCrystals = next;
                if (IsSpawned) GreenCrystals.Value = next;
            }

            NotifyCrystalsChanged();
            return true;
        }

        public void AddCrystals(ShrineType shrineType, int amount)
        {
            if (!GameModeHelper.IsServerOrOffline) return;
            if (shrineType == ShrineType.BodyShrine)
            {
                int next = CurrentRedCrystals + amount;
                _rawRedCrystals = next;
                if (IsSpawned) RedCrystals.Value = next;
            }
            else
            {
                int next = CurrentGreenCrystals + amount;
                _rawGreenCrystals = next;
                if (IsSpawned) GreenCrystals.Value = next;
            }
            NotifyCrystalsChanged();
        }

        // ================= QUẢN LÝ LỊCH SỬ TẾ ĐÀN (SHRINE TRACKING) =================

        /// <summary>
        /// Đã bỏ cơ chế khóa 1 lần theo yêu cầu: Người chơi có thể dùng lặp lại nhiều lần miễn còn đủ Pha Lê.
        /// </summary>
        public bool HasUsedShrine(int shrineId)
        {
            return false;
        }

        /// <summary>
        /// Ghi nhận tế đàn đã được tiêu thụ bởi người chơi này.
        /// </summary>
        public void RecordShrineUsed(int shrineId)
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                return;
            }

            _usedShrineIds.Add(shrineId);
            Debug.Log($"[PlayerStats] Người chơi {NetworkObjectId} đã dùng Tế đàn #{shrineId}.");
        }

        // ================= NÂNG CẤP CHỈ SỐ CƠ THỂ (BODY BUFF) =================

        /// <summary>
        /// Kiểm tra xem người chơi có còn khả năng nhận thêm buff chỉ số nào không (chưa chạm trần).
        /// </summary>
        public bool CanReceiveBodyBuff(float addHealth, float addArmor, float addSpeed)
        {
            bool canAddHp = addHealth > 0f && CurrentBonusMaxHealth < MaxBonusHealthCap;
            bool canAddArmor = addArmor > 0f && CurrentArmor < MaxArmorCap;
            bool canAddSpeed = addSpeed > 0f && CurrentMoveSpeedMultiplier < MaxSpeedMultiplierCap;

            return canAddHp || canAddArmor || canAddSpeed;
        }

        /// <summary>
        /// Thực thi cộng chỉ số cơ thể lên người chơi (Server-Authoritative hoặc Offline).
        /// Tự động clamp trong giới hạn trần cứng (Max Caps).
        /// </summary>
        /// <returns>True nếu có ít nhất 1 chỉ số được tăng, False nếu đã đạt trần tối đa.</returns>
        public bool TryApplyBodyBuff(float addHealth, float addArmor, float addSpeed)
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                Debug.LogWarning("[PlayerStats] Chỉ Server hoặc Offline mới có quyền tăng chỉ số PlayerStats!");
                return false;
            }

            if (!CanReceiveBodyBuff(addHealth, addArmor, addSpeed))
            {
                return false;
            }

            bool statModified = false;

            // 1. Tăng máu tối đa (kèm hồi lượng máu tương ứng vào Health)
            if (addHealth > 0f && CurrentBonusMaxHealth < MaxBonusHealthCap)
            {
                float newBonus = Mathf.Min(MaxBonusHealthCap, CurrentBonusMaxHealth + addHealth);
                float actualAdd = newBonus - CurrentBonusMaxHealth;

                if (IsSpawned)
                {
                    BonusMaxHealth.Value = newBonus;
                }
                _rawBonusMaxHealth = newBonus;

                if (_healthComponent != null)
                {
                    _healthComponent.IncreaseMaxHealthServer(actualAdd);
                }
                statModified = true;
            }

            // 2. Tăng Giáp
            if (addArmor > 0f && CurrentArmor < MaxArmorCap)
            {
                float newArmor = Mathf.Min(MaxArmorCap, CurrentArmor + addArmor);
                if (IsSpawned)
                {
                    Armor.Value = newArmor;
                }
                _rawArmor = newArmor;
                statModified = true;
            }

            // 3. Tăng Tốc độ chạy
            if (addSpeed > 0f && CurrentMoveSpeedMultiplier < MaxSpeedMultiplierCap)
            {
                float newSpeed = Mathf.Min(MaxSpeedMultiplierCap, CurrentMoveSpeedMultiplier + addSpeed);
                if (IsSpawned)
                {
                    MoveSpeedMultiplier.Value = newSpeed;
                }
                _rawMoveSpeedMultiplier = newSpeed;
                statModified = true;
            }

            if (statModified)
            {
                NotifyStatsChanged();
                Debug.Log($"[PlayerStats] Đã nâng cấp chỉ số cho Client {OwnerClientId}: +HP={CurrentBonusMaxHealth}/{MaxBonusHealthCap}, Armor={CurrentArmor}/{MaxArmorCap}, Speed={CurrentMoveSpeedMultiplier:F2}x");
            }

            return statModified;
        }
    }
}
