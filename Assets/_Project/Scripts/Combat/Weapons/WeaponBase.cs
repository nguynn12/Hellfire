using Hellfire.AI;
using Hellfire.Networking;
// Script: WeaponBase.cs
// Mục đích: Lớp cơ sở trừu tượng (Abstract Base Class) định nghĩa toàn bộ hành vi chung của vũ khí:
//           - Quản lý đạn dược (CurrentAmmo, ReserveAmmo) và Nạp đạn (Reload Coroutine)
//           - Trừ đạn và phân phối âm thanh ngẫu nhiên (Audio Variations with pitch)
//           - Văng vỏ đạn vật lý (Shell Ejection via WeaponFxPool)
//           - Chớp lửa nòng súng (Muzzle Flash via WeaponFxPool)
//           - Phân loại va chạm trúng thịt (Flesh Hit) vs trúng bề mặt/tường (Surface Hit)
//           - Cấp phát Decal vết đạn vĩnh viễn (Ring buffer) và Particle hoàn toàn Zero-GC
//           - Hàm trừu tượng ExecuteFire(Vector3 origin, Vector3 direction) cho các lớp con ghi đè
// Môi trường thực thi: Client & Server (MonoBehaviour).

using System;
using System.Collections;
using UnityEngine;

namespace Hellfire.Combat
{
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Weapon Data & Configuration")]
        [SerializeField] protected WeaponData _data;
        [SerializeField] protected Transform _muzzlePoint;
        [SerializeField] protected Transform _ejectionPort;
        [SerializeField] protected AudioSource _audioSource;

        protected WeaponController _controller;
        protected float _nextFireTime;
        protected Coroutine _reloadCoroutine;

        public WeaponData Data => _data;
        public Transform MuzzlePoint => _muzzlePoint;
        public Transform EjectionPort => _ejectionPort;
        public int CurrentAmmo { get; protected set; }
        public int ReserveAmmo { get; protected set; }
        public bool IsReloading { get; protected set; }
        public float NextFireTime => _nextFireTime;

        public event Action<int, int> OnAmmoChanged;
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;

        public virtual void Initialize(WeaponData data, WeaponController controller, Transform muzzlePoint = null, Transform ejectionPort = null)
        {
            _data = data;
            _controller = controller;

            if (muzzlePoint != null)
            {
                _muzzlePoint = muzzlePoint;
            }
            else if (_muzzlePoint == null)
            {
                _muzzlePoint = FindChildSocket(transform, "Muzzle") ?? FindChildSocket(transform, "MuzzlePoint") ?? transform;
            }

            if (ejectionPort != null)
            {
                _ejectionPort = ejectionPort;
            }
            else if (_ejectionPort == null)
            {
                _ejectionPort = FindChildSocket(transform, "EjectionPort") ?? FindChildSocket(transform, "ShellEject");
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>() ?? GetComponentInParent<AudioSource>();
            }

            if (_data != null)
            {
                CurrentAmmo = _data.MagSize;
                ReserveAmmo = _data.MaxReserveAmmo;
            }
        }

        public virtual void SetAmmo(int currentAmmo, int reserveAmmo)
        {
            CurrentAmmo = Mathf.Max(0, currentAmmo);
            ReserveAmmo = Mathf.Max(0, reserveAmmo);
            OnAmmoChanged?.Invoke(CurrentAmmo, _data != null ? _data.MagSize : 0);
        }

        public virtual bool CanFire()
        {
            return !IsReloading && CurrentAmmo > 0 && Time.time >= _nextFireTime;
        }

        public virtual bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (IsReloading) return false;

            if (CurrentAmmo <= 0)
            {
                if (_audioSource != null && _data != null && _data.EmptySound != null)
                {
                    _audioSource.PlayOneShot(_data.EmptySound);
                }
                StartReload();
                _nextFireTime = Time.time + 0.25f;
                return false;
            }

            if (Time.time < _nextFireTime) return false;

            // Kiểm tra InfiniteAmmo
            bool isInfinite = OfflineManager.Instance != null && OfflineManager.Instance.InfiniteAmmo;
            if (!isInfinite)
            {
                CurrentAmmo--;
            }

            OnAmmoChanged?.Invoke(CurrentAmmo, _data != null ? _data.MagSize : 0);

            // Tính toán thời gian bắn viên kế tiếp theo APS và AttackSpeedMultiplier
            float finalAps = _data != null ? _data.AttackRateAPS : 4.0f;
            if (_controller != null)
            {
                finalAps *= _controller.GlobalAttackSpeedMultiplier;
            }
            _nextFireTime = Time.time + (1.0f / Mathf.Max(0.1f, finalAps));

            // Thực thi cơ chế bắn đặc thù của từng dòng súng
            ExecuteFire(origin, direction);

            // Hiệu ứng lực bắn và âm thanh
            PlayFireAudio();
            EjectShell();
            PlayMuzzleFlash();
            ApplyRecoilAndPushback(direction);

            // Tự động nạp đạn khi bắn hết viên cuối
            if (CurrentAmmo <= 0 && !isInfinite)
            {
                StartReload();
            }

            return true;
        }

        protected abstract void ExecuteFire(Vector3 origin, Vector3 direction);

        protected virtual void HandleHitImpact(RaycastHit hit)
        {
            if (_data == null) return;

            bool isEnemy = hit.collider.CompareTag("Enemy") ||
                           hit.collider.GetComponentInParent<EnemyStateMachine>() != null ||
                           hit.collider.GetComponentInParent<Health>() != null;

            var fxPool = WeaponFxPool.GetOrCreate();

            if (isEnemy)
            {
                fxPool.PlayImpact(hit.point, hit.normal, true, _data.FleshImpactVfx);
                PlayImpactAudio(_data.FleshHitSounds, hit.point);
            }
            else
            {
                fxPool.PlayImpact(hit.point, hit.normal, false, _data.SurfaceImpactVfx);
                if (_data.EnableDecals)
                {
                    fxPool.SpawnBulletDecal(hit.point, hit.normal, hit.collider != null ? hit.collider.transform : null, _data.BulletHoleDecal);
                }
                PlayImpactAudio(_data.SurfaceImpactSounds, hit.point);
            }
        }

        protected virtual void PlayFireAudio()
        {
            if (_audioSource == null || _data == null) return;

            AudioClip sound = _data.GetRandomFireSound();
            if (sound != null)
            {
                _audioSource.pitch = _data.GetRandomPitch();
                _audioSource.PlayOneShot(sound, _data.FireVolume);
            }
        }

        protected virtual void PlayImpactAudio(AudioClip[] clips, Vector3 position)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null)
            {
                if (_audioSource != null)
                {
                    _audioSource.PlayOneShot(clip, 0.7f);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(clip, position, 0.7f);
                }
            }
        }

        protected virtual void EjectShell()
        {
            if (_data == null || !_data.EnableJuiceEffects || !_data.EnableShellEjection) return;

            Vector3 spawnPos = _ejectionPort != null
                ? _ejectionPort.position
                : (_muzzlePoint != null
                    ? _muzzlePoint.position - transform.forward * 0.22f + transform.right * 0.14f + transform.up * 0.05f
                    : transform.position);

            Vector3 fwd = transform.forward;
            Vector3 rgt = transform.right;
            Vector3 up = transform.up;

            WeaponFxPool.GetOrCreate().EjectShell(
                spawnPos,
                fwd,
                rgt,
                up,
                _data.ShellEjectionVelocity,
                _data.ShellCasingPrefab
            );
        }

        protected virtual void PlayMuzzleFlash()
        {
            if (_data == null || !_data.EnableJuiceEffects || !_data.UseMuzzleFlash || _muzzlePoint == null) return;
            WeaponFxPool.GetOrCreate().PlayMuzzleFlash(_muzzlePoint, _data.MuzzleFlashPrefab, _data.FlashDuration);
        }

        protected virtual void ApplyRecoilAndPushback(Vector3 shootDirection)
        {
            if (_controller != null && _data != null)
            {
                _controller.ApplyRecoilKick(_data, shootDirection);
            }
        }

        public virtual void StartReload()
        {
            if (IsReloading) return;
            if (_data == null) return;

            bool isInfinite = OfflineManager.Instance != null && OfflineManager.Instance.InfiniteAmmo;
            if (CurrentAmmo >= _data.MagSize) return;
            if (ReserveAmmo <= 0 && !isInfinite) return;

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
            }
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        public virtual void CancelReload()
        {
            if (!IsReloading) return;
            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _reloadCoroutine = null;
            }
            IsReloading = false;
        }

        protected virtual IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            OnReloadStarted?.Invoke();

            if (_audioSource != null && _data != null && _data.ReloadSound != null)
            {
                _audioSource.PlayOneShot(_data.ReloadSound);
            }

            float reloadDuration = _data != null ? _data.ReloadTime : 1.2f;
            if (_controller != null)
            {
                float reloadMult = _controller.GlobalReloadSpeedMultiplier;
                if (reloadMult > 0.05f) reloadDuration /= reloadMult;
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, reloadDuration));

            bool isInfinite = OfflineManager.Instance != null && OfflineManager.Instance.InfiniteAmmo;
            int needed = _data.MagSize - CurrentAmmo;
            if (isInfinite)
            {
                CurrentAmmo = _data.MagSize;
            }
            else
            {
                int toAdd = Mathf.Min(needed, ReserveAmmo);
                ReserveAmmo -= toAdd;
                CurrentAmmo += toAdd;
            }

            IsReloading = false;
            _reloadCoroutine = null;

            OnAmmoChanged?.Invoke(CurrentAmmo, _data.MagSize);
            OnReloadCompleted?.Invoke();
        }

        protected static Vector3 ApplyConeSpread(Vector3 forward, float angleDegrees)
        {
            if (angleDegrees <= 0.001f) return forward;

            float spreadRad = angleDegrees * Mathf.Deg2Rad;
            float u1 = UnityEngine.Random.value;
            float u2 = UnityEngine.Random.value;
            float radius = Mathf.Tan(spreadRad * 0.5f) * Mathf.Sqrt(u1);
            float theta = 2.0f * Mathf.PI * u2;

            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.Cross(forward, Vector3.right);
            }
            right.Normalize();
            Vector3 up = Vector3.Cross(right, forward).normalized;

            return (forward + right * (Mathf.Cos(theta) * radius) + up * (Mathf.Sin(theta) * radius)).normalized;
        }

        private static Transform FindChildSocket(Transform parent, string socketName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Equals(socketName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
                Transform deep = FindChildSocket(child, socketName);
                if (deep != null) return deep;
            }
            return null;
        }
    }
}
