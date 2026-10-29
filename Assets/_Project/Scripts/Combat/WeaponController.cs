// Script: WeaponController.cs
// Mục đích: Quản lý toàn bộ vũ khí súng tầm xa (Ranged Guns), bắn Hitscan, nạp đạn vô hạn không giới hạn số lần.
//           Hỗ trợ Nâng Cấp Vũ Khí Toàn Cục (Global Weapon Stats & Modifiers) — giữ nguyên và áp dụng cho TẤT CẢ vũ khí khi đổi súng.
// Môi trường thực thi: Cả hai (Client dự đoán & phát hiệu ứng, Server xác thực & trừ máu).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Player;
using Hellfire.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponController : NetworkBehaviour
    {
        [Header("Weapon Configuration")]
        [SerializeField] private WeaponData _currentWeapon;
        [SerializeField] private List<WeaponData> _availableWeapons = new List<WeaponData>();

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
        private Hellfire.UI.WeaponCrosshairHUD _crosshairHud;
        private Vector3 _recoilOffset;
        private Vector3 _recoilRotation;

        // ================= GLOBAL WEAPON STATS (FALLBACK OFFLINE + NETWORK VARIABLES) =================
        private float _rawGlobalDamageMultiplier = 1.0f;
        private float _rawGlobalAttackSpeedMultiplier = 1.0f;
        private float _rawGlobalArmorPenetration = 0f;
        private float _rawGlobalCritChanceBonus = 0f;
        private float _rawGlobalCritMultiplierBonus = 0f;
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

        public NetworkVariable<int> GlobalUpgradeLevelNet { get; } = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        // Safe Accessors cho chỉ số hỏa lực toàn cục (hỗ trợ đọc an toàn cả Online lẫn Offline)
        public float GlobalDamageMultiplier => IsSpawned ? GlobalDamageMultiplierNet.Value : _rawGlobalDamageMultiplier;
        public float GlobalAttackSpeedMultiplier => IsSpawned ? GlobalAttackSpeedMultiplierNet.Value : _rawGlobalAttackSpeedMultiplier;
        public float GlobalArmorPenetration => IsSpawned ? GlobalArmorPenetrationNet.Value : _rawGlobalArmorPenetration;
        public float GlobalCritChanceBonus => IsSpawned ? GlobalCritChanceBonusNet.Value : _rawGlobalCritChanceBonus;
        public float GlobalCritMultiplierBonus => IsSpawned ? GlobalCritMultiplierBonusNet.Value : _rawGlobalCritMultiplierBonus;
        public int GlobalUpgradeLevel => IsSpawned ? GlobalUpgradeLevelNet.Value : _rawGlobalUpgradeLevel;

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
        public event Action<string> OnWeaponChanged;               // weaponName
        public event Action<bool> OnReloadStateChanged;           // isReloading
        public event Action<bool> OnHitTargetConfirmed;           // isHeadshot
        public event Action<int> OnWeaponUpgraded;                 // newLevel

        public WeaponData CurrentWeaponData => CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.BaseData : _currentWeapon;
        public RuntimeWeapon CurrentRuntimeWeapon => (_runtimeWeapons != null && _currentWeaponIndex >= 0 && _currentWeaponIndex < _runtimeWeapons.Count) ? _runtimeWeapons[_currentWeaponIndex] : null;
        public IReadOnlyList<RuntimeWeapon> RuntimeWeapons => _runtimeWeapons;
        public IReadOnlyList<WeaponData> AvailableWeapons => _availableWeapons;
        public IReadOnlyList<IWeaponModifier> GlobalModifiers => _globalModifiers;

        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void AutoResolveReferences()
        {
            if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>(true);
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
            if (_crosshairHud == null)
            {
                _crosshairHud = GetComponent<Hellfire.UI.WeaponCrosshairHUD>();
                if (_crosshairHud == null) _crosshairHud = gameObject.AddComponent<Hellfire.UI.WeaponCrosshairHUD>();
            }
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
                for (int i = 0; i < _availableWeapons.Count; i++)
                {
                    if (_availableWeapons[i] != null)
                    {
                        _runtimeWeapons.Add(new RuntimeWeapon(_availableWeapons[i]));
                    }
                }
            }

            if (_availableWeapons.Count > 0 && _currentWeapon == null)
            {
                _currentWeapon = _availableWeapons[0];
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

            HandleWeaponSwitching();
            HandleReloading();
            HandleAttackInput();
            HandleRecoilRecovery();
        }

        private void HandleRecoilRecovery()
        {
            float recoverySpeed = _currentWeapon != null ? _currentWeapon.RecoilRecoverySpeed : 12f;
            _recoilOffset = Vector3.Lerp(_recoilOffset, Vector3.zero, Time.deltaTime * recoverySpeed);
            _recoilRotation = Vector3.Lerp(_recoilRotation, Vector3.zero, Time.deltaTime * recoverySpeed);

            if (_currentViewmodelInstance != null)
            {
                Vector3 basePos = _currentWeapon != null ? _currentWeapon.ViewmodelOffset : Vector3.zero;
                Vector3 baseRot = _currentWeapon != null ? _currentWeapon.ViewmodelRotation : Vector3.zero;

                _currentViewmodelInstance.transform.localPosition = basePos + _recoilOffset;
                _currentViewmodelInstance.transform.localRotation = Quaternion.Euler(baseRot + _recoilRotation);
            }
        }

        private void HandleWeaponSwitching()
        {
            if (_availableWeapons == null || _availableWeapons.Count <= 1 || _isReloading) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame && _availableWeapons.Count > 0) SwitchWeapon(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame && _availableWeapons.Count > 1) SwitchWeapon(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame && _availableWeapons.Count > 2) SwitchWeapon(2);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame && _availableWeapons.Count > 3) SwitchWeapon(3);
        }

        public void SwitchWeapon(int index)
        {
            if (index < 0 || index >= _availableWeapons.Count || _availableWeapons[index] == null) return;
            if (index == _currentWeaponIndex && _currentWeapon == _availableWeapons[index]) return;

            _currentWeaponIndex = index;
            _currentWeapon = _availableWeapons[index];
            _currentAmmo = _currentWeapon.MagSize;
            _isReloading = false;
            _nextFireTime = Time.time + 0.15f;

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

        public void UpdateWeaponVisuals(int index)
        {
            if (_weaponHolder == null)
            {
                _weaponHolder = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
            }
            if (_weaponHolder == null) return;

            // Đảm bảo WeaponViewmodel luôn chuẩn hóa rotation (0,0,0) chuẩn hệ FPS
            _weaponHolder.localRotation = Quaternion.identity;

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
            if (weapon != null && !_availableWeapons.Contains(weapon))
            {
                _availableWeapons.Add(weapon);
                _runtimeWeapons.Add(new RuntimeWeapon(weapon));
            }
        }

        public void AddOrSwitchWeapon(WeaponData weapon)
        {
            if (weapon == null) return;
            int index = _availableWeapons.IndexOf(weapon);
            if (index == -1)
            {
                _availableWeapons.Add(weapon);
                _runtimeWeapons.Add(new RuntimeWeapon(weapon));
                index = _availableWeapons.Count - 1;
            }

            SwitchWeapon(index);

            // Đồng bộ về Client sở hữu nếu Server gọi
            if (IsServer && !IsOwner && !GameModeHelper.IsOffline)
            {
                SwitchWeaponClientRpc(index);
            }
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
            // Bấm R để nạp đạn bất cứ khi nào đạn chưa đầy băng
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !_isReloading && _currentWeapon != null)
            {
                if (_currentAmmo < _currentWeapon.MagSize)
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
            _isReloading = true;
            _reloadTimer = _currentWeapon.ReloadTime;
            OnReloadStateChanged?.Invoke(true);

            if (_audioSource != null && _currentWeapon.ReloadSound != null)
            {
                _audioSource.PlayOneShot(_currentWeapon.ReloadSound);
            }
        }

        private void CompleteReload()
        {
            _isReloading = false;
            if (_currentWeapon != null)
            {
                _currentAmmo = _currentWeapon.MagSize; // Luôn nạp đầy băng, đạn dự trữ là vô hạn!
            }
            OnReloadStateChanged?.Invoke(false);
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon != null ? _currentWeapon.MagSize : 0);
        }

        private void CancelReload()
        {
            _isReloading = false;
            OnReloadStateChanged?.Invoke(false);
        }

        // ================= BẮN SÚNG TẦM XA (RANGED HITSCAN) =================

        private void HandleAttackInput()
        {
            if (_currentWeapon == null || _playerCamera == null) return;

            bool triggerPressed = false;
            if (Mouse.current != null)
            {
                triggerPressed = _currentWeapon.IsAutomatic
                    ? Mouse.current.leftButton.isPressed
                    : Mouse.current.leftButton.wasPressedThisFrame;
            }

            if (!triggerPressed) return;
            if (Time.time < _nextFireTime) return;
            if (_isReloading) return;

            // Nếu hết đạn trong băng: tự động nạp đạn vô hạn
            if (_currentAmmo <= 0)
            {
                if (_audioSource != null && _currentWeapon.EmptySound != null)
                {
                    _audioSource.PlayOneShot(_currentWeapon.EmptySound);
                }
                StartReload();
                _nextFireTime = Time.time + 0.3f;
                return;
            }

            ExecuteRangedFire();
        }

        private void ExecuteRangedFire()
        {
            if (OfflineManager.Instance == null || !OfflineManager.Instance.InfiniteAmmo)
            {
                _currentAmmo = Mathf.Max(0, _currentAmmo - 1);
            }
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);

            // Tính toán tốc độ ra đòn kết hợp hệ số tốc bắn toàn cục
            float finalAps = _currentWeapon.AttackRateAPS * GlobalAttackSpeedMultiplier;
            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAps));

            if (_audioSource != null && _currentWeapon.FireSound != null)
            {
                _audioSource.PlayOneShot(_currentWeapon.FireSound);
            }

            Vector3 cameraPos = _playerCamera.transform.position;
            Vector3 cameraForward = _playerCamera.transform.forward;
            int seed = UnityEngine.Random.Range(0, 100000);

            // Bắn viên đạn cosmetic (Option 1) từ đầu nòng súng
            int pellets = _currentWeapon.PelletCount;
            float range = _currentWeapon.Range;
            Vector3 muzzlePos = GetMuzzlePosition();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDir = CalculateSpreadDirection(cameraForward, _currentWeapon.Spread, seed + i);
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
            // 1. Độ giật súng trên tay (ViewModel Kickback)
            float kickForce = _currentWeapon != null && _currentWeapon.KickbackForce > 0.001f ? _currentWeapon.KickbackForce : 0.04f;
            float kickAngle = _currentWeapon != null && _currentWeapon.KickbackAngle > 0.001f ? _currentWeapon.KickbackAngle : 2.5f;
            _recoilOffset += new Vector3(0f, 0.005f, -kickForce);
            _recoilRotation += new Vector3(-kickAngle, UnityEngine.Random.Range(-0.8f, 0.8f), UnityEngine.Random.Range(-1f, 1f));

            // 2. Độ giật Camera hất lên (Camera Kick)
            if (_playerMovement != null && _currentWeapon != null)
            {
                float pitchKick = _currentWeapon.CameraRecoilPitch;
                float yawKick = UnityEngine.Random.Range(-_currentWeapon.CameraRecoilYaw, _currentWeapon.CameraRecoilYaw);
                _playerMovement.AddCameraRecoil(pitchKick, yawKick);
            }

            // 3. Nở tâm ngắm (Crosshair Dynamic Bloom)
            if (_crosshairHud != null)
            {
                _crosshairHud.TriggerFireBloom(8f);
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
                        var payload = new DamagePayload(attackDamage, armorPen, hitbox.Type, OwnerClientId);
                        hitbox.ForwardDamage(payload);

                        rw.NotifyHit(payload, hitbox.Damageable, _globalModifiers);

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
                            var payload = new DamagePayload(attackDamage, armorPen, HitboxType.Torso, OwnerClientId);
                            damageable.TakeDamage(payload);

                            rw.NotifyHit(payload, damageable, _globalModifiers);

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
                                SpawnImpactEffectInternal(hit.point, hit.normal, false);
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
        /// </summary>
        public void ApplyGlobalWeaponUpgradeServer(
            float bonusDamagePercent = 0.15f,
            float bonusApsPercent = 0.10f,
            float bonusArmorPen = 5f,
            float bonusCritChance = 0.05f,
            float bonusCritMultiplier = 0.2f
        )
        {
            if (!GameModeHelper.IsServerOrOffline)
            {
                Debug.LogWarning("[WeaponController] Chỉ Server hoặc Offline mới có quyền nâng cấp vũ khí!");
                return;
            }

            if (IsSpawned)
            {
                GlobalDamageMultiplierNet.Value += bonusDamagePercent;
                GlobalAttackSpeedMultiplierNet.Value += bonusApsPercent;
                GlobalArmorPenetrationNet.Value += bonusArmorPen;
                GlobalCritChanceBonusNet.Value = Mathf.Clamp01(GlobalCritChanceBonusNet.Value + bonusCritChance);
                GlobalCritMultiplierBonusNet.Value += bonusCritMultiplier;
                GlobalUpgradeLevelNet.Value += 1;
            }

            _rawGlobalDamageMultiplier += bonusDamagePercent;
            _rawGlobalAttackSpeedMultiplier += bonusApsPercent;
            _rawGlobalArmorPenetration += bonusArmorPen;
            _rawGlobalCritChanceBonus = Mathf.Clamp01(_rawGlobalCritChanceBonus + bonusCritChance);
            _rawGlobalCritMultiplierBonus += bonusCritMultiplier;
            _rawGlobalUpgradeLevel += 1;

            if (GameModeHelper.IsOffline || !IsSpawned)
            {
                NotifyWeaponUI();
                OnWeaponUpgraded?.Invoke(GlobalUpgradeLevel);
            }

            Debug.Log($"[WeaponController] Nâng cấp Vũ khí Toàn Cục lên Level {GlobalUpgradeLevel} " +
                      $"(Dame: +{Mathf.RoundToInt(bonusDamagePercent * 100)}%, APS: +{Mathf.RoundToInt(bonusApsPercent * 100)}%, Pen: +{bonusArmorPen})");
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

        /// <summary>
        /// Bắn tia Raycast loại bỏ hoàn toàn các collider của chính người chơi (tránh đạn bị va vào đầu/thân mình khi quay người).
        /// </summary>
        private bool PerformRaycastFiltered(Vector3 origin, Vector3 direction, float maxRange, out RaycastHit hitResult)
        {
            hitResult = default;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxRange, ~LayerMask.GetMask("Viewmodel", "PlayerBody"), QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                // Bỏ qua tất cả các collider thuộc về chính nhân vật người chơi (CharacterController, Head Hitbox, v.v.)
                if (h.collider.transform.root == transform.root || 
                    h.collider.GetComponentInParent<WeaponController>() == this ||
                    (_playerMovement != null && h.collider.GetComponentInParent<PlayerMovement>() == _playerMovement) ||
                    (_playerHealth != null && h.collider.GetComponentInParent<Health>() == _playerHealth))
                {
                    continue;
                }

                hitResult = h;
                return true;
            }

            return false;
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
            var bulletObj = new GameObject("CosmeticBullet");
            var bullet = bulletObj.AddComponent<CosmeticBullet>();

            float speed = GetBulletSpeedForCurrentWeapon();
            Material mat = GetOrCreateFxMaterial();
            Color coreColor = new Color(1.0f, 0.95f, 0.45f, 1f); // Lõi vàng rực
            Color trailColor = new Color(1.0f, 0.35f, 0.05f, 0.8f); // Đuôi lửa cam

            bullet.Launch(start, end, speed, mat, mat, coreColor, trailColor);
        }

        private void SpawnMuzzleFlash(Vector3 muzzlePos)
        {
            StartCoroutine(MuzzleFlashRoutine(muzzlePos));
        }

        private IEnumerator MuzzleFlashRoutine(Vector3 muzzlePos)
        {
            var flashObj = new GameObject("MuzzleFlash");
            flashObj.transform.position = muzzlePos;

            var light = flashObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.85f, 0.4f);
            light.range = 2.5f;
            light.intensity = 3.5f;
            light.shadows = LightShadows.None;

            yield return new WaitForSeconds(0.04f);
            Destroy(flashObj);
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

        private void SpawnImpactEffectInternal(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh)
        {
            StartCoroutine(ImpactEffectRoutine(hitPoint, hitNormal, isFlesh));
        }

        [ClientRpc]
        private void SpawnImpactEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh)
        {
            SpawnImpactEffectInternal(hitPoint, hitNormal, isFlesh);
        }

        private IEnumerator ImpactEffectRoutine(Vector3 pos, Vector3 normal, bool isFlesh)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.transform.position = pos;
            spark.transform.localScale = Vector3.one * 0.08f;
            var col = spark.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = spark.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = GetOrCreateFxMaterial();
                rend.material.color = isFlesh ? new Color(0.9f, 0.1f, 0.1f, 1f) : new Color(1f, 0.8f, 0.2f, 1f);
            }

            yield return new WaitForSeconds(0.15f);
            Destroy(spark);
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
                OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);
            }
        }
    }
}
