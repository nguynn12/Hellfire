// Script: PowerUpType.cs
// Mục đích: Định nghĩa các loại bùa lợi (Power-up) theo Mục 6.1 & 6.2 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai (Server và Client).

namespace Hellfire.Items
{
    public enum PowerUpType
    {
        MaxHealthUp,        // Trái tim máu lớn: +25 HP tối đa, hồi đầy ngay khi nhặt (vĩnh viễn trong run)
        SwiftBoots,         // Giày tốc độ: +20% tốc độ di chuyển đi/chạy/lùi (vĩnh viễn trong run)
        BerserkerCharm,     // Bùa sát thương: +30% sát thương gây ra mọi vũ khí (vĩnh viễn trong run)
        GuardianShield      // Khiên tạm thời: Miễn sát thương hoàn toàn trong 8 giây (dùng ngay)
    }
}
