// Script: DungeonRoomType.cs
// Mục đích: Định nghĩa các loại phòng trong hầm ngục (Mục 4.1 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server và Client).

namespace Hellfire.Dungeon
{
    public enum DungeonRoomType
    {
        Spawn,      // Phòng khởi đầu của người chơi
        Normal,     // Phòng thông thường (có thể có quái vật thường)
        Boss        // Phòng Chúa tể Địa ngục (phòng xa nhất tính theo BFS)
    }
}
