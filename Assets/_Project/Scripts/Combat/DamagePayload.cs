// Script: DamagePayload.cs
// Mục đích: Struct bất biến (readonly struct) chứa thông tin gói dữ liệu sát thương truyền giữa kẻ tấn công và mục tiêu (Zero-GC).
// Môi trường thực thi: Cả hai (Server tính toán, Client nhận thông tin hiển thị).

using System;

namespace Hellfire.Combat
{
    /// <summary>
    /// Gói dữ liệu sát thương bất biến phục vụ truyền thông tin va chạm và tính toán giảm trừ giáp.
    /// Thiết kế dạng readonly struct để tối ưu hóa bộ nhớ, tránh cấp phát trên Heap (Zero-GC).
    /// </summary>
    [Serializable]
    public readonly struct DamagePayload
    {
        /// <summary>
        /// Sát thương thô ban đầu (chưa trừ giáp và chưa nhân hệ số hitbox).
        /// </summary>
        public readonly float RawDamage;

        /// <summary>
        /// Chỉ số xuyên giáp của đòn đánh (trừ trực tiếp vào giáp của mục tiêu).
        /// </summary>
        public readonly float ArmorPenetration;

        /// <summary>
        /// Vị trí va chạm trúng (Đầu, Thân, Tay chân).
        /// </summary>
        public readonly HitboxType Hitbox;

        /// <summary>
        /// Client ID của người chơi hoặc NetworkObjectId của đối tượng tấn công.
        /// </summary>
        public readonly ulong AttackerClientId;

        public DamagePayload(float rawDamage, float armorPenetration, HitboxType hitbox, ulong attackerClientId)
        {
            RawDamage = rawDamage;
            ArmorPenetration = armorPenetration;
            Hitbox = hitbox;
            AttackerClientId = attackerClientId;
        }

        /// <summary>
        /// Tạo một bản sao Payload với loại Hitbox được cập nhật mới.
        /// </summary>
        public DamagePayload WithHitbox(HitboxType newHitbox)
        {
            return new DamagePayload(RawDamage, ArmorPenetration, newHitbox, AttackerClientId);
        }
    }
}
