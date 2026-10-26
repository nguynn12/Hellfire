// Script: WeaponRecoil.cs
// Mục đích: Đợt 1 - Combat Foundation: Hiệu ứng súng giật nảy (Weapon Recoil) khi bắn và hồi vị trí.
//           Hỗ trợ cấu hình theo từng loại vũ khí, đảm bảo không lệch tâm vĩnh viễn và không phá vỡ hitscan.
// Môi trường thực thi: Client-side Procedural Cosmetic & Camera Punch.

using System;
using UnityEngine;

namespace Hellfire.Combat
{
    [Serializable]
    public struct RecoilProfile
    {
        [Tooltip("Độ giật lùi dọc trục Z (m)")]
        public float kickBackZ;

        [Tooltip("Độ nảy súng lên trên dọc trục Y (m)")]
        public float kickUpY;

        [Tooltip("Góc giật ngóc nòng lên (độ)")]
        public float kickPitchX;

        [Tooltip("Độ rung ngẫu nhiên ngang (độ)")]
        public float kickYawY;

        [Tooltip("Độ xoay nghiêng ngẫu nhiên (độ)")]
        public float kickRollZ;

        [Tooltip("Tốc độ giật tức thời (Snappiness)")]
        public float snappiness;

        [Tooltip("Tốc độ hồi tâm/vị trí ban đầu (Return Speed)")]
        public float returnSpeed;

        public static RecoilProfile Default => new RecoilProfile
        {
            kickBackZ = 0.06f,
            kickUpY = 0.025f,
            kickPitchX = 3.5f,
            kickYawY = 1.0f,
            kickRollZ = 0.8f,
            snappiness = 22.0f,
            returnSpeed = 9.0f
        };
    }

    [DisallowMultipleComponent]
    public class WeaponRecoil : MonoBehaviour
    {
        [Header("Default Recoil Profile")]
        [SerializeField] private RecoilProfile _defaultProfile = RecoilProfile.Default;

        [Header("Per-Weapon Profiles (Cấu hình theo vũ khí)")]
        [SerializeField] private RecoilProfile _pistolProfile = new RecoilProfile { kickBackZ = 0.05f, kickUpY = 0.02f, kickPitchX = 3.0f, kickYawY = 0.8f, kickRollZ = 0.5f, snappiness = 24f, returnSpeed = 10f };
        [SerializeField] private RecoilProfile _rifleProfile = new RecoilProfile { kickBackZ = 0.06f, kickUpY = 0.025f, kickPitchX = 2.5f, kickYawY = 1.2f, kickRollZ = 0.7f, snappiness = 20f, returnSpeed = 11f };
        [SerializeField] private RecoilProfile _shotgunProfile = new RecoilProfile { kickBackZ = 0.12f, kickUpY = 0.06f, kickPitchX = 7.0f, kickYawY = 2.0f, kickRollZ = 1.5f, snappiness = 26f, returnSpeed = 7f };
        [SerializeField] private RecoilProfile _smgProfile = new RecoilProfile { kickBackZ = 0.035f, kickUpY = 0.015f, kickPitchX = 1.8f, kickYawY = 1.4f, kickRollZ = 0.9f, snappiness = 28f, returnSpeed = 14f };

        [Header("ADS Recoil Multiplier")]
        [Tooltip("Hệ số giảm độ giật khi ngắm bắn ADS")]
        [SerializeField] private float _adsRecoilMultiplier = 0.55f;

        [Header("Camera Kick (Tùy chọn)")]
        [Tooltip("Bật rung nhẹ camera góc nhìn thứ nhất khi bắn (100% tự hồi về 0, không lệch tâm vĩnh viễn)")]
        [SerializeField] private bool _enableCameraKick = true;
        [SerializeField] private float _cameraKickPitch = 0.8f;
        [SerializeField] private float _cameraKickReturnSpeed = 12.0f;

        [Header("References")]
        [SerializeField] private WeaponController _weaponController;
        [SerializeField] private WeaponAimDownSights _adsComponent;
        [SerializeField] private Transform _cameraPivot;

        // Current calculated offsets
        public Vector3 RecoilPositionOffset { get; private set; }
        public Quaternion RecoilRotationOffset { get; private set; } = Quaternion.identity;

        private Vector3 _targetRecoilPos;
        private Vector3 _currentRecoilPos;
        private Vector3 _targetRecoilRot;
        private Vector3 _currentRecoilRot;

        // Camera kick recovery tracking
        private float _targetCamKick;
        private float _currentCamKick;
        private float _lastCamKickApplied;

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void AutoResolveReferences()
        {
            if (_weaponController == null)
            {
                _weaponController = GetComponentInParent<WeaponController>();
                if (_weaponController == null) _weaponController = GetComponent<WeaponController>();
            }

            if (_adsComponent == null)
            {
                _adsComponent = GetComponentInParent<WeaponAimDownSights>();
                if (_adsComponent == null) _adsComponent = GetComponent<WeaponAimDownSights>();
            }

            if (_cameraPivot == null)
            {
                var cam = GetComponentInParent<Camera>();
                if (cam != null && cam.transform.parent != null)
                {
                    _cameraPivot = cam.transform.parent;
                }
            }
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired += HandleWeaponFired;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired -= HandleWeaponFired;
            }

            // Hoàn nguyên góc camera khi bị vô hiệu hóa
            RestoreCameraKick();
            RecoilPositionOffset = Vector3.zero;
            RecoilRotationOffset = Quaternion.identity;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.0001f) return;

            RecoilProfile profile = GetCurrentRecoilProfile();

            // 1. Phục hồi dần target về 0 (Decay)
            _targetRecoilPos = Vector3.Lerp(_targetRecoilPos, Vector3.zero, profile.returnSpeed * deltaTime);
            _targetRecoilRot = Vector3.Lerp(_targetRecoilRot, Vector3.zero, profile.returnSpeed * deltaTime);

            // 2. Nội suy vị trí hiện tại đuổi theo target (Snappiness)
            _currentRecoilPos = Vector3.Lerp(_currentRecoilPos, _targetRecoilPos, profile.snappiness * deltaTime);
            _currentRecoilRot = Vector3.Lerp(_currentRecoilRot, _targetRecoilRot, profile.snappiness * deltaTime);

            RecoilPositionOffset = _currentRecoilPos;
            RecoilRotationOffset = Quaternion.Euler(_currentRecoilRot);

            // 3. Xử lý Camera Kick hồi phục mượt mà
            UpdateCameraKick(deltaTime);
        }

        /// <summary>
        /// Kích hoạt giật súng thủ công hoặc qua event OnWeaponFired
        /// </summary>
        public void HandleWeaponFired()
        {
            RecoilProfile profile = GetCurrentRecoilProfile();
            bool isAiming = _adsComponent != null && _adsComponent.IsAiming;
            float multiplier = isAiming ? _adsRecoilMultiplier : 1.0f;

            // Sinh độ lệch ngẫu nhiên nhỏ sang trái/phải
            float randYaw = UnityEngine.Random.Range(-profile.kickYawY, profile.kickYawY) * multiplier;
            float randRoll = UnityEngine.Random.Range(-profile.kickRollZ, profile.kickRollZ) * multiplier;

            // Giật lùi về sau (-Z) và nảy lên (+Y)
            _targetRecoilPos += new Vector3(0f, profile.kickUpY * multiplier, -profile.kickBackZ * multiplier);

            // Giật xoay nòng ngóc lên (-X)
            _targetRecoilRot += new Vector3(-profile.kickPitchX * multiplier, randYaw, randRoll);

            // Camera Kick
            if (_enableCameraKick)
            {
                _targetCamKick += _cameraKickPitch * multiplier;
            }
        }

        private void UpdateCameraKick(float deltaTime)
        {
            if (_cameraPivot == null || !_enableCameraKick) return;

            // Camera kick hồi về 0
            _targetCamKick = Mathf.Lerp(_targetCamKick, 0f, _cameraKickReturnSpeed * deltaTime);
            float prevKick = _currentCamKick;
            _currentCamKick = Mathf.Lerp(_currentCamKick, _targetCamKick, 25f * deltaTime);

            // Chỉ áp dụng delta để không tích lũy sai số hoặc lệch vĩnh viễn
            float deltaKick = _currentCamKick - prevKick;
            _cameraPivot.localRotation *= Quaternion.Euler(-deltaKick, 0f, 0f);
            _lastCamKickApplied += deltaKick;

            if (Mathf.Abs(_currentCamKick) < 0.001f && Mathf.Abs(_targetCamKick) < 0.001f)
            {
                _currentCamKick = 0f;
                _targetCamKick = 0f;
            }
        }

        private void RestoreCameraKick()
        {
            if (_cameraPivot != null && Mathf.Abs(_lastCamKickApplied) > 0.001f)
            {
                _cameraPivot.localRotation *= Quaternion.Euler(_lastCamKickApplied, 0f, 0f);
                _lastCamKickApplied = 0f;
            }
        }

        private RecoilProfile GetCurrentRecoilProfile()
        {
            if (_weaponController == null || _weaponController.CurrentWeaponData == null)
            {
                return _defaultProfile;
            }

            // Kiểm tra WeaponData nếu có cấu hình riêng
            var data = _weaponController.CurrentWeaponData;
            if (data.RecoilKickBack > 0.001f)
            {
                return new RecoilProfile
                {
                    kickBackZ = data.RecoilKickBack,
                    kickUpY = data.RecoilKickUp,
                    kickPitchX = data.RecoilAngleX,
                    kickYawY = data.RecoilAngleY,
                    kickRollZ = data.RecoilAngleY * 0.5f,
                    snappiness = data.RecoilSnappiness > 0.1f ? data.RecoilSnappiness : 20f,
                    returnSpeed = data.RecoilReturnSpeed > 0.1f ? data.RecoilReturnSpeed : 10f
                };
            }

            // Fallback theo Id
            string id = data.WeaponId != null ? data.WeaponId.ToLowerInvariant() : "";
            if (id.Contains("pistol")) return _pistolProfile;
            if (id.Contains("rifle")) return _rifleProfile;
            if (id.Contains("shotgun")) return _shotgunProfile;
            if (id.Contains("smg")) return _smgProfile;

            return _defaultProfile;
        }
    }
}
