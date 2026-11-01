// Script: ZombieGroupAlert.cs
// Mục đích: Hệ thống cảnh báo bầy đàn (Group Alert Perception) cho Zombie trong Hellfire (Đợt Z3).
// Môi trường: Server-Authoritative quyết định việc phát và nhận cảnh báo đồng minh.
// Nguyên tắc: Không nhìn xuyên tường; quái nhận cảnh báo nếu chưa thấy mục tiêu sẽ đi Investigate, nếu thấy sẽ vào Combat.

using System;
using System.Collections.Generic;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class ZombieGroupAlert : MonoBehaviour
    {
        [Header("Group Alert Settings")]
        [Tooltip("Bán kính phát cảnh báo tới đồng minh (mặc định 15m theo yêu cầu C1)")]
        [SerializeField] private float _alertRadius = 15f;

        [Tooltip("Thời gian hồi chiêu giữa các lần phát cảnh báo (tránh spam)")]
        [SerializeField] private float _alertCooldown = 4.0f;

        [Header("References")]
        [SerializeField] private RangedZombieAI _rangedAI;
        [SerializeField] private ZombieInvestigation _investigation;
        [SerializeField] private Health _health;

        // Static registry quản lý tất cả Zombie trong màn chơi (O(N), Zero Allocation)
        private static readonly List<ZombieGroupAlert> ActiveAlertListeners = new List<ZombieGroupAlert>();

        // Bộ đếm thời gian và cờ chống lặp vô hạn (Infinite Loop Prevention)
        private float _lastAlertTime = -999f;
        private bool _isProcessingAlert = false;

        public event Action<Vector3, Transform> OnAlertSent;
        public event Action<Vector3, Transform> OnAlertReceived;

        public float AlertRadius
        {
            get => _alertRadius;
            set => _alertRadius = Mathf.Max(1.0f, value);
        }

        public float AlertCooldown
        {
            get => _alertCooldown;
            set => _alertCooldown = Mathf.Max(0.5f, value);
        }

        private void Awake()
        {
            if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();
            if (_investigation == null) _investigation = GetComponent<ZombieInvestigation>();
            if (_health == null) _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (!ActiveAlertListeners.Contains(this))
            {
                ActiveAlertListeners.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveAlertListeners.Remove(this);
        }

        /// <summary>
        /// Phát cảnh báo tới tất cả đồng minh trong bán kính 15m khi phát hiện Player.
        /// </summary>
        public void BroadcastAlert(Vector3 lastKnownPlayerPos, Transform playerTransform)
        {
            // Kiểm tra quyền Server
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            // Kiểm tra quái còn sống không
            if (_health != null && _health.IsDead != null && _health.IsSpawned && _health.IsDead.Value)
            {
                return;
            }

            // Kiểm tra Cooldown phát cảnh báo
            if (Time.time - _lastAlertTime < _alertCooldown)
            {
                return;
            }

            _lastAlertTime = Time.time;
            OnAlertSent?.Invoke(lastKnownPlayerPos, playerTransform);

            if (RangedZombieAI.IsDebugLoggingEnabled())
            {
                Debug.Log($"[ZombieGroupAlert] Zombie '{name}' phát cảnh báo bầy đàn tại {transform.position} (Bán kính: {_alertRadius}m, Vị trí Player: {lastKnownPlayerPos}).");
            }

            // Đảm bảo static list luôn đầy đủ kể cả trong Edit/Play transition
            EnsureActiveListeners();

            for (int i = 0; i < ActiveAlertListeners.Count; i++)
            {
                var ally = ActiveAlertListeners[i];
                if (ally != null && ally != this && ally.isActiveAndEnabled)
                {
                    float dist = Vector3.Distance(transform.position, ally.transform.position);
                    if (dist <= _alertRadius)
                    {
                        ally.ReceiveAlert(lastKnownPlayerPos, playerTransform, this);
                    }
                }
            }
        }

        /// <summary>
        /// Nhận cảnh báo từ một Zombie đồng minh khác.
        /// </summary>
        public void ReceiveAlert(Vector3 lastKnownPlayerPos, Transform playerTransform, ZombieGroupAlert sender)
        {
            // Kiểm tra an toàn: nếu đã chết thì bỏ qua
            if (_health == null) _health = GetComponent<Health>();
            if (_health != null && _health.IsDead != null && _health.IsSpawned && _health.IsDead.Value)
            {
                return;
            }

            // Chống lặp vô hạn (Infinite Loop Prevention):
            // Không phản hồi nếu đang xử lý cảnh báo hoặc vừa phát cảnh báo gần đây
            if (_isProcessingAlert) return;
            if (Time.time - _lastAlertTime < 1.0f) return;

            _isProcessingAlert = true;
            try
            {
                OnAlertReceived?.Invoke(lastKnownPlayerPos, playerTransform);

                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    string senderName = sender != null ? sender.name : "Ally";
                    Debug.Log($"[ZombieGroupAlert] Zombie '{name}' nhận được cảnh báo từ '{senderName}'.");
                }

                if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();
                if (_investigation == null) _investigation = GetComponent<ZombieInvestigation>();

                // Nếu Zombie đã đang trong trạng thái Attack trực tiếp mục tiêu thì không bị phân tâm
                if (_rangedAI != null && _rangedAI.CurrentState == EnemyState.Attack)
                {
                    return;
                }

                // KIỂM TRA QUY TẮC C1:
                // Zombie nhận cảnh báo biết vị trí Player cuối cùng, KHÔNG tự động nhìn xuyên tường.
                bool hasDirectSight = false;
                if (playerTransform != null && _rangedAI != null)
                {
                    hasDirectSight = _rangedAI.HasLineOfSightForTest(playerTransform);
                }

                if (hasDirectSight && _rangedAI != null)
                {
                    // Nếu nhìn thấy mục tiêu rõ ràng -> Chuyển sang Combat (Chase / Attack)
                    if (RangedZombieAI.IsDebugLoggingEnabled())
                    {
                        Debug.Log($"[ZombieGroupAlert] Zombie '{name}' nhìn thấy Player trực tiếp -> Vào Combat!");
                    }
                    _rangedAI.EngageSpottedPlayer(playerTransform);
                }
                else if (_investigation != null)
                {
                    // Nếu KHÔNG nhìn thấy mục tiêu (bị tường/vật cản che khuất) -> Chuyển sang Investigate!
                    if (RangedZombieAI.IsDebugLoggingEnabled())
                    {
                        Debug.Log($"[ZombieGroupAlert] Zombie '{name}' bị che khuất tầm nhìn -> Di chuyển điều tra vị trí cuối cùng {lastKnownPlayerPos}.");
                    }
                    _investigation.OnNoiseHeard(lastKnownPlayerPos, playerTransform);
                }
            }
            finally
            {
                _isProcessingAlert = false;
            }
        }

        private static void EnsureActiveListeners()
        {
            ActiveAlertListeners.RemoveAll(item => item == null);
            var found = UnityEngine.Object.FindObjectsByType<ZombieGroupAlert>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !ActiveAlertListeners.Contains(found[i]))
                {
                    ActiveAlertListeners.Add(found[i]);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _alertRadius);
        }
    }
}
