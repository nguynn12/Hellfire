// Script: PlayerMovement.cs
// Mục đích: Điều khiển di chuyển góc nhìn thứ nhất (FPS) bằng CharacterController và Unity Input System (Mục 1 & 1.3).
// Môi trường thực thi: Cả hai (Client-side prediction cho IsOwner, NetworkTransform đồng bộ cho Client khác).

using Hellfire.Combat;
using Hellfire.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private GameObject _playerBodyMesh;
        [SerializeField] private GameObject _weaponViewmodel;
        [SerializeField] private Health _health;

        [Header("Movement Parameters")]
        [Tooltip("Tốc độ đi bộ thông thường (m/s)")]
        [SerializeField] private float _walkSpeed = 4.5f;

        [Tooltip("Tốc độ chạy nhanh khi giữ Shift (m/s)")]
        [SerializeField] private float _sprintSpeed = 7.0f;

        [Tooltip("Hệ số tốc độ khi đi lùi (70% walkSpeed)")]
        [SerializeField] private float _backwardMultiplier = 0.7f;

        [Tooltip("Chiều cao nhảy (m)")]
        [SerializeField] private float _jumpHeight = 1.2f;

        [Tooltip("Gia tốc trọng lực (m/s^2) - nhịp nhanh")]
        [SerializeField] private float _gravity = -20.0f;

        [Header("Camera & Look Parameters")]
        [Tooltip("Độ nhạy xoay chuột")]
        [SerializeField] private float _mouseSensitivity = 2.0f;

        [Tooltip("Góc nhìn ngẩng lên tối đa (độ)")]
        [SerializeField] private float _pitchClampMin = -85.0f;

        [Tooltip("Góc nhìn cúi xuống tối đa (độ)")]
        [SerializeField] private float _pitchClampMax = 85.0f;

        [Header("Layers")]
        [SerializeField] private string _playerBodyLayerName = "PlayerBody";
        [SerializeField] private string _viewmodelLayerName = "Viewmodel";

        // Properties
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public Camera PlayerCamera => _playerCamera;

        private float _cameraPitch;
        private float _verticalVelocity;
        private bool _isCursorLocked = true;
        private PlayerBuffManager _buffManager;

        private void Reset()
        {
            AutoResolveReferences();
            if (_characterController != null)
            {
                _characterController.radius = 0.35f;
                _characterController.height = 1.8f;
                _characterController.center = new Vector3(0f, 0.9f, 0f);
                _characterController.slopeLimit = 45f;
                _characterController.stepOffset = 0.3f;
            }
        }

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void AutoResolveReferences()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_cameraPivot == null) _cameraPivot = transform.Find("CameraPivot");
            if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>(true);
            if (_audioListener == null) _audioListener = GetComponentInChildren<AudioListener>(true);
            if (_playerBodyMesh == null) _playerBodyMesh = transform.Find("PlayerBodyMesh")?.gameObject;
            if (_weaponViewmodel == null) _weaponViewmodel = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel")?.gameObject;
            if (_health == null) _health = GetComponent<Health>();
            if (_buffManager == null) _buffManager = GetComponent<PlayerBuffManager>();
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();

            if (IsOwner)
            {
                // Bật CharacterController cho chính chủ để nhận physics & input
                if (_characterController != null)
                {
                    _characterController.enabled = true;
                }

                // Cấu hình Camera cho chính chủ sở hữu (Mục 1.1 & 1.2)
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(true);
                    _playerCamera.fieldOfView = 90.0f;

                    // Culling Mask: Ẩn layer PlayerBody của chính mình, hiện layer Viewmodel
                    int bodyLayer = LayerMask.NameToLayer(_playerBodyLayerName);
                    int viewmodelLayer = LayerMask.NameToLayer(_viewmodelLayerName);

                    if (bodyLayer >= 0)
                    {
                        _playerCamera.cullingMask &= ~(1 << bodyLayer);
                    }

                    if (viewmodelLayer >= 0)
                    {
                        _playerCamera.cullingMask |= (1 << viewmodelLayer);
                    }
                }

                if (_audioListener != null)
                {
                    _audioListener.enabled = true;
                }

                if (_weaponViewmodel != null)
                {
                    _weaponViewmodel.SetActive(true);
                }

                // Load độ nhạy chuột từ Settings nếu có
                float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
                SetMouseSensitivity(savedSensitivity);

                SetCursorLock(true);
            }
            else
            {
                // Trên máy client khác: tắt CharacterController để NetworkTransform nội suy vị trí mượt mà
                if (_characterController != null)
                {
                    _characterController.enabled = false;
                }

                // Vô hiệu hóa Camera và AudioListener của nhân vật thuộc Client khác
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                }

                if (_audioListener != null)
                {
                    _audioListener.enabled = false;
                }

                if (_weaponViewmodel != null)
                {
                    _weaponViewmodel.SetActive(false);
                }

                // Người khác PHẢI thấy mô hình toàn thân (Mục 1.1)
                if (_playerBodyMesh != null)
                {
                    _playerBodyMesh.SetActive(true);
                }
            }
        }

        private void Update()
        {
            // Chỉ xử lý Input và Prediction trên máy của chính chủ sở hữu
            if (!IsOwner)
            {
                return;
            }

            HandleCursorToggle();

            // Vô hiệu hóa di chuyển khi đang Gục (Downed) hoặc Chết (Dead) theo Mục 3.3
            if (_health != null && (_health.IsDowned.Value || _health.IsDead.Value))
            {
                ApplyGravityOnly();
                return;
            }

            // Kiểm tra trạng thái GameManager trước khi cho phép di chuyển
            if (GameManager.Instance != null && !GameManager.Instance.CanPlayerMove())
            {
                ApplyGravityOnly();
                return;
            }

            if (_isCursorLocked)
            {
                HandleLook();
                HandleMovement();
            }
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(!_isCursorLocked);
            }
        }

        public void SetCursorLock(bool locked)
        {
            _isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SetMouseSensitivity(float newSensitivity)
        {
            _mouseSensitivity = Mathf.Max(0.1f, newSensitivity);
        }

        private void HandleLook()
        {
            Vector2 lookDelta = Vector2.zero;

            if (Mouse.current != null)
            {
                lookDelta = Mouse.current.delta.ReadValue() * (0.1f * _mouseSensitivity);
            }

            // Xoay ngang (Yaw): xoay trực tiếp Player root
            transform.Rotate(Vector3.up * lookDelta.x);

            // Xoay dọc (Pitch): xoay CameraPivot độc lập và clamp từ -85 đến +85 độ
            if (_cameraPivot != null)
            {
                _cameraPitch -= lookDelta.y;
                _cameraPitch = Mathf.Clamp(_cameraPitch, _pitchClampMin, _pitchClampMax);
                _cameraPivot.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            if (_characterController == null || !_characterController.enabled)
            {
                return;
            }

            // 1. Đọc Input WASD
            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
                if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
                if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            }

            moveInput = moveInput.normalized;

            // 2. Xác định tốc độ (đi bộ, chạy nhanh, đi lùi)
            bool isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            bool isMovingBackward = moveInput.y < -0.1f;

            float currentSpeed;
            if (isMovingBackward)
            {
                // Đi lùi: 70% walkSpeed (Mục 1.3)
                currentSpeed = _walkSpeed * _backwardMultiplier;
            }
            else if (isSprinting && moveInput.sqrMagnitude > 0.01f)
            {
                currentSpeed = _sprintSpeed;
            }
            else
            {
                currentSpeed = _walkSpeed;
            }

            // Áp dụng hệ số bùa lợi Giày tốc độ (Swift Boots - Mục 6.2)
            if (_buffManager != null)
            {
                currentSpeed *= _buffManager.SpeedMultiplier;
            }

            // 3. Tính hướng di chuyển theo local transform
            Vector3 moveDirection = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

            // 4. Xử lý trọng lực và tiếp đất
            bool grounded = _characterController.isGrounded;
            if (grounded && _verticalVelocity < 0f)
            {
                // Giữ lực dính mặt đất nhẹ
                _verticalVelocity = -2f;
            }

            // 5. Xử lý nhảy: v = sqrt(2 * |g| * h) (Mục 1.3)
            bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (jumpPressed && grounded)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * _jumpHeight);
            }

            // Áp dụng trọng lực
            _verticalVelocity += _gravity * Time.deltaTime;

            // 6. Di chuyển CharacterController
            Vector3 velocity = moveDirection * currentSpeed + Vector3.up * _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (_characterController == null || !_characterController.enabled) return;

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            _characterController.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
        }
    }
}
