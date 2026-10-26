// Script: RangedZombieAI.cs
// Mục đích: Quái Zombie tấn công tầm xa độc lập cho Đợt Z1.
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative). Client nhận VFX qua ClientRpc và NetworkTransform.

using System.Collections;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class RangedZombieAI : EnemyStateMachine
    {
        [Header("Z1 Ranged Settings (25 HP, 12 DMG, 3.5 m/s, 10–20m range)")]
        [SerializeField] private float _minComfortDistance = 10f; // < 10m -> Kiting lùi lại
        [SerializeField] private float _maxCombatDistance = 20f;  // 10-20m -> Cự ly tấn công
        [SerializeField] private float _chargeDuration = 0.45f;    // Thời gian tụ năng lượng (Telegraph) trước khi bắn
        [SerializeField] private LayerMask _obstacleMask = ~0;    // Layer kiểm tra cản tầm nhìn

        [Header("References")]
        [SerializeField] private RangedZombieVisuals _visuals;

        // Runtime states
        private float _nextAttackTime;
        private bool _isCharging;
        private float _chargeStartTime;

        public float MinComfortDistance => _minComfortDistance;
        public float MaxCombatDistance => _maxCombatDistance;
        public float ChargeDuration => _chargeDuration;
        public bool IsCharging => _isCharging;

        protected override void Awake()
        {
            base.Awake();
            _enemyType = EnemyType.HellspawnArcher;
            _moveSpeed = 3.5f;
            _attackDamage = 12f;
            _attackRange = 18f;
            _attackCooldown = 1.6f;
            _detectionRange = 25f; // Tầm phát hiện lớn hơn tầm bắn tối đa để kịp bắt mục tiêu

            if (_visuals == null)
            {
                _visuals = GetComponent<RangedZombieVisuals>();
                if (_visuals == null) _visuals = gameObject.AddComponent<RangedZombieVisuals>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _nextAttackTime = Time.time + 0.5f;
        }

        protected override void UpdateChaseState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                CancelCharge();
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);

            // Kiểm tra Line of Sight an toàn (không bị chặn bởi collider bản thân)
            bool hasLos = CheckLineOfSight(_currentTarget, out _);

            // Nếu người chơi ở cự ly 10–20m và có tầm nhìn -> Vào trạng thái Tấn công (Attack)
            if (distToTarget >= _minComfortDistance && distToTarget <= _maxCombatDistance && hasLos)
            {
                SetState(EnemyState.Attack);
                return;
            }

            // Nếu người chơi quá xa (> 20m) hoặc mất tầm nhìn -> Tiến lại gần
            if (distToTarget > _maxCombatDistance || !hasLos)
            {
                if (_navAgent != null && _navAgent.isOnNavMesh)
                {
                    _navAgent.isStopped = false;
                    _navAgent.speed = _moveSpeed;
                    _navAgent.SetDestination(_currentTarget.position);
                }
            }
            // Nếu người chơi áp sát quá gần (< 10m) -> Lùi lại giữ khoảng cách (Kiting)
            else if (distToTarget < _minComfortDistance)
            {
                Vector3 retreatDir = (transform.position - _currentTarget.position).normalized;
                Vector3 retreatTarget = transform.position + retreatDir * 6f;

                if (_navAgent != null && _navAgent.isOnNavMesh)
                {
                    _navAgent.isStopped = false;
                    _navAgent.speed = _moveSpeed * 1.1f; // Lùi nhanh hơn một chút để giữ khoảng cách
                    _navAgent.SetDestination(retreatTarget);
                }
            }
        }

        protected override void UpdateAttackState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                CancelCharge();
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);
            bool hasLos = CheckLineOfSight(_currentTarget, out _);

            // Nếu người chơi chạy ra ngoài cự ly hoặc chạy sau tường chắn -> Chuyển sang Chase
            if (distToTarget < _minComfortDistance || distToTarget > _maxCombatDistance || !hasLos)
            {
                CancelCharge();
                SetState(EnemyState.Chase);
                return;
            }

            // Dừng di chuyển khi ngắm bắn
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
            }

            // Xoay mặt về phía người chơi
            Vector3 lookDir = (_currentTarget.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            // Quá trình tấn công tầm xa với Telegraph
            HandleRangedAttackCycle();
        }

        private void HandleRangedAttackCycle()
        {
            Vector3 shootOrigin = GetShootOrigin();
            Vector3 targetAimPoint = _currentTarget.position + Vector3.up * 1.0f;

            // 1. Nếu chưa nạp đạn và đã hết cooldown -> Bắt đầu nạp đạn (Charge)
            if (!_isCharging && Time.time >= _nextAttackTime)
            {
                _isCharging = true;
                _chargeStartTime = Time.time;

                // Bật tia ngắm cảnh báo (Telegraph) tới Client
                NotifyTelegraphClientRpc(true, shootOrigin, targetAimPoint);
            }

            // 2. Nếu đang nạp đạn
            if (_isCharging)
            {
                // Cập nhật hướng tia ngắm theo vị trí Player
                NotifyTelegraphClientRpc(true, shootOrigin, targetAimPoint);

                // Khi hết thời gian nạp đạn -> Bắn!
                if (Time.time - _chargeStartTime >= _chargeDuration)
                {
                    _isCharging = false;
                    _nextAttackTime = Time.time + _attackCooldown;

                    NotifyTelegraphClientRpc(false, Vector3.zero, Vector3.zero);
                    ExecuteRangedShot(shootOrigin, targetAimPoint);
                }
            }
        }

        public void InitializeForTest(float detectionRange = 25f)
        {
            _detectionRange = detectionRange;
            _enemyType = EnemyType.HellspawnArcher;
            _moveSpeed = 3.5f;
            _attackDamage = 12f;
            _attackRange = 18f;
            _attackCooldown = 1.6f;
            _chargeDuration = 0.45f;
            _minComfortDistance = 10f;
            _maxCombatDistance = 20f;
        }

        private void ExecuteRangedShot(Vector3 shootOrigin, Vector3 targetAimPoint)
        {
            Physics.SyncTransforms();
            Vector3 shootDir = (targetAimPoint - shootOrigin).normalized;
            Vector3 endPoint = shootOrigin + shootDir * _maxCombatDistance;
            bool hitPlayer = false;

            // Raycast kiểm tra trúng đích Server-Authoritative
            var hits = Physics.RaycastAll(shootOrigin, shootDir, _maxCombatDistance, _obstacleMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Bỏ qua collider của chính quái này
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                endPoint = hit.point;

                // Kiểm tra xem va chạm có phải là Player / IDamageable hay không
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(_attackDamage, HitboxType.Torso, NetworkObjectId);
                    hitPlayer = true;
                }

                // Tia đạn dừng lại ở vật cản đầu tiên (dù là Player hay Tường chắn) -> KHÔNG bắn xuyên tường
                break;
            }

            // Phát hiệu ứng Tracer & Muzzle Flash
            if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                PlayRangedShotFeedbackClientRpc(shootOrigin, endPoint, hitPlayer);
            }
            else if (_visuals != null)
            {
                _visuals.PlayShootVfx(shootOrigin, endPoint, hitPlayer);
            }
        }

        private void CancelCharge()
        {
            if (_isCharging)
            {
                _isCharging = false;
                SendTelegraph(false, Vector3.zero, Vector3.zero);
            }
        }

        private void SendTelegraph(bool active, Vector3 origin, Vector3 target)
        {
            if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NotifyTelegraphClientRpc(active, origin, target);
            }
            else if (_visuals != null)
            {
                _visuals.SetTelegraphActive(active, origin, target);
            }
        }

        protected override void HandleDeath(ulong killerId)
        {
            CancelCharge();
            base.HandleDeath(killerId);
        }

        public Vector3 GetShootOrigin()
        {
            return transform.position + Vector3.up * 1.4f;
        }

        /// <summary>
        /// Kiểm tra Line of Sight chuẩn xác: Bỏ qua các collider của bản thân và xác định có tường chắn hay không.
        /// </summary>
        public bool CheckLineOfSight(Transform target, out RaycastHit blockingHit)
        {
            blockingHit = default;
            if (target == null) return false;

            Physics.SyncTransforms();

            Vector3 eyePos = GetShootOrigin();
            Vector3 targetCenter = target.position + Vector3.up * 1.0f;
            Vector3 dir = targetCenter - eyePos;
            float dist = dir.magnitude;

            float maxRange = _detectionRange > 0.1f ? _detectionRange : 25f;
            if (dist > maxRange) return false;

            var hits = Physics.RaycastAll(eyePos, dir.normalized, dist, _obstacleMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Bỏ qua collider của chính quái
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                // Nếu chạm người chơi hoặc con của người chơi -> Tầm nhìn thông suốt
                if (hit.collider.CompareTag("Player") || hit.transform == target || hit.transform.IsChildOf(target))
                {
                    return true;
                }

                // Nếu chạm vật thể khác (tường, cột, hộp) trước khi tới người chơi -> Bị cản tầm nhìn
                blockingHit = hit;
                return false;
            }

            return true;
        }

        // ================= CLIENT RPCS =================

        [ClientRpc]
        private void NotifyTelegraphClientRpc(bool active, Vector3 origin, Vector3 target)
        {
            if (_visuals != null)
            {
                _visuals.SetTelegraphActive(active, origin, target);
            }
        }

        [ClientRpc]
        private void PlayRangedShotFeedbackClientRpc(Vector3 from, Vector3 to, bool hitPlayer)
        {
            if (_visuals != null)
            {
                _visuals.PlayShootVfx(from, to, hitPlayer);
            }
        }

        // ================= TEST SUPPORT =================
        public void SetTargetForTest(Transform target, Health targetHealth)
        {
            _currentTarget = target;
            _currentTargetHealth = targetHealth;
        }

        public void ForceExecuteShotForTest(Vector3 targetPos)
        {
            Vector3 origin = GetShootOrigin();
            ExecuteRangedShot(origin, targetPos);
        }
    }
}
