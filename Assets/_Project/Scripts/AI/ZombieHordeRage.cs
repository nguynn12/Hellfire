// Script: ZombieHordeRage.cs
// Mục đích: Trạng thái cuồng nộ bầy đàn (Horde Rage) khi HP < 30% (Đợt Z3).
// Môi trường: Server-Authoritative quyết định, đồng bộ qua NetworkVariable<bool> IsEnraged tới Client.
// Tính năng: +25% tốc độ chạy, -15% cooldown tấn công, hiệu ứng mắt đỏ/aura đỏ, dừng khi chết, KHÔNG nhân đôi sát thương.

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class ZombieHordeRage : NetworkBehaviour
    {
        [Header("Rage Settings")]
        [Tooltip("Ngưỡng máu phần trăm để kích hoạt cuồng nộ (mặc định 0.30f = 30%)")]
        [Range(0.1f, 0.5f)]
        [SerializeField] private float _rageThresholdRatio = 0.30f;

        [Tooltip("Hệ số tăng tốc độ di chuyển khi cuồng nộ (+25% = 1.25f)")]
        [SerializeField] private float _speedMultiplier = 1.25f;

        [Tooltip("Hệ số giảm cooldown tấn công khi cuồng nộ (-15% = 0.85f)")]
        [SerializeField] private float _attackCooldownMultiplier = 0.85f;

        [Header("Visual Effects (Rage Eyes / Aura)")]
        [SerializeField] private Light _rageEyeLight;
        [SerializeField] private ParticleSystem _rageAuraParticles;
        [SerializeField] private Color _rageEyeColor = new Color(1.0f, 0.05f, 0.05f, 1f);

        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private NavMeshAgent _navAgent;
        [SerializeField] private RangedZombieAI _rangedAI;

        // Đồng bộ trạng thái cuồng nộ tới toàn bộ Client
        public NetworkVariable<bool> IsEnraged { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private bool _hasEnteredRage = false;
        private float _baseNavSpeed = -1f;

        public event Action<bool> OnRageStateChanged;

        public bool IsRaged => IsEnraged.Value;
        public float SpeedMultiplier => _speedMultiplier;
        public float AttackCooldownMultiplier => _attackCooldownMultiplier;
        public float RageThresholdRatio => _rageThresholdRatio;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();

            EnsureRageVisuals();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            IsEnraged.OnValueChanged += HandleRageStateChanged;
            ApplyVisualState(IsEnraged.Value);
        }

        public override void OnNetworkDespawn()
        {
            IsEnraged.OnValueChanged -= HandleRageStateChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            // Kiểm tra trạng thái chết: Dù ở Server hay Client, khi chết phải tắt ngay Rage
            if (_health != null && _health.IsDead != null && _health.IsSpawned && _health.IsDead.Value)
            {
                if (IsServer && IsEnraged.Value)
                {
                    IsEnraged.Value = false;
                }
                if (_hasEnteredRage)
                {
                    StopRageEffects();
                }
                return;
            }

            // Chỉ Server kiểm tra và kích hoạt Rage
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            EvaluateRageCondition();
        }

        /// <summary>
        /// Đánh giá lượng máu hiện tại so với ngưỡng 30% để kích hoạt Rage.
        /// </summary>
        public void EvaluateRageCondition()
        {
            if (_health == null || _hasEnteredRage) return;

            float maxHp = 100f; // Mặc định Health cơ bản
            float currentHp = _health.CurrentHealth != null ? _health.CurrentHealth.Value : 100f;
            float ratio = currentHp / maxHp;

            if (ratio <= _rageThresholdRatio && currentHp > 0f)
            {
                EnterRage();
            }
        }

        private void EnterRage()
        {
            if (_hasEnteredRage) return;
            _hasEnteredRage = true;

            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();
            if (_health == null) _health = GetComponent<Health>();

            if (IsServer)
            {
                IsEnraged.Value = true;
            }

            // 1. Tăng tốc độ di chuyển 25%
            if (_navAgent != null)
            {
                if (_baseNavSpeed <= 0f) _baseNavSpeed = _navAgent.speed;
                _navAgent.speed = _baseNavSpeed * _speedMultiplier;
            }

            // 2. Giảm cooldown tấn công 15% (không nhân đôi sát thương)
            if (_rangedAI != null)
            {
                _rangedAI.SetRageAttackCooldownMultiplier(_attackCooldownMultiplier);
            }

            // 3. Kích hoạt hiệu ứng visual
            ApplyVisualState(true);

            OnRageStateChanged?.Invoke(true);

            if (RangedZombieAI.IsDebugLoggingEnabled())
            {
                Debug.Log($"[ZombieHordeRage] Zombie '{name}' kích hoạt HORDE RAGE (Tốc độ: +25%, Cooldown: -15%, Giữ nguyên DMG 12).");
            }
        }

        private void HandleRageStateChanged(bool previousValue, bool newValue)
        {
            ApplyVisualState(newValue);
            OnRageStateChanged?.Invoke(newValue);
        }

        private void ApplyVisualState(bool enraged)
        {
            if (_rageEyeLight != null)
            {
                _rageEyeLight.enabled = enraged;
            }
            if (_rageAuraParticles != null)
            {
                if (enraged && !_rageAuraParticles.isPlaying) _rageAuraParticles.Play();
                else if (!enraged && _rageAuraParticles.isPlaying) _rageAuraParticles.Stop();
            }
        }

        private void StopRageEffects()
        {
            _hasEnteredRage = false;
            ApplyVisualState(false);

            if (_navAgent != null && _baseNavSpeed > 0f)
            {
                _navAgent.speed = _baseNavSpeed;
            }
            if (_rangedAI != null)
            {
                _rangedAI.SetRageAttackCooldownMultiplier(1.0f);
            }

            OnRageStateChanged?.Invoke(false);
        }

        private void EnsureRageVisuals()
        {
            if (_rageEyeLight == null)
            {
                var lightObj = new GameObject("Rage_Eye_PointLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 1.7f, 0.35f); // Ngay trước mắt quái
                _rageEyeLight = lightObj.AddComponent<Light>();
                _rageEyeLight.type = LightType.Point;
                _rageEyeLight.color = _rageEyeColor;
                _rageEyeLight.intensity = 2.5f;
                _rageEyeLight.range = 3.5f;
                _rageEyeLight.enabled = false;
            }
        }

        /// <summary>
        /// Cho phép kích hoạt trực tiếp trong bài test hoặc offline mode.
        /// </summary>
        public void ForceTriggerRageForTest()
        {
            EnterRage();
        }
    }
}
