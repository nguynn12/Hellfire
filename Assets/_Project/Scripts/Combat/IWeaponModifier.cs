// Script: IWeaponModifier.cs
// Mục đích: Interface mở rộng (Modifier Slot) cho vũ khí, cho phép gắn ngọc, phụ kiện, hiệu ứng đòn đánh mà không cần sửa code lõi.
// Môi trường thực thi: Cả hai (Server tính toán chỉ số và hiệu ứng on-hit).

namespace Hellfire.Combat
{
    /// <summary>
    /// Giao diện đại diện cho một modifier/phụ kiện/ngọc được gắn vào vũ khí.
    /// Tuân thủ nguyên lý Open/Closed Principle (OCP).
    /// </summary>
    public interface IWeaponModifier
    {
        /// <summary>
        /// Mã định danh duy nhất của modifier (ví dụ: "fire_gem_tier1", "armor_pierce_mod").
        /// </summary>
        string ModifierId { get; }

        /// <summary>
        /// Tên hiển thị của modifier cho giao diện người dùng.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Can thiệp và hiệu chỉnh các chỉ số tấn công cơ bản của vũ khí khi tính toán RuntimeWeapon.
        /// </summary>
        /// <param name="damage">Sát thương gốc (cộng thêm hoặc nhân thêm)</param>
        /// <param name="attackRateAPS">Tốc độ đánh (Attacks Per Second)</param>
        /// <param name="armorPenetration">Chỉ số xuyên giáp</param>
        /// <param name="critChance">Tỷ lệ chí mạng (0.0 đến 1.0)</param>
        /// <param name="critDamageMultiplier">Hệ số sát thương chí mạng (ví dụ 1.5, 2.0)</param>
        void ModifyStats(
            ref float damage,
            ref float attackRateAPS,
            ref float armorPenetration,
            ref float critChance,
            ref float critDamageMultiplier
        );

        /// <summary>
        /// Callback được gọi khi đòn đánh trúng mục tiêu (áp dụng hiệu ứng đốt cháy, làm chậm, hút máu, v.v.).
        /// </summary>
        /// <param name="payload">Gói dữ liệu sát thương vừa gây ra</param>
        /// <param name="target">Mục tiêu nhận sát thương</param>
        void OnHit(DamagePayload payload, IDamageable target);
    }
}
