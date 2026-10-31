// Script: PlayerCrouchSlide.cs
// Mục đích: Quản lý tính năng Ngồi (Crouch) và Trượt (Slide) độc lập với PlayerMovement (Giai đoạn P1).
// Môi trường thực thi: Cả hai (Client-side cho IsOwner, NetworkVariable đồng bộ tư thế cho Client khác).

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCrouchSlide : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerStamina _playerStamina;

        [Header("Crouch Configuration")]
        [Tooltip("Chiều cao Collider khi đứng (m)")]
        [SerializeField] private float _standingHeight = 1.8f;

        [Tooltip("Chiều cao Collider khi ngồi (m)")]
        [SerializeField] private float _crouchHeight = 1.0f;

        [Tooltip("Tọa độ Y của CameraPivot khi đứng")]
        [SerializeField] private float _cameraStandingY = 1.6f;

        [Tooltip("Tọa độ Y của CameraPivot khi ngồi")]
        [SerializeField] private float _cameraCrouchY = 0.85f;

        [Tooltip("Tốc độ chuyển đổi giữa đứng và ngồi (mượt mà)")]
        [SerializeField] private float _crouchTransitionSpeed = 10f;

        [Tooltip("Tốc độ di chuyển tối đa khi ngồi (m/s)")]
        [SerializeField] private float _crouchSpeed = 2.0f;

        [Header("Slide Configuration")]
        [Tooltip("Tốc độ ban đầu khi bắt đầu Slide (m/s)")]
        [SerializeField] private float _slideInitialSpeed = 9.5f;

        [Tooltip("Thời gian trượt tối đa (giây)")]
        [SerializeField] private float _slideDuration = 0.85f;

        [Tooltip("Thời gian hồi giữa các lần Slide (giây)")]
        [SerializeField] private float _slideCooldown = 0.8f;

        [Tooltip("Chiều cao Collider khi trượt (m)")]
        [SerializeField] private float _slideHeight = 0.8f;

        [Tooltip("Tọa độ Y của CameraPivot khi trượt")]
        [SerializeField] private float _cameraSlideY = 0.7f;

        [Header("Overhead Obstacle Check")]
        [Tooltip("LayerMask vật cản trần nhà (tránh đứng dậy khi đang chui hầm)")]
        [SerializeField] private LayerMask _ceilingCheckLayers = ~0;

        // Network synchronized state
        public NetworkVariable<bool> IsCrouchingNet { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public NetworkVariable<bool> IsSlidingNet { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        // Local state
        private bool _isCrouching;
        private bool _isSliding;
        private float _slideTimer;
        private float _lastSlideEndTime;
        private Vector3 _slideDirection;
        private float _currentSlideSpeed;

        // Events
        public event Action<bool> OnCrouchChanged;
        public event Action OnSlideStarted;
        public event Action OnSlideEnded;

        // Properties
        public bool IsCrouching => IsSpawned ? IsCrouchingNet.Value : _isCrouching;
        public bool IsSliding => IsSpawned ? IsSlidingNet.Value : _isSliding;
        public float CrouchSpeed => _crouchSpeed;
        public float SlideDuration => _slideDuration;

        private void Reset()
        {
            AutoResolveReferences();
        }

        private void Awake()
        {
            AutoResolveReferences();
            _lastSlideEndTime = -_slideCooldown;
        }

        private void AutoResolveReferences()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_playerStamina == null) _playerStamina = GetComponent<PlayerStamina>();
            if (_cameraPivot == null) _cameraPivot = transform.Find("CameraPivot");
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
        }

        private void Update()
        {
            // Chỉ xử lý Input và vật lý cho Owner hoặc test offline
            if (IsSpawned && !IsOwner)
            {
                // Trên remote client: chỉ cập nhật tư thế mượt mà theo NetworkVariable
                ApplyRemoteVisualHeights();
                return;
            }

            HandleInput();
            UpdateSlidePhysics();
            UpdateHeights();
            ApplySpeedAdjustments();
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            bool ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            bool ctrlDownThisFrame = Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame;
            bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;

            // Đọc hướng di chuyển
            bool isMovingForward = Keyboard.current.wKey.isPressed;

            // Kiểm tra kích hoạt Slide: Đang Sprint tiến tới + Nhấn phím Crouch + Có thể lực
            if (ctrlDownThisFrame && shiftPressed && isMovingForward && !_isSliding)
            {
                if (CanStartSlide())
                {
                    StartSlide();
                    return;
                }
            }

            // Xử lý Crouch thông thường khi không Slide
            if (!_isSliding)
            {
                if (ctrlPressed)
                {
                    SetCrouching(true);
                }
                else
                {
                    // Người chơi thả phím Crouch: chỉ đứng lên nếu không có trần cản trở
                    if (_isCrouching && HasCeilingClearance())
                    {
                        SetCrouching(false);
                    }
                }
            }
        }

        private bool CanStartSlide()
        {
            if (Time.time < _lastSlideEndTime + _slideCooldown) return false;
            if (_characterController != null && !_characterController.isGrounded) return false;
            if (_playerStamina != null && !_playerStamina.CanSlide) return false;

            return true;
        }

        private void StartSlide()
        {
            if (_playerStamina != null && !_playerStamina.TryConsumeSlideCost())
            {
                return;
            }

            _isSliding = true;
            _isCrouching = true;
            _slideTimer = _slideDuration;
            _currentSlideSpeed = _slideInitialSpeed;
            _slideDirection = transform.forward;

            if (IsSpawned && IsOwner)
            {
                IsSlidingNet.Value = true;
                IsCrouchingNet.Value = true;
            }

            OnSlideStarted?.Invoke();
            OnCrouchChanged?.Invoke(true);
        }

        private void StopSlide()
        {
            _isSliding = false;
            _lastSlideEndTime = Time.time;

            if (IsSpawned && IsOwner)
            {
                IsSlidingNet.Value = false;
            }

            OnSlideEnded?.Invoke();

            // Khi Slide kết thúc: kiểm tra xem người chơi còn giữ phím Ctrl không
            bool ctrlPressed = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);
            if (!ctrlPressed && HasCeilingClearance())
            {
                SetCrouching(false);
            }
        }

        private void SetCrouching(bool crouch)
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
        /// Kiểm tra trần nhà bằng SphereCast xem có đủ khoảng trống 1.8m để đứng dậy không.
        /// </summary>
        public bool HasCeilingClearance()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_characterController == null) return true;

            float radius = _characterController.radius * 0.85f;
            Vector3 castOrigin = transform.position + Vector3.up * (_crouchHeight - radius);
            float castDistance = _standingHeight - _crouchHeight + 0.15f;

            RaycastHit[] hits = Physics.SphereCastAll(castOrigin, radius, Vector3.up, castDistance, _ceilingCheckLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider == _characterController || hit.collider.gameObject == gameObject) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                return false; // Phát hiện vật cản trần phía trên
            }

            return true;
        }

        private void UpdateSlidePhysics()
        {
            if (!_isSliding) return;

            _slideTimer -= Time.deltaTime;
            if (_slideTimer <= 0f)
            {
                StopSlide();
                return;
            }

            // Tốc độ giảm dần tuyến tính theo thời gian trượt
            float progress = 1f - (_slideTimer / _slideDuration);
            _currentSlideSpeed = Mathf.Lerp(_slideInitialSpeed, _crouchSpeed, progress);

            // Đẩy CharacterController theo hướng slide
            if (_characterController != null && _characterController.enabled)
            {
                Vector3 slideVelocity = _slideDirection * _currentSlideSpeed;
                _characterController.Move(slideVelocity * Time.deltaTime);
            }
        }

        private void UpdateHeights()
        {
            if (_characterController == null) return;

            float targetHeight = _standingHeight;
            float targetCameraY = _cameraStandingY;

            if (_isSliding)
            {
                targetHeight = _slideHeight;
                targetCameraY = _cameraSlideY;
            }
            else if (_isCrouching)
            {
                targetHeight = _crouchHeight;
                targetCameraY = _cameraCrouchY;
            }

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

            float targetCameraY = _cameraStandingY;
            if (IsSlidingNet.Value) targetCameraY = _cameraSlideY;
            else if (IsCrouchingNet.Value) targetCameraY = _cameraCrouchY;

            Vector3 camPos = _cameraPivot.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * _crouchTransitionSpeed);
            _cameraPivot.localPosition = camPos;
        }

        /// <summary>
        /// Điều chỉnh tốc độ di chuyển:
        /// - Khi Crouch: bù trừ để tốc độ thực tế bằng _crouchSpeed (thay vì _walkSpeed 4.5m/s của PlayerMovement).
        /// - Khi hết Stamina: nếu đang bấm Shift thì bù trừ tốc độ sprint về walk speed và drain stamina.
        /// </summary>
        private void ApplySpeedAdjustments()
        {
            if (_characterController == null || !_characterController.enabled) return;
            if (Keyboard.current == null) return;

            // 1. Tính toán hướng di chuyển đầu vào hiện tại
            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;

            if (moveInput.sqrMagnitude < 0.01f) return;
            moveInput = moveInput.normalized;
            Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

            bool isSprinting = Keyboard.current.leftShiftKey.isPressed && moveInput.y > 0.1f && !_isCrouching && !_isSliding;

            // 2. Quản lý Stamina khi Sprint
            if (isSprinting && _playerStamina != null)
            {
                bool hasStamina = _playerStamina.DrainSprintStamina(Time.deltaTime);
                if (!hasStamina)
                {
                    // Hết thể lực: bù trừ vận tốc để hạ từ SprintSpeed (7.0) về WalkSpeed (4.5)
                    float walkSpeed = _playerMovement != null ? _playerMovement.WalkSpeed : 4.5f;
                    float sprintSpeed = _playerMovement != null ? _playerMovement.SprintSpeed : 7.0f;
                    float counterSpeed = sprintSpeed - walkSpeed;
                    _characterController.Move(-moveDir * (counterSpeed * Time.deltaTime));
                }
            }

            // 3. Quản lý vận tốc khi Crouch (hạ từ WalkSpeed về CrouchSpeed)
            if (_isCrouching && !_isSliding)
            {
                float baseSpeed = _playerMovement != null ? _playerMovement.WalkSpeed : 4.5f;
                if (baseSpeed > _crouchSpeed)
                {
                    float speedDiff = baseSpeed - _crouchSpeed;
                    _characterController.Move(-moveDir * (speedDiff * Time.deltaTime));
                }
            }
        }
    }
}
