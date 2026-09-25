// Script: EnemyType.cs
// Mục đích: Định nghĩa các loại quái vật và Boss theo Mục 5 & 6.2 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai.

namespace Hellfire.AI
{
    public enum EnemyType
    {
        Imp,                // Quỷ nhỏ: 30 HP, 10 DMG, 5 m/s
        HellspawnArcher,    // Cung thủ: 25 HP, 12 DMG (hitscan), 3.5 m/s
        Brute,              // Quỷ khổng lồ: 120 HP, 25 DMG (stagger), 2.5 m/s
        BossHellfireLord    // Chúa tể Địa ngục: 800 HP, 2 Pha
    }

    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Dead
    }
}
