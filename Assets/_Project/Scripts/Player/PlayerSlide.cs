// Script: PlayerSlide.cs
// Mục đích: Quản lý tính năng Trượt né (Slide), tiêu hao thể lực, cooldown, gia tốc đẩy và hạ thấp collider (Giai đoạn P1).
// Môi trường thực thi: Cả hai (Client-authoritative cho IsOwner, NetworkVariable đồng bộ cho remote client).

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSlide : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerStamina _playerStamina;
        [SerializeField] private PlayerCrouch _playerCrouch;

        [Header("Slide Configuration")]
        [Tooltip("Vận tốc đẩy ban đầu khi kích hoạt Slide (m/s)")]
        [SerializeField] private float _slideInitialSpeed = 9.5f;

        [Tooltip("Thời gian trượt tối đa (giây)")]
        [SerializeField] private float _slideDuration = 0.85f;

        [Tooltip("Thời gian hồi giữa 2 lần trượt liên tiếp (giây)")]
        [SerializeField] private float _slideCooldown = 0.8f;

        [Tooltip("Chiều cao Collider khi đang trượt (m)")]
        [SerializeField] private float _slideHeight = 0.8f;

        [Tooltip("Tọa độ Y của CameraPivot khi đang trượt (m)")]
        [SerializeField] private float _cameraSlideY = 0.7f;

        [Tooltip("Tốc độ chuyển đổi camera/collider khi bắt đầu/kết thúc Slide")]
        [SerializeField] private float _transitionSpeed = 12f;

        // Đồng bộ trạng thái mạng
        public NetworkVariable<bool> IsSlidingNet { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        // Trạng thái cục bộ
        private bool _isSliding;
        private float _slideTimer;
        private float _lastSlideEndTime;
        private Vector3 _slideDirection;
        private float _currentSlideSpeed;

        // Events
        public event Action OnSlideStarted;
        public event Action OnSlideEnded;

        // Properties
        public bool IsSliding => IsSpawned ? IsSlidingNet.Value : _isSliding;
        public float SlideDuration => _slideDuration;
        public float SlideCooldown => _slideCooldown;
        public float SlideInitialSpeed => _slideInitialSpeed;
        public float SlideHeight => _slideHeight;
        public float CameraSlideY => _cameraSlideY;

        private void Reset()
        {
            _slideInitialSpeed = 9.5f;
            _slideDuration = 0.85f;
            _slideCooldown = 0.8f;
            _slideHeight = 0.8f;
            _cameraSlideY = 0.7f;
            _transitionSpeed = 12f;
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
            if (_playerCrouch == null) _playerCrouch = GetComponent<PlayerCrouch>();
            if (_cameraPivot == null) _cameraPivot = transform.Find("CameraPivot");
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
        }

        private void Update()
        {
            if (IsSpawned && !IsOwner)
            {
                ApplyRemoteSlideVisuals();
                return;
            }

            HandleInput();
            UpdateSlidePhysics();
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            bool ctrlDown = Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame;
            bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;
            bool isMovingForward = Keyboard.current.wKey.isPressed;

            // Kích hoạt Slide: Đang chạy nhanh tiến tới (Shift + W) + Nhấn Ctrl/C
            if (ctrlDown && shiftPressed && isMovingForward && !_isSliding)
            {
                if (CanStartSlide())
                {
                    StartSlide();
                }
            }
        }

        public bool CanStartSlide()
        {
            if (Time.time < _lastSlideEndTime + _slideCooldown) return false;
            if (_characterController != null && !_characterController.isGrounded) return false;
            if (_playerStamina != null && !_playerStamina.CanSlide) return false;

            return true;
        }

        public void StartSlide()
        {
            if (_playerStamina == null) _playerStamina = GetComponent<PlayerStamina>();
            if (_playerCrouch == null) _playerCrouch = GetComponent<PlayerCrouch>();

            if (_playerStamina != null && !_playerStamina.TryConsumeSlideCost())
            {
                return;
            }

            _isSliding = true;
            _slideTimer = _slideDuration;
            _currentSlideSpeed = _slideInitialSpeed;
            _slideDirection = transform.forward;

            if (_playerCrouch != null)
            {
                _playerCrouch.SetCrouching(true);
            }

            if (IsSpawned && IsOwner)
            {
                IsSlidingNet.Value = true;
            }

            OnSlideStarted?.Invoke();
        }

        public void StopSlide()
        {
            _isSliding = false;
            _lastSlideEndTime = Time.time;

            if (_playerMovement != null)
            {
                _playerMovement.IsHorizontalMovementOverridden = false;
                _playerMovement.OverriddenHorizontalVelocity = Vector3.zero;
            }

            if (IsSpawned && IsOwner)
            {
                IsSlidingNet.Value = false;
            }

            OnSlideEnded?.Invoke();

            // Khi hết Slide: kiểm tra xem người chơi còn giữ Ctrl không
            bool ctrlPressed = Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);
            if (!ctrlPressed && _playerCrouch != null && _playerCrouch.HasCeilingClearance())
            {
                _playerCrouch.SetCrouching(false);
            }
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

            // Giảm dần tốc độ slide từ InitialSpeed (9.5) về CrouchSpeed (2.0)
            float progress = 1f - (_slideTimer / _slideDuration);
            float targetEndSpeed = _playerCrouch != null ? _playerCrouch.CrouchSpeed : 2.0f;
            _currentSlideSpeed = Mathf.Lerp(_slideInitialSpeed, targetEndSpeed, progress);

            // Điều phối vận tốc cho PlayerMovement thay vì gọi Move độc lập
            Vector3 slideVelocity = _slideDirection * _currentSlideSpeed;
            if (_playerMovement != null)
            {
                _playerMovement.IsHorizontalMovementOverridden = true;
                _playerMovement.OverriddenHorizontalVelocity = slideVelocity;
            }
            else if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(slideVelocity * Time.deltaTime);
            }

            // Hạ collider độ cao Slide
            if (_characterController != null && _characterController.enabled)
            {
                float newHeight = Mathf.Lerp(_characterController.height, _slideHeight, Time.deltaTime * _transitionSpeed);
                _characterController.height = newHeight;
                _characterController.center = new Vector3(0f, newHeight * 0.5f, 0f);
            }

            // Hạ CameraPivot
            if (_cameraPivot != null)
            {
                Vector3 camPos = _cameraPivot.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, _cameraSlideY, Time.deltaTime * _transitionSpeed);
                _cameraPivot.localPosition = camPos;
            }
        }

        private void ApplyRemoteSlideVisuals()
        {
            if (_cameraPivot == null) return;
            if (!IsSlidingNet.Value) return;

            Vector3 camPos = _cameraPivot.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, _cameraSlideY, Time.deltaTime * _transitionSpeed);
            _cameraPivot.localPosition = camPos;
        }
    }
}
