// Script: TestEnemyImp.cs
// Mục đích: Quái vật thử nghiệm Imp (30 HP) phục vụ kiểm thử bắn hitscan, hitbox và đồng bộ máu (Mục 6.2).
// Môi trường thực thi: Cả hai (Server quản lý máu, Client hiển thị thanh máu overhead và hiệu ứng).

using Hellfire.Combat;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NetworkObject))]
    public class TestEnemyImp : NetworkBehaviour
    {
        [Header("Imp Configuration (Mục 6.2)")]
        [SerializeField] private float _maxHealth = 30f;
        [SerializeField] private string _enemyName = "Quỷ nhỏ (Imp)";

        [Header("References")]
        [SerializeField] private Health _health;
        [SerializeField] private TextMeshProUGUI _overheadNameText;
        [SerializeField] private Slider _overheadHealthSlider;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Renderer _meshRenderer;

        private Vector3 _initialPosition;
        private Material _originalMaterial;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void AutoResolveReferences()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_health != null)
            {
                _health.SetMaxHealth(_maxHealth);
            }

            if (_visualRoot == null) _visualRoot = transform.Find("Visual");
            if (_meshRenderer == null && _visualRoot != null)
            {
                _meshRenderer = _visualRoot.GetComponentInChildren<Renderer>();
            }

            if (_overheadHealthSlider == null) _overheadHealthSlider = GetComponentInChildren<Slider>(true);
            if (_overheadNameText == null) _overheadNameText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
            _initialPosition = transform.position;

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _health.OnDied += HandleDeath;
                _health.OnDamageTaken += HandleDamageTaken;

                UpdateHealthDisplay(_health.CurrentHealth.Value, _maxHealth);
            }

            if (_overheadNameText != null)
            {
                _overheadNameText.text = _enemyName;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDied -= HandleDeath;
                _health.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void Update()
        {
            // Hiệu ứng nhấp nhô nhẹ lúc đứng yên (Idle bobbing)
            if (_health != null && !_health.IsDead.Value)
            {
                float bobOffset = Mathf.Sin(Time.time * 2.5f) * 0.1f;
                transform.position = new Vector3(_initialPosition.x, _initialPosition.y + bobOffset, _initialPosition.z);
            }

            // Billboard Canvas quay mặt về phía Camera chính
            var cam = Camera.main;
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null && cam != null)
            {
                canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - cam.transform.position);
            }
        }

        private void HandleHealthChanged(float currentHealth, float maxHealth)
        {
            UpdateHealthDisplay(currentHealth, maxHealth);
        }

        private void UpdateHealthDisplay(float current, float max)
        {
            if (_overheadHealthSlider != null)
            {
                _overheadHealthSlider.maxValue = max;
                _overheadHealthSlider.value = current;
            }

            if (_overheadNameText != null)
            {
                _overheadNameText.text = $"{_enemyName} [{Mathf.CeilToInt(current)}/{max}]";
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            // Flash màu đỏ khi trúng đạn
            if (_meshRenderer != null)
            {
                StartCoroutine(DamageFlashRoutine());
            }
        }

        private System.Collections.IEnumerator DamageFlashRoutine()
        {
            if (_meshRenderer == null) yield break;
            var originalColor = _meshRenderer.material.color;
            _meshRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            if (_meshRenderer != null)
            {
                _meshRenderer.material.color = originalColor;
            }
        }

        private void HandleDeath(ulong attackerClientId)
        {
            Debug.Log($"[TestEnemyImp] Imp đã bị tiêu diệt bởi người chơi {attackerClientId}!");

            if (_overheadHealthSlider != null)
            {
                _overheadHealthSlider.gameObject.SetActive(false);
            }

            if (_overheadNameText != null)
            {
                _overheadNameText.text = "[ĐÃ CHẾT]";
                _overheadNameText.color = Color.gray;
            }

            // Hạ thấp xuống mặt đất
            transform.position = new Vector3(transform.position.x, 0.2f, transform.position.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
