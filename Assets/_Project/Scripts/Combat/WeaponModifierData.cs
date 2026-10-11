// Script: WeaponModifierData.cs
// Mục đích: ScriptableObject định nghĩa dữ liệu Modifier/Ngọc gắn vào vũ khí (OCP + Designer Friendly).
// Môi trường thực thi: Cả hai (Dữ liệu tĩnh đọc bởi Server và Client).

using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// ScriptableObject đại diện cho một modifier/ngọc nâng cấp vũ khí.
    /// Cho phép Game Designer tạo file .asset trong Unity Editor mà không cần viết code.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponModifier", menuName = "Hellfire/Combat/Weapon Modifier", order = 2)]
    public class WeaponModifierData : ScriptableObject, IWeaponModifier
    {
        [Header("Modifier Identity")]
        [SerializeField] private string _modifierId = "mod_power";
        [SerializeField] private string _displayName = "Ngọc Cường Hóa";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "+15% Sát thương cho tất cả vũ khí";
        [SerializeField] private Sprite _icon;

        [Header("Stat Adjustments (Tỉ lệ phần trăm hoặc giá trị cộng thẳng)")]
        [Tooltip("Hệ số sát thương cộng thêm (+0.15 = +15%)")]
        [SerializeField] private float _damageBonusPercent = 0.15f;

        [Tooltip("Hệ số tốc độ bắn cộng thêm (+0.10 = +10%)")]
        [SerializeField] private float _attackSpeedBonusPercent = 0f;

        [Tooltip("Chỉ số xuyên giáp cộng thêm")]
        [SerializeField] private float _armorPenetrationBonus = 0f;

        [Tooltip("Tỷ lệ chí mạng cộng thêm (+0.05 = +5%)")]
        [SerializeField] private float _critChanceBonus = 0f;

        [Tooltip("Hệ số sát thương chí mạng cộng thêm (+0.25 = +25%)")]
        [SerializeField] private float _critMultiplierBonus = 0f;

        public string ModifierId => _modifierId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;

        public void ModifyStats(
            ref float damage,
            ref float attackRateAPS,
            ref float armorPenetration,
            ref float critChance,
            ref float critDamageMultiplier
        )
        {
            damage *= (1.0f + _damageBonusPercent);
            attackRateAPS *= (1.0f + _attackSpeedBonusPercent);
            armorPenetration += _armorPenetrationBonus;
            critChance = Mathf.Clamp01(critChance + _critChanceBonus);
            critDamageMultiplier += _critMultiplierBonus;
        }

        public virtual void OnHit(DamagePayload payload, IDamageable target)
        {
            // Các hiệu ứng On-hit có thể mở rộng tại đây
        }
    }
}
