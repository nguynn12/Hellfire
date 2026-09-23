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
        /// <param name="damage">Lượng sát thương sau khi đã nhân hệ số hitbox.</param>
        /// <param name="hitboxType">Vùng va chạm bị bắn trúng.</param>
        /// <param name="attackerClientId">ID của người chơi đã bắn.</param>
        void TakeDamage(float damage, HitboxType hitboxType, ulong attackerClientId);
    }
}
