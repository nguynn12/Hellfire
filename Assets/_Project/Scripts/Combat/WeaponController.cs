// Script: WeaponController.cs

// Mục đích: Quản lý toàn bộ vũ khí súng tầm xa (Ranged Guns), bắn Hitscan, nạp đạn vô hạn không giới hạn số lần.

//           Hỗ trợ Nâng Cấp Vũ Khí Toàn Cục (Global Weapon Stats & Modifiers) — giữ nguyên và áp dụng cho TẤT CẢ vũ khí khi đổi súng.

// Môi trường thực thi: Cả hai (Client dự đoán & phát hiệu ứng, Server xác thực & trừ máu).



using System;

using System.Collections;

using System.Collections.Generic;

using Hellfire.Player;

using Hellfire.Networking;
using Hellfire.Core;

using Unity.Netcode;

using UnityEngine;

using UnityEngine.InputSystem;



namespace Hellfire.Combat

{

    [DisallowMultipleComponent]

    public class WeaponController : NetworkBehaviour

    {

        // ================= GIỚI HẠN VŨ KHÍ (SLOT LIMITS) =================

        public const int MaxWeaponSlots = 2; // Chỉ có thể cầm tối đa 2 loại súng (Slot 1 & Slot 2)



        [Header("Weapon Configuration")]

        [SerializeField] private WeaponData _currentWeapon;

        [SerializeField] private List<WeaponData> _availableWeapons = new List<WeaponData>();

        [Tooltip("Súng lục mặc định khi bắt đầu game (luôn có sẵn ở Slot 1)")]

        [SerializeField] private WeaponData _starterPistolData;

        [Tooltip("Prefab vật phẩm rơi trên sàn đấu khi nhặt đổi súng")]

        [SerializeField] private GameObject _itemPickupPrefab;



        [Header("References")]

        [SerializeField] private Camera _playerCamera;

        [SerializeField] private Transform _weaponHolder;

        [SerializeField] private Transform _worldWeaponHolder;

        [SerializeField] private Transform _weaponMuzzlePoint;

        [SerializeField] private Health _playerHealth;

        [SerializeField] private AudioSource _audioSource;



        [Header("Visual Effects (Cosmetic)")]

        [SerializeField] private float _tracerDuration = 0.05f;



        private PlayerMovement _playerMovement;



        private Vector3 _recoilOffset;

        private Vector3 _recoilRotation;



        // ADS, Input Buffering, Sway & Bobbing

        private bool _isAiming = false;

        private float _currentAdsFactor = 0f;

        [SerializeField] private float _defaultCameraFov = 90f;

        private bool _hasBufferedFireInput = false;

        private Vector3 _currentSwayOffset = Vector3.zero;

        private Vector3 _targetSwayOffset = Vector3.zero;

        private Vector3 _bobOffset = Vector3.zero;

        private float _bobTimer = 0f;



        // ================= GIỚI HẠN TỐI ĐA (MAX CAPS) CHO CHỈ SỐ SÚNG =================

        public const float MAX_DAMAGE_MULTIPLIER = 3.0f;       // Tối đa x3 sát thương (+200%)

        public const float MAX_APS_MULTIPLIER = 2.5f;          // Tối đa x2.5 tốc độ bắn (+150%)

        public const float MAX_ARMOR_PENETRATION = 50f;        // Tối đa 50 điểm xuyên giáp

        public const float MAX_CRIT_CHANCE = 1.0f;             // Tối đa 100% tỉ lệ chí mạng

        public const float MAX_CRIT_MULTIPLIER = 3.0f;         // Tối đa +300% sát thương chí mạng

        public const float MAX_RELOAD_SPEED_MULTIPLIER = 2.5f; // Tối đa x2.5 tốc độ nạp (giảm thời gian nạp còn 40%)

        public const float MAX_ACCURACY_BONUS = 0.65f;         // Giảm tối đa 65% độ tản đạn (tăng độ chính xác)

        public const float MAX_BONUS_MAG_PERCENT = 1.0f;       // Tối đa +100% kích thước băng đạn (gấp đôi)

        public const float MAX_RECOIL_REDUCTION = 0.70f;       // Triệt tiêu tối đa 70% độ giật



        // ================= GLOBAL WEAPON STATS (FALLBACK OFFLINE + NETWORK VARIABLES) =================

        private float _rawGlobalDamageMultiplier = 1.0f;

        private float _rawGlobalAttackSpeedMultiplier = 1.0f;

        private float _rawGlobalArmorPenetration = 0f;

        private float _rawGlobalCritChanceBonus = 0f;

        private float _rawGlobalCritMultiplierBonus = 0f;

        private float _rawGlobalReloadSpeedMultiplier = 1.0f;

        private float _rawGlobalAccuracyBonus = 0f;

        private float _rawGlobalBonusMagPercent = 0f;

        private float _rawGlobalRecoilReduction = 0f;

        private int _rawGlobalUpgradeLevel = 0;



        public NetworkVariable<float> GlobalDamageMultiplierNet { get; } = new NetworkVariable<float>(

            1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalAttackSpeedMultiplierNet { get; } = new NetworkVariable<float>(

            1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalArmorPenetrationNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalCritChanceBonusNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalCritMultiplierBonusNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalReloadSpeedMultiplierNet { get; } = new NetworkVariable<float>(

            1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalAccuracyBonusNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalBonusMagPercentNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<float> GlobalRecoilReductionNet { get; } = new NetworkVariable<float>(

            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        public NetworkVariable<int> GlobalUpgradeLevelNet { get; } = new NetworkVariable<int>(

            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server

        );



        // Safe Accessors cho chỉ số hỏa lực toàn cục (hỗ trợ đọc an toàn cả Online lẫn Offline)

        public float GlobalDamageMultiplier => IsSpawned ? GlobalDamageMultiplierNet.Value : _rawGlobalDamageMultiplier;

        public float GlobalAttackSpeedMultiplier => IsSpawned ? GlobalAttackSpeedMultiplierNet.Value : _rawGlobalAttackSpeedMultiplier;

        public float GlobalArmorPenetration => IsSpawned ? GlobalArmorPenetrationNet.Value : _rawGlobalArmorPenetration;

        public float GlobalCritChanceBonus => IsSpawned ? GlobalCritChanceBonusNet.Value : _rawGlobalCritChanceBonus;

        public float GlobalCritMultiplierBonus => IsSpawned ? GlobalCritMultiplierBonusNet.Value : _rawGlobalCritMultiplierBonus;

        public float GlobalReloadSpeedMultiplier => IsSpawned ? GlobalReloadSpeedMultiplierNet.Value : _rawGlobalReloadSpeedMultiplier;

        public float GlobalAccuracyBonus => IsSpawned ? GlobalAccuracyBonusNet.Value : _rawGlobalAccuracyBonus;

        public float GlobalBonusMagPercent => IsSpawned ? GlobalBonusMagPercentNet.Value : _rawGlobalBonusMagPercent;

        public float GlobalRecoilReduction => IsSpawned ? GlobalRecoilReductionNet.Value : _rawGlobalRecoilReduction;

        public int GlobalUpgradeLevel => IsSpawned ? GlobalUpgradeLevelNet.Value : _rawGlobalUpgradeLevel;



        public int CurrentMagSize => GetEffectiveMagSize(_currentWeapon);



        public int GetEffectiveMagSize(WeaponData weapon = null)

        {

            var w = weapon != null ? weapon : _currentWeapon;

            if (w == null) return 0;

            int bonus = Mathf.RoundToInt(w.MagSize * GlobalBonusMagPercent);

            return Mathf.Min(Mathf.RoundToInt(w.MagSize * (1.0f + MAX_BONUS_MAG_PERCENT)), w.MagSize + bonus);

        }



        public float EffectiveReloadTime => (_currentWeapon != null && _currentWeapon.ReloadTime > 0.01f)

            ? (_currentWeapon.ReloadTime / Mathf.Clamp(GlobalReloadSpeedMultiplier, 0.5f, MAX_RELOAD_SPEED_MULTIPLIER))

            : 0f;



        public float CurrentDamage

        {

            get

            {

                if (_currentWeapon == null) return 0f;

                int starLevel = CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.UpgradeLevel : 0;

                float levelMult = 1.0f + (starLevel * 0.20f);

                float buffMult = _buffManager != null ? _buffManager.DamageMultiplier : 1.0f;

                return _currentWeapon.BaseDamage * levelMult * GlobalDamageMultiplier * buffMult;

            }

        }



        public float CurrentAPS

        {

            get

            {

                if (_currentWeapon == null) return 0f;

                int starLevel = CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.UpgradeLevel : 0;

                return _currentWeapon.AttackRateAPS * (1.0f + starLevel * 0.05f) * GlobalAttackSpeedMultiplier;

            }

        }



        public float CurrentArmorPen => _currentWeapon != null ? (_currentWeapon.ArmorPenetration + GlobalArmorPenetration) : 0f;

        public float CurrentCritChance => _currentWeapon != null ? Mathf.Clamp01(_currentWeapon.CritChance + GlobalCritChanceBonus) : 0f;

        public float CurrentCritDamageMultiplier => _currentWeapon != null ? (_currentWeapon.CritDamageMultiplier + GlobalCritMultiplierBonus) : 1.5f;



        // Danh sách Modifier toàn cục (gắn ngọc/phụ kiện có hiệu lực trên mọi khẩu súng)

        private readonly List<IWeaponModifier> _globalModifiers = new List<IWeaponModifier>();



        // Runtime states

        private readonly List<RuntimeWeapon> _runtimeWeapons = new List<RuntimeWeapon>();

        private int _currentAmmo;

        private float _nextFireTime;

        private bool _isReloading;

        private float _reloadTimer;

        private int _currentWeaponIndex = 0;



        private static Material _cachedFxMaterial;

        private PlayerBuffManager _buffManager;



        // Events for UI HUD

        public event Action<int, int> OnAmmoChanged;              // currentAmmo, magSize

        public event Action<int, int, int> OnAmmoDetailsChanged;   // currentAmmo, magSize, reserveAmmo

        public event Action<string> OnWeaponChanged;               // weaponName

        public event Action<string> OnWeaponSwitched;              // weaponName (alias for uGUI GameplayHUD)

        public event Action<bool> OnReloadStateChanged;           // isReloading

        public event Action<bool> OnHitTargetConfirmed;           // isHeadshot

        public event Action<int> OnWeaponUpgraded;                 // newLevel



        public WeaponData CurrentWeaponData => CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.BaseData : _currentWeapon;

        public RuntimeWeapon CurrentRuntimeWeapon => (_runtimeWeapons != null && _currentWeaponIndex >= 0 && _currentWeaponIndex < _runtimeWeapons.Count) ? _runtimeWeapons[_currentWeaponIndex] : null;

        public IReadOnlyList<RuntimeWeapon> RuntimeWeapons => _runtimeWeapons;

        public IReadOnlyList<WeaponData> AvailableWeapons => _availableWeapons;

        public IReadOnlyList<IWeaponModifier> GlobalModifiers => _globalModifiers;



        public int CurrentWeaponIndex => _currentWeaponIndex;

        public int CurrentAmmo => _currentAmmo;

        public int CurrentReserveAmmo => CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.ReserveAmmo : (_currentWeapon != null ? _currentWeapon.MaxReserveAmmo : 0);

        public bool IsReloading => _isReloading;

        public float ReloadProgress => (_isReloading && _currentWeapon != null && EffectiveReloadTime > 0.01f) ? Mathf.Clamp01(1.0f - (_reloadTimer / EffectiveReloadTime)) : 0f;

        public float CurrentAdsFactor => _currentAdsFactor;

        public bool IsAiming => _isAiming;



        private void Awake()

        {

            AutoResolveReferences();

        }



        private void AutoResolveReferences()

        {

            if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>(true);

            if (_playerCamera != null && _playerCamera.fieldOfView > 0)

            {

                _defaultCameraFov = _playerCamera.fieldOfView;

            }

            if (_playerHealth == null) _playerHealth = GetComponent<Health>();

            if (_audioSource == null)

            {

                _audioSource = GetComponent<AudioSource>();

                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

                _audioSource.spatialBlend = 0f;

            }



            if (_weaponHolder == null)

            {

                _weaponHolder = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");

            }



            if (_weaponMuzzlePoint == null)

            {

                var viewmodel = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel/GunMesh");

                if (viewmodel != null) _weaponMuzzlePoint = viewmodel;

                else if (_weaponHolder != null) _weaponMuzzlePoint = _weaponHolder;

            }



            if (_buffManager == null) _buffManager = GetComponent<PlayerBuffManager>();

            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();



        }



        private void Start()

        {

            if (GameModeHelper.IsOffline && !IsSpawned)

            {

                InitializeWeapons();

                if (OfflineManager.Instance != null && OfflineManager.Instance.InitialWeaponIndex >= 0 && OfflineManager.Instance.InitialWeaponIndex < _availableWeapons.Count)

                {

                    _currentWeaponIndex = OfflineManager.Instance.InitialWeaponIndex;

                    _currentWeapon = _availableWeapons[_currentWeaponIndex];

                    _currentAmmo = _currentWeapon.MagSize;

                }

                UpdateWeaponVisuals(_currentWeaponIndex);

                NotifyWeaponUI();

            }

        }



        private void InitializeWeapons()

        {

            if (_runtimeWeapons.Count == 0)

            {

                // Tìm súng lục Pistol khởi đầu: ưu tiên _starterPistolData, sau đó tìm trong _availableWeapons hoặc _currentWeapon

                WeaponData starter = _starterPistolData;

                if (starter == null && _currentWeapon != null && _currentWeapon.WeaponId.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0)

                {

                    starter = _currentWeapon;

                }

                if (starter == null && _availableWeapons != null && _availableWeapons.Count > 0)

                {

                    starter = _availableWeapons.Find(w => w != null && w.WeaponId.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (starter == null) starter = _availableWeapons[0];

                }



                // Luôn bắt đầu ván đấu với duy nhất 1 khẩu súng lục (Pistol) ở Slot 1, slot 2 để trống chờ nhặt

                _availableWeapons.Clear();

                _runtimeWeapons.Clear();



                if (starter != null)

                {

                    _availableWeapons.Add(starter);

                    _runtimeWeapons.Add(new RuntimeWeapon(starter));

                    _currentWeapon = starter;

                    _currentWeaponIndex = 0;

                    _currentAmmo = starter.MagSize;

                }

            }



            if (_availableWeapons.Count > 0 && _currentWeapon == null)

            {

                _currentWeapon = _availableWeapons[0];

                _currentWeaponIndex = 0;

            }



            if (_currentWeapon != null && _currentAmmo <= 0)

            {

                _currentAmmo = _currentWeapon.MagSize;

            }

        }



        public override void OnNetworkSpawn()

        {

            AutoResolveReferences();

            InitializeWeapons();



            _rawGlobalDamageMultiplier = GlobalDamageMultiplierNet.Value;

            _rawGlobalAttackSpeedMultiplier = GlobalAttackSpeedMultiplierNet.Value;

            _rawGlobalArmorPenetration = GlobalArmorPenetrationNet.Value;

            _rawGlobalCritChanceBonus = GlobalCritChanceBonusNet.Value;

            _rawGlobalCritMultiplierBonus = GlobalCritMultiplierBonusNet.Value;

            _rawGlobalReloadSpeedMultiplier = GlobalReloadSpeedMultiplierNet.Value;

            _rawGlobalAccuracyBonus = GlobalAccuracyBonusNet.Value;

            _rawGlobalBonusMagPercent = GlobalBonusMagPercentNet.Value;

            _rawGlobalRecoilReduction = GlobalRecoilReductionNet.Value;

            _rawGlobalUpgradeLevel = GlobalUpgradeLevelNet.Value;



            GlobalUpgradeLevelNet.OnValueChanged += HandleGlobalUpgradeLevelChanged;



            if (IsOwner)

            {

                UpdateWeaponVisuals(_currentWeaponIndex);

                NotifyWeaponUI();

            }

        }



        public override void OnNetworkDespawn()

        {

            GlobalUpgradeLevelNet.OnValueChanged -= HandleGlobalUpgradeLevelChanged;

        }



        private void HandleGlobalUpgradeLevelChanged(int prevVal, int newVal)

        {

            _rawGlobalUpgradeLevel = newVal;

            if (IsOwner)

            {

                NotifyWeaponUI();

                OnWeaponUpgraded?.Invoke(newVal);

            }

        }



        private void Update()

        {

            if (!GameModeHelper.CanLocalControl(this)) return;



            // Nếu người chơi đang gục hoặc chết -> Không cho phép bắn / nạp đạn (kiểm tra an toàn cả Online lẫn Offline)

            if (_playerHealth != null)

            {

                bool isDownedOrDead = _playerHealth.IsSpawned

                    ? (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value)

                    : (_playerHealth.IsDownedValue || _playerHealth.IsDeadValue);



                if (isDownedOrDead)

                {

                    if (_isReloading) CancelReload();

                    return;

                }

            }



            HandleAdsInput();

            HandleViewmodelSwayAndBobbing();

            HandleWeaponSwitching();

            HandleReloading();

            HandleAttackInput();

            HandleRecoilRecovery();

        }



        private void HandleAdsInput()

        {

            _isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed 

                        && !_isReloading && _currentWeapon != null && _currentWeapon.HasAds;



            _currentAdsFactor = Mathf.MoveTowards(_currentAdsFactor, _isAiming ? 1.0f : 0.0f, Time.deltaTime * 10f);



            if (_playerCamera != null && _currentWeapon != null)

            {

                _playerCamera.fieldOfView = Mathf.Lerp(_defaultCameraFov, _currentWeapon.AdsFov, _currentAdsFactor);

            }

        }



        private void HandleViewmodelSwayAndBobbing()

        {

            // 1. Mouse Sway: Độ trễ và nghiêng theo quán tính lia chuột

            Vector2 mouseDelta = Vector2.zero;

            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)

            {

                mouseDelta = Mouse.current.delta.ReadValue() * 0.0015f;

            }

            float swayX = Mathf.Clamp(-mouseDelta.x, -0.04f, 0.04f);

            float swayY = Mathf.Clamp(-mouseDelta.y, -0.04f, 0.04f);

            _targetSwayOffset = new Vector3(swayX, swayY, 0f);

            _currentSwayOffset = Vector3.Lerp(_currentSwayOffset, _targetSwayOffset, Time.deltaTime * 12f);

            _targetSwayOffset = Vector3.Lerp(_targetSwayOffset, Vector3.zero, Time.deltaTime * 8f);



            // 2. Movement Bobbing: Nhấp nhô tự nhiên theo bước chân

            if (_playerMovement != null && _playerMovement.IsMoving)

            {

                float speedMult = _playerMovement.IsSprinting ? 14f : 9f;

                _bobTimer += Time.deltaTime * speedMult;

                float bobX = Mathf.Cos(_bobTimer * 0.5f) * 0.012f;

                float bobY = Mathf.Sin(_bobTimer) * 0.012f;

                _bobOffset = new Vector3(bobX, bobY, 0f);

            }

            else

            {

                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * 8f);

            }

        }



        private void HandleRecoilRecovery()

        {

            float recoverySpeed = _currentWeapon != null ? _currentWeapon.RecoilRecoverySpeed : 12f;

            _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, Time.deltaTime * recoverySpeed);

            _recoilRotation = Vector3.Lerp(_recoilRotation, Vector3.zero, Time.deltaTime * recoverySpeed);



            if (_currentViewmodelInstance != null && _currentWeapon != null)

            {

                Vector3 basePos = Vector3.Lerp(_currentWeapon.ViewmodelOffset, _currentWeapon.AdsOffset, _currentAdsFactor);

                Vector3 baseRot = _currentWeapon.ViewmodelRotation;

                Vector3 motionOffset = (_currentSwayOffset + _bobOffset) * (1.0f - _currentAdsFactor * 0.75f);



                _currentViewmodelInstance.transform.localPosition = basePos + _recoilOffset + motionOffset;

                _currentViewmodelInstance.transform.localRotation = Quaternion.Euler(baseRot + _recoilRotation);

            }

        }



        private void HandleWeaponSwitching()

        {

            if (_availableWeapons == null || _availableWeapons.Count <= 1 || _isReloading) return;



            // 1. Chuyển đổi bằng phím số 1 và 2

            if (Keyboard.current != null)

            {

                if (Keyboard.current.digit1Key.wasPressedThisFrame && _availableWeapons.Count > 0)

                {

                    SwitchWeapon(0);

                    return;

                }

                if (Keyboard.current.digit2Key.wasPressedThisFrame && _availableWeapons.Count > 1)

                {

                    SwitchWeapon(1);

                    return;

                }

            }



            // 2. Chuyển đổi bằng con lăn chuột (Mouse Scroll Wheel)

            if (Mouse.current != null && _availableWeapons.Count > 1)

            {

                float scroll = Mouse.current.scroll.ReadValue().y;

                if (Mathf.Abs(scroll) > 0.05f)

                {

                    int nextIndex = (_currentWeaponIndex == 0) ? 1 : 0;

                    SwitchWeapon(nextIndex);

                }

            }

        }



        public void SwitchWeapon(int index)

        {

            if (index < 0 || index >= _availableWeapons.Count || _availableWeapons[index] == null) return;

            if (index == _currentWeaponIndex && _currentWeapon == _availableWeapons[index]) return;



            // Lưu số đạn của vũ khí hiện tại vào RuntimeWeapon

            if (CurrentRuntimeWeapon != null)

            {

                CurrentRuntimeWeapon.CurrentAmmo = _currentAmmo;

            }



            _currentWeaponIndex = index;

            _currentWeapon = _availableWeapons[index];

            _isReloading = false;

            _hasBufferedFireInput = false;

            _nextFireTime = Time.time + 0.15f;



            // Khôi phục số đạn đã lưu của vũ khí mới

            if (CurrentRuntimeWeapon != null)

            {

                _currentAmmo = CurrentRuntimeWeapon.CurrentAmmo;

            }

            else

            {

                _currentAmmo = _currentWeapon.MagSize;

            }



            UpdateWeaponVisuals(index);

            NotifyWeaponUI();



            if (!GameModeHelper.IsOffline && IsSpawned)

            {

                SwitchWeaponServerRpc(index);

            }



            Debug.Log($"[WeaponController] Đã đổi sang vũ khí: {_currentWeapon.WeaponName} (Kế thừa Nâng cấp Level {GlobalUpgradeLevel})");

        }



        private GameObject _currentViewmodelInstance;

        private GameObject _currentWorldModelInstance;



        private void TriggerAmmoChanged()

        {

            int mag = CurrentMagSize;

            int reserve = CurrentReserveAmmo;

            OnAmmoChanged?.Invoke(_currentAmmo, mag);

            OnAmmoDetailsChanged?.Invoke(_currentAmmo, mag, reserve);

        }



        public void UpdateWeaponVisuals(int index)

        {

            if (_weaponHolder == null)

            {

                _weaponHolder = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");

            }

            if (_weaponHolder == null) return;



            // Đảm bảo WeaponViewmodel luôn chuẩn hóa vị trí, góc xoay (0,0,0) và scale (1,1,1) chuẩn hệ FPS

            _weaponHolder.localPosition = Vector3.zero;

            _weaponHolder.localRotation = Quaternion.identity;

            _weaponHolder.localScale = Vector3.one;



            // 1. Tắt placeholder box GunMesh nếu có

            var oldPlaceholder = _weaponHolder.Find("GunMesh");

            if (oldPlaceholder != null)

            {

                oldPlaceholder.gameObject.SetActive(false);

            }



            // 2. Dọn dẹp model súng cũ

            if (_currentViewmodelInstance != null)

            {

                Destroy(_currentViewmodelInstance);

                _currentViewmodelInstance = null;

            }



            for (int i = _weaponHolder.childCount - 1; i >= 0; i--)

            {

                Transform child = _weaponHolder.GetChild(i);

                if (child.name != "GunMesh" && (child.name.StartsWith("[Model]") || child.name.StartsWith("ViewModel_") || child.name.StartsWith("[VM]")))

                {

                    Destroy(child.gameObject);

                }

            }



            if (index < 0 || index >= _availableWeapons.Count) return;

            WeaponData data = _availableWeapons[index];

            if (data == null) return;



            // 3. Khởi tạo mô hình súng mới (từ Prefab hoặc tự sinh Procedural)

            if (data.ViewmodelPrefab != null)

            {

                _currentViewmodelInstance = Instantiate(data.ViewmodelPrefab, _weaponHolder);

                _currentViewmodelInstance.name = $"[Model]_{data.WeaponId}";

                _currentViewmodelInstance.transform.localPosition = data.ViewmodelOffset;

                _currentViewmodelInstance.transform.localRotation = Quaternion.Euler(data.ViewmodelRotation);

            }

            else

            {

                _currentViewmodelInstance = ProceduralWeaponModelBuilder.BuildProceduralGun(data, false);

                _currentViewmodelInstance.transform.SetParent(_weaponHolder, false);

                _currentViewmodelInstance.transform.localPosition = data.ViewmodelOffset;

                _currentViewmodelInstance.transform.localRotation = Quaternion.Euler(data.ViewmodelRotation);

            }



            // Vô hiệu hóa va chạm Collider với CharacterController người chơi

            var colliders = _currentViewmodelInstance.GetComponentsInChildren<Collider>();

            for (int i = 0; i < colliders.Length; i++)

            {

                colliders[i].enabled = false;

            }



            // Gán layer Viewmodel (Layer 7) cho toàn bộ bộ phận súng

            int vmLayer = LayerMask.NameToLayer("Viewmodel");

            if (vmLayer >= 0)

            {

                SetLayerRecursively(_currentViewmodelInstance, vmLayer);

            }



            // 4. Tìm Socket đầu nòng súng (Muzzle / MuzzlePoint)

            _weaponMuzzlePoint = FindMuzzlePoint(_currentViewmodelInstance.transform);

            if (_weaponMuzzlePoint == null)

            {

                _weaponMuzzlePoint = _currentViewmodelInstance.transform;

            }



            // 5. Cập nhật WorldModel cho đồng đội góc nhìn thứ 3

            UpdateWorldModelVisuals(data);

        }



        private void UpdateWorldModelVisuals(WeaponData data)

        {

            if (_worldWeaponHolder == null)

            {

                _worldWeaponHolder = transform.Find("WorldWeaponHolder") ?? transform.Find("RightHandSocket") ?? transform.Find("PlayerBodyMesh/RightHandSocket");

                if (_worldWeaponHolder == null)

                {

                    var holderObj = new GameObject("WorldWeaponHolder");

                    holderObj.transform.SetParent(transform, false);

                    holderObj.transform.localPosition = new Vector3(0.28f, 0.85f, 0.35f);

                    _worldWeaponHolder = holderObj.transform;

                }

            }



            if (_currentWorldModelInstance != null)

            {

                Destroy(_currentWorldModelInstance);

                _currentWorldModelInstance = null;

            }



            for (int i = _worldWeaponHolder.childCount - 1; i >= 0; i--)

            {

                Transform child = _worldWeaponHolder.GetChild(i);

                if (child != null) Destroy(child.gameObject);

            }



            if (data != null && data.WorldModelPrefab != null)

            {

                _currentWorldModelInstance = Instantiate(data.WorldModelPrefab, _worldWeaponHolder);

                _currentWorldModelInstance.name = $"[WorldModel]_{data.WeaponId}";

                _currentWorldModelInstance.transform.localPosition = Vector3.zero;

                _currentWorldModelInstance.transform.localRotation = Quaternion.identity;



                var cols = _currentWorldModelInstance.GetComponentsInChildren<Collider>();

                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;



                bool isLocalPlayer = IsOwner || GameModeHelper.IsOffline || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

                if (isLocalPlayer)

                {

                    int bodyLayer = LayerMask.NameToLayer("PlayerBody");

                    SetLayerRecursively(_currentWorldModelInstance, bodyLayer >= 0 ? bodyLayer : 6);

                }

            }

        }



        private static void SetLayerRecursively(GameObject obj, int newLayer)

        {

            if (obj == null) return;

            obj.layer = newLayer;

            foreach (Transform child in obj.transform)

            {

                if (child != null) SetLayerRecursively(child.gameObject, newLayer);

            }

        }



        private static Transform FindMuzzlePoint(Transform root)

        {

            if (root == null) return null;

            if (root.name.Equals("Muzzle", StringComparison.OrdinalIgnoreCase) ||

                root.name.Equals("MuzzlePoint", StringComparison.OrdinalIgnoreCase) ||

                root.name.Equals("Socket_Muzzle", StringComparison.OrdinalIgnoreCase))

            {

                return root;

            }



            foreach (Transform child in root)

            {

                var found = FindMuzzlePoint(child);

                if (found != null) return found;

            }

            return null;

        }



        [ServerRpc]

        private void SwitchWeaponServerRpc(int index)

        {

            SwitchWeaponClientRpc(index);

        }



        public bool HasWeapon(WeaponData weapon)

        {

            return weapon != null && _availableWeapons.Contains(weapon);

        }



        public void AddAvailableWeapon(WeaponData weapon)

        {

            AddOrSwitchWeapon(weapon);

        }



        /// <summary>

        /// Nhặt vũ khí mới hoặc đổi vũ khí:

        /// - Nếu đã có: chuyển tay sang súng đó và nạp thêm đạn dự trữ.

        /// - Nếu chưa có và túi đồ < 2 súng: thêm vào Slot 2 và chuyển sang cầm súng mới.

        /// - Nếu đã đủ 2 súng: VỨT SÚNG HIỆN TẠI RA ĐẤT (Drop & Swap) và trang bị súng mới vào đúng slot này.

        /// </summary>

        public void AddOrSwitchWeapon(WeaponData weapon, int initialAmmo = -1, int initialReserve = -1)

        {

            if (weapon == null) return;



            // Lưu số đạn của vũ khí hiện tại vào RuntimeWeapon trước khi xử lý

            if (CurrentRuntimeWeapon != null)

            {

                CurrentRuntimeWeapon.CurrentAmmo = _currentAmmo;

            }



            // 1. Trường hợp: Vũ khí đã có trong túi 2 slot

            int existingIndex = _availableWeapons.IndexOf(weapon);

            if (existingIndex >= 0)

            {

                if (existingIndex != _currentWeaponIndex)

                {

                    SwitchWeapon(existingIndex);

                }



                // Nạp thêm đạn dự trữ nếu nhặt trùng súng

                if (CurrentRuntimeWeapon != null)

                {

                    int addReserve = initialReserve >= 0 ? initialReserve : (initialAmmo >= 0 ? initialAmmo : weapon.MagSize);

                    CurrentRuntimeWeapon.ReserveAmmo = Mathf.Min(CurrentRuntimeWeapon.BaseData.MaxReserveAmmo, CurrentRuntimeWeapon.ReserveAmmo + addReserve);

                    NotifyWeaponUI();

                }

                return;

            }



            // 2. Trường hợp: Túi đồ chưa đầy (< 2 slot)

            if (_availableWeapons.Count < MaxWeaponSlots)

            {

                var newRw = new RuntimeWeapon(weapon);

                if (initialAmmo >= 0) newRw.CurrentAmmo = initialAmmo;

                if (initialReserve >= 0) newRw.ReserveAmmo = initialReserve;



                _availableWeapons.Add(weapon);

                _runtimeWeapons.Add(newRw);



                int newIndex = _availableWeapons.Count - 1;

                SwitchWeapon(newIndex);



                Debug.Log($"[WeaponController] Đã thêm vũ khí vào Slot {newIndex + 1}: {weapon.WeaponName}");



                if (IsServer && !IsOwner && !GameModeHelper.IsOffline)

                {

                    AddWeaponClientRpc(weapon.WeaponId, newRw.CurrentAmmo, newRw.ReserveAmmo);

                }

                return;

            }



            // 3. Trường hợp: Đã đủ 2 súng -> RỚT SÚNG ĐANG CẦM TRÊN TAY (Drop & Swap)

            WeaponData oldWeapon = _availableWeapons[_currentWeaponIndex];

            int dropAmmo = _currentAmmo;

            int dropReserve = CurrentReserveAmmo;



            // Sinh vật phẩm rơi ItemPickup cho súng cũ

            DropWeaponPickup(oldWeapon, dropAmmo, dropReserve);



            // Thay thế súng mới vào đúng slot hiện tại

            _availableWeapons[_currentWeaponIndex] = weapon;

            var replacedRw = new RuntimeWeapon(weapon);

            if (initialAmmo >= 0) replacedRw.CurrentAmmo = initialAmmo;

            if (initialReserve >= 0) replacedRw.ReserveAmmo = initialReserve;

            _runtimeWeapons[_currentWeaponIndex] = replacedRw;



            _currentWeapon = weapon;

            _currentAmmo = replacedRw.CurrentAmmo;

            _isReloading = false;

            _hasBufferedFireInput = false;

            _nextFireTime = Time.time + 0.15f;



            UpdateWeaponVisuals(_currentWeaponIndex);

            NotifyWeaponUI();



            Debug.Log($"[WeaponController] [DROP & SWAP] Đã rớt súng {oldWeapon.WeaponName} ({dropAmmo}/{dropReserve}) và trang bị súng mới {weapon.WeaponName} ({_currentAmmo}/{replacedRw.ReserveAmmo}) vào Slot {_currentWeaponIndex + 1}!");



            if (IsServer && !IsOwner && !GameModeHelper.IsOffline)

            {

                ReplaceWeaponClientRpc(_currentWeaponIndex, weapon.WeaponId, replacedRw.CurrentAmmo, replacedRw.ReserveAmmo);

            }

        }



        /// <summary>

        /// Tạo vật phẩm súng rơi trên sàn đấu phía trước mặt người chơi.

        /// </summary>

        public void DropWeaponPickup(WeaponData weaponToDrop, int ammoInMag, int reserveAmmo)

        {

            if (weaponToDrop == null) return;



            // Xác định tọa độ rơi: cách người chơi 1.2m về phía trước mặt

            Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;

            if (_dropRaycastMask == 0) _dropRaycastMask = ~LayerMask.GetMask("Player", "Viewmodel");
            if (Physics.Raycast(dropPos, Vector3.down, out RaycastHit hit, 3.0f, _dropRaycastMask))

            {

                dropPos = hit.point + Vector3.up * 0.25f;

            }



            GameObject prefabToUse = _itemPickupPrefab;

            if (prefabToUse == null)

            {

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.Prefabs != null)

                {

                    foreach (var netPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)

                    {

                        if (netPrefab.Prefab != null && netPrefab.Prefab.GetComponent<Hellfire.Items.ItemPickup>() != null)

                        {

                            prefabToUse = netPrefab.Prefab;

                            break;

                        }

                    }

                }

            }



            if (prefabToUse != null)

            {

                GameObject droppedObj = Instantiate(prefabToUse, dropPos, Quaternion.identity);

                var pickup = droppedObj.GetComponent<Hellfire.Items.ItemPickup>();

                if (pickup != null)

                {

                    pickup.InitializeWeapon(weaponToDrop, ammoInMag, reserveAmmo);

                }



                var netObj = droppedObj.GetComponent<NetworkObject>();

                if (!GameModeHelper.IsOffline && netObj != null && !netObj.IsSpawned && (IsServer || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)))

                {

                    netObj.Spawn(true);

                }

            }

            else

            {

                Debug.LogWarning($"[WeaponController] Không tìm thấy ItemPickup prefab để rơi súng {weaponToDrop.WeaponName}!");

            }

        }



        [ClientRpc]

        private void AddWeaponClientRpc(string weaponId, int ammo, int reserve)

        {

            if (IsOwner) return;

            WeaponData w = FindWeaponDataById(weaponId);

            if (w != null && !_availableWeapons.Contains(w))

            {

                _availableWeapons.Add(w);

                var rw = new RuntimeWeapon(w) { CurrentAmmo = ammo, ReserveAmmo = reserve };

                _runtimeWeapons.Add(rw);

                _currentWeaponIndex = _availableWeapons.Count - 1;

                _currentWeapon = w;

                UpdateWorldModelVisuals(w);

            }

        }



        [ClientRpc]

        private void ReplaceWeaponClientRpc(int slotIndex, string weaponId, int ammo, int reserve)

        {

            if (IsOwner) return;

            WeaponData w = FindWeaponDataById(weaponId);

            if (w != null && slotIndex >= 0 && slotIndex < _availableWeapons.Count)

            {

                _availableWeapons[slotIndex] = w;

                _runtimeWeapons[slotIndex] = new RuntimeWeapon(w) { CurrentAmmo = ammo, ReserveAmmo = reserve };

                _currentWeaponIndex = slotIndex;

                _currentWeapon = w;

                UpdateWorldModelVisuals(w);

            }

        }



        private WeaponData FindWeaponDataById(string weaponId)

        {

            if (string.IsNullOrEmpty(weaponId)) return null;



            if (_availableWeapons != null)

            {

                var found = _availableWeapons.Find(w => w != null && w.WeaponId == weaponId);

                if (found != null) return found;

            }



            var allData = Resources.LoadAll<WeaponData>("Weapons");

            if (allData != null)

            {

                for (int i = 0; i < allData.Length; i++)

                {

                    if (allData[i] != null && allData[i].WeaponId == weaponId) return allData[i];

                }

            }



            return null;

        }



        [ClientRpc]

        private void SwitchWeaponClientRpc(int index)

        {

            if (IsOwner)

            {

                SwitchWeapon(index);

            }

            else

            {

                if (index >= 0 && index < _availableWeapons.Count)

                {

                    _currentWeaponIndex = index;

                    _currentWeapon = _availableWeapons[index];

                    UpdateWorldModelVisuals(_currentWeapon);

                }

            }

        }



        // ================= NẠP ĐẠN LIÊN TỤC KHÔNG GIỚI HẠN (INFINITE RELOAD) =================



        private void HandleReloading()

        {

            // Bấm R để nạp đạn bất cứ khi nào đạn chưa đầy băng (Đặc tả: Thay đạn vô hạn)

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !_isReloading && _currentWeapon != null)

            {

                if (_currentAmmo < CurrentMagSize)

                {

                    StartReload();

                }

            }



            if (_isReloading)

            {

                _reloadTimer -= Time.deltaTime;

                if (_reloadTimer <= 0f)

                {

                    CompleteReload();

                }

            }

        }



        private void StartReload()

        {

            if (_isReloading || _currentWeapon == null) return;

            int effectiveMag = CurrentMagSize;

            if (_currentAmmo >= effectiveMag) return;



            _isReloading = true;

            _reloadTimer = EffectiveReloadTime;

            OnReloadStateChanged?.Invoke(true);



            if (_audioSource != null && _currentWeapon.ReloadSound != null)

            {

                _audioSource.PlayOneShot(_currentWeapon.ReloadSound);

            }

        }



        private void CompleteReload()

        {

            _isReloading = false;

            int effectiveMag = CurrentMagSize;



            if (CurrentRuntimeWeapon != null)

            {

                CurrentRuntimeWeapon.CurrentAmmo = effectiveMag;

            }

            _currentAmmo = effectiveMag;



            OnReloadStateChanged?.Invoke(false);

            TriggerAmmoChanged();



            // Cơ chế giữ bắn liên tục (Hold to fire): Nếu người chơi vẫn đang giữ chuột trái thì tiếp tục bắn ngay sau khi nạp xong

            if (_currentWeapon != null && Mouse.current != null && Mouse.current.leftButton.isPressed && _currentAmmo > 0)

            {

                _nextFireTime = Time.time + 0.05f;

            }

        }



        private void CancelReload()

        {

            _isReloading = false;

            OnReloadStateChanged?.Invoke(false);

        }



        // ================= BẮN SÚNG TẦM XA (RANGED HITSCAN) =================



        private void HandleAttackInput()

        {

            if (_currentWeapon == null || _playerCamera == null || Cursor.lockState != CursorLockMode.Locked) return;

            if (Hellfire.UI.ShrineChoiceModalUI.Instance != null && Hellfire.UI.ShrineChoiceModalUI.Instance.IsOpen) return;



            bool triggerPressed = false;

            if (Mouse.current != null)

            {

                // Hỗ trợ giữ chuột trái để bắn liên tục cho tất cả vũ khí theo đúng nhịp APS

                triggerPressed = Mouse.current.leftButton.isPressed;



                if (Mouse.current.leftButton.wasPressedThisFrame)

                {

                    triggerPressed = true;

                    // Input buffering: Lưu lại lệnh bấm nếu nhấp sớm hơn thời gian hồi nòng trong khoảng 0.15s

                    if (Time.time < _nextFireTime && (_nextFireTime - Time.time) <= 0.15f)

                    {

                        _hasBufferedFireInput = true;

                    }

                }

            }



            bool canFireNow = Time.time >= _nextFireTime && !_isReloading;

            bool shouldFire = triggerPressed || (_hasBufferedFireInput && canFireNow);

            if (!shouldFire || !canFireNow) return;



            _hasBufferedFireInput = false;



            // Nếu hết đạn trong băng: tự động nạp đạn

            if (_currentAmmo <= 0)

            {

                if (_audioSource != null && _currentWeapon.EmptySound != null)

                {

                    _audioSource.PlayOneShot(_currentWeapon.EmptySound);

                }

                StartReload();

                _nextFireTime = Time.time + 0.25f;

                return;

            }



            ExecuteRangedFire();

        }



        private void ExecuteRangedFire()

        {

            if (OfflineManager.Instance == null || !OfflineManager.Instance.InfiniteAmmo)

            {

                if (CurrentRuntimeWeapon != null)

                {

                    CurrentRuntimeWeapon.ConsumeAmmo(1);

                    _currentAmmo = CurrentRuntimeWeapon.CurrentAmmo;

                }

                else

                {

                    _currentAmmo = Mathf.Max(0, _currentAmmo - 1);

                }

            }

            TriggerAmmoChanged();



            // Tính toán tốc độ ra đòn kết hợp hệ số tốc bắn toàn cục

            float finalAps = _currentWeapon.AttackRateAPS * GlobalAttackSpeedMultiplier;

            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAps));



            if (_audioSource != null && _currentWeapon.FireSound != null)

            {

                _audioSource.PlayOneShot(_currentWeapon.FireSound);

            }



            Vector3 cameraPos = _playerCamera.transform.position;

            Vector3 cameraForward = _playerCamera.transform.forward;
            Vector3 cameraRight = _playerCamera.transform.right;
            Vector3 cameraUp = _playerCamera.transform.up;

            int seed = UnityEngine.Random.Range(0, 100000);



            // Bắn viên đạn cosmetic (Option 1) từ đầu nòng súng

            int pellets = _currentWeapon.PelletCount;

            float range = _currentWeapon.Range;

            Vector3 muzzlePos = GetMuzzlePosition();



            float spreadMultiplier = Mathf.Clamp(1.0f - GlobalAccuracyBonus, 0.35f, 1.0f);

            float effectiveSpread;
            if (_currentWeapon.EnableJuiceEffects && _currentWeapon.SpreadDegrees > 0.01f)
            {
                float spreadDeg = _currentWeapon.SpreadDegrees * Mathf.Lerp(1.0f, _currentWeapon.AdsSpreadMultiplier, _currentAdsFactor) * spreadMultiplier;
                effectiveSpread = Mathf.Tan(spreadDeg * Mathf.Deg2Rad * 0.5f);
            }
            else
            {
                effectiveSpread = _currentWeapon.Spread * Mathf.Lerp(1.0f, _currentWeapon.AdsSpreadMultiplier, _currentAdsFactor) * spreadMultiplier;
            }



            for (int i = 0; i < pellets; i++)

            {

                Vector3 shootDir = CalculateSpreadDirection(cameraForward, effectiveSpread, seed + i);

                Vector3 endPoint;

                bool hasHit = PerformRaycastFiltered(cameraPos, shootDir, range, out var hit);

                if (hasHit)

                {

                    endPoint = hit.point;

                    // Đảm bảo đạn luôn phóng về phía trước mặt, không bao giờ bay ngược về sau

                    if (Vector3.Dot(endPoint - muzzlePos, cameraForward) < 0.2f)

                    {

                        endPoint = muzzlePos + cameraForward * Mathf.Max(0.6f, hit.distance);

                    }

                }

                else

                {

                    endPoint = cameraPos + shootDir * range;

                }



                SpawnCosmeticBullet(muzzlePos, endPoint);

            }

            SpawnMuzzleFlash(muzzlePos);



            // ================= ÁP DỤNG ĐỘ GIẬT (RECOIL SYSTEM) =================

            float recoilFactor = Mathf.Clamp(1.0f - GlobalRecoilReduction, 0.30f, 1.0f);



            // 1. Độ giật súng trên tay (ViewModel Kickback)

            float kickForce = (_currentWeapon != null && _currentWeapon.KickbackForce > 0.001f ? _currentWeapon.KickbackForce : 0.04f) * recoilFactor;

            float kickAngle = (_currentWeapon != null && _currentWeapon.KickbackAngle > 0.001f ? _currentWeapon.KickbackAngle : 2.5f) * recoilFactor;

            _recoilOffset += new Vector3(0f, 0.005f * recoilFactor, -kickForce);

            _recoilRotation += new Vector3(-kickAngle, UnityEngine.Random.Range(-0.8f, 0.8f) * recoilFactor, UnityEngine.Random.Range(-1f, 1f) * recoilFactor);



            // 2. Độ giật Camera hất lên (Camera Kick)

            if (_playerMovement != null && _currentWeapon != null)

            {

                float pitchKick = _currentWeapon.CameraRecoilPitch * recoilFactor;

                float yawKick = UnityEngine.Random.Range(-_currentWeapon.CameraRecoilYaw, _currentWeapon.CameraRecoilYaw) * recoilFactor;

                _playerMovement.AddCameraRecoil(pitchKick, yawKick);

                // 3. Lực đẩy giật lùi nhân vật (Player Pushback)
                if (_currentWeapon.EnableJuiceEffects && _currentWeapon.PlayerPushbackForce > 0.01f)
                {
                    float pushbackForce = _currentWeapon.PlayerPushbackForce * recoilFactor;
                    _playerMovement.AddPushback(-cameraForward * pushbackForce);
                }
            }

            // 4. Văng vỏ đạn (Shell Casing Ejection)
            if (_currentWeapon != null && _currentWeapon.EnableJuiceEffects && _currentWeapon.EnableShellEjection)
            {
                Transform ejectSocket = FindEjectionSocket(_currentViewmodelInstance != null ? _currentViewmodelInstance.transform : null);
                Vector3 ejectPos = ejectSocket != null
                    ? ejectSocket.position
                    : (muzzlePos - cameraForward * 0.22f + cameraRight * 0.14f + cameraUp * 0.05f);

                WeaponFxPool.GetOrCreate().EjectShell(
                    ejectPos,
                    cameraForward,
                    cameraRight,
                    cameraUp,
                    _currentWeapon.ShellEjectionVelocity,
                    _currentWeapon.ShellPrefab
                );
            }







            // Gửi ServerRpc yêu cầu Server xác thực độc lập hoặc chạy cục bộ khi Offline

            Vector3 clientOrigin = cameraPos;

            Vector3 baseDirection = cameraForward;

            if (GameModeHelper.IsOffline || !IsSpawned)

            {

                // XỬ LÝ OFFLINE: Thực thi raycast trừ máu quái trực tiếp tại máy cục bộ

                ExecuteOfflineHitscan(clientOrigin, baseDirection, seed, _currentWeaponIndex);

            }

            else

            {

                // XỬ LÝ ONLINE: Gửi gói tin RPC lên Server kiểm duyệt

                FireServerRpc(clientOrigin, baseDirection, seed, _currentWeaponIndex);

            }



            // Khi đạn hiện tại về 0: Tự động bắt đầu nạp đạn ngay lập tức (nếu không phải infinite ammo)

            if (_currentAmmo <= 0 && (OfflineManager.Instance == null || !OfflineManager.Instance.InfiniteAmmo))

            {

                StartReload();

            }

        }



        /// <summary>

        /// Xử lý Offline: Thực thi raycast trừ máu quái trực tiếp tại máy cục bộ (không RPC).

        /// </summary>

        public void ExecuteOfflineHitscan(Vector3 clientOrigin, Vector3 baseDirection, int seed, int weaponIndex)

        {

            ExecuteHitscanFireLogic(clientOrigin, baseDirection, seed, weaponIndex);

        }



        [ServerRpc]

        private void FireServerRpc(Vector3 clientOrigin, Vector3 baseDirection, int seed, int weaponIndex)

        {

            ExecuteHitscanFireLogic(clientOrigin, baseDirection, seed, weaponIndex);

        }



        public void ExecuteHitscanFireLogic(Vector3 clientOrigin, Vector3 baseDirection, int seed, int weaponIndex)

        {

            RuntimeWeapon rw = GetRuntimeWeaponByIndex(weaponIndex);

            if (rw == null) return;



            WeaponData weaponToUse = rw.BaseData;



            if (_playerHealth != null)

            {

                bool isDownedOrDead = _playerHealth.IsSpawned

                    ? (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value)

                    : (_playerHealth.IsDownedValue || _playerHealth.IsDeadValue);



                if (isDownedOrDead) return;

            }



            // Tính sát thương kết hợp Nâng Cấp Toàn Cục và Bùa Cuồng Nộ nếu có

            float armorPen = weaponToUse.ArmorPenetration + GlobalArmorPenetration;

            int pellets = weaponToUse.PelletCount;

            float range = weaponToUse.Range;



            for (int i = 0; i < pellets; i++)

            {

                Vector3 shootDir = CalculateSpreadDirection(baseDirection, weaponToUse.Spread, seed + i);



                if (PerformRaycastFiltered(clientOrigin, shootDir, range, out var hit))

                {

                    // 1. Kiểm tra Friendly Fire (đồng đội)

                    if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerMovement>() != null)

                    {

                        var hitHealth = hit.collider.GetComponentInParent<Health>();

                        if (hitHealth != null && hitHealth == _playerHealth)

                        {

                            continue; // Bỏ qua chính bản thân

                        }



                        if (hitHealth != null && hitHealth.NetworkObjectId != NetworkObjectId)

                        {

                            if (GameModeHelper.IsOffline || !IsSpawned)

                                SpawnImpactEffectInternal(hit.point, hit.normal, true);

                            else

                                SpawnImpactEffectClientRpc(hit.point, hit.normal, true);

                            continue;

                        }

                    }



                    // 2. Tính sát thương (kết hợp Global Stats và Modifier toàn cục)

                    float attackDamage = rw.CalculateAttackDamage(

                        GlobalDamageMultiplier,

                        GlobalAttackSpeedMultiplier,

                        GlobalArmorPenetration,

                        GlobalCritChanceBonus,

                        GlobalCritMultiplierBonus,

                        _globalModifiers,

                        out bool isCrit

                    );



                    // 3. Nhân hệ số bùa Cuồng nộ (BerserkerCharm +30%) nếu có

                    if (_buffManager != null)

                    {

                        attackDamage *= _buffManager.DamageMultiplier;

                    }



                    // 4. Đóng gói DamagePayload gửi sang mục tiêu

                    var hitbox = hit.collider.GetComponent<HitboxIdentifier>();

                    if (hitbox != null)

                    {

                        var targetDamageable = hitbox.Damageable;
                        bool wasDeadBefore = targetDamageable != null && targetDamageable.IsDead;

                        var payload = new DamagePayload(attackDamage, armorPen, hitbox.Type, OwnerClientId);

                        hitbox.ForwardDamage(payload);



                        rw.NotifyHit(payload, targetDamageable, _globalModifiers);

                        bool isKill = !wasDeadBefore && (targetDamageable != null && targetDamageable.IsDead);
                        if (isKill)
                        {
                            HandleLethalShotJuice(weaponToUse, shootDir, hit.collider.gameObject);
                        }



                        if (GameModeHelper.IsOffline || !IsSpawned)

                        {

                            OnHitTargetConfirmed?.Invoke(hitbox.Type == HitboxType.Head);

                            SpawnImpactEffectInternal(hit.point, hit.normal, true);

                        }

                        else

                        {

                            NotifyHitConfirmClientRpc(hitbox.Type == HitboxType.Head);

                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);

                        }

                    }

                    else

                    {

                        var damageable = hit.collider.GetComponentInParent<IDamageable>();

                        if (damageable != null && !damageable.IsPlayerTarget)

                        {

                            bool wasDeadBefore = damageable.IsDead;

                            var payload = new DamagePayload(attackDamage, armorPen, HitboxType.Torso, OwnerClientId);

                            damageable.TakeDamage(payload);



                            rw.NotifyHit(payload, damageable, _globalModifiers);

                            bool isKill = !wasDeadBefore && damageable.IsDead;
                            if (isKill)
                            {
                                HandleLethalShotJuice(weaponToUse, shootDir, hit.collider.gameObject);
                            }



                            if (GameModeHelper.IsOffline || !IsSpawned)

                            {

                                OnHitTargetConfirmed?.Invoke(false);

                                SpawnImpactEffectInternal(hit.point, hit.normal, true);

                            }

                            else

                            {

                                NotifyHitConfirmClientRpc(false);

                                SpawnImpactEffectClientRpc(hit.point, hit.normal, true);

                            }

                        }

                        else

                        {

                            if (GameModeHelper.IsOffline || !IsSpawned)

                                SpawnImpactEffectInternal(hit.point, hit.normal, false, hit.collider != null ? hit.collider.transform : null);

                            else

                                SpawnImpactEffectClientRpc(hit.point, hit.normal, false);

                        }

                    }

                }

            }

        }



        private RuntimeWeapon GetRuntimeWeaponByIndex(int index)

        {

            if (_runtimeWeapons != null && index >= 0 && index < _runtimeWeapons.Count)

            {

                return _runtimeWeapons[index];

            }

            return CurrentRuntimeWeapon;

        }



        // ================= NÂNG CẤP VŨ KHÍ TOÀN CỤC (GLOBAL WEAPON UPGRADES) =================



        /// <summary>

        /// Nâng cấp chỉ số toàn cục cho mọi vũ khí (Server-Authoritative). Khi đổi súng vẫn giữ nguyên!

        /// Hỗ trợ đầy đủ Max Caps để tránh vượt ngưỡng cân bằng game.

        /// </summary>

        public void ApplyGlobalWeaponUpgradeServer(

            float bonusDamagePercent = 0f,

            float bonusApsPercent = 0f,

            float bonusArmorPen = 0f,

            float bonusCritChance = 0f,

            float bonusCritMultiplier = 0f,

            float bonusReloadSpeedPercent = 0f,

            float bonusAccuracyPercent = 0f,

            float bonusMagPercent = 0f,

            float bonusRecoilReduction = 0f

        )

        {

            if (!GameModeHelper.IsServerOrOffline)

            {

                Debug.LogWarning("[WeaponController] Chỉ Server hoặc Offline mới có quyền nâng cấp vũ khí!");

                return;

            }



            if (IsSpawned)

            {

                GlobalDamageMultiplierNet.Value = Mathf.Min(MAX_DAMAGE_MULTIPLIER, GlobalDamageMultiplierNet.Value + bonusDamagePercent);

                GlobalAttackSpeedMultiplierNet.Value = Mathf.Min(MAX_APS_MULTIPLIER, GlobalAttackSpeedMultiplierNet.Value + bonusApsPercent);

                GlobalArmorPenetrationNet.Value = Mathf.Min(MAX_ARMOR_PENETRATION, GlobalArmorPenetrationNet.Value + bonusArmorPen);

                GlobalCritChanceBonusNet.Value = Mathf.Clamp(GlobalCritChanceBonusNet.Value + bonusCritChance, 0f, MAX_CRIT_CHANCE);

                GlobalCritMultiplierBonusNet.Value = Mathf.Min(MAX_CRIT_MULTIPLIER, GlobalCritMultiplierBonusNet.Value + bonusCritMultiplier);

                GlobalReloadSpeedMultiplierNet.Value = Mathf.Min(MAX_RELOAD_SPEED_MULTIPLIER, GlobalReloadSpeedMultiplierNet.Value + bonusReloadSpeedPercent);

                GlobalAccuracyBonusNet.Value = Mathf.Clamp(GlobalAccuracyBonusNet.Value + bonusAccuracyPercent, 0f, MAX_ACCURACY_BONUS);

                GlobalBonusMagPercentNet.Value = Mathf.Clamp(GlobalBonusMagPercentNet.Value + bonusMagPercent, 0f, MAX_BONUS_MAG_PERCENT);

                GlobalRecoilReductionNet.Value = Mathf.Clamp(GlobalRecoilReductionNet.Value + bonusRecoilReduction, 0f, MAX_RECOIL_REDUCTION);

                GlobalUpgradeLevelNet.Value += 1;

            }



            _rawGlobalDamageMultiplier = Mathf.Min(MAX_DAMAGE_MULTIPLIER, _rawGlobalDamageMultiplier + bonusDamagePercent);

            _rawGlobalAttackSpeedMultiplier = Mathf.Min(MAX_APS_MULTIPLIER, _rawGlobalAttackSpeedMultiplier + bonusApsPercent);

            _rawGlobalArmorPenetration = Mathf.Min(MAX_ARMOR_PENETRATION, _rawGlobalArmorPenetration + bonusArmorPen);

            _rawGlobalCritChanceBonus = Mathf.Clamp(_rawGlobalCritChanceBonus + bonusCritChance, 0f, MAX_CRIT_CHANCE);

            _rawGlobalCritMultiplierBonus = Mathf.Min(MAX_CRIT_MULTIPLIER, _rawGlobalCritMultiplierBonus + bonusCritMultiplier);

            _rawGlobalReloadSpeedMultiplier = Mathf.Min(MAX_RELOAD_SPEED_MULTIPLIER, _rawGlobalReloadSpeedMultiplier + bonusReloadSpeedPercent);

            _rawGlobalAccuracyBonus = Mathf.Clamp(_rawGlobalAccuracyBonus + bonusAccuracyPercent, 0f, MAX_ACCURACY_BONUS);

            _rawGlobalBonusMagPercent = Mathf.Clamp(_rawGlobalBonusMagPercent + bonusMagPercent, 0f, MAX_BONUS_MAG_PERCENT);

            _rawGlobalRecoilReduction = Mathf.Clamp(_rawGlobalRecoilReduction + bonusRecoilReduction, 0f, MAX_RECOIL_REDUCTION);

            _rawGlobalUpgradeLevel += 1;



            if (GameModeHelper.IsOffline || !IsSpawned)

            {

                NotifyWeaponUI();

                OnWeaponUpgraded?.Invoke(GlobalUpgradeLevel);

            }



            TriggerAmmoChanged();



            Debug.Log($"[WeaponController] Nâng cấp Vũ khí Toàn Cục lên Level {GlobalUpgradeLevel} " +

                      $"(Dame: {Mathf.RoundToInt(GlobalDamageMultiplier * 100)}%, APS: {Mathf.RoundToInt(GlobalAttackSpeedMultiplier * 100)}%, Reload: {Mathf.RoundToInt(GlobalReloadSpeedMultiplier * 100)}%, RecoilReduc: {Mathf.RoundToInt(GlobalRecoilReduction * 100)}%, Mag: +{Mathf.RoundToInt(GlobalBonusMagPercent * 100)}%)");

        }



        /// <summary>

        /// Gắn thêm 1 modifier toàn cục có hiệu lực trên mọi khẩu súng.

        /// </summary>

        public void AddGlobalModifierServer(IWeaponModifier modifier)

        {

            if (!GameModeHelper.IsServerOrOffline || modifier == null) return;

            if (!_globalModifiers.Contains(modifier))

            {

                _globalModifiers.Add(modifier);

                Debug.Log($"[WeaponController] Client {OwnerClientId} đã gắn Global Modifier: {modifier.DisplayName}");

            }

        }



        /// <summary>

        /// Tương thích ngược với lời gọi cũ từ Tế Đàn: Tự động nâng cấp toàn cục cho tất cả vũ khí!

        /// </summary>

        public void UpgradeCurrentWeaponServer(int levels = 1)

        {

            if (!GameModeHelper.IsServerOrOffline) return;

            for (int i = 0; i < levels; i++)

            {

                ApplyGlobalWeaponUpgradeServer(0.20f, 0.08f, 5f, 0.04f, 0.15f);

            }

        }



        private static int _dropRaycastMask = 0;

        private static int _cachedRaycastMask = 0;



        /// <summary>

        /// Bắn tia Raycast NonAlloc tối ưu hóa tuyệt đối (0 bytes GC Allocation), loại bỏ collider chính mình.

        /// </summary>

        private bool PerformRaycastFiltered(Vector3 origin, Vector3 direction, float maxRange, out RaycastHit hitResult)

        {

            hitResult = default;

            if (_cachedRaycastMask == 0)

            {

                _cachedRaycastMask = ~LayerMask.GetMask("Viewmodel", "PlayerBody");

            }



            int hitCount = Physics.RaycastNonAlloc(origin, direction, ZeroGcHitscanBuffer.HitBuffer, maxRange, _cachedRaycastMask, QueryTriggerInteraction.Ignore);

            if (hitCount <= 0) return false;



            Transform playerRoot = transform.root;

            float closestDist = float.MaxValue;

            bool foundValid = false;



            for (int i = 0; i < hitCount; i++)

            {

                var h = ZeroGcHitscanBuffer.HitBuffer[i];

                if (h.collider == null) continue;



                // Bỏ qua tất cả các collider thuộc về chính nhân vật người chơi (CharacterController, Head Hitbox, v.v.)

                Transform colRoot = h.collider.transform.root;

                if (colRoot == playerRoot)

                {

                    continue;

                }



                if (h.distance < closestDist)

                {

                    closestDist = h.distance;

                    hitResult = h;

                    foundValid = true;

                }

            }



            return foundValid;

        }



        private Vector3 GetMuzzlePosition()

        {

            Vector3 candidatePos = Vector3.zero;

            bool hasValidSocket = false;



            // 1. Lấy từ Socket Muzzle của ViewModel đang cầm trên tay

            if (_weaponMuzzlePoint != null && _weaponMuzzlePoint.gameObject.activeInHierarchy)

            {

                candidatePos = _weaponMuzzlePoint.position;

                hasValidSocket = true;

            }

            // 2. Tự động tìm lại Socket Muzzle trong ViewModel hiện tại nếu bị mất liên kết

            else if (_currentViewmodelInstance != null)

            {

                var mz = FindMuzzlePoint(_currentViewmodelInstance.transform);

                if (mz != null)

                {

                    _weaponMuzzlePoint = mz;

                    candidatePos = mz.position;

                    hasValidSocket = true;

                }

            }



            // Xác thực Socket: đảm bảo nòng súng thực sự nằm phía trước Camera (tránh trường hợp model bị xoay ngược vào mặt)

            if (hasValidSocket && _playerCamera != null)

            {

                float forwardDist = Vector3.Dot(candidatePos - _playerCamera.transform.position, _playerCamera.transform.forward);

                if (forwardDist >= 0.12f)

                {

                    return candidatePos;

                }

            }

            else if (hasValidSocket)

            {

                return candidatePos;

            }



            // 3. Fallback chuẩn xác vị trí cầm súng (phía trước dưới bên phải camera)

            if (_playerCamera != null)

            {

                return _playerCamera.transform.position 

                       + _playerCamera.transform.forward * 0.55f 

                       + _playerCamera.transform.right * 0.22f 

                       - _playerCamera.transform.up * 0.16f;

            }



            return transform.position + Vector3.up * 1.5f;

        }



        private Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spread, int seed)

        {

            if (spread <= 0.001f) return baseDirection.normalized;



            var prng = new System.Random(seed);

            float angle = (float)(prng.NextDouble() * Math.PI * 2);

            float radius = (float)(prng.NextDouble() * spread);



            Vector3 right = Vector3.Cross(baseDirection, Vector3.up).normalized;

            if (right == Vector3.zero) right = Vector3.right;

            Vector3 up = Vector3.Cross(right, baseDirection).normalized;



            Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;

            return (baseDirection + offset).normalized;

        }



        private void SpawnCosmeticBullet(Vector3 start, Vector3 end)
        {
            var pool = WeaponFxPool.GetOrCreate();
            if (pool != null)
            {
                if (_currentWeapon != null && _currentWeapon.EnableJuiceEffects)
                {
                    float speed = GetBulletSpeedForCurrentWeapon();
                    pool.PlayShortTracer(start, end, speed, _currentWeapon.TracerLength, _currentWeapon.TracerSpeedJitter);
                }
                else
                {
                    pool.PlayTracer(start, end, 0.045f);
                }
            }
        }

        private void SpawnMuzzleFlash(Vector3 muzzlePos)
        {
            var pool = WeaponFxPool.GetOrCreate();
            if (pool != null && _weaponMuzzlePoint != null)
            {
                float duration = _currentWeapon != null ? _currentWeapon.FlashDuration : 0.04f;
                GameObject customPrefab = _currentWeapon != null ? _currentWeapon.MuzzleFlashPrefab : null;
                pool.PlayMuzzleFlash(_weaponMuzzlePoint, customPrefab, duration);
            }
        }



        private float GetBulletSpeedForCurrentWeapon()

        {

            if (_currentWeapon == null) return 220f;

            switch (_currentWeapon.WeaponId.ToLower())

            {

                case "shotgun": return 175f;

                case "rifle":   return 280f;

                case "smg":     return 220f;

                default:        return 200f; // Pistol

            }

        }



        [ClientRpc]

        private void NotifyHitConfirmClientRpc(bool isHeadshot)

        {

            if (IsOwner)

            {

                OnHitTargetConfirmed?.Invoke(isHeadshot);

            }

        }



        private void SpawnImpactEffectInternal(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh, Transform hitTransform = null)
        {
            var pool = WeaponFxPool.GetOrCreate();
            if (pool != null)
            {
                pool.PlayImpact(hitPoint, hitNormal, isFlesh);
                if (!isFlesh && _currentWeapon != null && _currentWeapon.EnableJuiceEffects && _currentWeapon.EnableDecals)
                {
                    pool.SpawnBulletDecal(hitPoint, hitNormal, hitTransform, _currentWeapon.BulletDecalPrefab);
                }
            }
        }

        private void HandleLethalShotJuice(WeaponData weapon, Vector3 shootDir, GameObject hitObject)
        {
            if (weapon == null || !weapon.EnableJuiceEffects) return;

            // 1. Slow-Motion Hit Stop trên máy người bắn
            if (weapon.EnableKillSlowMo)
            {
                if (GameModeHelper.IsOffline || !IsSpawned || IsOwner)
                {
                    Hellfire.Core.TimeDilationManager.TriggerSlowMo(weapon.KillSlowMoScale, weapon.KillSlowMoDuration);
                }
                else
                {
                    TriggerKillSlowMoClientRpc(weapon.KillSlowMoScale, weapon.KillSlowMoDuration);
                }
            }

            // 2. Lực văng quái vật (Death Impulse)
            if (weapon.EnemyDeathImpulse > 0.01f && hitObject != null)
            {
                var enemyFsm = hitObject.GetComponentInParent<Hellfire.AI.EnemyStateMachine>();
                if (enemyFsm != null)
                {
                    enemyFsm.ApplyDeathImpulse(shootDir, weapon.EnemyDeathImpulse);
                }
            }
        }

        [ClientRpc]
        private void TriggerKillSlowMoClientRpc(float scale, float duration)
        {
            if (IsOwner)
            {
                Hellfire.Core.TimeDilationManager.TriggerSlowMo(scale, duration);
            }
        }

        private static Transform FindEjectionSocket(Transform root)
        {
            if (root == null) return null;
            if (root.name.Equals("EjectionPort", System.StringComparison.OrdinalIgnoreCase) ||
                root.name.Equals("ShellEject", System.StringComparison.OrdinalIgnoreCase) ||
                root.name.Equals("Socket_Shell", System.StringComparison.OrdinalIgnoreCase) ||
                root.name.Equals("Eject", System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindEjectionSocket(root.GetChild(i));
                if (found != null) return found;
            }
            return null;
        }



        [ClientRpc]

        private void SpawnImpactEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh)

        {

            SpawnImpactEffectInternal(hitPoint, hitNormal, isFlesh);

        }



        private static Material GetOrCreateFxMaterial()

        {

            if (_cachedFxMaterial == null)

            {

                var s = Shader.Find("Universal Render Pipeline/Unlit");

                if (s == null) s = Shader.Find("Sprites/Default");

                if (s != null)

                {

                    _cachedFxMaterial = new Material(s);

                    _cachedFxMaterial.color = new Color(1f, 0.9f, 0.4f, 1f);

                }

            }

            return _cachedFxMaterial;

        }



        private void NotifyWeaponUI()

        {

            if (_currentWeapon != null)

            {

                string displayName = _currentWeapon.WeaponName;

                int lvl = GlobalUpgradeLevelNet.Value;

                if (lvl > 0)

                {

                    displayName += $" +{lvl}";

                }



                OnWeaponChanged?.Invoke(displayName);

                OnWeaponSwitched?.Invoke(displayName);

                TriggerAmmoChanged();

            }

        }

    }

}

