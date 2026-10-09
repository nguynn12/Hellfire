// Script: RuntimeWeapon.cs
// Mục đích: Quản lý trạng thái thực thi của 1 khẩu vũ khí tại runtime (Cấp độ nâng cấp, danh sách Modifiers/Ngọc, tính toán chỉ số cuối).
// Môi trường thực thi: Cả hai (Server tính toán chỉ số và sát thương, Client đồng bộ hiển thị).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Đóng gói trạng thái runtime của một vũ khí cụ thể của người chơi.
    /// Cho phép nâng cấp cấp độ từ Tế đàn (Shrine) và gắn các IWeaponModifier mở rộng mà không sửa đổi file ScriptableObject gốc.
    /// </summary>
    [Serializable]
    public class RuntimeWeapon
    {
        [SerializeField] private WeaponData _baseData;
        [SerializeField] private int _upgradeLevel = 0;

        private readonly List<IWeaponModifier> _modifiers = new List<IWeaponModifier>();

        public WeaponData BaseData => _baseData;
        public int UpgradeLevel => _upgradeLevel;
        public IReadOnlyList<IWeaponModifier> Modifiers => _modifiers;

        public RuntimeWeapon(WeaponData baseData)
        {
            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _upgradeLevel = 0;
        }

        /// <summary>
        /// Nâng cấp cấp độ vũ khí (tăng % sát thương và chỉ số tấn công cơ bản).
        /// </summary>
        /// <param name="levels">Số cấp tăng thêm</param>
        public void ApplyLevelUpgrade(int levels = 1)
        {
            _upgradeLevel += Mathf.Max(1, levels);
            Debug.Log($"[RuntimeWeapon] Vũ khí {_baseData.WeaponName} đã được nâng cấp lên Level {_upgradeLevel}!");
        }

        /// <summary>
        /// Gắn thêm một modifier (ngọc / phụ kiện) vào vũ khí.
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
        /// Đánh giá và tính toán các chỉ số tấn công cuối cùng (Final Stats) sau khi kết hợp BaseData, Cấp độ nâng cấp và các Modifiers.
        /// Mỗi cấp độ nâng cấp tăng thêm +20% sát thương cơ bản và +5% tốc đánh.
        /// </summary>
        public void EvaluateFinalStats(
            out float finalDamage,
            out float finalAPS,
            out float finalArmorPen,
            out float finalCritChance,
            out float finalCritMultiplier
        )
        {
            // 1. Chỉ số cơ sở từ WeaponData
            float levelMultiplier = 1.0f + (_upgradeLevel * 0.20f); // Mỗi level tăng +20% base damage
            finalDamage = _baseData.BaseDamage * levelMultiplier;
            finalAPS = _baseData.AttackRateAPS * (1.0f + _upgradeLevel * 0.05f); // Mỗi level tăng +5% tốc đánh
            finalArmorPen = _baseData.ArmorPenetration;
            finalCritChance = _baseData.CritChance;
            finalCritMultiplier = _baseData.CritDamageMultiplier;

            // 2. Chạy qua các Modifier để can thiệp chỉ số theo OCP
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i] != null)
                {
                    _modifiers[i].ModifyStats(
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
        /// Tung xúc xắc tính toán sát thương đòn đánh (xét tỷ lệ chí mạng Crit).
        /// </summary>
        /// <param name="isCrit">Trả về True nếu đòn đánh kích hoạt chí mạng</param>
        /// <returns>Sát thương sau khi đã nhân tỷ lệ chí mạng nếu có</returns>
        public float CalculateAttackDamage(out bool isCrit)
        {
            EvaluateFinalStats(
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
        /// Kích hoạt callback OnHit cho toàn bộ các modifier đã gắn trên vũ khí.
        /// </summary>
        public void NotifyHit(DamagePayload payload, IDamageable target)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                _modifiers[i]?.OnHit(payload, target);
            }
        }
    }
}
