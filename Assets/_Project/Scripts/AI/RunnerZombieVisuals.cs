using System.Collections;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    /// <summary>
    /// Đồng bộ hoạt ảnh (Animator) và hiệu ứng hình ảnh (VFX/Hit Flash) cho Runner Zombie.
    /// Hoạt động tương thích với EnemyStateMachine mà không can thiệp vào logic của đồng đội.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class RunnerZombieVisuals : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator _animator;
        [SerializeField] private float _runSpeedThreshold = 0.2f;

        [Header("Renderers & Hit Flash")]
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Color _hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float _flashDuration = 0.12f;

        private Health _health;
        private NavMeshAgent _navAgent;
        private EnemyStateMachine _esm;
        private Vector3 _lastPosition;
        private bool _isDead;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private void Awake()
        {
            _health = GetComponent<Health>();
            _navAgent = GetComponent<NavMeshAgent>();
            _esm = GetComponent<EnemyStateMachine>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken += HandleDamageTaken;
                _health.OnDied += HandleDied;
            }
            _lastPosition = transform.position;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
                _health.OnDied -= HandleDied;
            }
        }

        private void Update()
        {
            if (_isDead || _animator == null) return;

            // Tính toán tốc độ di chuyển thực tế (áp dụng mượt cho cả Server và Client)
            float speed = 0f;
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                speed = _navAgent.velocity.magnitude;
            }
            else
            {
                float distMoved = Vector3.Distance(transform.position, _lastPosition);
                speed = (Time.deltaTime > 0f) ? (distMoved / Time.deltaTime) : 0f;
            }
            _lastPosition = transform.position;

            _animator.SetFloat(SpeedHash, speed);

            // Kiểm tra trạng thái tấn công từ EnemyStateMachine nếu có
            if (_esm != null && _esm.CurrentState == EnemyState.Attack)
            {
                _animator.SetTrigger(AttackHash);
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitbox, ulong attackerId)
        {
            if (_isDead) return;

            if (_animator != null)
            {
                _animator.SetTrigger(HitHash);
            }

            StartCoroutine(FlashRoutine());
        }

        private void HandleDied(ulong killerId)
        {
            if (_isDead) return;
            _isDead = true;

            if (_animator != null)
            {
                _animator.SetTrigger(DieHash);
            }

            // Tắt va chạm và di chuyển khi chết
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
                _navAgent.enabled = false;
            }
        }

        private IEnumerator FlashRoutine()
        {
            if (_renderers == null || _renderers.Length == 0) yield break;

            foreach (var r in _renderers)
            {
                var mat = Application.isPlaying ? r.material : r.sharedMaterial;
                if (r != null && mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", _hitFlashColor * 2f);
                }
            }

            yield return new WaitForSeconds(_flashDuration);

            foreach (var r in _renderers)
            {
                var mat = Application.isPlaying ? r.material : r.sharedMaterial;
                if (r != null && mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }
}
