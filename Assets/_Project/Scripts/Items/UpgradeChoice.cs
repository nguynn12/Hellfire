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
        public string Title;          // Ví dụ: "Sinh Lực Bất Diệt"
        public string Description;    // Ví dụ: "+30 Máu Tối Đa"
        public string BadgeText;      // "[VŨ KHÍ]" hoặc "[THÂN THỂ]"
        public UpgradeCategory Category;
        public float PrimaryValue;    // Giá trị cộng thêm chính (đã nhân hệ số sao)
        public float SecondaryValue;  // Giá trị cộng thêm phụ (nếu có)
        public int Stars;             // Số sao phẩm chất: 1, 2 hoặc 3 sao
        public float StarMultiplier;  // Hệ số thưởng thêm: 1.0f (100%), 1.5f (150%), 2.0f (200%)

        /// <summary>
        /// Trả về chuỗi ký tự hiển thị sao (ví dụ: "★☆☆", "★★☆", "★★★")
        /// </summary>
        public string GetStarIcons()
        {
            switch (Stars)
            {
                case 3: return "★★★";
                case 2: return "★★☆";
                default: return "★☆☆";
            }
        }

        /// <summary>
        /// Tên phẩm chất theo số sao
        /// </summary>
        public string GetTierName()
        {
            switch (Stars)
            {
                case 3: return "HUYỀN THOẠI (200%)";
                case 2: return "HIẾM (150%)";
                default: return "CƠ BẢN (100%)";
            }
        }
    }
}
