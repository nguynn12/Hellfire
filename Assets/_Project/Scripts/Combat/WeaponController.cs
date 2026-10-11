// Script: WeaponController.cs
// Mục đích: Quản lý toàn bộ vũ khí súng tầm xa (Ranged Guns), bắn Hitscan, nạp đạn vô hạn không giới hạn số lần.
//           Hỗ trợ Nâng Cấp Vũ Khí Toàn Cục (Global Weapon Stats & Modifiers) — giữ nguyên và áp dụng cho TẤT CẢ vũ khí khi đổi súng.
// Môi trường thực thi: Cả hai (Client dự đoán & phát hiệu ứng, Server xác thực & trừ máu).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Player;
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
        [SerializeField] private Transform _weaponMuzzlePoint;
        [SerializeField] private Health _playerHealth;
        [SerializeField] private AudioSource _audioSource;

        [Header("Visual Effects (Cosmetic)")]
        [SerializeField] private float _tracerDuration = 0.05f;

        // ================= GLOBAL WEAPON STATS (SERVER-AUTHORITATIVE) =================
        // Áp dụng cho TOÀN BỘ vũ khí của người chơi. Khi đổi súng vẫn giữ nguyên toàn bộ nâng cấp!
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
        public int GlobalUpgradeLevel => GlobalUpgradeLevelNet.Value;

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

            if (_weaponMuzzlePoint == null)
            {
                var viewmodel = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel/GunMesh");
                if (viewmodel != null) _weaponMuzzlePoint = viewmodel;
            }

            if (_buffManager == null) _buffManager = GetComponent<PlayerBuffManager>();
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();

            // Khởi tạo danh sách RuntimeWeapon từ danh sách ScriptableObject tĩnh
            _runtimeWeapons.Clear();
            for (int i = 0; i < _availableWeapons.Count; i++)
            {
                if (_availableWeapons[i] != null)
                {
                    _runtimeWeapons.Add(new RuntimeWeapon(_availableWeapons[i]));
                }
            }

            if (_availableWeapons.Count > 0 && _currentWeapon == null)
            {
                _currentWeapon = _availableWeapons[0];
            }

            if (_currentWeapon != null)
            {
                _currentAmmo = _currentWeapon.MagSize;
            }

            GlobalUpgradeLevelNet.OnValueChanged += HandleGlobalUpgradeLevelChanged;

            if (IsOwner)
            {
                NotifyWeaponUI();
            }
        }

        public override void OnNetworkDespawn()
        {
            GlobalUpgradeLevelNet.OnValueChanged -= HandleGlobalUpgradeLevelChanged;
        }

        private void HandleGlobalUpgradeLevelChanged(int prevVal, int newVal)
        {
            if (IsOwner)
            {
                NotifyWeaponUI();
                OnWeaponUpgraded?.Invoke(newVal);
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Nếu người chơi đang gục hoặc chết -> Không cho phép bắn / nạp đạn
            if (_playerHealth != null && (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value))
            {
                if (_isReloading) CancelReload();
                return;
            }

            HandleWeaponSwitching();
            HandleReloading();
            HandleAttackInput();
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

            NotifyWeaponUI();
            Debug.Log($"[WeaponController] Đã đổi sang vũ khí: {_currentWeapon.WeaponName} (Kế thừa Nâng cấp Level {GlobalUpgradeLevelNet.Value})");
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
            if (IsServer && !IsOwner)
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
            _currentAmmo = Mathf.Max(0, _currentAmmo - 1);
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);

            // Tính toán tốc độ ra đòn kết hợp hệ số tốc bắn toàn cục
            float finalAps = _currentWeapon.AttackRateAPS * GlobalAttackSpeedMultiplierNet.Value;
            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAps));

            if (_audioSource != null && _currentWeapon.FireSound != null)
            {
                _audioSource.PlayOneShot(_currentWeapon.FireSound);
            }

            Vector3 cameraPos = _playerCamera.transform.position;
            Vector3 cameraForward = _playerCamera.transform.forward;
            int seed = UnityEngine.Random.Range(0, 100000);

            // Vẽ tracer đạn cosmetic cục bộ cho người bắn
            int pellets = _currentWeapon.PelletCount;
            float range = _currentWeapon.Range;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDir = CalculateSpreadDirection(cameraForward, _currentWeapon.Spread, seed + i);
                Vector3 endPoint = Physics.Raycast(cameraPos, shootDir, out var hit, range) ? hit.point : cameraPos + shootDir * range;
                DrawCosmeticTracer(GetMuzzlePosition(), endPoint);
            }

            // Gửi ServerRpc yêu cầu Server xác thực độc lập
            FireServerRpc(cameraPos, cameraForward, seed, _currentWeaponIndex);

            // Khi đạn hiện tại về 0: Tự động bắt đầu nạp đạn ngay lập tức
            if (_currentAmmo <= 0)
            {
                StartReload();
            }
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 clientOrigin, Vector3 baseDirection, int seed, int weaponIndex)
        {
            RuntimeWeapon rw = GetRuntimeWeaponByIndex(weaponIndex);
            if (rw == null) return;

            WeaponData weaponToUse = rw.BaseData;

            if (_playerHealth != null && (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value))
            {
                return;
            }

            // Tính sát thương kết hợp Nâng Cấp Toàn Cục và Bùa Cuồng Nộ nếu có
            float armorPen = weaponToUse.ArmorPenetration + GlobalArmorPenetrationNet.Value;
            int pellets = weaponToUse.PelletCount;
            float range = weaponToUse.Range;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDir = CalculateSpreadDirection(baseDirection, weaponToUse.Spread, seed + i);

                if (Physics.Raycast(clientOrigin, shootDir, out var hit, range))
                {
                    // 1. Kiểm tra Friendly Fire (đồng đội)
                    if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerMovement>() != null)
                    {
                        var hitHealth = hit.collider.GetComponentInParent<Health>();
                        if (hitHealth != null && hitHealth.NetworkObjectId != NetworkObjectId)
                        {
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                            continue;
                        }
                    }

                    // 2. Tính sát thương (kết hợp Global Stats và Modifier toàn cục)
                    float attackDamage = rw.CalculateAttackDamage(
                        GlobalDamageMultiplierNet.Value,
                        GlobalAttackSpeedMultiplierNet.Value,
                        GlobalArmorPenetrationNet.Value,
                        GlobalCritChanceBonusNet.Value,
                        GlobalCritMultiplierBonusNet.Value,
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

                        rw.NotifyHit(payload, hitbox.Damageable);
                        NotifyHitConfirmClientRpc(hitbox.Type == HitboxType.Head);
                        SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                    }
                    else
                    {
                        var damageable = hit.collider.GetComponentInParent<IDamageable>();
                        if (damageable != null && !damageable.IsPlayerTarget)
                        {
                            var payload = new DamagePayload(attackDamage, armorPen, HitboxType.Torso, OwnerClientId);
                            damageable.TakeDamage(payload);

                            rw.NotifyHit(payload, damageable);
                            NotifyHitConfirmClientRpc(false);
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                        }
                        else
                        {
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
            if (!IsServer)
            {
                Debug.LogWarning("[WeaponController] Chỉ Server mới có quyền nâng cấp vũ khí!");
                return;
            }

            GlobalDamageMultiplierNet.Value += bonusDamagePercent;
            GlobalAttackSpeedMultiplierNet.Value += bonusApsPercent;
            GlobalArmorPenetrationNet.Value += bonusArmorPen;
            GlobalCritChanceBonusNet.Value = Mathf.Clamp01(GlobalCritChanceBonusNet.Value + bonusCritChance);
            GlobalCritMultiplierBonusNet.Value += bonusCritMultiplier;
            GlobalUpgradeLevelNet.Value += 1;

            Debug.Log($"[WeaponController] Client {OwnerClientId} nâng cấp Vũ khí Toàn Cục lên Level {GlobalUpgradeLevelNet.Value} " +
                      $"(Dame: +{Mathf.RoundToInt(bonusDamagePercent * 100)}%, APS: +{Mathf.RoundToInt(bonusApsPercent * 100)}%, Pen: +{bonusArmorPen})");
        }

        /// <summary>
        /// Gắn thêm 1 modifier toàn cục có hiệu lực trên mọi khẩu súng.
        /// </summary>
        public void AddGlobalModifierServer(IWeaponModifier modifier)
        {
            if (!IsServer || modifier == null) return;
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
            if (!IsServer) return;
            for (int i = 0; i < levels; i++)
            {
                ApplyGlobalWeaponUpgradeServer(0.20f, 0.08f, 5f, 0.04f, 0.15f);
            }
        }

        private Vector3 GetMuzzlePosition()
        {
            if (_weaponMuzzlePoint != null)
            {
                return _weaponMuzzlePoint.position;
            }
            return _playerCamera != null ? _playerCamera.transform.position + _playerCamera.transform.forward * 0.5f : transform.position;
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

        private void DrawCosmeticTracer(Vector3 start, Vector3 end)
        {
            StartCoroutine(TracerRoutine(start, end));
        }

        private IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            var tracerObj = new GameObject("Tracer");
            var line = tracerObj.AddComponent<LineRenderer>();
            line.startWidth = 0.04f;
            line.endWidth = 0.01f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            line.material = GetOrCreateFxMaterial();
            line.startColor = new Color(1f, 0.9f, 0.4f, 0.9f);
            line.endColor = new Color(1f, 0.4f, 0.1f, 0.1f);

            yield return new WaitForSeconds(_tracerDuration);
            Destroy(tracerObj);
        }

        [ClientRpc]
        private void NotifyHitConfirmClientRpc(bool isHeadshot)
        {
            if (IsOwner)
            {
                OnHitTargetConfirmed?.Invoke(isHeadshot);
            }
        }

        [ClientRpc]
        private void SpawnImpactEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh)
        {
            StartCoroutine(ImpactEffectRoutine(hitPoint, hitNormal, isFlesh));
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
                var s = Shader.Find("Sprites/Default");
                if (s != null) _cachedFxMaterial = new Material(s);
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
