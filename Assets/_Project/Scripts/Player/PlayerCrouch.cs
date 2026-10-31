// Script: PlayerCrouch.cs
// Mục đích: Quản lý tính năng Ngồi (Crouch), hạ thấp Collider & Camera, kiểm tra trần nhà (Ceiling Clearance) để chống đứng xuyên trần (Giai đoạn P1).
// Môi trường thực thi: Cả hai (Client dự đoán cho IsOwner, NetworkVariable đồng bộ tư thế cho remote client).

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCrouch : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerSlide _playerSlide;

        [Header("Crouch Configuration")]
        [Tooltip("Chiều cao Collider khi đứng (m)")]
        [SerializeField] private float _standingHeight = 1.8f;

        [Tooltip("Chiều cao Collider khi ngồi (m)")]
        [SerializeField] private float _crouchHeight = 1.0f;

        [Tooltip("Tọa độ Y của CameraPivot khi đứng (m)")]
        [SerializeField] private float _cameraStandingY = 1.6f;

        [Tooltip("Tọa độ Y của CameraPivot khi ngồi (m)")]
        [SerializeField] private float _cameraCrouchY = 0.85f;

        [Tooltip("Tốc độ chuyển đổi giữa đứng và ngồi (mượt mà)")]
        [SerializeField] private float _crouchTransitionSpeed = 10f;

        [Tooltip("Tốc độ di chuyển tối đa khi ngồi (m/s)")]
        [SerializeField] private float _crouchSpeed = 2.0f;

        [Header("Overhead Obstacle Check")]
        [Tooltip("LayerMask vật cản trần nhà (tránh đứng dậy khi đang chui hầm)")]
        [SerializeField] private LayerMask _ceilingCheckLayers = ~0;

        // Đồng bộ mạng tư thế ngồi
        public NetworkVariable<bool> IsCrouchingNet { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        // Trạng thái cục bộ
        private bool _isCrouching;

        // Events
        public event Action<bool> OnCrouchChanged;

        // Properties
        public bool IsCrouching => IsSpawned ? IsCrouchingNet.Value : _isCrouching;
        public float CrouchSpeed => _crouchSpeed;
        public float StandingHeight => _standingHeight;
        public float CrouchHeight => _crouchHeight;
        public float CameraStandingY => _cameraStandingY;
        public float CameraCrouchY => _cameraCrouchY;

        private void Reset()
        {
            _standingHeight = 1.8f;
            _crouchHeight = 1.0f;
            _cameraStandingY = 1.6f;
            _cameraCrouchY = 0.85f;
            _crouchTransitionSpeed = 10f;
            _crouchSpeed = 2.0f;
            _ceilingCheckLayers = ~0;
            AutoResolveReferences();
        }

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void AutoResolveReferences()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_playerSlide == null) _playerSlide = GetComponent<PlayerSlide>();
            if (_cameraPivot == null) _cameraPivot = transform.Find("CameraPivot");
            if (_ceilingCheckLayers.value == 0) _ceilingCheckLayers = ~0;
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
        }

        private void Update()
        {
            // Chỉ nhận Input và áp dụng vật lý trên máy Owner hoặc test offline
            if (IsSpawned && !IsOwner)
            {
                ApplyRemoteVisualHeights();
                return;
            }

            HandleInput();
            UpdateHeights();
            ApplyCrouchSpeedLimit();
        }

        private void HandleInput()
        {
            // Nếu đang Slide thì PlayerSlide đang kiểm soát tư thế
            if (_playerSlide != null && _playerSlide.IsSliding)
            {
                return;
            }

            if (Keyboard.current == null) return;

            bool ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;

            if (ctrlPressed)
            {
                SetCrouching(true);
            }
            else
            {
                // Khi người chơi nhả phím: Chỉ đứng lên nếu trên đầu không có trần cản
                if (_isCrouching && HasCeilingClearance())
                {
                    SetCrouching(false);
                }
            }
        }

        public void SetCrouching(bool crouch)
        {
            if (_isCrouching == crouch) return;

            _isCrouching = crouch;
            if (IsSpawned && IsOwner)
            {
                IsCrouchingNet.Value = crouch;
            }

            OnCrouchChanged?.Invoke(crouch);
        }

        /// <summary>
        /// Kiểm tra trần nhà bằng SphereCast xem có đủ khoảng trống chiều cao 1.8m để đứng dậy không.
        /// </summary>
        public bool HasCeilingClearance()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            float standingHeight = _standingHeight > 0f ? _standingHeight : 1.8f;
            float crouchHeight = _crouchHeight > 0f ? _crouchHeight : 1.0f;
            float radius = _characterController != null ? _characterController.radius * 0.85f : 0.3f;
            LayerMask mask = _ceilingCheckLayers.value != 0 ? _ceilingCheckLayers : ~0;

            Vector3 castOrigin = transform.position + Vector3.up * (crouchHeight - radius);
            float castDistance = standingHeight - crouchHeight + 0.1f;

            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, radius, Vector3.up, castDistance, mask, QueryTriggerInteraction.Ignore);
            foreach (var hitInfo in hits)
            {
                if (hitInfo.collider == null || hitInfo.collider == _characterController || hitInfo.collider.gameObject == gameObject) continue;
                if (hitInfo.collider.transform.IsChildOf(transform)) continue;
                return false;
            }

            return true;
        }

        private void UpdateHeights()
        {
            if (_characterController == null) return;

            // Nếu đang Slide thì PlayerSlide sẽ cập nhật độ cao Slide (0.8m)
            if (_playerSlide != null && _playerSlide.IsSliding) return;

            float targetHeight = _isCrouching ? _crouchHeight : _standingHeight;
            float targetCameraY = _isCrouching ? _cameraCrouchY : _cameraStandingY;

            // 1. Cập nhật CharacterController Height & Center mượt mà
            float currentHeight = _characterController.height;
            float newHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * _crouchTransitionSpeed);
            _characterController.height = newHeight;
            _characterController.center = new Vector3(0f, newHeight * 0.5f, 0f);

            // 2. Cập nhật vị trí CameraPivot Y mượt mà
            if (_cameraPivot != null)
            {
                Vector3 camPos = _cameraPivot.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * _crouchTransitionSpeed);
                _cameraPivot.localPosition = camPos;
            }
        }

        private void ApplyRemoteVisualHeights()
        {
            if (_cameraPivot == null) return;

            bool sliding = _playerSlide != null && _playerSlide.IsSliding;
            if (sliding) return;

            float targetCameraY = IsCrouchingNet.Value ? _cameraCrouchY : _cameraStandingY;
            Vector3 camPos = _cameraPivot.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * _crouchTransitionSpeed);
            _cameraPivot.localPosition = camPos;
        }

        /// <summary>
        /// Bù trừ tốc độ di chuyển khi đang ngồi để không vượt quá _crouchSpeed (2.0 m/s), ưu tiên dùng hook ExternalSpeedMultiplier của PlayerMovement.
        /// </summary>
        private void ApplyCrouchSpeedLimit()
        {
            if (_playerMovement != null)
            {
                bool shouldSlow = _isCrouching && !(_playerSlide != null && _playerSlide.IsSliding);
                _playerMovement.ExternalSpeedMultiplier = shouldSlow
                    ? (_crouchSpeed / Mathf.Max(0.1f, _playerMovement.WalkSpeed))
                    : 1.0f;
                return;
            }

            if (!_isCrouching) return;
            if (_playerSlide != null && _playerSlide.IsSliding) return;
            if (_characterController == null || !_characterController.enabled) return;
            if (Keyboard.current == null) return;

            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;

            if (moveInput.sqrMagnitude < 0.01f) return;
            moveInput = moveInput.normalized;
            Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

            float baseWalkSpeed = _playerMovement != null ? _playerMovement.WalkSpeed : 4.5f;
            if (baseWalkSpeed > _crouchSpeed)
            {
                float diff = baseWalkSpeed - _crouchSpeed;
                _characterController.Move(-moveDir * (diff * Time.deltaTime));
            }
        }
    }
}
