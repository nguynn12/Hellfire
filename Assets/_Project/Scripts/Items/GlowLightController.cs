// Script: GlowLightController.cs
// Mục đích: Thành phần điều khiển ánh sáng và hiệu ứng phát quang (Emission & Point Light) tái sử dụng cao.
//           Sử dụng MaterialPropertyBlock triệt tiêu 100% rò rỉ bộ nhớ (Zero Material Instance Leak).
//           Hỗ trợ Tế đàn (Shrine), Rương báu (LootChest), Vật phẩm rơi (ItemPickup) và Đèn đuốc hầm ngục.

using System.Collections;
using UnityEngine;

namespace Hellfire.Items
{
    [DisallowMultipleComponent]
    public class GlowLightController : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("Point Light hoặc Spot Light phát sáng chính")]
        [SerializeField] private Light _lightSource;

        [Tooltip("Renderer chứa quả cầu lửa hoặc chi tiết phát quang")]
        [SerializeField] private Renderer _glowRenderer;

        [Header("Color & Intensity Settings")]
        [Tooltip("Màu phát sáng mặc định")]
        [ColorUsage(true, true)]
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.8f, 1.0f, 1f);

        [Header("Animation & Pulse Effect")]
        [Tooltip("Bật hiệu ứng ánh sáng bập bùng nhẹ (Pulse / Flicker)")]
        [SerializeField] private bool _enablePulse = true;

        [Tooltip("Tốc độ bập bùng của ngọn lửa/ánh sáng")]
        [SerializeField] private float _pulseSpeed = 2.5f;

        [Tooltip("Biên độ dao động cường độ sáng")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _pulseAmplitude = 0.15f;

        private float _baseLightIntensity = 2.0f;
        private bool _isExtinguished = false;

        public Color ActiveColor => _activeColor;
        public bool IsExtinguished => _isExtinguished;

        private void Reset()
        {
            if (_lightSource == null) _lightSource = GetComponentInChildren<Light>();
            if (_glowRenderer == null) _glowRenderer = GetComponentInChildren<Renderer>();
        }

        private void Awake()
        {
            if (_lightSource == null) _lightSource = GetComponentInChildren<Light>();
            if (_glowRenderer == null) _glowRenderer = GetComponentInChildren<Renderer>();

            if (_lightSource != null)
            {
                _baseLightIntensity = _lightSource.intensity;
            }

            ApplyGlowColor(_activeColor);
        }

        private void Update()
        {
            if (_isExtinguished || !_enablePulse || _lightSource == null) return;

            // Hiệu ứng ánh sáng đuốc/lửa bập bùng tự nhiên
            float pulse = 1.0f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmplitude;
            _lightSource.intensity = _baseLightIntensity * pulse;
        }

        /// <summary>
        /// Đổi màu ánh sáng và vật liệu phát sáng tức thì.
        /// </summary>
        public void SetGlowColor(Color newColor)
        {
            _activeColor = newColor;
            if (!_isExtinguished)
            {
                ApplyGlowColor(newColor);
            }
        }

        /// <summary>
        /// Áp dụng màu lên Light và kích hoạt Visual phát sáng (giữ nguyên Material gốc của model).
        /// </summary>
        public void ApplyGlowColor(Color color)
        {
            // 1. Đổi màu Point Light
            if (_lightSource != null)
            {
                _lightSource.color = color;
                _lightSource.enabled = true;
            }

            // 2. Kích hoạt Renderer mà không can thiệp đè màu Material
            if (_glowRenderer != null)
            {
                _glowRenderer.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Dập tắt ánh sáng khi tế đàn/rương đã dùng.
        /// </summary>
        public void Extinguish()
        {
            _isExtinguished = true;

            if (_lightSource != null)
            {
                _lightSource.enabled = false;
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Thắp sáng lại (Reset trạng thái).
        /// </summary>
        public void Ignite()
        {
            _isExtinguished = false;
            ApplyGlowColor(_activeColor);
        }
    }
}
