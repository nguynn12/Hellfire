using Hellfire.Combat;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Component nhận sự kiện sát thương từ Health và tự động gọi FloatingDamageManager để sinh số sát thương bay.
    /// Gắn trực tiếp lên Zombie hoặc Target Dummy thuộc phần việc cá nhân.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class FloatingDamageReceiver : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector3 _popupOffset = new Vector3(0f, 1.7f, 0f);
        [SerializeField] private float _scatterRadius = 0.25f;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitbox, ulong attackerId)
        {
            // Không sinh popup sát thương khi người chơi tự nhận sát thương (chỉ áp dụng cho quái vật / bia tập)
            if (_health != null && _health.IsPlayer) return;

            if (FloatingDamageManager.Instance != null && damage > 0f)
            {
                Vector3 spawnPos = transform.position + _popupOffset + Random.insideUnitSphere * _scatterRadius;
                bool isHeadshot = (hitbox == HitboxType.Head);
                FloatingDamageManager.Instance.SpawnDamagePopup(spawnPos, damage, isHeadshot);
            }
        }
    }
}

