// Script: PlayerWallet.cs
// Mục đích: Quản lý tiền tệ "Linh hồn" (Souls) của người chơi trong từng lượt chơi (Mục 17.7).
//           Chỉ Server có quyền ghi, chỉ chủ nhân đọc (NetworkVariableReadPermission.Owner).
//           Tự động reset về 0 khi bắt đầu lượt mới.
// Môi trường thực thi: Cả hai (Server quản lý giao dịch, Client đọc hiển thị HUD).

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    public class PlayerWallet : NetworkBehaviour
    {
        // NetworkVariable Souls: chỉ Server ghi, chỉ Owner đọc (Mục 17.7)
        public NetworkVariable<int> Souls { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );

        public event Action<int> OnSoulsChanged;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Bắt đầu mỗi lượt chơi = 0 (không giữ giữa các lượt - Mục 17.7)
                Souls.Value = 0;
            }

            if (IsOwner)
            {
                Souls.OnValueChanged += HandleSoulsValueChanged;
                OnSoulsChanged?.Invoke(Souls.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                Souls.OnValueChanged -= HandleSoulsValueChanged;
            }
        }

        private void HandleSoulsValueChanged(int previousValue, int newValue)
        {
            OnSoulsChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Cộng thêm Linh hồn cho người chơi này (Chỉ Server được gọi).
        /// </summary>
        public void AddSouls(int amount)
        {
            if (!IsServer || amount <= 0) return;
            Souls.Value += amount;
            Debug.Log($"[PlayerWallet] Client {OwnerClientId} nhận +{amount} Linh hồn. Tổng: {Souls.Value}");
        }

        /// <summary>
        /// Trừ Linh hồn khi mua sắm tại Shop (Chỉ Server được gọi).
        /// Trả về true nếu trừ thành công, false nếu không đủ tiền.
        /// </summary>
        public bool TrySpendSouls(int amount)
        {
            if (!IsServer || amount <= 0) return false;
            if (Souls.Value < amount) return false;

            Souls.Value -= amount;
            Debug.Log($"[PlayerWallet] Client {OwnerClientId} đã tiêu {amount} Linh hồn. Còn lại: {Souls.Value}");
            return true;
        }

        /// <summary>
        /// Thưởng chia sẻ Linh hồn cho tất cả người chơi còn sống (không Dead) trong đội (Mục 17.7).
        /// Được gọi khi quái chết hoặc phòng Combat Cleared (+20).
        /// </summary>
        public static void DistributeSoulsToLivingPlayers(int amount)
        {
            if (amount <= 0 || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;

                var health = client.PlayerObject.GetComponent<Health>();
                // Người chơi còn sống (không Dead) - người gục (Downed) chưa chết vẫn được nhận (Mục 17.7)
                if (health != null && health.IsDead.Value) continue;

                var wallet = client.PlayerObject.GetComponent<PlayerWallet>();
                if (wallet != null)
                {
                    wallet.AddSouls(amount);
                }
            }
        }
    }
}
