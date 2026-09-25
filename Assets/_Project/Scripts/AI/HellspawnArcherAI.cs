// Script: HellspawnArcherAI.cs
// Mục đích: Cung thủ địa ngục (Hellspawn Archer) - Tấn công tầm xa hitscan, giữ khoảng cách 10–20m (Mục 5.1 & 6.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER.

using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class HellspawnArcherAI : EnemyStateMachine
    {
        [Header("Archer Specific Settings (Mục 6.2: 25 HP, 12 DMG, 3.5 m/s, 10–20m range)")]
        [SerializeField] private float _minComfortDistance = 10f; // Nếu người chơi gần hơn 10m thì lùi lại
        [SerializeField] private float _maxCombatDistance = 20f;  // Tầm xa tối đa để bắn
        [SerializeField] private LineRenderer _tracerRenderer;

        protected override void Awake()
        {
            base.Awake();
            _enemyType = EnemyType.HellspawnArcher;
            _moveSpeed = 3.5f;
            _attackDamage = 12f;
            _attackRange = 18f;
            _attackCooldown = 1.6f;
        }

        protected override void UpdateChaseState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);

            // Nếu người chơi ở trong cự ly 10–20m và có tầm nhìn -> Vào Attack
            if (distToTarget >= _minComfortDistance && distToTarget <= _maxCombatDistance && HasLineOfSightTo(_currentTarget))
            {
                SetState(EnemyState.Attack);
                return;
            }

            // Nếu người chơi chạy quá xa (> 20m) -> Tiến lại gần
            if (distToTarget > _maxCombatDistance)
            {
                if (_navAgent.isOnNavMesh)
                {
                    _navAgent.isStopped = false;
                    _navAgent.SetDestination(_currentTarget.position);
                }
            }
            // Nếu người chơi quá gần (< 10m) -> Lùi ra xa (kite)
            else if (distToTarget < _minComfortDistance)
            {
                Vector3 retreatDir = (transform.position - _currentTarget.position).normalized;
                Vector3 retreatTarget = transform.position + retreatDir * 5f;

                if (_navAgent.isOnNavMesh)
                {
                    _navAgent.isStopped = false;
                    _navAgent.SetDestination(retreatTarget);
                }
            }
        }

        protected override void UpdateAttackState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);

            // Nếu người chơi áp sát < 10m hoặc chạy mất tầm nhìn -> chuyển lại Chase để di chuyển
            if (distToTarget < _minComfortDistance || distToTarget > _maxCombatDistance || !HasLineOfSightTo(_currentTarget))
            {
                SetState(EnemyState.Chase);
                return;
            }

            // Xoay mặt về phía người chơi
            Vector3 lookDir = (_currentTarget.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            // Bắn hitscan theo cooldown
            if (Time.time - _lastAttackTime >= _attackCooldown)
            {
                _lastAttackTime = Time.time;
                ExecuteRangedHitscanAttack();
            }
        }

        private float _lastAttackTime;

        private void ExecuteRangedHitscanAttack()
        {
            if (_currentTarget == null) return;

            Vector3 shootOrigin = transform.position + Vector3.up * 1.5f;
            Vector3 shootTarget = _currentTarget.position + Vector3.up * 1.0f;
            Vector3 shootDir = (shootTarget - shootOrigin).normalized;

            // Server tự raycast độc lập (Mục 5.1: không qua ServerRpc vì quái vật là Server)
            if (Physics.Raycast(shootOrigin, shootDir, out RaycastHit hit, _maxCombatDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                var playerHealth = hit.collider.GetComponentInParent<Health>();
                if (playerHealth != null && IsTargetAliveAndValid(playerHealth))
                {
                    playerHealth.TakeDamage(_attackDamage, HitboxType.Torso, NetworkObjectId);
                }

                SpawnRangedTracerClientRpc(shootOrigin, hit.point);
            }
            else
            {
                SpawnRangedTracerClientRpc(shootOrigin, shootOrigin + shootDir * _maxCombatDistance);
            }
        }

        [ClientRpc]
        private void SpawnRangedTracerClientRpc(Vector3 from, Vector3 to)
        {
            StartCoroutine(ArcherTracerRoutine(from, to));
        }

        private System.Collections.IEnumerator ArcherTracerRoutine(Vector3 from, Vector3 to)
        {
            var tracerObj = new GameObject("ArcherTracer");
            var lr = tracerObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.06f;
            lr.endWidth = 0.03f;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.magenta;
            lr.endColor = Color.red;

            yield return new WaitForSeconds(0.08f);
            Destroy(tracerObj);
        }
    }
}
