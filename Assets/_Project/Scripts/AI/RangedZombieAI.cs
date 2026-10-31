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
        [SerializeField] private float _chargeDuration = 0.80f;   // Đồng bộ với 0.80s vung tay ném của Zombie@attack
        [SerializeField] private LayerMask _obstacleMask = ~0;    // Layer kiểm tra cản tầm nhìn

        [Header("Hand Bone & Projectile")]
        [SerializeField] private Transform _launchHandTransform;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _projectileSpeed = 20f;

        [Header("References")]
        [SerializeField] private RangedZombieVisuals _visuals;
        [SerializeField] private ZombieInvestigation _investigation;
        [SerializeField] private ZombieHearing _hearing;
        [SerializeField] private ZombieGroupAlert _groupAlert;
        [SerializeField] private ZombieHordeRage _hordeRage;

        // Runtime states
        private float _nextAttackTime;
        private bool _isCharging;
        private float _chargeStartTime;
        private float _rageCooldownMultiplier = 1.0f;

        public float MinComfortDistance => _minComfortDistance;
        public float MaxCombatDistance => _maxCombatDistance;
        public float ChargeDuration => _chargeDuration;
        public bool IsCharging => _isCharging;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public Transform LaunchHandTransform => _launchHandTransform;
        public ZombieInvestigation Investigation => _investigation;
        public ZombieHearing Hearing => _hearing;
        public ZombieGroupAlert GroupAlert => _groupAlert;
        public ZombieHordeRage HordeRage => _hordeRage;
        public float RageCooldownMultiplier => _rageCooldownMultiplier;

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

            if (_launchHandTransform == null)
            {
                var transforms = GetComponentsInChildren<Transform>();
                foreach (var t in transforms)
                {
                    if (t.name == "Bip01 R Hand" || t.name == "R Hand")
                    {
                        _launchHandTransform = t;
                        break;
                    }
                }
            }

            if (_investigation == null) _investigation = GetComponent<ZombieInvestigation>();
            if (_hearing == null) _hearing = GetComponent<ZombieHearing>();
            if (_groupAlert == null) _groupAlert = GetComponent<ZombieGroupAlert>();
            if (_hordeRage == null) _hordeRage = GetComponent<ZombieHordeRage>();
        }

        protected override void SetState(EnemyState newState)
        {
            if (_navAgent == null)
            {
                _navAgent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
            }

            base.SetState(newState);

            // Khi Zombie chuyển sang trạng thái chiến đấu (Chase, Attack, Dead), lập tức hủy bỏ điều tra
            if (newState != EnemyState.Idle && _investigation != null && _investigation.IsInvestigating)
            {
                _investigation.CancelInvestigation();
            }

            // Kích hoạt Group Alert khi vào Chase hoặc Attack nếu có mục tiêu hợp lệ
            if ((newState == EnemyState.Attack || newState == EnemyState.Chase) && _currentTarget != null && _groupAlert != null)
            {
                _groupAlert.BroadcastAlert(_currentTarget.position, _currentTarget);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _nextAttackTime = Time.time + 0.5f;
        }

        protected override void UpdateIdleState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                SearchNearestLivingTarget();
            }

            if (_currentTarget != null && CheckLineOfSight(_currentTarget, out _))
            {
                float distToTarget = Vector3.Distance(transform.position, _currentTarget.position);
                if (distToTarget >= _minComfortDistance && distToTarget <= _maxCombatDistance)
                {
                    SetState(EnemyState.Attack);
                }
                else
                {
                    SetState(EnemyState.Chase);
                }
            }
        }

        protected override void UpdateChaseState()
        {
            if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
            {
                SearchNearestLivingTarget();
                if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
                {
                    CancelCharge();
                    _currentTarget = null;
                    SetState(EnemyState.Idle);
                    return;
                }
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
                SearchNearestLivingTarget();
                if (_currentTarget == null || !IsTargetAliveAndValid(_currentTargetHealth))
                {
                    CancelCharge();
                    _currentTarget = null;
                    SetState(EnemyState.Idle);
                    return;
                }
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

        private void SearchNearestLivingTarget()
        {
            float closestDistSqr = _detectionRange * _detectionRange;
            Transform bestTarget = null;
            Health bestHealth = null;

            // 1. Quét ConnectedClientsList từ NetworkManager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var health = client.PlayerObject.GetComponent<Health>();
                        if (IsTargetAliveAndValid(health))
                        {
                            float distSqr = (client.PlayerObject.transform.position - transform.position).sqrMagnitude;
                            if (distSqr < closestDistSqr && CheckLineOfSight(client.PlayerObject.transform, out _))
                            {
                                closestDistSqr = distSqr;
                                bestTarget = client.PlayerObject.transform;
                                bestHealth = health;
                            }
                        }
                    }
                }
            }

            // 2. Dự phòng (Fallback) cho Scene Test hoặc khi PlayerObject chưa gán: Quét đối tượng có tag "Player"
            if (bestTarget == null)
            {
                var playerObjects = GameObject.FindGameObjectsWithTag("Player");
                foreach (var pObj in playerObjects)
                {
                    var health = pObj.GetComponent<Health>() ?? pObj.GetComponentInParent<Health>();
                    if (IsTargetAliveAndValid(health))
                    {
                        float distSqr = (pObj.transform.position - transform.position).sqrMagnitude;
                        if (distSqr < closestDistSqr && CheckLineOfSight(pObj.transform, out _))
                        {
                            closestDistSqr = distSqr;
                            bestTarget = pObj.transform;
                            bestHealth = health;
                        }
                    }
                }
            }

            if (bestTarget != null)
            {
                _currentTarget = bestTarget;
                _currentTargetHealth = bestHealth;
                if (IsDebugLoggingEnabled())
                {
                    float dist = Vector3.Distance(transform.position, _currentTarget.position);
                    Debug.Log($"[ArcherDebug] Zombie phát hiện Player: {_currentTarget.name} tại cự ly {dist:F1}m (LoS: True).");
                }
            }
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

                if (IsDebugLoggingEnabled())
                {
                    Debug.Log($"[ArcherDebug] Bắt đầu Attack Telegraph ({_chargeDuration:F2}s) nhắm vào {_currentTarget.name}.");
                }

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
                    _nextAttackTime = Time.time + (_attackCooldown * _rageCooldownMultiplier);

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
            _chargeDuration = 0.80f;
            _minComfortDistance = 10f;
            _maxCombatDistance = 20f;
            _projectileSpeed = 18f;
        }

        public void SetProjectilePrefabForTest(GameObject prefab)
        {
            _projectilePrefab = prefab;
        }

        private void ExecuteRangedShot(Vector3 shootOrigin, Vector3 targetAimPoint)
        {
            Physics.SyncTransforms();
            Vector3 shootDir = (targetAimPoint - shootOrigin).normalized;

            // Spawn cách vị trí bàn tay 0.35m theo hướng bắn để không chạm phải collider cánh tay/bàn tay
            Vector3 spawnPos = shootOrigin + shootDir * 0.35f;

            // 1. Phóng Projectile Server-Authoritative (tính va chạm và sát thương 12 DMG trên Server)
#if UNITY_EDITOR
            if (_projectilePrefab == null)
            {
                _projectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectile_Hellfire_Orb.prefab");
            }
#endif
            if (_projectilePrefab != null)
            {
                var projObj = Instantiate(_projectilePrefab, spawnPos, Quaternion.LookRotation(shootDir));
                var proj = projObj.GetComponent<ArcherProjectile>();
                if (proj != null)
                {
                    bool isServerAuth = !IsSpawned || IsServer;
                    proj.Initialize(shootDir, _projectileSpeed, _attackDamage, NetworkObjectId, transform, isServerAuth);

                    if (IsDebugLoggingEnabled())
                    {
                        string handName = _launchHandTransform != null ? _launchHandTransform.name : "Root";
                        Debug.Log($"[ArcherDebug] Projectile Hellfire Orb đã spawn tại {handName} (pos: {spawnPos}) hướng về {_currentTarget?.name} với vận tốc {_projectileSpeed}m/s.");
                    }

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        int safetyMaxSteps = 300;
                        while (!proj.HasHit && proj != null && safetyMaxSteps-- > 0)
                        {
                            proj.SimulateStep(0.05f);
                        }
                    }
#endif
                }
            }

            // 2. Đồng bộ đạn bay trực quan tới các Client trong Multiplayer
            if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                PlayProjectileLaunchClientRpc(spawnPos, shootDir, _projectileSpeed);
            }

            // 3. Hiệu ứng lóe sáng tại bàn tay khi phóng (Muzzle Flash)
            if (_visuals != null)
            {
                _visuals.PlayShootVfx(shootOrigin, shootOrigin + shootDir * 1.5f, false);
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
            if (_launchHandTransform != null)
            {
                return _launchHandTransform.position;
            }
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
        private void PlayProjectileLaunchClientRpc(Vector3 origin, Vector3 dir, float speed)
        {
            if (IsServer) return; // Server already spawned the authoritative projectile
            if (_visuals != null)
            {
                _visuals.SpawnClientProjectile(origin, dir, speed);
                _visuals.PlayShootVfx(origin, origin + dir * 1.5f, false);
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

        public static bool IsDebugLoggingEnabled()
        {
#if UNITY_EDITOR
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return activeScene.name == "TestArcherAI" || activeScene.name == "TestCombatJuice" || activeScene.name == "TestZombieHearing" || activeScene.name == "TestZombieGroupAI";
#else
            return false;
#endif
        }

        // ================= Z3 HORDE RAGE & GROUP ALERT SUPPORT =================
        public void SetRageAttackCooldownMultiplier(float multiplier)
        {
            _rageCooldownMultiplier = Mathf.Clamp(multiplier, 0.5f, 1.0f);
        }

        public void EngageSpottedPlayer(Transform target)
        {
            if (target == null) return;
            _currentTarget = target;
            _currentTargetHealth = target.GetComponent<Health>() ?? target.GetComponentInParent<Health>();
            SetState(EnemyState.Chase);
        }

        public bool HasLineOfSightForTest(Transform target)
        {
            if (target == null) return false;
            return CheckLineOfSight(target, out _);
        }
    }
}
