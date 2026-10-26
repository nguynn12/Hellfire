// Script: GameModeHelper.cs
// Mục đích: Cung cấp cờ chuyển đổi tập trung (Master Offline Switch) và các phương thức kiểm tra phân quyền lai (Dual-Mode Architecture) cho cả Offline Test và Multiplayer.
// Môi trường thực thi: Cả hai (Server/Host, Client và Offline Test).

using Unity.Netcode;
using UnityEngine;
using Hellfire.Networking;

namespace Hellfire
{
    public static class GameModeHelper
    {
        /// <summary>
        /// Cưỡng ép chạy ở chế độ ngoại tuyến (Offline Test), bỏ qua toàn bộ xác thực Netcode.
        /// </summary>
        public static bool ForceOfflineMode = false;

        /// <summary>
        /// Hệ thống được coi là Offline nếu:
        /// 1. Cờ ForceOfflineMode được bật HOẶC
        /// 2. Tồn tại OfflineManager trong Scene hiện tại HOẶC
        /// 3. NetworkManager chưa khởi tạo hoặc chưa lắng nghe mạng
        /// </summary>
        public static bool IsOffline =>
            ForceOfflineMode ||
            OfflineManager.Instance != null ||
            (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        /// <summary>
        /// Xác định quyền Server hoặc đang ở chế độ Offline (cho phép thực thi logic Server khi test offline).
        /// </summary>
        public static bool IsServerOrOffline =>
            IsOffline || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        /// <summary>
        /// Xác định quyền Client hoặc đang ở chế độ Offline.
        /// </summary>
        public static bool IsClientOrOffline =>
            IsOffline || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient);

        /// <summary>
        /// Kiểm tra xem người chơi cục bộ có quyền điều khiển component này không.
        /// Khi Offline: luôn true; Khi Online: chỉ true khi IsOwner.
        /// </summary>
        public static bool CanLocalControl(NetworkBehaviour behaviour)
        {
            if (behaviour == null) return false;
            return IsOffline || behaviour.IsOwner;
        }
    }
}
