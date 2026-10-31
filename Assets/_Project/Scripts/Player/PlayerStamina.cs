// Script: PlayerStamina.cs
// Mục đích: Quản lý thể lực (Stamina) cho Player độc lập, hỗ trợ tiêu hao khi Sprint và Slide (Giai đoạn P1).
// Môi trường thực thi: Cả hai (Client-authoritative cho IsOwner, NetworkVariable đồng bộ nếu cần).

using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    public class PlayerStamina : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerCrouch _playerCrouch;
        [SerializeField] private PlayerSlide _playerSlide;

        [Header("Stamina Settings")]
        [Tooltip("Thể lực tối đa")]
        [SerializeField] private float _maxStamina = 100f;

        [Tooltip("Tốc độ tiêu hao thể lực khi Sprint (điểm/giây)")]
        [SerializeField] private float _sprintDrainRate = 18f;

        [Tooltip("Lượng thể lực tiêu hao cho mỗi cú Slide")]
        [SerializeField] private float _slideStaminaCost = 20f;

        [Tooltip("Tốc độ hồi phục thể lực (điểm/giây)")]
        [SerializeField] private float _regenRate = 22f;

        [Tooltip("Thời gian trễ trước khi bắt đầu hồi phục sau hành động tiêu hao (giây)")]
        [SerializeField] private float _regenDelay = 1.2f;

        [Tooltip("Mức thể lực tối thiểu cần có để bắt đầu Sprint trở lại")]
        [SerializeField] private float _sprintResumeThreshold = 10f;

        // Runtime state
        private float _currentStamina = 100f;
        private float _timeSinceLastDrain = 0f;
        private bool _isExhausted = false;

        // Events
        public event Action<float, float> OnStaminaChanged; // (current, max)
        public event Action<bool> OnExhaustedChanged;       // (isExhausted)

        // Properties
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => _maxStamina;
        public float StaminaNormalized => _maxStamina > 0.001f ? Mathf.Clamp01(_currentStamina / _maxStamina) : 0f;
        public bool CanSprint => !_isExhausted && _currentStamina >= _sprintResumeThreshold;
        public bool CanSlide => !_isExhausted && _currentStamina >= _slideStaminaCost;
        public bool IsExhausted => _isExhausted;

        private void Reset()
        {
            AutoResolveReferences();
            _currentStamina = _maxStamina;
            _timeSinceLastDrain = _regenDelay;
            _isExhausted = false;
        }

        private void Awake()
        {
            AutoResolveReferences();
            _currentStamina = _maxStamina;
            _timeSinceLastDrain = _regenDelay;
            _isExhausted = false;
        }

        private void AutoResolveReferences()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_playerCrouch == null) _playerCrouch = GetComponent<PlayerCrouch>();
            if (_playerSlide == null) _playerSlide = GetComponent<PlayerSlide>();
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();
        }

        private void Update()
        {
            if (IsSpawned && !IsOwner) return;

            if (_playerMovement != null)
            {
                _playerMovement.CanSprint = this.CanSprint;
            }

            HandleSprintInputAndDrain();
            UpdateRegeneration();
        }

        private void HandleSprintInputAndDrain()
        {
            if (Keyboard.current == null) return;

            bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;
            bool isMovingForward = Keyboard.current.wKey.isPressed;
            bool isCrouched = _playerCrouch != null && _playerCrouch.IsCrouching;
            bool isSliding = _playerSlide != null && _playerSlide.IsSliding;

            bool isSprinting = shiftPressed && isMovingForward && !isCrouched && !isSliding;

            if (isSprinting)
            {
                bool hasStamina = DrainSprintStamina(Time.deltaTime);
                if (!hasStamina)
                {
                    if (_playerMovement != null)
                    {
                        _playerMovement.CanSprint = false;
                    }
                    else if (_characterController != null && _characterController.enabled)
                    {
                        // Fallback nếu không có PlayerMovement hook
                        float walkSpeed = 4.5f;
                        float sprintSpeed = 7.0f;
                        float counterSpeed = sprintSpeed - walkSpeed;
                        _characterController.Move(-transform.forward * (counterSpeed * Time.deltaTime));
                    }
                }
            }
        }

        private void UpdateRegeneration()
        {
            _timeSinceLastDrain += Time.deltaTime;
            if (_timeSinceLastDrain < _regenDelay) return;

            if (_currentStamina < _maxStamina)
            {
                float prev = _currentStamina;
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _regenRate * Time.deltaTime);

                if (_isExhausted && _currentStamina >= _sprintResumeThreshold)
                {
                    _isExhausted = false;
                    OnExhaustedChanged?.Invoke(false);
                }

                if (Mathf.Abs(prev - _currentStamina) > 0.05f)
                {
                    OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
                }
            }
        }

        /// <summary>
        /// Tiêu hao thể lực theo thời gian khi Sprint. Trả về true nếu còn thể lực.
        /// </summary>
        public bool DrainSprintStamina(float deltaTime)
        {
            if (!CanSprint && _currentStamina <= 0.01f)
            {
                return false;
            }

            float cost = _sprintDrainRate * deltaTime;
            _currentStamina = Mathf.Max(0f, _currentStamina - cost);
            _timeSinceLastDrain = 0f;

            if (_currentStamina <= 0.01f && !_isExhausted)
            {
                _isExhausted = true;
                OnExhaustedChanged?.Invoke(true);
            }

            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
            return _currentStamina > 0f;
        }

        /// <summary>
        /// Tiêu hao chi phí thể lực cho một cú trượt Slide (20 điểm).
        /// </summary>
        public bool TryConsumeSlideCost()
        {
            return TryConsumeStamina(_slideStaminaCost);
        }

        /// <summary>
        /// Tiêu hao thể lực theo lượng cố định.
        /// </summary>
        public bool TryConsumeStamina(float amount)
        {
            if (_isExhausted || _currentStamina < amount)
            {
                return false;
            }

            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _timeSinceLastDrain = 0f;

            if (_currentStamina < _sprintResumeThreshold)
            {
                _isExhausted = true;
                OnExhaustedChanged?.Invoke(true);
            }

            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
            return true;
        }

        /// <summary>
        /// Đặt lại thể lực (dùng cho Respawn hoặc Test).
        /// </summary>
        public void ResetStamina()
        {
            _currentStamina = _maxStamina;
            _isExhausted = false;
            _timeSinceLastDrain = _regenDelay;
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
            OnExhaustedChanged?.Invoke(false);
        }
    }
}
