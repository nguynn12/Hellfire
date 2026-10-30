// Script: WeaponAimDownSights.cs
// Mục đích: Đợt 1 - Combat Foundation: Cơ chế ngắm bắn (Aim Down Sights - ADS) khi giữ chuột phải.
//           Điều chỉnh mượt mà FOV Camera và đưa súng vào tâm ngắm, hỗ trợ cấu hình tốc độ.
//           An toàn tuyệt đối: không lỗi khi đổi súng, reload hoặc chuyển Scene.
// Môi trường thực thi: Client-side Cosmetic & Camera Controller.

using Hellfire.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponAimDownSights : MonoBehaviour
    {
        [Header("ADS Aim Positions")]
        [Tooltip("Vị trí mặc định ở hông (Hipfire Position)")]
        [SerializeField] private Vector3 _hipPosition = new Vector3(0.25f, -0.2f, 0.5f);

        [Tooltip("Góc xoay mặc định ở hông (Hipfire Rotation)")]
        [SerializeField] private Vector3 _hipRotation = Vector3.zero;

        [Tooltip("Vị trí ngắm súng ngay giữa tâm màn hình (ADS Position)")]
        [SerializeField] private Vector3 _adsPosition = new Vector3(0.0f, -0.14f, 0.38f);

        [Tooltip("Góc xoay súng khi ngắm (ADS Rotation)")]
        [SerializeField] private Vector3 _adsRotation = Vector3.zero;

        [Header("Camera FOV")]
        [Tooltip("Góc nhìn FOV của camera khi ngắm bắn ADS")]
        [SerializeField] private float _adsFOV = 65.0f;

        [Header("Transition Settings")]
        [Tooltip("Tốc độ chuyển đổi giữa bắn thường và ngắm bắn (đơn vị/giây)")]
        [SerializeField] private float _adsTransitionSpeed = 12.0f;

        [Tooltip("Chế độ ngắm: False = Giữ chuột phải (Hold), True = Bấm chuyển đổi (Toggle)")]
        [SerializeField] private bool _toggleMode = false;

        [Header("Spread Reduction")]
        [Tooltip("Tỷ lệ thu hẹp độ lệch đạn (Spread) khi ngắm bắn (50% mặc định)")]
        [SerializeField] private float _adsSpreadMultiplier = 0.5f;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private WeaponController _weaponController;
        [SerializeField] private PlayerMovement _playerMovement;

        // Current status
        public bool IsAiming { get; private set; }
        public float AimProgress { get; private set; } // 0 = Hip, 1 = Full ADS
        public Vector3 CurrentAimPosition { get; private set; }
        public Quaternion CurrentAimRotation { get; private set; } = Quaternion.identity;

        public Vector3 AimPositionOffset => CurrentAimPosition - _hipPosition;
        public Quaternion AimRotationOffset => Quaternion.Inverse(Quaternion.Euler(_hipRotation)) * CurrentAimRotation;

        private float _defaultFOV = 90.0f;
        private bool _isAimInputActive;
        private bool _wasAimingBeforeReload;

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void AutoResolveReferences()
        {
            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInParent<Camera>();
                if (_playerCamera == null) _playerCamera = Camera.main;
            }

            if (_weaponController == null)
            {
                _weaponController = GetComponentInParent<WeaponController>();
                if (_weaponController == null) _weaponController = GetComponent<WeaponController>();
            }

            if (_playerMovement == null)
            {
                _playerMovement = GetComponentInParent<PlayerMovement>();
            }

            if (_playerCamera != null)
            {
                _defaultFOV = _playerCamera.fieldOfView;
            }

            CurrentAimPosition = _hipPosition;
            CurrentAimRotation = Quaternion.Euler(_hipRotation);
        }

        private void Start()
        {
            if (_playerCamera != null)
            {
                _defaultFOV = GameSettings.FieldOfView > 0 ? GameSettings.FieldOfView : _playerCamera.fieldOfView;
                GameSettings.OnFOVChanged += HandleBaseFOVChanged;
            }

            UpdateWeaponSpecificADSSettings();
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnReloadStateChanged += HandleReloadStateChanged;
                _weaponController.OnWeaponChanged += HandleWeaponChanged;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnReloadStateChanged -= HandleReloadStateChanged;
                _weaponController.OnWeaponChanged -= HandleWeaponChanged;
                _weaponController.IsAimingDownSights = false;
            }

            GameSettings.OnFOVChanged -= HandleBaseFOVChanged;

            // Khôi phục Camera FOV an toàn khi component bị disable hoặc chuyển scene
            RestoreCameraFOV();
            IsAiming = false;
            AimProgress = 0f;
        }

        private void OnDestroy()
        {
            RestoreCameraFOV();
        }

        private void HandleBaseFOVChanged(float newFOV)
        {
            _defaultFOV = newFOV;
            if (!IsAiming && _playerCamera != null)
            {
                _playerCamera.fieldOfView = newFOV;
            }
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (isReloading)
            {
                _wasAimingBeforeReload = _isAimInputActive;
                _isAimInputActive = false; // Tự động hạ ngắm khi đang thay đạn
            }
            else
            {
                if (_wasAimingBeforeReload && Mouse.current != null && Mouse.current.rightButton.isPressed)
                {
                    _isAimInputActive = true; // Tiếp tục ngắm nếu người chơi vẫn giữ chuột phải
                }
            }
        }

        private void HandleWeaponChanged(string newWeaponName)
        {
            UpdateWeaponSpecificADSSettings();
        }

        private void UpdateWeaponSpecificADSSettings()
        {
            if (_weaponController == null || _weaponController.CurrentWeaponData == null) return;

            var data = _weaponController.CurrentWeaponData;
            if (data.AdsPosition != Vector3.zero)
            {
                _adsPosition = data.AdsPosition;
            }
            if (data.AdsRotation != Vector3.zero)
            {
                _adsRotation = data.AdsRotation;
            }
            if (data.AdsFOV > 10f)
            {
                _adsFOV = data.AdsFOV;
            }
            if (data.AdsSpeed > 0.1f)
            {
                _adsTransitionSpeed = data.AdsSpeed;
            }
        }

        private void Update()
        {
            HandleAimInput();
            UpdateAimState(Time.deltaTime);
        }

        private void HandleAimInput()
        {
            // Kiểm tra xem chuột có bị khóa và player có quyền điều khiển cục bộ không
            if (_playerMovement != null && !GameModeHelper.CanLocalControl(_playerMovement))
            {
                _isAimInputActive = false;
                return;
            }

            // Không cho phép ngắm khi đang nạp đạn
            if (_weaponController != null && _weaponController.IsReloading)
            {
                _isAimInputActive = false;
                return;
            }

            if (Mouse.current == null) return;

            if (_toggleMode)
            {
                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    _isAimInputActive = !_isAimInputActive;
                }
            }
            else
            {
                _isAimInputActive = Mouse.current.rightButton.isPressed;
            }
        }

        private void UpdateAimState(float deltaTime)
        {
            if (deltaTime <= 0.0001f) return;

            float targetProgress = _isAimInputActive ? 1.0f : 0.0f;
            AimProgress = Mathf.MoveTowards(AimProgress, targetProgress, _adsTransitionSpeed * deltaTime);
            IsAiming = AimProgress > 0.5f;

            if (_weaponController != null)
            {
                _weaponController.IsAimingDownSights = IsAiming;
            }

            // Nội suy vị trí và góc xoay
            CurrentAimPosition = Vector3.Lerp(_hipPosition, _adsPosition, AimProgress);
            CurrentAimRotation = Quaternion.Slerp(Quaternion.Euler(_hipRotation), Quaternion.Euler(_adsRotation), AimProgress);

            // Nội suy FOV camera
            if (_playerCamera != null)
            {
                float targetFov = Mathf.Lerp(_defaultFOV, _adsFOV, AimProgress);
                _playerCamera.fieldOfView = targetFov;
            }
        }

        private void RestoreCameraFOV()
        {
            if (_playerCamera != null)
            {
                _playerCamera.fieldOfView = _defaultFOV;
            }
        }
    }
}
