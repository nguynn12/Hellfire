// Script: IDamageable.cs
// Mục đích: Interface chuẩn cho mọi thực thể có thể nhận sát thương (Người chơi, Quái vật, Boss) theo Mục 3 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai (Server tính toán, Client quan sát).

namespace Hellfire.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        bool IsDowned { get; }
        float CurrentHealthValue { get; }
        float MaxHealthValue { get; }
        bool IsPlayerTarget { get; }

        /// <summary>
        /// Gây sát thương lên thực thể (Server-Authoritative).
        /// </summary>
        /// <param name="payload">Gói dữ liệu sát thương chứa RawDamage, ArmorPenetration, Hitbox, và AttackerId.</param>
        void TakeDamage(DamagePayload payload);
    }
}
