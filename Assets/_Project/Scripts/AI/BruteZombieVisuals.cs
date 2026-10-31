using System.Collections;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    /// <summary>
    /// Đồng bộ hoạt ảnh (Animator), bước chân nặng (Screen Shake) và hiệu ứng thị giác cho Brute Zombie.
    /// Tương thích hoàn toàn với BruteAI mà không sửa code của đồng đội.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BruteZombieVisuals : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator _animator;

        [Header("Renderers & Hit Flash")]
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Color _hitFlashColor = new Color(1f, 0.4f, 0f, 1f); // Cam đậm phát sáng
        [SerializeField] private float _flashDuration = 0.15f;

        [Header("Footstep & Impact")]
        [SerializeField] private float _stepShakeRadius = 8f;

        private Health _health;
        private NavMeshAgent _navAgent;
        private BruteAI _bruteAI;
        private Vector3 _lastPosition;
        private bool _isDead;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private void Awake()
        {
            _health = GetComponent<Health>();
            _navAgent = GetComponent<NavMeshAgent>();
            _bruteAI = GetComponent<BruteAI>();

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

            if (_bruteAI != null && _bruteAI.CurrentState == EnemyState.Attack)
            {
                _animator.SetTrigger(AttackHash);
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitbox, ulong attackerId)
        {
            if (_isDead) return;
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
                    mat.SetColor("_EmissionColor", _hitFlashColor * 2.5f);
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
