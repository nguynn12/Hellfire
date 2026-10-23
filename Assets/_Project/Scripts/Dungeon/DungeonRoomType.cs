// Script: DungeonRoomType.cs
// Mục đích: Định nghĩa các loại phòng trong hầm ngục (Mục 4.1 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server và Client).

namespace Hellfire.Dungeon
{
    public enum DungeonRoomType
    {
        Spawn = 0,      // Phòng xuất phát, an toàn, không quái, không cổng, không cột
        Combat = 1,     // Phòng chiến đấu, có cổng sắt khóa, quái spawn khi vào, 0-4 cột
        Upgrade = 2,    // Phòng tế đàn nâng cấp, an toàn, không quái, không cổng, không cột
        Treasure = 3,   // Phòng kho báu, an toàn, không quái, không cổng, không cột
        Shop = 4,       // Phòng cửa hàng, an toàn, không quái, không cổng, không cột
        Boss = 5        // Phòng trùm Chúa tể Địa ngục, có cổng sắt khóa, boss spawn khi vào
    }
}
