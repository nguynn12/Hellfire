// Script: UpgradeChoice.cs
// Mục đích: Định nghĩa cấu trúc dữ liệu cho hệ thống 3 lựa chọn nâng cấp tại Tế đàn & Bàn Modifier.
// Môi trường thực thi: Cả hai (Server tính toán và áp dụng, Client hiển thị UI 3 thẻ chọn).

using System;

namespace Hellfire.Items
{
    public enum UpgradeCategory
    {
        BodyStat,       // Nâng cấp chỉ số thân thể (Máu, Giáp, Tốc độ chạy)
        GlobalWeapon,   // Nâng cấp chỉ số hỏa lực toàn cục (Sát thương, Tốc bắn, Xuyên giáp, Chí mạng)
        SpecialModifier // Ngọc / Modifier vũ khí toàn cục
    }

    [Serializable]
    public struct UpgradeOptionData
    {
        public string Id;
        public string Title;          // Ví dụ: "Cường Hóa Sát Thương"
        public string Description;    // Ví dụ: "+20% Sát thương cho tất cả vũ khí"
        public string BadgeText;      // "[VŨ KHÍ]" hoặc "[THÂN THỂ]"
        public UpgradeCategory Category;
        public float PrimaryValue;    // Giá trị cộng thêm chính
        public float SecondaryValue;  // Giá trị cộng thêm phụ (nếu có)
    }
}
