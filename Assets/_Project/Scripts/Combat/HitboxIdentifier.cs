// Script: HitboxIdentifier.cs
// Mục đích: Định danh vùng va chạm Hitbox (Head, Torso, Limb) và chuyển hướng sát thương về IDamageable cha (Mục 3.2).
// Môi trường thực thi: Cả hai (Server tính toán, Client kiểm tra va chạm).

using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class HitboxIdentifier : MonoBehaviour
    {
        [Header("Hitbox Configuration")]
        [SerializeField] private HitboxType _hitboxType = HitboxType.Torso;
        [SerializeField] private Component _damageableTarget;

        private IDamageable _damageable;

        public HitboxType Type => _hitboxType;
        public IDamageable Damageable => _damageable;

        private void Awake()
        {
            ResolveDamageable();
        }

        private void Reset()
        {
            ResolveDamageable();
        }

        public void SetHitboxType(HitboxType type)
        {
            _hitboxType = type;
        }

        public void SetDamageableTarget(Component target)
        {
            _damageableTarget = target;
            ResolveDamageable();
        }

        private void ResolveDamageable()
        {
            if (_damageableTarget != null && _damageableTarget is IDamageable d)
            {
                _damageable = d;
                return;
            }

            _damageable = GetComponentInParent<IDamageable>();
            if (_damageable is Component c)
            {
                _damageableTarget = c;
            }
        }

        /// <summary>
        /// Nhận sát thương và chuyển tiếp về component IDamageable ở root (Mục 3.1 & 3.2).
        /// </summary>
        public void ForwardDamage(float damage, ulong attackerClientId)
        {
            if (_damageable == null)
            {
                ResolveDamageable();
            }

            if (_damageable != null)
            {
                _damageable.TakeDamage(damage, _hitboxType, attackerClientId);
            }
            else
            {
                Debug.LogWarning($"[HitboxIdentifier] Không tìm thấy IDamageable trên {gameObject.name} hoặc cha của nó!");
            }
        }
    }
}
