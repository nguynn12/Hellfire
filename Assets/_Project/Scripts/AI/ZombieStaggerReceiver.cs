using System.Collections;
using Hellfire.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    /// <summary>
    /// Component quản lý phản ứng khựng/bị đẩy lùi (Stagger & Knockback) của Zombie khi nhận sát thương lớn hoặc Headshot.
    /// Gián đoạn tạm thời đòn tấn công và bước di chuyển của AI trong thời gian ngắn (0.4s - 0.6s) để người chơi có khoảng trống chiến thuật.
    /// Có Cooldown chống lạm dụng stun-lock liên tục.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class ZombieStaggerReceiver : MonoBehaviour
    {
        [Header("Stagger Thresholds")]
        [Tooltip("Sát thương tối thiểu để kích hoạt khựng người")]
        [SerializeField] private float _damageStaggerThreshold = 18f;

        [Tooltip("Thời gian khựng/tê liệt đòn đánh (giây)")]
        [SerializeField] private float _staggerDuration = 0.5f;

        [Tooltip("Lực đẩy lùi về phía sau (m)")]
        [SerializeField] private float _knockbackDistance = 0.85f;

        [Tooltip("Thời gian hồi giữa 2 lần bị khựng (giây) chống lạm dụng")]
        [SerializeField] private float _staggerCooldown = 1.6f;

        private Health _health;
        private NavMeshAgent _navAgent;
        private EnemyStateMachine _enemyFSM;
        private float _lastStaggerTime = -10f;
        private bool _isStaggered = false;

        public bool IsStaggered => _isStaggered;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _navAgent = GetComponent<NavMeshAgent>();
            _enemyFSM = GetComponent<EnemyStateMachine>();
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
            if (_isStaggered) return;
            if (Time.time < _lastStaggerTime + _staggerCooldown) return;
            if (_health != null && _health.IsDead.Value) return;

            // Kích hoạt khi bị Headshot hoặc sát thương vượt ngưỡng (Shotgun / Heavy damage)
            bool shouldStagger = (hitbox == HitboxType.Head) || (damage >= _damageStaggerThreshold);
            if (!shouldStagger) return;

            // Xác định hướng bị bắn để đẩy lùi về phía sau
            Vector3 pushDirection = -transform.forward;
            GameObject attacker = GameObject.FindWithTag("Player");
            if (attacker != null)
            {
                pushDirection = (transform.position - attacker.transform.position).normalized;
                pushDirection.y = 0f;
            }

            StartCoroutine(ExecuteStaggerRoutine(pushDirection));
        }

        private IEnumerator ExecuteStaggerRoutine(Vector3 knockbackDir)
        {
            _isStaggered = true;
            _lastStaggerTime = Time.time;

            // 1. Tạm dừng di chuyển NavMesh
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
                _navAgent.velocity = Vector3.zero;
            }

            // 2. Đẩy lùi nhẹ bằng nội suy vị trí mượt mà
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + knockbackDir * _knockbackDistance;

            // Kiểm tra NavMesh SamplePosition để không bị đẩy lọt ra ngoài map
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, _knockbackDistance + 0.5f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            float elapsed = 0f;
            float pushTime = 0.18f;
            while (elapsed < pushTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / pushTime);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // 3. Giữ trạng thái khựng trong thời gian còn lại
            float remainingStagger = Mathf.Max(0.05f, _staggerDuration - pushTime);
            yield return new WaitForSeconds(remainingStagger);

            // 4. Phục hồi di chuyển AI
            if (_navAgent != null && _navAgent.isOnNavMesh && _health != null && !_health.IsDead.Value)
            {
                _navAgent.isStopped = false;
            }

            _isStaggered = false;
        }
    }
}

