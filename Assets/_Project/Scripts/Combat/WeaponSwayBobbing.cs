// Script: WeaponSwayBobbing.cs
// Mục đích: Đợt 1 - Combat Foundation: Hiệu ứng đung đưa súng khi lia chuột (Weapon Sway)
//           và nhấp nhô súng khi di chuyển (Weapon Bobbing).
// Môi trường thực thi: Client-side Cosmetic (không ảnh hưởng đường raycast của đạn).

using Hellfire.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponSwayBobbing : MonoBehaviour
    {
        [Header("Sway Configuration (Xoay chuột)")]
        [Tooltip("Bật/Tắt hiệu ứng Weapon Sway")]
        [SerializeField] private bool _enableSway = true;

        [Tooltip("Cường độ dịch chuyển vị trí súng khi xoay chuột")]
        [SerializeField] private float _swayPositionAmount = 0.02f;

        [Tooltip("Giới hạn dịch chuyển vị trí tối đa (m)")]
        [SerializeField] private float _maxSwayPosition = 0.06f;

        [Tooltip("Cường độ xoay góc nghiêng súng khi xoay chuột (độ)")]
        [SerializeField] private float _swayRotationAmount = 2.0f;

        [Tooltip("Giới hạn góc xoay tối đa (độ)")]
        [SerializeField] private float _maxSwayRotation = 5.0f;

        [Tooltip("Tốc độ làm mượt Sway")]
        [SerializeField] private float _swaySmoothSpeed = 10.0f;

        [Header("Bobbing Configuration (Di chuyển)")]
        [Tooltip("Bật/Tắt hiệu ứng Weapon Bobbing")]
        [SerializeField] private bool _enableBobbing = true;

        [Tooltip("Biên độ nhấp nhô ngang (X)")]
        [SerializeField] private float _bobHorizontalAmount = 0.015f;

        [Tooltip("Biên độ nhấp nhô dọc (Y)")]
        [SerializeField] private float _bobVerticalAmount = 0.02f;

        [Tooltip("Tần số nhịp bước chân khi đi bộ")]
        [SerializeField] private float _walkBobFrequency = 8.0f;

        [Tooltip("Tần số nhịp bước chân khi chạy nhanh")]
        [SerializeField] private float _sprintBobFrequency = 12.0f;

        [Tooltip("Biên độ nghiêng súng nhẹ theo bước chân (độ)")]
        [SerializeField] private float _bobRotationRoll = 1.5f;

        [Tooltip("Tốc độ hồi vị trí bobbing khi dừng")]
        [SerializeField] private float _bobSmoothSpeed = 8.0f;

        [Header("ADS Multipliers (Giảm rung khi ngắm)")]
        [Tooltip("Hệ số nhân Sway khi đang ADS")]
        [SerializeField] private float _adsSwayMultiplier = 0.25f;

        [Tooltip("Hệ số nhân Bobbing khi đang ADS")]
        [SerializeField] private float _adsBobMultiplier = 0.15f;

        [Header("Optional References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private WeaponAimDownSights _adsComponent;

        // Calculated Offsets (Exposed for Coordinator or internal application)
        public Vector3 SwayPositionOffset { get; private set; }
        public Quaternion SwayRotationOffset { get; private set; } = Quaternion.identity;
        public Vector3 BobPositionOffset { get; private set; }
        public Quaternion BobRotationOffset { get; private set; } = Quaternion.identity;

        public Vector3 CombinedPositionOffset => SwayPositionOffset + BobPositionOffset;
        public Quaternion CombinedRotationOffset => SwayRotationOffset * BobRotationOffset;

        private float _bobTimer;
        private Vector3 _targetSwayPos;
        private Vector3 _targetSwayRotEuler;
        private Vector3 _targetBobPos;
        private Vector3 _targetBobRotEuler;

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void AutoResolveReferences()
        {
            if (_characterController == null)
            {
                _characterController = GetComponentInParent<CharacterController>();
            }

            if (_adsComponent == null)
            {
                _adsComponent = GetComponentInParent<WeaponAimDownSights>();
                if (_adsComponent == null)
                {
                    _adsComponent = GetComponent<WeaponAimDownSights>();
                }
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.0001f) return;

            bool isAiming = _adsComponent != null && _adsComponent.IsAiming;
            float swayMult = isAiming ? _adsSwayMultiplier : 1.0f;
            float bobMult = isAiming ? _adsBobMultiplier : 1.0f;

            CalculateSway(deltaTime, swayMult);
            CalculateBobbing(deltaTime, bobMult);
        }

        private void CalculateSway(float deltaTime, float multiplier)
        {
            if (!_enableSway || Mouse.current == null)
            {
                SwayPositionOffset = Vector3.Lerp(SwayPositionOffset, Vector3.zero, _swaySmoothSpeed * deltaTime);
                SwayRotationOffset = Quaternion.Slerp(SwayRotationOffset, Quaternion.identity, _swaySmoothSpeed * deltaTime);
                return;
            }

            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.1f;

            // Translation Sway: ngược chiều di chuột
            float posX = Mathf.Clamp(-mouseDelta.x * _swayPositionAmount * multiplier, -_maxSwayPosition, _maxSwayPosition);
            float posY = Mathf.Clamp(-mouseDelta.y * _swayPositionAmount * multiplier, -_maxSwayPosition, _maxSwayPosition);
            _targetSwayPos = new Vector3(posX, posY, 0f);

            // Rotation Sway: nghiêng súng tự nhiên khi rê chuột
            float rotPitch = Mathf.Clamp(-mouseDelta.y * _swayRotationAmount * multiplier, -_maxSwayRotation, _maxSwayRotation);
            float rotYaw = Mathf.Clamp(mouseDelta.x * _swayRotationAmount * multiplier, -_maxSwayRotation, _maxSwayRotation);
            float rotRoll = Mathf.Clamp(-mouseDelta.x * _swayRotationAmount * 0.5f * multiplier, -_maxSwayRotation, _maxSwayRotation);
            _targetSwayRotEuler = new Vector3(rotPitch, rotYaw, rotRoll);

            // Interpolation mượt mà
            SwayPositionOffset = Vector3.Lerp(SwayPositionOffset, _targetSwayPos, _swaySmoothSpeed * deltaTime);
            SwayRotationOffset = Quaternion.Slerp(SwayRotationOffset, Quaternion.Euler(_targetSwayRotEuler), _swaySmoothSpeed * deltaTime);
        }

        private void CalculateBobbing(float deltaTime, float multiplier)
        {
            if (!_enableBobbing)
            {
                BobPositionOffset = Vector3.Lerp(BobPositionOffset, Vector3.zero, _bobSmoothSpeed * deltaTime);
                BobRotationOffset = Quaternion.Slerp(BobRotationOffset, Quaternion.identity, _bobSmoothSpeed * deltaTime);
                return;
            }

            Vector3 horizontalVelocity = Vector3.zero;
            bool isGrounded = true;

            if (_characterController != null)
            {
                horizontalVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
                isGrounded = _characterController.isGrounded;
            }

            float speed = horizontalVelocity.magnitude;
            bool isMoving = isGrounded && speed > 0.1f;

            if (isMoving)
            {
                bool isSprinting = speed > 5.5f;
                float frequency = isSprinting ? _sprintBobFrequency : _walkBobFrequency;

                _bobTimer += deltaTime * frequency;

                // Chu kỳ bước chân hình số 8 (Lissajous curve)
                float bobX = Mathf.Cos(_bobTimer * 0.5f) * _bobHorizontalAmount * multiplier;
                float bobY = (Mathf.Sin(_bobTimer) - 0.5f) * _bobVerticalAmount * multiplier;
                _targetBobPos = new Vector3(bobX, bobY, 0f);

                // Nghiêng nhẹ trục Z (Roll) theo chân bước
                float bobRoll = Mathf.Sin(_bobTimer * 0.5f) * _bobRotationRoll * multiplier;
                _targetBobRotEuler = new Vector3(bobY * 50f, 0f, bobRoll);
            }
            else
            {
                _bobTimer = 0f;
                _targetBobPos = Vector3.zero;
                _targetBobRotEuler = Vector3.zero;
            }

            BobPositionOffset = Vector3.Lerp(BobPositionOffset, _targetBobPos, _bobSmoothSpeed * deltaTime);
            BobRotationOffset = Quaternion.Slerp(BobRotationOffset, Quaternion.Euler(_targetBobRotEuler), _bobSmoothSpeed * deltaTime);
        }
    }
}
