// Script: GameModeHelper.cs
// Mục đích: Cung cấp cờ chuyển đổi tập trung (Master Offline Switch) và các phương thức kiểm tra phân quyền lai (Dual-Mode Architecture) cho cả Offline Test và Multiplayer.
// Môi trường thực thi: Cả hai (Server/Host, Client và Offline Test).

using Unity.Netcode;
using UnityEngine;

namespace Hellfire
{
    public static class GameModeHelper
    {
        /// <summary>
        /// Cưỡng ép chạy ở chế độ ngoại tuyến (Offline Test), bỏ qua toàn bộ xác thực Netcode.
        /// </summary>
        public static bool ForceOfflineMode = false;

        /// <summary>
        /// Xác định xem ván chơi hiện tại có đang chạy ngoại tuyến hay không.
        /// Tự động true khi ForceOfflineMode bật, hoặc khi NetworkManager chưa khởi tạo hoặc chưa Listening.
        /// </summary>
        public static bool IsOffline =>
            ForceOfflineMode ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening;

        /// <summary>
        /// Xác định quyền Server hoặc đang ở chế độ Offline (cho phép thực thi logic Server khi test offline).
        /// </summary>
        public static bool IsServerOrOffline =>
            IsOffline ||
            (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        /// <summary>
        /// Xác định quyền Client hoặc đang ở chế độ Offline.
        /// </summary>
        public static bool IsClientOrOffline =>
            IsOffline ||
            (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient);

        /// <summary>
        /// Kiểm tra xem người chơi cục bộ có quyền điều khiển component này không.
        /// Khi Offline: luôn true; Khi Online: chỉ true khi IsOwner.
        /// </summary>
        public static bool CanLocalControl(NetworkBehaviour behaviour)
        {
            if (IsOffline) return true;
            return behaviour != null && behaviour.IsOwner;
        }
    }
}
