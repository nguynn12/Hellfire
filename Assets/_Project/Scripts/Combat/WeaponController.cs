// Script: WeaponController.cs
// Mục đích: Quản lý kho vũ khí RuntimeWeapon, thực thi bắn Hitscan và chém cận chiến Melee (Server-Authoritative).
//           Zero-GC optimization cho vòng lặp chiến đấu và hỗ trợ nâng cấp vũ khí qua Tế đàn (Shrine).
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
        private readonly List<RuntimeWeapon> _runtimeWeapons = new List<RuntimeWeapon>();
        private int _currentAmmo;
        private float _nextFireTime;
        private bool _isReloading;
        private float _reloadTimer;
        private int _currentWeaponIndex = 0;

        // Bộ đệm va chạm cận chiến dùng lại để đạt Zero-GC (Mục I.5)
        private static readonly Collider[] _meleeHitBuffer = new Collider[32];
        private static Material _cachedFxMaterial;

        // Events for UI HUD
        public event Action<int, int> OnAmmoChanged;              // currentAmmo, magSize
        public event Action<string> OnWeaponChanged;               // weaponName
        public event Action<bool> OnReloadStateChanged;           // isReloading
        public event Action<bool> OnHitTargetConfirmed;           // isHeadshot
        public event Action<int> OnWeaponUpgraded;                 // newLevel

        public WeaponData CurrentWeaponData => CurrentRuntimeWeapon != null ? CurrentRuntimeWeapon.BaseData : _currentWeapon;
        public RuntimeWeapon CurrentRuntimeWeapon => (_runtimeWeapons != null && _currentWeaponIndex >= 0 && _currentWeaponIndex < _runtimeWeapons.Count) ? _runtimeWeapons[_currentWeaponIndex] : null;
        public IReadOnlyList<RuntimeWeapon> RuntimeWeapons => _runtimeWeapons;
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
                _audioSource.spatialBlend = 0f; // 2D sound for local player
            }

            if (_weaponMuzzlePoint == null)
            {
                var viewmodel = transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel/GunMesh");
                if (viewmodel != null) _weaponMuzzlePoint = viewmodel;
            }
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

            if (IsOwner)
            {
                NotifyWeaponUI();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Nếu người chơi đang gục hoặc chết -> Không cho phép tấn công / nạp đạn (Mục 3.3)
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
            Debug.Log($"[WeaponController] Đã đổi sang vũ khí: {_currentWeapon.WeaponName}");
        }

        public void SetWeaponData(WeaponData newWeapon)
        {
            if (newWeapon == null) return;
            _currentWeapon = newWeapon;
            _currentAmmo = newWeapon.MagSize;
            _isReloading = false;

            // Đồng bộ hoặc tạo RuntimeWeapon mới nếu chưa có
            bool found = false;
            for (int i = 0; i < _runtimeWeapons.Count; i++)
            {
                if (_runtimeWeapons[i].BaseData == newWeapon)
                {
                    _currentWeaponIndex = i;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var rw = new RuntimeWeapon(newWeapon);
                _runtimeWeapons.Add(rw);
                _currentWeaponIndex = _runtimeWeapons.Count - 1;
            }

            NotifyWeaponUI();
        }

        public void AddAvailableWeapon(WeaponData weapon)
        {
            if (weapon != null && !_availableWeapons.Contains(weapon))
            {
                _availableWeapons.Add(weapon);
                _runtimeWeapons.Add(new RuntimeWeapon(weapon));
            }
        }

        private void HandleReloading()
        {
            // Vũ khí cận chiến không cần nạp đạn
            if (_currentWeapon != null && _currentWeapon.Type == WeaponType.MeleeWeapon) return;

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

        private void HandleAttackInput()
        {
            if (_currentWeapon == null || _playerCamera == null) return;

            bool triggerPressed = false;

            if (Mouse.current != null)
            {
                if (_currentWeapon.IsAutomatic)
                {
                    triggerPressed = Mouse.current.leftButton.isPressed;
                }
                else
                {
                    triggerPressed = Mouse.current.leftButton.wasPressedThisFrame;
                }
            }

            if (!triggerPressed) return;
            if (Time.time < _nextFireTime) return;
            if (_isReloading) return;

            // Xử lý nạp đạn cho súng
            if (_currentWeapon.Type == WeaponType.RangedGun && _currentAmmo <= 0)
            {
                if (_audioSource != null && _currentWeapon.EmptySound != null)
                {
                    _audioSource.PlayOneShot(_currentWeapon.EmptySound);
                }
                StartReload();
                _nextFireTime = Time.time + 0.3f;
                return;
            }

            // Thực thi tấn công theo hình thái vũ khí (Cận chiến hoặc Súng)
            if (_currentWeapon.Type == WeaponType.MeleeWeapon)
            {
                ExecuteMeleeAttack();
            }
            else
            {
                ExecuteRangedFire();
            }
        }

        // ================= CẬN CHIẾN (MELEE ATTACK) =================

        private void ExecuteMeleeAttack()
        {
            RuntimeWeapon rw = CurrentRuntimeWeapon;
            rw.EvaluateFinalStats(out _, out float finalAPS, out _, out _, out _);
            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAPS));

            // Phát âm thanh chém vung
            if (_audioSource != null && _currentWeapon.FireSound != null)
            {
                _audioSource.PlayOneShot(_currentWeapon.FireSound);
            }

            Vector3 cameraPos = _playerCamera.transform.position;
            Vector3 cameraForward = _playerCamera.transform.forward;

            // Gửi ServerRpc để Server thực hiện quét OverlapSphereNonAlloc
            MeleeAttackServerRpc(cameraPos, cameraForward, _currentWeaponIndex);
        }

        [ServerRpc]
        private void MeleeAttackServerRpc(Vector3 origin, Vector3 forwardDir, int weaponIndex)
        {
            RuntimeWeapon rw = GetRuntimeWeaponByIndex(weaponIndex);
            if (rw == null) return;

            if (_playerHealth != null && (_playerHealth.IsDowned.Value || _playerHealth.IsDead.Value))
            {
                return;
            }

            rw.EvaluateFinalStats(
                out _,
                out _,
                out float armorPen,
                out _,
                out _
            );

            float range = rw.BaseData.MeleeRange;
            float maxAngle = rw.BaseData.MeleeSwingAngle * 0.5f;

            // Quét hình cầu không cấp phát bộ nhớ (Zero-GC)
            int hitCount = Physics.OverlapSphereNonAlloc(origin, range, _meleeHitBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCol = _meleeHitBuffer[i];
                if (hitCol == null) continue;

                // Bỏ qua chính bản thân người đánh
                if (hitCol.transform.IsChildOf(transform) || hitCol.gameObject == gameObject)
                {
                    continue;
                }

                // Kiểm tra góc quét hình nón trước mặt
                Vector3 toTarget = (hitCol.bounds.center - origin);
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(forwardDir, toTarget.normalized);
                    if (angle > maxAngle)
                    {
                        continue;
                    }
                }

                // Kiểm tra Friendly Fire với đồng đội
                if (hitCol.CompareTag("Player") || hitCol.GetComponentInParent<PlayerMovement>() != null)
                {
                    var hitHealth = hitCol.GetComponentInParent<Health>();
                    if (hitHealth != null && hitHealth.NetworkObjectId != NetworkObjectId)
                    {
                        continue; // Không gây sát thương cho đồng đội
                    }
                }

                // Tính sát thương (xét tỷ lệ chí mạng Crit)
                float strikeDamage = rw.CalculateAttackDamage(out bool isCrit);

                var hitbox = hitCol.GetComponent<HitboxIdentifier>();
                if (hitbox != null)
                {
                    var payload = new DamagePayload(strikeDamage, armorPen, hitbox.Type, OwnerClientId);
                    hitbox.ForwardDamage(payload);

                    rw.NotifyHit(payload, hitbox.Damageable);
                    NotifyHitConfirmClientRpc(hitbox.Type == HitboxType.Head);
                    SpawnImpactEffectClientRpc(hitCol.bounds.center, Vector3.up, true);
                }
                else
                {
                    var damageable = hitCol.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsPlayerTarget)
                    {
                        var payload = new DamagePayload(strikeDamage, armorPen, HitboxType.Torso, OwnerClientId);
                        damageable.TakeDamage(payload);

                        rw.NotifyHit(payload, damageable);
                        NotifyHitConfirmClientRpc(false);
                        SpawnImpactEffectClientRpc(hitCol.bounds.center, Vector3.up, true);
                    }
                }
            }
        }

        // ================= SÚNG BẮN (RANGED HITSCAN) =================

        private void ExecuteRangedFire()
        {
            RuntimeWeapon rw = CurrentRuntimeWeapon;
            rw.EvaluateFinalStats(out _, out float finalAPS, out _, out _, out _);

            _currentAmmo--;
            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAPS));
            OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);

            // 1. Client Cosmetic Visuals ngay lập tức
            if (_audioSource != null && _currentWeapon.FireSound != null)
            {
                _audioSource.PlayOneShot(_currentWeapon.FireSound);
            }

            Vector3 cameraPos = _playerCamera.transform.position;
            Vector3 cameraForward = _playerCamera.transform.forward;

            int pellets = _currentWeapon.PelletCount;
            int seed = UnityEngine.Random.Range(0, 100000);

            // Client vẽ tracer cục bộ
            for (int i = 0; i < pellets; i++)
            {
                Vector3 spreadDir = CalculateSpreadDirection(cameraForward, _currentWeapon.Spread, seed + i);
                Vector3 endPoint = cameraPos + spreadDir * _currentWeapon.Range;

                if (Physics.Raycast(cameraPos, spreadDir, out var hit, _currentWeapon.Range))
                {
                    endPoint = hit.point;
                }

                DrawCosmeticTracer(GetMuzzlePosition(), endPoint);
            }

            // 2. Gửi ServerRpc yêu cầu Server xác thực độc lập
            FireServerRpc(cameraPos, cameraForward, seed, _currentWeaponIndex);
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

            rw.EvaluateFinalStats(
                out _,
                out _,
                out float armorPen,
                out _,
                out _
            );

            int pellets = weaponToUse.PelletCount;
            float range = weaponToUse.Range;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shootDir = CalculateSpreadDirection(baseDirection, weaponToUse.Spread, seed + i);

                if (Physics.Raycast(clientOrigin, shootDir, out var hit, range))
                {
                    // 1. Kiểm tra Friendly Fire
                    if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerMovement>() != null)
                    {
                        var hitHealth = hit.collider.GetComponentInParent<Health>();
                        if (hitHealth != null && hitHealth.NetworkObjectId != NetworkObjectId)
                        {
                            SpawnImpactEffectClientRpc(hit.point, hit.normal, true);
                            continue;
                        }
                    }

                    // 2. Tính sát thương (xét Crit)
                    float attackDamage = rw.CalculateAttackDamage(out bool isCrit);

                    // 3. Đóng gói DamagePayload gửi sang mục tiêu
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

        // ================= NÂNG CẤP VŨ KHÍ TỪ TẾ ĐÀN (SHRINE UPGRADE) =================

        /// <summary>
        /// Nâng cấp vũ khí hiện tại của người chơi trên Server.
        /// </summary>
        /// <param name="levels">Số cấp tăng</param>
        public void UpgradeCurrentWeaponServer(int levels = 1)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[WeaponController] Chỉ Server mới có quyền nâng cấp vũ khí!");
                return;
            }

            RuntimeWeapon rw = CurrentRuntimeWeapon;
            if (rw != null)
            {
                rw.ApplyLevelUpgrade(levels);
                NotifyWeaponUpgradedClientRpc(_currentWeaponIndex, rw.UpgradeLevel);
            }
        }

        [ClientRpc]
        private void NotifyWeaponUpgradedClientRpc(int weaponIndex, int newLevel)
        {
            if (weaponIndex >= 0 && weaponIndex < _runtimeWeapons.Count)
            {
                int diff = newLevel - _runtimeWeapons[weaponIndex].UpgradeLevel;
                if (diff > 0)
                {
                    _runtimeWeapons[weaponIndex].ApplyLevelUpgrade(diff);
                }
            }

            if (IsOwner)
            {
                NotifyWeaponUI();
                OnWeaponUpgraded?.Invoke(newLevel);
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
                if (CurrentRuntimeWeapon != null && CurrentRuntimeWeapon.UpgradeLevel > 0)
                {
                    displayName += $" +{CurrentRuntimeWeapon.UpgradeLevel}";
                }

                OnWeaponChanged?.Invoke(displayName);
                OnAmmoChanged?.Invoke(_currentAmmo, _currentWeapon.MagSize);
            }
        }
    }
}
