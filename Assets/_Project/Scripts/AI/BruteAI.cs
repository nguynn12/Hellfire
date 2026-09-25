// Script: BruteAI.cs
// Mục đích: Quỷ khổng lồ (Brute) - Tank cận chiến 120 HP, đòn đánh 25 DMG gây choáng ngắn (Mục 5.1 & 6.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER.

using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class BruteAI : EnemyStateMachine
    {
        [Header("Brute Settings (Mục 6.2: 120 HP, 25 DMG, 2.5 m/s)")]
        [SerializeField] private float _staggerDuration = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            _enemyType = EnemyType.Brute;
            _moveSpeed = 2.5f;
            _attackDamage = 25f;
            _attackRange = 2.8f;
            _attackCooldown = 2.0f;
            _detectionRange = 16f;
        }

        protected override void ExecuteAttack()
        {
            if (_currentTargetHealth != null && IsTargetAliveAndValid(_currentTargetHealth))
            {
                // Sát thương 25 DMG
                _currentTargetHealth.TakeDamage(_attackDamage, HitboxType.Torso, NetworkObjectId);

                // Phát hiệu ứng đòn đánh nặng (rung chấn / stagger) cho client
                PlayHeavySmashFeedbackClientRpc(transform.position + transform.forward * 1.5f);
            }
        }

        [ClientRpc]
        private void PlayHeavySmashFeedbackClientRpc(Vector3 impactPoint)
        {
            StartCoroutine(FlashMaterialRoutine(Color.black, 0.2f));
        }
    }
}
