// Script: PlayerStatusHUD.cs
// Trạng thái: ĐÃ VÔ HIỆU HÓA HOÀN TOÀN (DEPRECATED).
// Lý do: Đã đồng nhất và chuyển đổi toàn bộ hệ thống hiển thị máu & giáp sang uGUI Canvas (GameplayHUD.cs).
// Loại bỏ hoàn toàn OnGUI, triệt tiêu 100% GC Allocations và hiện tượng vẽ đè 2 thanh máu.

using System;
using UnityEngine;

namespace Hellfire.UI
{
    [Obsolete("Hệ thống OnGUI Player Status đã bị vô hiệu hóa. Sử dụng uGUI GameplayHUD để hiển thị máu và giáp.")]
    public class PlayerStatusHUD : MonoBehaviour
    {
        // Toàn bộ logic OnGUI và Update đã bị vô hiệu hóa để bảo toàn hiệu năng
    }
}
