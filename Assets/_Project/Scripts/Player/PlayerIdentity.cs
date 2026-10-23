// Script: PlayerIdentity.cs
// Mục đích: Quản lý danh tính người chơi (Tên, ColorIndex 0-3, Trạng thái Ready) (Mục 16.1 & 16.10 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server-Authoritative ghi, Client đồng bộ và đọc).

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerIdentity : NetworkBehaviour
    {
        public const string PlayerNamePrefKey = "PlayerName";
        public const int MaxNameLength = 16;

        [Header("Network Variables (Server-Authoritative ghi)")]
        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes("Player 1"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> ColorIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Static Registry giúp UI và hệ thống khác truy vấn người chơi nhanh chóng
        public static readonly List<PlayerIdentity> All = new List<PlayerIdentity>();
        public static event Action OnAnyPlayerIdentityChanged;
        public static event Action<PlayerIdentity> OnPlayerIdentityAdded;
        public static event Action<PlayerIdentity> OnPlayerIdentityRemoved;
        public static PlayerIdentity LocalIdentity { get; private set; }

        // Quản lý việc cấp phát màu trên Server
        private static readonly bool[] UsedColorIndices = new bool[PlayerPalette.MaxPlayers];

        public Color Color => PlayerPalette.GetColor(ColorIndex.Value);
        public int PlayerNumber => PlayerPalette.GetPlayerNumber(ColorIndex.Value);
        public string FormattedLabel => PlayerPalette.FormatPlayerLabel(ColorIndex.Value, PlayerName.Value.ToString());
        public string ColoredLabel => PlayerPalette.FormatColoredPlayerLabel(ColorIndex.Value, PlayerName.Value.ToString());

        public static PlayerIdentity GetByClientId(ulong clientId)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i].OwnerClientId == clientId)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static void ResetServerColorRegistry()
        {
            for (int i = 0; i < UsedColorIndices.Length; i++)
            {
                UsedColorIndices[i] = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this))
            {
                All.Add(this);
                OnPlayerIdentityAdded?.Invoke(this);
            }

            if (IsOwner)
            {
                LocalIdentity = this;
            }

            // Đăng ký sự kiện thay đổi dữ liệu đồng bộ
            PlayerName.OnValueChanged += HandleNameChanged;
            ColorIndex.OnValueChanged += HandleColorIndexChanged;
            IsReady.OnValueChanged += HandleReadyChanged;

            if (IsServer)
            {
                AssignServerColorAndDefaults();
            }

            if (IsOwner)
            {
                // Chủ sở hữu đọc tên lưu trong PlayerPrefs và gửi lên Server
                string savedName = PlayerPrefs.GetString(PlayerNamePrefKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(savedName))
                {
                    SetPlayerNameServerRpc(new FixedString32Bytes(savedName.Trim()));
                }
                else
                {
                    // Nếu rỗng, gửi rỗng để Server dùng tên mặc định "Player N"
                    SetPlayerNameServerRpc(new FixedString32Bytes(string.Empty));
                }
            }

            OnAnyPlayerIdentityChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            PlayerName.OnValueChanged -= HandleNameChanged;
            ColorIndex.OnValueChanged -= HandleColorIndexChanged;
            IsReady.OnValueChanged -= HandleReadyChanged;

            if (IsServer)
            {
                ReleaseServerColorIndex();
            }

            if (LocalIdentity == this)
            {
                LocalIdentity = null;
            }

            if (All.Contains(this))
            {
                All.Remove(this);
                OnPlayerIdentityRemoved?.Invoke(this);
            }

            OnAnyPlayerIdentityChanged?.Invoke();
        }

        private void AssignServerColorAndDefaults()
        {
            // Tìm chỉ số màu nhỏ nhất chưa dùng trong 0-3
            int assignedIndex = 0;
            for (int i = 0; i < PlayerPalette.MaxPlayers; i++)
            {
                if (!UsedColorIndices[i])
                {
                    assignedIndex = i;
                    UsedColorIndices[i] = true;
                    break;
                }
            }

            ColorIndex.Value = assignedIndex;

            // Tên mặc định "Player N" với N = ColorIndex + 1
            PlayerName.Value = new FixedString32Bytes($"Player {assignedIndex + 1}");

            // Host mặc định sẵn sàng (Mục 16.10: "Host mặc định coi như sẵn sàng")
            bool isHost = OwnerClientId == NetworkManager.ServerClientId;
            IsReady.Value = isHost;
        }

        private void ReleaseServerColorIndex()
        {
            int index = ColorIndex.Value;
            if (index >= 0 && index < UsedColorIndices.Length)
            {
                UsedColorIndices[index] = false;
            }
        }

        private void HandleNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            OnAnyPlayerIdentityChanged?.Invoke();
        }

        private void HandleColorIndexChanged(int previousValue, int newValue)
        {
            OnAnyPlayerIdentityChanged?.Invoke();
        }

        private void HandleReadyChanged(bool previousValue, bool newValue)
        {
            OnAnyPlayerIdentityChanged?.Invoke();
        }

        // ================= SERVER RPCs =================

        /// <summary>
        /// Chủ sở hữu gửi tên người chơi lên Server (Mục 16.1).
        /// Server kiểm tra, cắt tối đa 16 ký tự, trim khoảng trắng rồi ghi đè.
        /// </summary>
        [ServerRpc]
        public void SetPlayerNameServerRpc(FixedString32Bytes requestedName, ServerRpcParams rpcParams = default)
        {
            string nameStr = requestedName.ToString().Trim();

            if (string.IsNullOrEmpty(nameStr))
            {
                // Nếu rỗng thì dùng "Player N" (N = ColorIndex + 1)
                nameStr = $"Player {ColorIndex.Value + 1}";
            }
            else if (nameStr.Length > MaxNameLength)
            {
                nameStr = nameStr.Substring(0, MaxNameLength).Trim();
            }

            PlayerName.Value = new FixedString32Bytes(nameStr);
        }

        /// <summary>
        /// Client bật/tắt trạng thái Sẵn sàng ở Lobby (Mục 16.10).
        /// Host luôn coi như sẵn sàng.
        /// </summary>
        [ServerRpc]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            if (OwnerClientId == NetworkManager.ServerClientId)
            {
                // Host luôn sẵn sàng
                IsReady.Value = true;
                return;
            }

            IsReady.Value = ready;
        }
    }
}
