// Script: ZombieInvestigation.cs
// Mục đích: Điều khiển hành vi Điều tra (Investigate) khi Zombie nghe thấy tiếng súng.
// Môi trường: Server-Authoritative (Server điều khiển NavMeshAgent di chuyển, tìm kiếm 3-5s, hoặc chuyển sang Chase/Attack).

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    public enum InvestigationState
    {
        None,           // Bình thường (Idle/Chase/Attack)
        MovingToNoise,  // Đang di chuyển bằng NavMeshAgent tới vị trí tiếng động
        SearchingArea   // Đã tới vị trí, dừng lại và quan sát tìm kiếm trong 3-5 giây
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieInvestigation : MonoBehaviour
    {
        [Header("Investigation Settings")]
        [Tooltip("Tốc độ di chuyển khi đi điều tra tiếng súng (m/s)")]
        [SerializeField] private float _investigateSpeed = 3.2f;

        [Tooltip("Thời gian tìm kiếm quan sát tại vị trí tiếng động (3–5 giây theo Mục Giai Đoạn 2)")]
        [SerializeField] private float _searchDuration = 4.0f;

        [Tooltip("Khoảng cách coi như đã tới đích tiếng động (m)")]
        [SerializeField] private float _arrivalTolerance = 1.2f;

        [Tooltip("Khoảng cách tiếng động mới tối thiểu để cập nhật đường đi (chống giật/jitter)")]
        [SerializeField] private float _repathThreshold = 2.0f;

        [Tooltip("Khoảng thời gian tối thiểu giữa 2 lần cập nhật đích đến khi nghe tiếng súng liên tục")]
        [SerializeField] private float _repathCooldown = 0.35f;

        [Header("References")]
        [SerializeField] private NavMeshAgent _navAgent;
        [SerializeField] private Health _health;
        [SerializeField] private RangedZombieAI _rangedAI;
        [SerializeField] private ZombieHearing _hearing;

        // Runtime states
        private InvestigationState _state = InvestigationState.None;
        private Vector3 _investigationTarget;
        private Vector3 _originPosition;
        private float _searchTimer;
        private float _lastRepathTime;
        private float _searchBaseYAngle;

        public InvestigationState State => _state;
        public bool IsInvestigating => _state != InvestigationState.None;
        public Vector3 InvestigationTarget => _investigationTarget;
        public float SearchDuration
        {
            get => _searchDuration;
            set => _searchDuration = Mathf.Clamp(value, 3.0f, 5.0f);
        }

        public event Action<InvestigationState> OnInvestigationStateChanged;

        private void Awake()
        {
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_health == null) _health = GetComponent<Health>();
            if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();
            if (_hearing == null) _hearing = GetComponent<ZombieHearing>();

            _originPosition = transform.position;
        }

        private void Start()
        {
            _originPosition = transform.position;
        }

        private void Update()
        {
            // BẮT BUỘC: Logic AI điều tra chỉ chạy duy nhất trên Server
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            // Nếu Zombie đã chết, hủy bỏ điều tra ngay lập tức
            if (_health != null && _health.IsDead.Value)
            {
                if (_state != InvestigationState.None)
                {
                    CancelInvestigation();
                }
                return;
            }

            if (_state == InvestigationState.None) return;

            // KIỂM TRA TẦM NHÌN (GIAI ĐOẠN 2 - BƯỚC 4):
            // Nếu nhìn thấy Player hợp lệ trong khi điều tra -> Chuyển ngay sang Chase/Attack!
            if (CheckAndEngageVisiblePlayer())
            {
                return;
            }

            switch (_state)
            {
                case InvestigationState.MovingToNoise:
                    TickMovingToNoise();
                    break;

                case InvestigationState.SearchingArea:
                    TickSearchingArea();
                    break;
            }
        }

        /// <summary>
        /// Kích hoạt khi ZombieHearing phát hiện tiếng động hợp lệ trong bán kính nghe.
        /// </summary>
        public void OnNoiseHeard(Vector3 noisePosition, Transform noiseSource)
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_rangedAI == null) _rangedAI = GetComponent<RangedZombieAI>();

            // Nếu đã chết -> Bỏ qua
            if (_health != null && _health.IsDead != null && _health.IsSpawned && _health.IsDead.Value) return;

            // Nếu Zombie đang trong trạng thái Tấn công hoặc Đang bám theo người chơi nhìn thấy được:
            // Line of Sight trực quan luôn được ưu tiên hơn nghe tiếng động
            if (_rangedAI != null && _rangedAI.CurrentState == EnemyState.Attack)
            {
                return;
            }

            // Chống giật (Jitter / Stutter): Nếu đang di chuyển tới tiếng súng và tiếng súng mới quá gần điểm cũ
            if (_state == InvestigationState.MovingToNoise)
            {
                float distToCurrentGoal = Vector3.Distance(_investigationTarget, noisePosition);
                if (distToCurrentGoal < _repathThreshold && Time.time - _lastRepathTime < _repathCooldown)
                {
                    // Giữ nguyên đà di chuyển, không ngắt quãng path
                    return;
                }
            }

            // Ghi nhận vị trí phát ra tiếng động & chuyển sang Investigate
            _investigationTarget = noisePosition;
            _lastRepathTime = Time.time;
            SetState(InvestigationState.MovingToNoise);

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = false;
                _navAgent.speed = _investigateSpeed;
                _navAgent.SetDestination(noisePosition);
            }

            if (RangedZombieAI.IsDebugLoggingEnabled())
            {
                Debug.Log($"[ZombieInvestigation] Zombie '{name}' chuyển sang trạng thái Investigate -> Di chuyển tới {noisePosition}.");
            }
        }

        private void TickMovingToNoise()
        {
            if (_navAgent == null || !_navAgent.isOnNavMesh) return;

            // Kiểm tra xem đã tới vị trí tiếng động chưa
            if (!_navAgent.pathPending && _navAgent.remainingDistance <= _arrivalTolerance)
            {
                // Bước 5: Chuyển sang tìm kiếm tại chỗ trong 3–5 giây
                SetState(InvestigationState.SearchingArea);
                _searchTimer = _searchDuration;
                _searchBaseYAngle = transform.eulerAngles.y;

                _navAgent.isStopped = true;
                _navAgent.ResetPath();

                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    Debug.Log($"[ZombieInvestigation] Zombie '{name}' đã tới vị trí tiếng động. Bắt đầu tìm kiếm trong {_searchDuration:F1}s.");
                }
            }
        }

        private void TickSearchingArea()
        {
            _searchTimer -= Time.deltaTime;

            // Xoay nhẹ đầu/thân qua lại quan sát khu vực (Tạo cảm giác Zombie đang dáo dác tìm kiếm)
            float lookOffset = Mathf.Sin(Time.time * 2.2f) * 50f;
            Quaternion targetRot = Quaternion.Euler(0f, _searchBaseYAngle + lookOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);

            // Bước 6: Hết 3–5 giây mà không thấy ai -> Quay lại trạng thái Idle ban đầu
            if (_searchTimer <= 0f)
            {
                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    Debug.Log($"[ZombieInvestigation] Zombie '{name}' không tìm thấy ai sau {_searchDuration:F1}s. Kết thúc điều tra, quay về trạng thái Idle.");
                }

                FinishInvestigation();
            }
        }

        /// <summary>
        /// Quét tìm người chơi xung quanh. Nếu có người chơi nhìn thấy được (LoS = True), lập tức hủy điều tra và tấn công.
        /// </summary>
        private bool CheckAndEngageVisiblePlayer()
        {
            Transform spottedPlayer = null;
            Health spottedHealth = null;

            // 1. Quét ConnectedClients từ NetworkManager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.PlayerObject != null)
                    {
                        var hp = client.PlayerObject.GetComponent<Health>();
                        if (hp != null && !hp.IsDead.Value && !hp.IsDowned.Value)
                        {
                            if (HasLineOfSight(client.PlayerObject.transform))
                            {
                                spottedPlayer = client.PlayerObject.transform;
                                spottedHealth = hp;
                                break;
                            }
                        }
                    }
                }
            }

            // 2. Dự phòng trong scene test
            if (spottedPlayer == null)
            {
                var playerObjs = GameObject.FindGameObjectsWithTag("Player");
                foreach (var pObj in playerObjs)
                {
                    var hp = pObj.GetComponent<Health>() ?? pObj.GetComponentInParent<Health>();
                    if (hp != null && !hp.IsDead.Value && !hp.IsDowned.Value)
                    {
                        if (HasLineOfSight(pObj.transform))
                        {
                            spottedPlayer = pObj.transform;
                            spottedHealth = hp;
                            break;
                        }
                    }
                }
            }

            if (spottedPlayer != null)
            {
                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    Debug.Log($"[ZombieInvestigation] Zombie '{name}' phát hiện Player '{spottedPlayer.name}' trong khi điều tra! Chuyển ngay sang Chase/Attack.");
                }

                // Chuyển giao mục tiêu cho RangedZombieAI
                if (_rangedAI != null)
                {
                    _rangedAI.SetTargetForTest(spottedPlayer, spottedHealth);
                }

                CancelInvestigation();
                return true;
            }

            return false;
        }

        private bool HasLineOfSight(Transform target)
        {
            if (_rangedAI != null)
            {
                return _rangedAI.CheckLineOfSight(target, out _);
            }

            Vector3 eyePos = transform.position + Vector3.up * 1.4f;
            Vector3 targetPos = target.position + Vector3.up * 1.0f;
            Vector3 dir = targetPos - eyePos;
            float dist = dir.magnitude;

            if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag("Player") || hit.transform == target || hit.transform.IsChildOf(target))
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        public void CancelInvestigation()
        {
            SetState(InvestigationState.None);
        }

        public void FinishInvestigation()
        {
            SetState(InvestigationState.None);
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
                _navAgent.ResetPath();
            }
        }

        private void SetState(InvestigationState newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnInvestigationStateChanged?.Invoke(_state);
        }

        public void ForceStartInvestigationForTest(Vector3 targetPos)
        {
            OnNoiseHeard(targetPos, null);
        }

        public void ForceTriggerSearchStateForTest()
        {
            SetState(InvestigationState.SearchingArea);
            _searchTimer = _searchDuration;
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
            }
        }
    }
}
