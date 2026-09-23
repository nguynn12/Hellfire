// Script: HitboxType.cs
// Mục đích: Phân loại các vùng va chạm Hitbox để tính hệ số sát thương theo Mục 3.2 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai (Server & Client).

namespace Hellfire.Combat
{
    public enum HitboxType
    {
        Head,   // Hệ số nhân x2.0
        Torso,  // Hệ số nhân x1.0
        Limb    // Hệ số nhân x0.75
    }
}
