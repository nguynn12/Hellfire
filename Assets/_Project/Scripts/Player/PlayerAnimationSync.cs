// Script: PlayerAnimationSync.cs
// Mục đích: Đồng bộ hóa trạng thái di chuyển, Ngồi (Crouch), Chạy nhanh (Sprint), Trượt (Slide) vào Animator của mô hình Sci-Fi Soldier (Giai đoạn P1).
// Môi trường thực thi: Cả hai (Hiển thị visual trên local client và đồng bộ trên remote client).

using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    public class PlayerAnimationSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerCrouch _playerCrouch;
        [SerializeField] private PlayerSlide _playerSlide;
        [SerializeField] private PlayerStamina _playerStamina;

        [Header("Animator Parameter Names")]
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField] private string _isCrouchingParam = "IsCrouching";
        [SerializeField] private string _isSprintingParam = "IsSprinting";
        [SerializeField] private string _isSlidingParam = "IsSliding";
        [SerializeField] private string _slideTriggerParam = "Slide";
        [SerializeField] private string _onGroundParam = "OnGround";

        [Header("Debug & Visual Settings")]
        [Tooltip("Cho phép nhấn phím V để chuyển đổi góc nhìn thứ ba khi test Play Mode")]
        [SerializeField] private bool _enableThirdPersonToggle = true;
        [SerializeField] private Camera _testThirdPersonCamera;

        // Hash IDs for performance
        private int _speedHash;
        private int _isCrouchingHash;
        private int _isSprintingHash;
        private int _isSlidingHash;
        private int _slideTriggerHash;
        private int _onGroundHash;

        private Vector3 _lastPosition;
        private float _currentSpeedValue;

        private void Reset()
        {
            AutoResolveReferences();
        }

        private void Awake()
        {
            AutoResolveReferences();
            CacheParameterHashes();
            _lastPosition = transform.position;

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        private void AutoResolveReferences()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_playerCrouch == null) _playerCrouch = GetComponent<PlayerCrouch>();
            if (_playerSlide == null) _playerSlide = GetComponent<PlayerSlide>();
            if (_playerStamina == null) _playerStamina = GetComponent<PlayerStamina>();
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                var soldierTransform = transform.Find("PlayerBodyMesh/Space_Soldier_A");
                if (soldierTransform != null)
                {
                    _animator = soldierTransform.GetComponent<Animator>();
                }
                if (_animator == null || _animator.runtimeAnimatorController == null)
                {
                    var allAnims = GetComponentsInChildren<Animator>(true);
                    foreach (var a in allAnims)
                    {
                        if (a.runtimeAnimatorController != null)
                        {
                            _animator = a;
                            break;
                        }
                    }
                }
            }
        }

        private void CacheParameterHashes()
        {
            _speedHash = Animator.StringToHash(_speedParam);
            _isCrouchingHash = Animator.StringToHash(_isCrouchingParam);
            _isSprintingHash = Animator.StringToHash(_isSprintingParam);
            _isSlidingHash = Animator.StringToHash(_isSlidingParam);
            _slideTriggerHash = Animator.StringToHash(_slideTriggerParam);
            _onGroundHash = Animator.StringToHash(_onGroundParam);
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
            RegisterEvents();
        }

        public override void OnNetworkDespawn()
        {
            UnregisterEvents();
        }

        private void Start()
        {
            AutoResolveReferences();
            RegisterEvents();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UnregisterEvents();
        }

        private void RegisterEvents()
        {
            if (_playerSlide != null)
            {
                _playerSlide.OnSlideStarted -= HandleSlideStarted;
                _playerSlide.OnSlideStarted += HandleSlideStarted;
            }
        }

        private void UnregisterEvents()
        {
            if (_playerSlide != null)
            {
                _playerSlide.OnSlideStarted -= HandleSlideStarted;
            }
        }

        private void HandleSlideStarted()
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetTrigger(_slideTriggerHash);
            }
        }

        private void Update()
        {
            HandleThirdPersonToggle();
            UpdateAnimatorParameters();
        }

        private void HandleThirdPersonToggle()
        {
            if (!_enableThirdPersonToggle) return;
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                ToggleThirdPersonCamera();
            }
        }

        public void ToggleThirdPersonCamera()
        {
            var firstPersonCam = _playerMovement != null ? _playerMovement.PlayerCamera : GetComponentInChildren<Camera>(true);
            if (_testThirdPersonCamera == null)
            {
                var camObj = new GameObject("TestThirdPersonCamera");
                var pivot = transform.Find("CameraPivot") ?? transform;
                camObj.transform.SetParent(pivot, false);
                camObj.transform.localPosition = new Vector3(0.55f, 0.25f, -2.6f);
                camObj.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                _testThirdPersonCamera = camObj.AddComponent<Camera>();
                _testThirdPersonCamera.fieldOfView = 75f;
                int viewmodelLayer = LayerMask.NameToLayer("Viewmodel");
                if (viewmodelLayer >= 0)
                {
                    _testThirdPersonCamera.cullingMask &= ~(1 << viewmodelLayer);
                }
                camObj.SetActive(false);
            }

            if (firstPersonCam != null && _testThirdPersonCamera != null)
            {
                bool enableThirdPerson = !_testThirdPersonCamera.gameObject.activeSelf;
                _testThirdPersonCamera.gameObject.SetActive(enableThirdPerson);

                // Giữ GameObject của firstPersonCam active để AudioListener trên đó vẫn hoạt động (không bị 0 hoặc 2 AudioListener)
                firstPersonCam.enabled = !enableThirdPerson;

                var bodyMesh = transform.Find("PlayerBodyMesh");
                if (bodyMesh != null)
                {
                    bodyMesh.gameObject.SetActive(true);
                }
            }
        }

        private void UpdateAnimatorParameters()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            // 1. Vận tốc ngang thực tế
            Vector3 deltaPos = transform.position - _lastPosition;
            _lastPosition = transform.position;
            deltaPos.y = 0f;
            float instantSpeed = Time.deltaTime > 0.0001f ? deltaPos.magnitude / Time.deltaTime : 0f;

            // Làm mượt tốc độ để Blend Tree chuyển trạng thái mượt mà
            _currentSpeedValue = Mathf.Lerp(_currentSpeedValue, instantSpeed, Time.deltaTime * 12f);

            bool isGrounded = _characterController != null ? _characterController.isGrounded : true;
            bool isCrouching = _playerCrouch != null && _playerCrouch.IsCrouching;
            bool isSliding = _playerSlide != null && _playerSlide.IsSliding;

            bool isSprinting = false;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && _playerStamina != null && _playerStamina.CanSprint && !isCrouching && !isSliding)
            {
                isSprinting = _currentSpeedValue > 3.0f;
            }

            // 2. Cập nhật parameters vào Animator
            _animator.SetFloat(_speedHash, _currentSpeedValue);
            _animator.SetBool(_isCrouchingHash, isCrouching);
            _animator.SetBool(_isSprintingHash, isSprinting);
            _animator.SetBool(_isSlidingHash, isSliding);
            _animator.SetBool(_onGroundHash, isGrounded);
        }
    }
}
