// Script: CorridorLightFlicker.cs
// Mục đích: Điều khiển đèn hành lang nháy nhẹ theo mục 9.1 và 17.2 đặc tả kỹ thuật.
// Môi trường thực thi: Client/Host (Visual representation).

using UnityEngine;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class CorridorLightFlicker : MonoBehaviour
    {
        [Header("Flicker Settings")]
        [Tooltip("Cường độ gốc của đèn")]
        [SerializeField] private float _baseIntensity = 1.2f;

        [Tooltip("Biên độ nháy nhẹ (±10-15%)")]
        [SerializeField] private float _amplitude = 0.15f;

        [Tooltip("Tốc độ nháy")]
        [SerializeField] private float _flickerSpeed = 3.5f;

        private Light _light;
        private float _noiseOffset;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
            }
            _noiseOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (_light == null) return;

            // Nháy ngẫu nhiên nhẹ qua Perlin Noise theo Mục 9.1
            float noise = Mathf.PerlinNoise((Time.time * _flickerSpeed) + _noiseOffset, 0.5f) - 0.5f;
            _light.intensity = Mathf.Max(0.1f, _baseIntensity + (noise * 2f * _amplitude * _baseIntensity));
        }
    }
}
