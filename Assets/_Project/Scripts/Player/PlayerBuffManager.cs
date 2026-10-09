// Script: PlayerBuffManager.cs
// Mục đích: Quản lý toàn bộ bùa lợi (Power-up) nhận được của người chơi (Mục 6.1 & 6.2 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Server tính toán và ghi NetworkVariables, Client đọc hiển thị UI).

using System;
using Hellfire.Combat;
using Hellfire.Items;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class PlayerBuffManager : NetworkBehaviour
    {
        [Header("Active Buff Values (Server-Authoritative)")]
        public NetworkVariable<float> SpeedMultiplierNet { get; } = new NetworkVariable<float>(
            1.0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> DamageMultiplierNet { get; } = new NetworkVariable<float>(
            1.0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> ShieldTimerNet { get; } = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private Health _health;

        public float SpeedMultiplier => SpeedMultiplierNet.Value;
        public float DamageMultiplier => DamageMultiplierNet.Value;
        public float ShieldTimeRemaining => ShieldTimerNet.Value;
        public bool HasGuardianShield => ShieldTimerNet.Value > 0f;

        public event Action<PowerUpType, float> OnPowerUpApplied;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            ShieldTimerNet.OnValueChanged += HandleShieldTimerChanged;
        }

        public override void OnNetworkDespawn()
        {
            ShieldTimerNet.OnValueChanged -= HandleShieldTimerChanged;
        }

        private void HandleShieldTimerChanged(float previousValue, float newValue)
        {
            if (_health != null)
            {
                _health.IsInvulnerable = (newValue > 0f);
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Đếm ngược thời gian của Khiên tạm thời (Guardian Shield - 8s)
            if (ShieldTimerNet.Value > 0f)
            {
                float newTimer = Mathf.Max(0f, ShieldTimerNet.Value - Time.deltaTime);
                ShieldTimerNet.Value = newTimer;

                if (newTimer <= 0f && _health != null)
                {
                    _health.IsInvulnerable = false;
                }
            }
        }

        /// <summary>
        /// Kích hoạt bùa lợi lên người chơi (CHỈ SERVER GỌI theo Mục 6.1 & 6.2).
        /// </summary>
        public void ApplyPowerUp(PowerUpData powerUp)
        {
            if (!IsServer || powerUp == null) return;

            switch (powerUp.Type)
            {
                case PowerUpType.MaxHealthUp:
                    // +25 máu tối đa, hồi đầy ngay khi nhặt (vĩnh viễn trong run)
                    if (_health != null)
                    {
                        _health.IncreaseMaxHealthAndHeal(powerUp.Value);
                    }
                    break;

                case PowerUpType.SwiftBoots:
                    // +20% tốc độ di chuyển
                    SpeedMultiplierNet.Value += powerUp.Value;
                    break;

                case PowerUpType.BerserkerCharm:
                    // +30% sát thương gây ra
                    DamageMultiplierNet.Value += powerUp.Value;
                    break;

                case PowerUpType.GuardianShield:
                    // Miễn sát thương hoàn toàn trong 8 giây (kích hoạt ngay khi nhặt)
                    ShieldTimerNet.Value = powerUp.Duration > 0f ? powerUp.Duration : 8.0f;
                    if (_health != null)
                    {
                        _health.IsInvulnerable = true;
                    }
                    break;
            }

            NotifyPowerUpAppliedClientRpc(powerUp.Type, powerUp.Value);
        }

        [ClientRpc]
        private void NotifyPowerUpAppliedClientRpc(PowerUpType type, float value)
        {
            OnPowerUpApplied?.Invoke(type, value);
        }
    }
}
