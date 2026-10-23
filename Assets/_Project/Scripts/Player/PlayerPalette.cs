// Script: PlayerPalette.cs
// Mục đích: Bảng màu Okabe-Ito thân thiện với người mù màu cho danh tính người chơi (Mục 16.1 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Client và Server dùng chung, lớp tĩnh không phụ thuộc MonoBehaviour).

using UnityEngine;

namespace Hellfire.Player
{
    /// <summary>
    /// Lớp tĩnh duy nhất quản lý bảng 4 màu định danh người chơi theo bảng Okabe-Ito (Mục 16.1).
    /// BẮT BUỘC: Không dùng màu đỏ/vàng cho người chơi. Mọi hiển thị phải đi kèm số 1-4.
    /// </summary>
    public static class PlayerPalette
    {
        public const int MaxPlayers = 4;

        // Bảng màu Okabe-Ito chính thức theo mục 16.1:
        // Index 0: Số 1 - Xanh da trời (#56B4E9)
        // Index 1: Số 2 - Cam (#E69F00)
        // Index 2: Số 3 - Xanh lục (#009E73)
        // Index 3: Số 4 - Tím hồng (#CC79A7)
        public static readonly Color SkyBlue = new Color(0.337f, 0.706f, 0.914f, 1f); // #56B4E9
        public static readonly Color Orange = new Color(0.902f, 0.624f, 0.000f, 1f);  // #E69F00
        public static readonly Color Green = new Color(0.000f, 0.620f, 0.451f, 1f);   // #009E73
        public static readonly Color PurplePink = new Color(0.800f, 0.475f, 0.655f, 1f);// #CC79A7

        public const string HexSkyBlue = "#56B4E9";
        public const string HexOrange = "#E69F00";
        public const string HexGreen = "#009E73";
        public const string HexPurplePink = "#CC79A7";

        private static readonly Color[] Colors = new Color[MaxPlayers]
        {
            SkyBlue,
            Orange,
            Green,
            PurplePink
        };

        private static readonly string[] HexCodes = new string[MaxPlayers]
        {
            HexSkyBlue,
            HexOrange,
            HexGreen,
            HexPurplePink
        };

        private static readonly string[] ColorNamesVi = new string[MaxPlayers]
        {
            "Xanh da trời",
            "Cam",
            "Xanh lục",
            "Tím hồng"
        };

        /// <summary>
        /// Lấy màu theo ColorIndex (0-3). Tự động clamp trong khoảng hợp lệ.
        /// </summary>
        public static Color GetColor(int colorIndex)
        {
            int index = Mathf.Clamp(colorIndex, 0, MaxPlayers - 1);
            return Colors[index];
        }

        /// <summary>
        /// Lấy mã Hex theo ColorIndex (0-3).
        /// </summary>
        public static string GetColorHex(int colorIndex)
        {
            int index = Mathf.Clamp(colorIndex, 0, MaxPlayers - 1);
            return HexCodes[index];
        }

        /// <summary>
        /// Lấy số thứ tự hiển thị của người chơi (1-4) tương ứng với ColorIndex (0-3).
        /// BẮT BUỘC đi kèm màu để hỗ trợ người mù màu (Mục 16.1).
        /// </summary>
        public static int GetPlayerNumber(int colorIndex)
        {
            return Mathf.Clamp(colorIndex, 0, MaxPlayers - 1) + 1;
        }

        /// <summary>
        /// Lấy tên tiếng Việt của màu (cho mục đích ghi log hoặc tooltip trợ năng).
        /// </summary>
        public static string GetColorName(int colorIndex)
        {
            int index = Mathf.Clamp(colorIndex, 0, MaxPlayers - 1);
            return ColorNamesVi[index];
        }

        /// <summary>
        /// Định dạng chuỗi hiển thị chuẩn kèm số thứ tự (ví dụ: "[1] TênNgườiChơi").
        /// </summary>
        public static string FormatPlayerLabel(int colorIndex, string playerName)
        {
            int number = GetPlayerNumber(colorIndex);
            string name = string.IsNullOrWhiteSpace(playerName) ? $"Player {number}" : playerName.Trim();
            return $"[{number}] {name}";
        }

        /// <summary>
        /// Định dạng chuỗi rich text TMPro có mã màu (ví dụ: "<color=#56B4E9>[1] Player 1</color>").
        /// </summary>
        public static string FormatColoredPlayerLabel(int colorIndex, string playerName)
        {
            string hex = GetColorHex(colorIndex);
            string label = FormatPlayerLabel(colorIndex, playerName);
            return $"<color={hex}>{label}</color>";
        }
    }
}
