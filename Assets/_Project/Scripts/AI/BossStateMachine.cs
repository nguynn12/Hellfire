// Script: BossStateMachine.cs
// Mục đích: Chúa tể Địa ngục (Hellfire Lord) - 800 HP, 3 trạng thái FSM (Phase1 -> PhaseTransition -> Phase2) (Mục 5.2 đặc tả kỹ thuật).
// Môi trường thực thi: CHỈ TRÊN SERVER (Server-Authoritative).

using System.Collections;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.AI
{
    public enum BossPhase
    {
        Phase1,
        PhaseTransition,
        Phase2
    }

    [DisallowMultipleComponent]
    public class BossStateMachine : EnemyStateMachine
    {
        [Header("Boss Identity & Phases (Mục 5.2)")]
        [SerializeField] private BossPhase _currentPhase = BossPhase.Phase1;
        [SerializeField] private float _phase1Speed = 3.5f;
        [SerializeField] private float _phase2Speed = 4.5f;
        [SerializeField] private float _phase1MeleeDamage = 30f;
        [SerializeField] private float _phase2FireballDamage = 20f;
        [SerializeField] private float _fireballExplosionRadius = 3f;

        [Header("Boss Reward & Victory (Mục 6.3 & 10.2)")]
        [SerializeField] private GameObject _bossChestPrefab;

        [Header("Minion Summoning (Mỗi 20% máu mất ở Phase 2 triệu hồi 2 Imp)")]
        [SerializeField] private GameObject _impMinionPrefab;
        [SerializeField] private Vector3 _bossRoomCenter;

        private bool _hasTransitioned = false;
        private float _lastSummonHealthThreshold;
        private float _phase2RangedTimer;
        private bool _isPhase2RangedMode = false;

        public BossPhase CurrentPhase => _currentPhase;

        protected override void Awake()
        {
            base.Awake();
            _enemyType = EnemyType.BossHellfireLord;
            _moveSpeed = _phase1Speed;
            _attackDamage = _phase1MeleeDamage;
            _attackRange = 4.0f;
            _attackCooldown = 1.8f;
            _detectionRange = 25f;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                _bossRoomCenter = transform.position;
                _lastSummonHealthThreshold = 400f; // Bắt đầu theo dõi ngưỡng mất 20% từ mốc 50% (400 HP)
            }
        }

        protected override void Update()
        {
            if (!IsServer || CurrentState == EnemyState.Dead) return;

            // Nếu đang trong PhaseTransition thì không chạy logic thường (đang đợi coroutine chuyển phase)
            if (_currentPhase == BossPhase.PhaseTransition)
            {
                return;
            }

            // Quản lý việc chuyển đổi giữa cận chiến và tầm xa ở Phase 2 (60% melee, 40% ranged)
            if (_currentPhase == BossPhase.Phase2)
            {
                _phase2RangedTimer += Time.deltaTime;
                if (!_isPhase2RangedMode && _phase2RangedTimer >= 6.0f) // 6s cận chiến
                {
                    _isPhase2RangedMode = true;
                    _phase2RangedTimer = 0f;
                    _attackRange = 16f;
                }
                else if (_isPhase2RangedMode && _phase2RangedTimer >= 4.0f) // 4s tầm xa
                {
                    _isPhase2RangedMode = false;
                    _phase2RangedTimer = 0f;
                    _attackRange = 4.0f;
                }
            }

            base.Update();
        }

        protected override void HandleDamageTaken(float dmg, HitboxType hitbox, ulong attackerId)
        {
            base.HandleDamageTaken(dmg, hitbox, attackerId);

            if (!IsServer || CurrentState == EnemyState.Dead) return;

            float currentHp = _health.CurrentHealth.Value;
            float maxHp = _health.MaxHealth;

            // 1. Kiểm tra kích hoạt PhaseTransition khi máu giảm xuống <= 50% lần đầu tiên (Mục 5.2)
            if (!_hasTransitioned && currentHp <= maxHp * 0.5f)
            {
                StartCoroutine(PhaseTransitionRoutine());
                return;
            }

            // 2. Trong Phase 2: Cứ mỗi 20% máu tối đa mất đi (160 HP), triệu hồi 2 Imp (Mục 5.2)
            if (_currentPhase == BossPhase.Phase2)
            {
                float damageSinceLastSummon = _lastSummonHealthThreshold - currentHp;
                float summonInterval = maxHp * 0.20f; // 800 * 0.2 = 160 HP

                if (damageSinceLastSummon >= summonInterval)
                {
                    _lastSummonHealthThreshold = currentHp;
                    SummonImpMinions(2);
                }
            }
        }

        private IEnumerator PhaseTransitionRoutine()
        {
            _hasTransitioned = true;
            _currentPhase = BossPhase.PhaseTransition;

            // BẮT BUỘC: Bật cờ miễn sát thương trong 2 giây (Mục 5.2 đặc tả)
            _health.IsInvulnerable = true;

            // Dừng NavMeshAgent và di chuyển về giữa phòng
            if (_navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = false;
                _navAgent.speed = 6.0f;
                _navAgent.SetDestination(_bossRoomCenter);
            }

            // Báo hiệu cho các Client biết boss đang chuyển phase (gầm thét / hiệu ứng)
            NotifyPhaseTransitionClientRpc();

            yield return new WaitForSeconds(2.0f);

            // Kết thúc 2 giây bất tử: Tắt cờ miễn sát thương và chuyển sang Phase 2
            _health.IsInvulnerable = false;
            _currentPhase = BossPhase.Phase2;
            _moveSpeed = _phase2Speed;
            _navAgent.speed = _phase2Speed;

            NotifyPhase2StartedClientRpc();

            SetState(EnemyState.Chase);
        }

        [ClientRpc]
        private void NotifyPhaseTransitionClientRpc()
        {
            StartCoroutine(FlashMaterialRoutine(Color.yellow, 2.0f));
        }

        [ClientRpc]
        private void NotifyPhase2StartedClientRpc()
        {
            StartCoroutine(FlashMaterialRoutine(Color.red, 0.5f));
        }

        protected override void ExecuteAttack()
        {
            if (_currentPhase == BossPhase.Phase1 || !_isPhase2RangedMode)
            {
                // Đòn cận chiến quét rộng AOE 4m (Mục 5.2: trúng nhiều người chơi cùng lúc)
                ExecuteMeleeSwipeAOE();
            }
            else
            {
                // Đòn tầm xa quả cầu lửa AOE 3m (Mục 5.2: 20 sát thương)
                ExecuteFireballRangedAOE();
            }
        }

        private void ExecuteMeleeSwipeAOE()
        {
            PlayAttackFeedbackClientRpc();

            // Quét vùng trước mặt trong bán kính 4m
            Collider[] hitColliders = Physics.OverlapSphere(transform.position + transform.forward * 2f, 2.5f);
            foreach (var col in hitColliders)
            {
                var playerHealth = col.GetComponentInParent<Health>();
                if (playerHealth != null && IsTargetAliveAndValid(playerHealth))
                {
                    playerHealth.TakeDamage(_phase1MeleeDamage, HitboxType.Torso, NetworkObjectId);
                }
            }
        }

        private void ExecuteFireballRangedAOE()
        {
            if (_currentTarget == null) return;

            Vector3 targetPos = _currentTarget.position;
            PlayFireballFeedbackClientRpc(targetPos);

            // Nổ AOE tại vị trí người chơi sau 0.4s
            StartCoroutine(FireballExplosionRoutine(targetPos, 0.4f));
        }

        private IEnumerator FireballExplosionRoutine(Vector3 center, float delay)
        {
            yield return new WaitForSeconds(delay);

            Collider[] hitColliders = Physics.OverlapSphere(center, _fireballExplosionRadius);
            foreach (var col in hitColliders)
            {
                var playerHealth = col.GetComponentInParent<Health>();
                if (playerHealth != null && IsTargetAliveAndValid(playerHealth))
                {
                    playerHealth.TakeDamage(_phase2FireballDamage, HitboxType.Torso, NetworkObjectId);
                }
            }
        }

        [ClientRpc]
        private void PlayFireballFeedbackClientRpc(Vector3 targetPos)
        {
            // Hiệu ứng quả cầu lửa cho Client
            StartCoroutine(FireballVisualRoutine(transform.position + Vector3.up * 2f, targetPos));
        }

        private IEnumerator FireballVisualRoutine(Vector3 from, Vector3 to)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = from;
            sphere.transform.localScale = Vector3.one * 0.8f;
            sphere.GetComponent<Renderer>().material.color = Color.red;
            Destroy(sphere.GetComponent<Collider>());

            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                sphere.transform.position = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            Destroy(sphere);
        }

        private void SummonImpMinions(int count)
        {
            if (_impMinionPrefab == null || !IsServer) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnOffset = new Vector3((i == 0 ? 3f : -3f), 0.5f, 3f);
                Vector3 spawnPos = transform.position + spawnOffset;

                var imp = Instantiate(_impMinionPrefab, spawnPos, Quaternion.identity);
                var netObj = imp.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
            }
        }

        protected override void HandleDeath(ulong killerId)
        {
            base.HandleDeath(killerId);

            if (!IsServer) return;

            // 1. Sinh Rương Boss (Mục 6.3: 100% rơi bùa lợi, 50% rơi thêm vũ khí)
            if (_bossChestPrefab != null)
            {
                Vector3 chestPos = transform.position + transform.forward * 2f;
                var chestObj = Instantiate(_bossChestPrefab, chestPos, Quaternion.identity);
                var netObj = chestObj.GetComponent<NetworkObject>();
                var chest = chestObj.GetComponent<Hellfire.Items.LootChest>();
                if (chest != null)
                {
                    chest.IsBossChest = true;
                }
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                Debug.Log($"[BossStateMachine] Chúa quỷ bị hạ gục! Đã sinh Rương Boss tại {chestPos}");
            }

            // 2. Kích hoạt trạng thái Victory trên GameManager (Mục 10.2)
            if (Hellfire.Networking.GameManager.Instance != null)
            {
                Hellfire.Networking.GameManager.Instance.TriggerVictory();
            }
        }
    }
}
