// Script: PlayerRevive.cs
// Mục đích: Quản lý cơ chế tương tác Cứu đồng đội (Revive) khi gục (Mục 3.3).
// Môi trường thực thi: Cả hai (Client gửi input giữ phím E, Server xác thực cự ly 2m và thời gian giữ 3s).

using System;
using Hellfire.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class PlayerRevive : NetworkBehaviour
    {
        [Header("Revive Settings (Mục 3.3)")]
        [Tooltip("Khoảng cách tối đa để có thể cứu đồng đội (mét)")]
        [SerializeField] private float _reviveDistance = 2.0f;

        [Tooltip("Thời gian giữ phím tương tác để hoàn tất cứu (giây)")]
        [SerializeField] private float _reviveHoldDuration = 3.0f;

        private Health _myHealth;
        private Health _currentReviveTarget;
        private float _currentReviveProgress;
        private bool _isHoldingRevive;

        // Events for UI HUD
        public event Action<string> OnRevivePromptChanged;              // "Giữ [E] để cứu Player #X"
        public event Action<float, float> OnReviveProgressChanged;       // current, max

        public Health MyHealth => _myHealth;

        private void Awake()
        {
            _myHealth = GetComponent<Health>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Nếu chính mình đang gục hoặc chết -> Không thể đi cứu ai khác (Mục 3.3)
            if (_myHealth != null && (_myHealth.IsDowned.Value || _myHealth.IsDead.Value))
            {
                if (_isHoldingRevive)
                {
                    CancelRevive();
                }
                OnRevivePromptChanged?.Invoke(null);
                OnReviveProgressChanged?.Invoke(0f, _reviveHoldDuration);
                return;
            }

            FindDownedTeammateNearby();
            HandleReviveInput();
        }

        private void FindDownedTeammateNearby()
        {
            if (_isHoldingRevive) return;

            Health nearestDowned = null;
            float nearestDist = _reviveDistance;

            var allHealths = FindObjectsByType<Health>(FindObjectsInactive.Exclude);
            foreach (var h in allHealths)
            {
                if (h == null || h == _myHealth || !h.IsPlayer) continue;

                if (h.IsDowned.Value && !h.IsDead.Value)
                {
                    float dist = Vector3.Distance(transform.position, h.transform.position);
                    if (dist <= nearestDist)
                    {
                        nearestDist = dist;
                        nearestDowned = h;
                    }
                }
            }

            _currentReviveTarget = nearestDowned;

            if (_currentReviveTarget != null)
            {
                OnRevivePromptChanged?.Invoke("Giữ [E] để cứu đồng đội");
            }
            else
            {
                OnRevivePromptChanged?.Invoke(null);
            }
        }

        private void HandleReviveInput()
        {
            if (_currentReviveTarget == null)
            {
                if (_isHoldingRevive) CancelRevive();
                return;
            }

            bool ePressed = Keyboard.current != null && Keyboard.current.eKey.isPressed;

            if (ePressed)
            {
                if (!_isHoldingRevive)
                {
                    _isHoldingRevive = true;
                    _currentReviveProgress = 0f;
                    StartReviveServerRpc(_currentReviveTarget.NetworkObjectId);
                }

                _currentReviveProgress += Time.deltaTime;
                OnReviveProgressChanged?.Invoke(_currentReviveProgress, _reviveHoldDuration);

                if (_currentReviveProgress >= _reviveHoldDuration)
                {
                    // Hoàn tất cứu: Gửi yêu cầu Server khôi phục máu
                    CompleteReviveServerRpc(_currentReviveTarget.NetworkObjectId);
                    _isHoldingRevive = false;
                    _currentReviveProgress = 0f;
                    OnReviveProgressChanged?.Invoke(0f, _reviveHoldDuration);
                }
            }
            else
            {
                if (_isHoldingRevive)
                {
                    CancelRevive();
                }
            }
        }

        private void CancelRevive()
        {
            _isHoldingRevive = false;
            _currentReviveProgress = 0f;
            OnReviveProgressChanged?.Invoke(0f, _reviveHoldDuration);

            if (_currentReviveTarget != null)
            {
                CancelReviveServerRpc(_currentReviveTarget.NetworkObjectId);
            }
        }

        // ================= SERVER RPC VALIDATION & TIMING =================

        private float _serverReviveStartTime;
        private ulong _serverReviveTargetId;

        [Rpc(SendTo.Server)]
        private void StartReviveServerRpc(ulong targetNetworkId)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out var targetNetObj))
            {
                return;
            }

            var targetHealth = targetNetObj.GetComponent<Health>();
            if (targetHealth == null || !targetHealth.IsDowned.Value || targetHealth.IsDead.Value)
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (dist > _reviveDistance + 1.0f)
            {
                return;
            }

            _serverReviveTargetId = targetNetworkId;
            _serverReviveStartTime = Time.time;
            Debug.Log($"[PlayerRevive] Server: Bắt đầu theo dõi quá trình cứu của Client {OwnerClientId} cho mục tiêu {targetNetworkId}");
        }

        [Rpc(SendTo.Server)]
        public void CompleteReviveServerRpc(ulong targetNetworkId)
        {
            if (_serverReviveTargetId != targetNetworkId)
            {
                Debug.LogWarning($"[PlayerRevive] Server: Từ chối hoàn tất cứu vì mục tiêu không khớp {_serverReviveTargetId} != {targetNetworkId}");
                return;
            }

            float heldTime = Time.time - _serverReviveStartTime;
            if (heldTime < _reviveHoldDuration - 0.25f)
            {
                Debug.LogWarning($"[PlayerRevive] Server: Từ chối cứu vì thời gian giữ chưa đủ ({heldTime:F2}s / {_reviveHoldDuration}s)");
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out var targetNetObj))
            {
                return;
            }

            var targetHealth = targetNetObj.GetComponent<Health>();
            if (targetHealth == null || !targetHealth.IsDowned.Value || targetHealth.IsDead.Value)
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (dist <= _reviveDistance + 1.0f)
            {
                targetHealth.ServerRevive();
                _serverReviveTargetId = 0;
                _serverReviveStartTime = 0f;
                Debug.Log($"[PlayerRevive] Server: Cứu thành công mục tiêu {targetNetworkId} bởi Client {OwnerClientId}");
            }
        }

        [Rpc(SendTo.Server)]
        private void CancelReviveServerRpc(ulong targetNetworkId)
        {
            if (_serverReviveTargetId == targetNetworkId)
            {
                _serverReviveTargetId = 0;
                _serverReviveStartTime = 0f;
            }
            Debug.Log($"[PlayerRevive] Client {OwnerClientId} đã hủy cứu người chơi {targetNetworkId}");
        }
    }
}
