// Script: EnemyStateMachine.cs
// Mục đích: Máy trạng thái hữu hạn (FSM) điều khiển AI quái vật (Mục 5.1 & 6.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative). Client chỉ nhận vị trí qua NetworkTransform.

using System.Collections;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NetworkObject))]
    public class EnemyStateMachine : NetworkBehaviour
    {
        [Header("Enemy Identity & Stats (Mục 6.2)")]
        [SerializeField] protected EnemyType _enemyType = EnemyType.Imp;
        [SerializeField] protected float _moveSpeed = 5f;
        [SerializeField] protected float _attackDamage = 10f;
        [SerializeField] protected float _attackRange = 2f;
        [SerializeField] protected float _attackCooldown = 1.0f;
        [SerializeField] protected float _detectionRange = 15f;

        [Header("Line of Sight & Targeting")]
        [SerializeField] protected LayerMask _sightObstacleMask = ~0; // Layer kiểm tra vật cản tầm nhìn
        [SerializeField] protected float _targetSearchInterval = 0.5f;

        [Header("Visual & Feedback")]
        [SerializeField] protected Transform _meshRoot;
        [SerializeField] protected Renderer[] _renderers;

        protected NavMeshAgent _navAgent;
        protected Health _health;
        protected Transform _currentTarget;
        protected Health _currentTargetHealth;

        private EnemyState _currentState = EnemyState.Idle;
        private float _lastTargetSearchTime;
        private float _lastAttackTime;
        private Vector3 _spawnPosition;

        public EnemyState CurrentState => _currentState;
        public EnemyType Type => _enemyType;

        protected virtual void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _spawnPosition = transform.position;
                _navAgent.speed = _moveSpeed;
                _health.OnDied += HandleDeath;
                _health.OnDamageTaken += HandleDamageTaken;

                SetState(EnemyState.Idle);
            }
            else
            {
                // Trên Client: vô hiệu hóa NavMeshAgent để tránh xung đột với NetworkTransform
                _navAgent.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                _health.OnDied -= HandleDeath;
                _health.OnDamageTaken -= HandleDamageTaken;
            }
        }

        protected virtual void Update()
        {
            // BẮT BUỘC: Logic AI chỉ chạy duy nhất trên Server
            if (!IsServer || _currentState == EnemyState.Dead)
            {
                return;
            }

            // Định kỳ quét tìm người chơi gần nhất còn sống (mục 5.1: mỗi 0.5s, không tính mỗi frame)
            if (Time.time - _lastTargetSearchTime >= _targetSearchInterval)
            {
                _lastTargetSearchTime = Time.time;
                SearchNearestLivingPlayer();
            }

            switch (_currentState)
            {
                case EnemyState.Idle:
                    UpdateIdleState();
                    break;
                case EnemyState.Chase:
                    UpdateChaseState();
                    break;
                case EnemyState.Attack:
                    UpdateAttackState();
                    break;
            }
        }

        protected virtual void SetState(EnemyState newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;

            switch (_currentState)
            {
                case EnemyState.Idle:
                    if (_navAgent.isOnNavMesh)
                    {
                        _navAgent.isStopped = true;
                        _navAgent.ResetPath();
                    }
                    break;

                case EnemyState.Chase:
                    if (_navAgent.isOnNavMesh)
                    {
                        _navAgent.isStopped = false;
                        _navAgent.speed = _moveSpeed;
                    }
                    break;

                case EnemyState.Attack:
                    if (_navAgent.isOnNavMesh)
                    {
                        _navAgent.isStopped = true;
                    }
                    break;

                case EnemyState.Dead:
                    if (_navAgent.isOnNavMesh)
                    {
                        _navAgent.isStopped = true;
                        _navAgent.enabled = false;
                    }
                    break;
            }
        }

        protected virtual void UpdateIdleState()
        {
            if (_currentTarget != null && HasLineOfSightTo(_currentTarget))
            {
                SetState(EnemyState.Chase);
            }
        }

        protected virtual void UpdateChaseState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);

            if (distToTarget <= _attackRange)
            {
                SetState(EnemyState.Attack);
                return;
            }

            if (_navAgent.isOnNavMesh)
            {
                _navAgent.SetDestination(_currentTarget.position);
            }
        }

        protected virtual void UpdateAttackState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                _currentTarget = null;
                SetState(EnemyState.Idle);
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);

            // Nếu người chơi chạy ra khỏi tầm tấn công -> quay lại Chase
            if (distToTarget > _attackRange * 1.25f)
            {
                SetState(EnemyState.Chase);
                return;
            }

            // Xoay mặt về phía người chơi
            Vector3 lookDir = (_currentTarget.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
            }

            // Thực hiện đòn đánh theo nhịp cooldown
            if (Time.time - _lastAttackTime >= _attackCooldown)
            {
                _lastAttackTime = Time.time;
                ExecuteAttack();
            }
        }

        protected virtual void ExecuteAttack()
        {
            if (_currentTargetHealth != null && IsTargetAliveAndValid(_currentTargetHealth))
            {
                // Gây sát thương cận chiến Server-Authoritative lên người chơi
                _currentTargetHealth.TakeDamage(_attackDamage, HitboxType.Torso, NetworkObjectId);

                // Phát hiệu ứng đòn đánh cho client
                PlayAttackFeedbackClientRpc();
            }
        }

        [ClientRpc]
        protected void PlayAttackFeedbackClientRpc()
        {
            // Hiệu ứng đòn đánh ngắn hạn
            StartCoroutine(FlashMaterialRoutine(Color.yellow, 0.1f));
        }

        protected virtual void HandleDamageTaken(float dmg, HitboxType hitbox, ulong attackerId)
        {
            // Khi bị bắn trúng: dù chưa thấy cũng lập tức chuyển sang Chase và nhắm vào người bắn (Mục 5.1)
            if (_currentState != EnemyState.Dead && NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(attackerId, out var client))
                {
                    if (client.PlayerObject != null)
                    {
                        var health = client.PlayerObject.GetComponent<Health>();
                        if (IsTargetAliveAndValid(health))
                        {
                            _currentTarget = client.PlayerObject.transform;
                            _currentTargetHealth = health;
                            SetState(EnemyState.Chase);
                        }
                    }
                }
            }

            PlayDamageFeedbackClientRpc();
        }

        [ClientRpc]
        private void PlayDamageFeedbackClientRpc()
        {
            StartCoroutine(FlashMaterialRoutine(Color.red, 0.12f));
        }

        protected IEnumerator FlashMaterialRoutine(Color flashColor, float duration)
        {
            if (_renderers == null) yield break;

            var origColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    origColors[i] = _renderers[i].material.color;
                    _renderers[i].material.color = flashColor;
                }
            }

            yield return new WaitForSeconds(duration);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    _renderers[i].material.color = origColors[i];
                }
            }
        }

        protected virtual void HandleDeath(ulong killerId)
        {
            SetState(EnemyState.Dead);
            PlayDeathFeedbackClientRpc();

            // Tự hủy sau 3 giây
            StartCoroutine(DespawnRoutine(3.0f));
        }

        [ClientRpc]
        private void PlayDeathFeedbackClientRpc()
        {
            if (_meshRoot != null)
            {
                _meshRoot.localRotation = Quaternion.Euler(75f, 0f, 0f);
            }
            else
            {
                transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            }
        }

        private IEnumerator DespawnRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>
        /// Quét tìm người chơi gần nhất còn sống và không ở trạng thái Downed (Mục 5.1).
        /// </summary>
        protected void SearchNearestLivingPlayer()
        {
            if (NetworkManager.Singleton == null) return;

            float closestDistSqr = _detectionRange * _detectionRange;
            Transform bestTarget = null;
            Health bestTargetHealth = null;

            Vector3 currentPos = transform.position;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;

                var health = client.PlayerObject.GetComponent<Health>();
                if (!IsTargetAliveAndValid(health)) continue;

                Transform playerTrans = client.PlayerObject.transform;
                Vector3 toPlayer = playerTrans.position - currentPos;
                float distSqr = toPlayer.sqrMagnitude;

                if (distSqr < closestDistSqr)
                {
                    if (HasLineOfSightTo(playerTrans))
                    {
                        closestDistSqr = distSqr;
                        bestTarget = playerTrans;
                        bestTargetHealth = health;
                    }
                }
            }

            if (bestTarget != null)
            {
                _currentTarget = bestTarget;
                _currentTargetHealth = bestTargetHealth;
            }
        }

        protected bool HasLineOfSightTo(Transform target)
        {
            if (target == null) return false;

            Vector3 eyePos = transform.position + Vector3.up * 1.2f;
            Vector3 targetCenter = target.position + Vector3.up * 1.0f;
            Vector3 dir = targetCenter - eyePos;
            float dist = dir.magnitude;

            if (dist > _detectionRange) return false;

            // Raycast kiểm tra vật cản tường (không nhìn xuyên tường)
            if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, _sightObstacleMask, QueryTriggerInteraction.Ignore))
            {
                // Nếu va chạm không phải là Player thì bị cản bởi tường
                if (!hit.collider.CompareTag("Player") && hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    return false;
                }
            }

            return true;
        }

        protected bool IsTargetAliveAndValid(Health targetHealth)
        {
            if (targetHealth == null) return false;
            // Mục tiêu phải còn sống và không trong trạng thái gục (Downed)
            return !targetHealth.IsDead.Value && !targetHealth.IsDowned.Value;
        }
    }
}
