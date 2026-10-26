// Script: WeaponController.cs
// Mục đích: Quản lý logic bắn súng Hitscan từ tâm camera, hiệu ứng cosmetic client, và Server-Authoritative raycast tính sát thương (Mục 3.1 & 3.4).
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

        // Runtime states
        private int _currentAmmo;
        private float _nextFireTime;
        private bool _isReloading;
        private float _reloadTimer;
        private int _currentWeaponIndex = 0;

        // Events for UI HUD
        public event Action<int, int> OnAmmoChanged;              // currentAmmo, magSize
        public event Action<string> OnWeaponChanged;               // weaponName
        public event Action<bool> OnReloadStateChanged;           // isReloading
        public event Action<bool> OnHitTargetConfirmed;           // isHeadshot
        public event Action OnWeaponFired;                        // Đợt 1: Kích hoạt hiệu ứng giật súng (Recoil)
        public event Action<Vector3, Vector3> OnTracerRequested;  // start, end (VFX Tracer)
        public event Action<Vector3, Vector3, bool> OnImpactRequested; // point, normal, isFlesh (VFX Impact)

        public WeaponData CurrentWeaponData => _currentWeapon;
        public int CurrentAmmo => _currentAmmo;
        public bool IsReloading => _isReloading;
        public bool IsAimingDownSights { get; set; }              // Đợt 1: Trạng thái ngắm bắn (ADS)
        private Player.PlayerBuffManager _buffManager;

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
                _audioSource.spatialBlend = 0f; // 2D sound for local player
            }

            if (_weaponMuzzlePoint == null)
            {
                var shootFx = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel/RifleMesh/WeaponGrip/ShootFX");
                if (shootFx != null)
                {
                    _weaponMuzzlePoint = shootFx;
                }
                else
                {
                    var viewmodel = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel/GunMesh");
                    if (viewmodel != null) _weaponMuzzlePoint = viewmodel;
                }
            }
            if (_buffManager == null) _buffManager = GetComponent<Player.PlayerBuffManager>();
        }

        public bool IsLocallyControlled => !IsSpawned || IsOwner;

        private void Start()
        {
            if (!IsSpawned)
            {
                InitializeOffline();
            }
        }

        public void InitializeOffline()
        {
            AutoResolveReferences();

            if (_availableWeapons.Count > 0 && _currentWeapon == null)
            {
                _currentWeapon = _availableWeapons[0];
            }

            if (_currentWeapon != null)
            {
                _currentAmmo = _currentWeapon.MagSize;
            }

            NotifyWeaponUI();
        }

        public override void OnNetworkSpawn()
        {
            AutoResolveReferences();

            if (_availableWeapons.Count > 0 && _currentWeapon == null)
            {
                _currentWeapon = _availableWeapons[0];
            }

            if (_currentWeapon != null)
            {
                _currentAmmo = _currentWeapon.MagSize;
            }

            if (IsOwner)
            {
                NotifyWeaponUI();
            }
        }

        private void Update()
        {
            if (!IsLocallyControlled) return;

            // Nếu người chơi đang gục hoặc chết -> Không cho phép bắn/nạp đạn (Mục 3.3)
            if (_playerHealth != null)
            {
                IDamageable dmg = _playerHealth;
                if (dmg.IsDowned || dmg.IsDead)
                {
                    if (_isReloading) CancelReload();
                    return;
                }
            }

            HandleWeaponSwitching();
            HandleReloading();
            HandleFiring();
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
            IsAimingDownSights = false;
            _nextFireTime = Time.time + 0.15f;

            NotifyWeaponUI();
            Debug.Log($"[WeaponController] Đã đổi sang vũ khí: {_currentWeapon.WeaponName}");
        }

        public void SetWeaponData(WeaponData newWeapon)
        {
            if (newWeapon == null) return;
            _currentWeapon = newWeapon;
            _currentAmmo = newWeapon.MagSize;
            _isReloading = false;
            NotifyWeaponUI();
        }

        public IReadOnlyList<WeaponData> AvailableWeapons => _availableWeapons;

        public bool HasWeapon(WeaponData weapon)
        {
            return weapon != null && _availableWeapons.Contains(weapon);
        }

        public void AddAvailableWeapon(WeaponData weapon)
        {
            if (weapon != null && !_availableWeapons.Contains(weapon))
            {
                _availableWeapons.Add(weapon);
            }
        }

        public void AddOrSwitchWeapon(WeaponData weapon)
        {
            if (weapon == null) return;
            int index = _availableWeapons.IndexOf(weapon);
            if (index == -1)
            {
                _availableWeapons.Add(weapon);
                index = _availableWeapons.Count - 1;
            }
            SwitchWeapon(index);
        }

        private void HandleReloading()
        {
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
            IsAimingDownSights = false;
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
                _currentAmmo = _currentWeapon.MagSize;
            }
            OnReloadStateChanged?.Invoke(false);
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon != null ? _currentWeapon.MagSize : 0);
        }

        private void CancelReload()
        {
            _isReloading = false;
            OnReloadStateChanged?.Invoke(false);
        }

        private void HandleFiring()
        {
            if (_currentWeapon == null || _playerCamera == null) return;

            bool fireTriggered = false;

            if (Mouse.current != null)
            {
                if (_currentWeapon.IsAutomatic)
                {
                    fireTriggered = Mouse.current.leftButton.isPressed;
                }
                else
                {
                    fireTriggered = Mouse.current.leftButton.wasPressedThisFrame;
                }
            }

            if (!fireTriggered) return;

            if (Time.time < _nextFireTime) return;

            if (_isReloading) return;

            if (_currentAmmo <= 0)
            {
                // Hết đạn: phát âm thanh rỗng và tự động nạp đạn
                if (_audioSource != null && _currentWeapon.EmptySound != null)
                {
                    _audioSource.PlayOneShot(_currentWeapon.EmptySound);
                }
                StartReload();
                _nextFireTime = Time.time + 0.3f;
                return;
            }

            // Thực thi bắn
            ExecuteFire();
        }

        public void ExecuteFire()
        {
            _currentAmmo--;
            _nextFireTime = Time.time + _currentWeapon.FireRate;
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);
            OnWeaponFired?.Invoke();

            // Tính toán đường đạn & Raycast từ tâm camera
            Vector3 cameraPos = _playerCamera.transform.position;
            Vector3 cameraForward = _playerCamera.transform.forward;

            int pellets = _currentWeapon.PelletCount;
            int seed = UnityEngine.Random.Range(0, 100000);

            // Thu hẹp spread khi ngắm ADS
            float effectiveSpread = IsAimingDownSights ? _currentWeapon.Spread * 0.5f : _currentWeapon.Spread;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 spreadDir = CalculateSpreadDirection(cameraForward, effectiveSpread, seed + i);
                Vector3 endPoint = cameraPos + spreadDir * _currentWeapon.Range;

                bool hasHit = Physics.Raycast(cameraPos, spreadDir, out var hit, _currentWeapon.Range);
                if (hasHit)
                {
                    endPoint = hit.point;
                }

                // Gửi event để WeaponVisualEffects vẽ Bullet Tracer
                OnTracerRequested?.Invoke(GetMuzzlePosition(), endPoint);

                // Xử lý trúng đích cục bộ ngay khi chạy Offline trong scene test
                if (!IsSpawned && hasHit)
                {
                    ProcessHitLocal(hit, spreadDir, _currentWeapon);
                }
            }

            // Nếu đang trong phòng Multiplayer, gửi ServerRpc để Server xác thực
            if (IsSpawned)
            {
                FireServerRpc(cameraPos, cameraForward, seed, _currentWeaponIndex);
            }
        }

        private void ProcessHitLocal(RaycastHit hit, Vector3 shootDir, WeaponData weapon)
        {
            // Kiểm tra Friendly Fire
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<Player.PlayerMovement>() != null)
            {
                OnImpactRequested?.Invoke(hit.point, hit.normal, false);
                return;
            }

            var hitbox = hit.collider.GetComponent<HitboxIdentifier>();
            float buffMultiplier = (_buffManager != null) ? _buffManager.DamageMultiplier : 1.0f;

            if (hitbox != null)
            {
                float multiplier = weapon.GetMultiplier(hitbox.Type);
                float damage = weapon.BaseDamage * multiplier * buffMultiplier;
                hitbox.ForwardDamage(damage, 0);

                OnHitTargetConfirmed?.Invoke(hitbox.Type == HitboxType.Head);
                OnImpactRequested?.Invoke(hit.point, hit.normal, true);
            }
            else
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsPlayerTarget)
                {
                    float damage = weapon.BaseDamage * weapon.GetMultiplier(HitboxType.Torso) * buffMultiplier;
                    damageable.TakeDamage(damage, HitboxType.Torso, 0);

                    OnHitTargetConfirmed?.Invoke(false);
                    OnImpactRequested?.Invoke(hit.point, hit.normal, true);
                }
                else
                {
                    // Trúng môi trường / tường
                    OnImpactRequested?.Invoke(hit.point, hit.normal, false);
                }
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



        // ================= SERVER RPC & VALIDATION =================

        [ServerRpc]
        private void FireServerRpc(Vector3 clientOrigin, Vector3 baseDirection, int seed, int weaponIndex)
        {
            // Xác định WeaponData trên Server
            WeaponData weaponToUse = _currentWeapon;
            if (_availableWeapons != null && weaponIndex >= 0 && weaponIndex < _availableWeapons.Count && _availableWeapons[weaponIndex] != null)
            {
                weaponToUse = _availableWeapons[weaponIndex];
            }

            if (weaponToUse == null)
            {
                Debug.LogWarning("[WeaponController] ServerRpc: WeaponData không hợp lệ!");
                return;
            }

            // Kiểm tra trạng thái của người bắn trên Server
            if (_playerHealth != null && (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value))
            {
                return;
            }

            // Server-Authoritative Raycast độc lập (Mục 3.1 bước 5)
            int pellets = weaponToUse.PelletCount;
            float range = weaponToUse.Range;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDir = CalculateSpreadDirection(baseDirection, weaponToUse.Spread, seed + i);

                // Raycast toàn bộ layer va chạm
                if (Physics.Raycast(clientOrigin, shootDir, out var hit, range))
                {
                    // 1. Kiểm tra Friendly Fire (Mục 3.4 — ĐÃ CHỐT: TẮT)
                    if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerMovement>() != null)
                    {
                        var hitHealth = hit.collider.GetComponentInParent<Health>();
                        // Nếu bắn trúng đồng đội (cùng tag Player và không phải chính mình):
                        if (hitHealth != null && hitHealth.NetworkObjectId != NetworkObjectId)
                        {
                            // Đạn dừng lại tại người chơi đồng đội (chặn đường đạn), NHƯNG KHÔNG trừ máu!
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                            continue;
                        }
                    }

                    // 2. Kiểm tra Hitbox (Mục 3.2: Head x2.0, Torso x1.0, Limb x0.75)
                    var hitbox = hit.collider.GetComponent<HitboxIdentifier>();
                    float buffMultiplier = (_buffManager != null) ? _buffManager.DamageMultiplier : 1.0f;

                    if (hitbox != null)
                    {
                        float multiplier = weaponToUse.GetMultiplier(hitbox.Type);
                        float damage = weaponToUse.BaseDamage * multiplier * buffMultiplier;

                        hitbox.ForwardDamage(damage, OwnerClientId);

                        // Gửi hit confirm về cho người bắn
                        NotifyHitConfirmClientRpc(hitbox.Type == HitboxType.Head);
                        SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                    }
                    else
                    {
                        // Kiểm tra IDamageable trực tiếp (nếu không gắn HitboxIdentifier chi tiết)
                        var damageable = hit.collider.GetComponentInParent<IDamageable>();
                        if (damageable != null && !damageable.IsPlayerTarget)
                        {
                            float damage = weaponToUse.BaseDamage * weaponToUse.GetMultiplier(HitboxType.Torso) * buffMultiplier;
                            damageable.TakeDamage(damage, HitboxType.Torso, OwnerClientId);

                            NotifyHitConfirmClientRpc(false);
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                        }
                        else
                        {
                            // Bắn trúng môi trường / tường
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, false);
                        }
                    }
                }
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

        [ClientRpc]
        private void SpawnImpactEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool isFlesh)
        {
            // Báo event cho WeaponVisualEffects xử lý client-side
            OnImpactRequested?.Invoke(hitPoint, hitNormal, isFlesh);
        }

        private void NotifyWeaponUI()
        {
            if (_currentWeapon != null)
            {
                OnWeaponChanged?.Invoke(_currentWeapon.WeaponName);
                OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);
            }
        }
    }
}
