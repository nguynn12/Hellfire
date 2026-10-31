// Script: WeaponCrosshairHUD.cs
// Trạng thái: ĐÃ VÔ HIỆU HÓA HOÀN TOÀN (DEPRECATED).
// Lý do: Đã đồng nhất và chuyển đổi toàn bộ hệ thống tâm ngắm sang uGUI Canvas (GameplayHUD.cs).
// Loại bỏ hoàn toàn OnGUI, triệt tiêu 100% GC Allocations và hiện tượng vẽ đè 2 tâm ngắm.

using System;
using UnityEngine;

namespace Hellfire.UI
{
    [Obsolete("Hệ thống OnGUI Crosshair đã bị vô hiệu hóa. Sử dụng uGUI GameplayHUD để hiển thị tâm ngắm.")]
    public class WeaponCrosshairHUD : MonoBehaviour
    {
        // Toàn bộ logic OnGUI và Update đã bị vô hiệu hóa để bảo toàn hiệu năng
    }
}
