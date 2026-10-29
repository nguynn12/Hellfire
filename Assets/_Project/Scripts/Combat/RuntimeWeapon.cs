// Script: RuntimeWeapon.cs
// Mục đích: Quản lý trạng thái thực thi của 1 khẩu vũ khí tại runtime (Cấp độ nâng cấp, danh sách Modifiers/Ngọc, tính toán chỉ số cuối).
//           Hỗ trợ kết hợp chỉ số Nâng Cấp Toàn Cục (Global Weapon Upgrades) áp dụng cho mọi vũ khí khi đổi súng.
// Môi trường thực thi: Cả hai (Server tính toán chỉ số và sát thương, Client đồng bộ hiển thị).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Đóng gói trạng thái runtime của một vũ khí cụ thể của người chơi.
    /// Cho phép nâng cấp cấp độ và kết hợp các chỉ số toàn cục (Global Upgrades / Modifiers)
    /// để khi người chơi đổi vũ khí vẫn giữ nguyên toàn bộ các nâng cấp.
    /// </summary>
    [Serializable]
    public class RuntimeWeapon
    {
        [SerializeField] private WeaponData _baseData;
        [SerializeField] private int _upgradeLevel = 0;
        [SerializeField] private int _currentAmmo = 0;
        [SerializeField] private int _reserveAmmo = 0;

        private readonly List<IWeaponModifier> _modifiers = new List<IWeaponModifier>();

        public WeaponData BaseData => _baseData;
        public int UpgradeLevel => _upgradeLevel;
        public int CurrentAmmo { get => _currentAmmo; set => _currentAmmo = Mathf.Max(0, value); }
        public int ReserveAmmo { get => _reserveAmmo; set => _reserveAmmo = Mathf.Max(0, value); }
        public IReadOnlyList<IWeaponModifier> Modifiers => _modifiers;

        public RuntimeWeapon(WeaponData baseData)
        {
            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _upgradeLevel = 0;
            _currentAmmo = _baseData.MagSize;
            _reserveAmmo = _baseData.MaxReserveAmmo;
        }

        /// <summary>
        /// Tiêu hao đạn trong băng khi bắn.
        /// </summary>
        public bool ConsumeAmmo(int amount = 1)
        {
            if (_currentAmmo >= amount)
            {
                _currentAmmo -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Nạp đạn từ kho dự trữ vào băng đạn. Trả về số lượng đạn nạp thực tế.
        /// </summary>
        public int Reload()
        {
            int needed = _baseData.MagSize - _currentAmmo;
            if (needed <= 0 || _reserveAmmo <= 0) return 0;
            int toAdd = Mathf.Min(needed, _reserveAmmo);
            _reserveAmmo -= toAdd;
            _currentAmmo += toAdd;
            return toAdd;
        }

        /// <summary>
        /// Thêm đạn dự trữ (nhặt hòm đạn / power-up).
        /// </summary>
        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0) return;
            _reserveAmmo = Mathf.Min(_reserveAmmo + amount, _baseData.MaxReserveAmmo);
        }

        /// <summary>
        /// Nâng cấp cấp độ vũ khí (tăng % sát thương và chỉ số tấn công cơ bản).
        /// </summary>
        public void ApplyLevelUpgrade(int levels = 1)
        {
            _upgradeLevel += Mathf.Max(1, levels);
            Debug.Log($"[RuntimeWeapon] Vũ khí {_baseData.WeaponName} đã được nâng cấp lên Level {_upgradeLevel}!");
        }

        /// <summary>
        /// Gắn thêm một modifier (ngọc / phụ kiện) vào vũ khí cục bộ.
        /// </summary>
        public void AddModifier(IWeaponModifier modifier)
        {
            if (modifier != null && !_modifiers.Contains(modifier))
            {
                _modifiers.Add(modifier);
            }
        }

        /// <summary>
        /// Gỡ bỏ modifier theo mã ID.
        /// </summary>
        public void RemoveModifier(string modifierId)
        {
            _modifiers.RemoveAll(m => m.ModifierId == modifierId);
        }

        /// <summary>
        /// Đánh giá chỉ số tấn công cuối cùng kết hợp BaseData, Cấp độ nâng cấp vũ khí,
        /// và các chỉ số Nâng Cấp Toàn Cục (Global Weapon Stats) được truyền từ người chơi.
        /// </summary>
        public void EvaluateFinalStats(
            float globalDamageMultiplier,
            float globalApsMultiplier,
            float globalArmorPen,
            float globalCritChanceBonus,
            float globalCritMultiplierBonus,
            IReadOnlyList<IWeaponModifier> globalModifiers,
            out float finalDamage,
            out float finalAPS,
            out float finalArmorPen,
            out float finalCritChance,
            out float finalCritMultiplier
        )
        {
            // 1. Chỉ số cơ sở từ WeaponData kết hợp cấp độ riêng và hệ số toàn cục
            float levelMultiplier = 1.0f + (_upgradeLevel * 0.20f);
            finalDamage = _baseData.BaseDamage * levelMultiplier * Mathf.Max(0.1f, globalDamageMultiplier);
            finalAPS = _baseData.AttackRateAPS * (1.0f + _upgradeLevel * 0.05f) * Mathf.Max(0.1f, globalApsMultiplier);
            finalArmorPen = _baseData.ArmorPenetration + globalArmorPen;
            finalCritChance = _baseData.CritChance + globalCritChanceBonus;
            finalCritMultiplier = _baseData.CritDamageMultiplier + globalCritMultiplierBonus;

            // 2. Chạy qua các Modifier cục bộ của súng
            for (int i = 0; i < _modifiers.Count; i++)
            {
                _modifiers[i]?.ModifyStats(
                    ref finalDamage,
                    ref finalAPS,
                    ref finalArmorPen,
                    ref finalCritChance,
                    ref finalCritMultiplier
                );
            }

            // 3. Chạy qua các Modifier toàn cục (áp dụng cho toàn bộ vũ khí)
            if (globalModifiers != null)
            {
                for (int i = 0; i < globalModifiers.Count; i++)
                {
                    globalModifiers[i]?.ModifyStats(
                        ref finalDamage,
                        ref finalAPS,
                        ref finalArmorPen,
                        ref finalCritChance,
                        ref finalCritMultiplier
                    );
                }
            }

            // Đảm bảo không có chỉ số nào bị âm hoặc bất thường
            finalDamage = Mathf.Max(1f, finalDamage);
            finalAPS = Mathf.Max(0.1f, finalAPS);
            finalArmorPen = Mathf.Max(0f, finalArmorPen);
            finalCritChance = Mathf.Clamp01(finalCritChance);
            finalCritMultiplier = Mathf.Max(1.0f, finalCritMultiplier);
        }

        /// <summary>
        /// Quá tải tương thích ngược: Đánh giá chỉ số khi không có tham số toàn cục.
        /// </summary>
        public void EvaluateFinalStats(
            out float finalDamage,
            out float finalAPS,
            out float finalArmorPen,
            out float finalCritChance,
            out float finalCritMultiplier
        )
        {
            EvaluateFinalStats(
                1.0f, 1.0f, 0f, 0f, 0f, null,
                out finalDamage, out finalAPS, out finalArmorPen, out finalCritChance, out finalCritMultiplier
            );
        }

        /// <summary>
        /// Tung xúc xắc tính toán sát thương đòn đánh có áp dụng chỉ số toàn cục (xét Crit).
        /// </summary>
        public float CalculateAttackDamage(
            float globalDamageMultiplier,
            float globalApsMultiplier,
            float globalArmorPen,
            float globalCritChanceBonus,
            float globalCritMultiplierBonus,
            IReadOnlyList<IWeaponModifier> globalModifiers,
            out bool isCrit
        )
        {
            EvaluateFinalStats(
                globalDamageMultiplier,
                globalApsMultiplier,
                globalArmorPen,
                globalCritChanceBonus,
                globalCritMultiplierBonus,
                globalModifiers,
                out float finalDamage,
                out _,
                out _,
                out float critChance,
                out float critMultiplier
            );

            isCrit = UnityEngine.Random.value <= critChance;
            return isCrit ? (finalDamage * critMultiplier) : finalDamage;
        }

        /// <summary>
        /// Quá tải tương thích ngược tính sát thương cơ bản không có hệ số toàn cục.
        /// </summary>
        public float CalculateAttackDamage(out bool isCrit)
        {
            return CalculateAttackDamage(1.0f, 1.0f, 0f, 0f, 0f, null, out isCrit);
        }

        /// <summary>
        /// Kích hoạt callback OnHit cho toàn bộ các modifier đã gắn trên vũ khí và modifier toàn cục (Mục 10).
        /// </summary>
        public void NotifyHit(DamagePayload payload, IDamageable target, IReadOnlyList<IWeaponModifier> globalModifiers = null)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                _modifiers[i]?.OnHit(payload, target);
            }

            if (globalModifiers != null)
            {
                for (int i = 0; i < globalModifiers.Count; i++)
                {
                    globalModifiers[i]?.OnHit(payload, target);
                }
            }
        }
    }
}
