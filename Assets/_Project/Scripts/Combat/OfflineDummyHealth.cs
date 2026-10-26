using System;
using System.Collections;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Component quản lý máu độc lập cho bia bắn Dummy trong Scene test.
    /// Hoàn toàn tách biệt khỏi Netcode NGO và Health.cs của đồng đội.
    /// </summary>
    [DisallowMultipleComponent]
    public class OfflineDummyHealth : MonoBehaviour, IDamageable
    {
        [Header("Dummy Health Configuration")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _despawnDelay = 3.0f;

        [SerializeField] private float _currentHealth = 100f;
        private bool _isDead;
        private Collider[] _colliders;

        public event Action<float, float> OnHealthChanged;            // current, max
        public event Action<float, HitboxType, ulong> OnDamageTaken;   // damage, hitbox, attacker
        public event Action<ulong> OnDied;                            // attackerClientId

        // IDamageable implementation
        bool IDamageable.IsDead => _isDead;
        bool IDamageable.IsDowned => false;
        float IDamageable.CurrentHealthValue => _currentHealth;
        float IDamageable.MaxHealthValue => _maxHealth;
        bool IDamageable.IsPlayerTarget => false;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsDead => _isDead;

        public void ResetHealth(float maxHealth = 100f)
        {
            _maxHealth = maxHealth;
            _currentHealth = _maxHealth;
            _isDead = false;
        }

        private void Reset()
        {
            _colliders = GetComponentsInChildren<Collider>();
            _currentHealth = _maxHealth;
        }

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>();
            if (_currentHealth <= 0f) _currentHealth = _maxHealth;
        }

        public void TakeDamage(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            if (_isDead || damage <= 0f) return;
            if (_currentHealth < 0f) _currentHealth = _maxHealth;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnDamageTaken?.Invoke(damage, hitboxType, attackerClientId);

            if (_currentHealth <= 0f)
            {
                _isDead = true;
                DisableColliders();
                OnDied?.Invoke(attackerClientId);
                StartCoroutine(DespawnRoutine());
            }
        }

        private void DisableColliders()
        {
            if (_colliders == null) return;
            foreach (var col in _colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        private IEnumerator DespawnRoutine()
        {
            yield return new WaitForSeconds(_despawnDelay);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
