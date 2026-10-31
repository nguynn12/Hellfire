// Script: ZombieHearing.cs
// Mục đích: Thính giác Zombie (AI Perception - Hearing) độc lập với AudioSource.
// Môi trường: Server-Authoritative quyết định Zombie có nghe thấy hay không dựa trên khoảng cách.

using System;
using System.Collections.Generic;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class ZombieHearing : MonoBehaviour
    {
        [Header("Hearing Settings")]
        [Tooltip("Bán kính nghe tối đa của Zombie (m)")]
        [SerializeField] private float _hearingRadius = 20f;

        [Tooltip("Bật/tắt khả năng nghe")]
        [SerializeField] private bool _enableHearing = true;

        [Header("References")]
        [SerializeField] private ZombieInvestigation _investigation;
        [SerializeField] private Health _health;

        // Static registry để phân phối tiếng động O(N) không sinh rác bộ nhớ (Garbage-Free)
        private static readonly List<ZombieHearing> ActiveListeners = new List<ZombieHearing>();

        public event Action<Vector3, Transform> OnSoundHeard;

        public float HearingRadius
        {
            get => _hearingRadius;
            set => _hearingRadius = Mathf.Max(0.1f, value);
        }

        public bool EnableHearing
        {
            get => _enableHearing;
            set => _enableHearing = value;
        }

        public Health ZombieHealth => _health;

        private void Awake()
        {
            if (_investigation == null) _investigation = GetComponent<ZombieInvestigation>();
            if (_health == null) _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (!ActiveListeners.Contains(this))
            {
                ActiveListeners.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveListeners.Remove(this);
        }

        /// <summary>
        /// Phân phối sự kiện tiếng động tới toàn bộ Zombie đang hoạt động trên Server.
        /// </summary>
        public static void BroadcastNoise(Vector3 noisePosition, float noiseRadius, Transform noiseSource)
        {
            if (ActiveListeners.Count == 0)
            {
                var listeners = UnityEngine.Object.FindObjectsByType<ZombieHearing>(FindObjectsSortMode.None);
                for (int j = 0; j < listeners.Length; j++)
                {
                    if (listeners[j] != null && !ActiveListeners.Contains(listeners[j]))
                    {
                        ActiveListeners.Add(listeners[j]);
                    }
                }
            }

            for (int i = 0; i < ActiveListeners.Count; i++)
            {
                var listener = ActiveListeners[i];
                if (listener != null && listener.isActiveAndEnabled)
                {
                    listener.EvaluateAndReceiveNoise(noisePosition, noiseRadius, noiseSource);
                }
            }
        }

        /// <summary>
        /// Đánh giá xem Zombie có nghe thấy âm thanh hay không dựa trên cự ly.
        /// </summary>
        public void EvaluateAndReceiveNoise(Vector3 noisePosition, float noiseRadius, Transform noiseSource)
        {
            if (!_enableHearing) return;

            if (_investigation == null) _investigation = GetComponent<ZombieInvestigation>();
            if (_health == null) _health = GetComponent<Health>();

            // Kiểm tra trạng thái sống
            if (_health != null && _health.IsDead != null && _health.IsSpawned && _health.IsDead.Value) return;

            // Kiểm tra quyền Server: Quyết định nghe chỉ thực hiện trên Server trong môi trường mạng
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, noisePosition);
            float effectiveRadius = Mathf.Min(_hearingRadius, noiseRadius);

            // Nếu nằm trong bán kính nghe
            if (dist <= effectiveRadius)
            {
                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    string sourceName = noiseSource != null ? noiseSource.name : "Unknown";
                    Debug.Log($"[ZombieHearing] Zombie '{name}' nghe thấy tiếng động từ '{sourceName}' tại {noisePosition} (Cự ly: {dist:F1}m, Bán kính nghe: {_hearingRadius}m).");
                }

                OnSoundHeard?.Invoke(noisePosition, noiseSource);

                if (_investigation != null)
                {
                    _investigation.OnNoiseHeard(noisePosition, noiseSource);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _hearingRadius);
        }
    }
}
