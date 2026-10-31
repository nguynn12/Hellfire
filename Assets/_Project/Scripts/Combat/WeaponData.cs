// Script: WeaponData.cs
// Mục đích: ScriptableObject định nghĩa thông số dữ liệu vũ khí (Tấn công, APS, Xuyên giáp, Crit, Tầm quét Melee) theo đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai (Dữ liệu tĩnh đọc bởi Server và Client).

using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Phân loại hình thái của vũ khí: Súng tầm xa hoặc Vũ khí cận chiến.
    /// </summary>
    public enum WeaponType
    {
        RangedGun,
        MeleeWeapon
    }

    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Hellfire/Combat/Weapon Data", order = 1)]
    public class WeaponData : ScriptableObject
    {
        [Header("General Info")]
        [SerializeField] private string _weaponName = "Súng lục (Pistol)";
        [SerializeField] private string _weaponId = "pistol";
        [SerializeField] private Sprite _weaponIcon;
        [SerializeField] private WeaponType _weaponType = WeaponType.RangedGun;

        [Header("Offensive Stats & Combat (Mục I.2 & II.2)")]
        [Tooltip("Sát thương gốc mỗi đòn đánh/phát bắn")]
        [SerializeField] private int _baseDamage = 15;

        [Tooltip("Tốc độ ra đòn (Attacks Per Second - APS). Ví dụ: 4 APS = 0.25s mỗi đòn")]
        [SerializeField] private float _attackRateAPS = 4.0f;

        [Tooltip("Chỉ số xuyên giáp của vũ khí")]
        [SerializeField] private float _armorPenetration = 0f;

        [Tooltip("Tỷ lệ chí mạng cơ bản (0.0 đến 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] private float _critChance = 0.1f;

        [Tooltip("Hệ số nhân sát thương khi kích hoạt chí mạng")]
        [SerializeField] private float _critDamageMultiplier = 1.5f;

        [Header("Hitbox Multipliers (Mục 3.2)")]
        [Tooltip("Hệ số sát thương khi trúng Đầu (Head)")]
        [SerializeField] private float _headMultiplier = 2.0f;

        [Tooltip("Hệ số sát thương khi trúng Thân (Torso)")]
        [SerializeField] private float _torsoMultiplier = 1.0f;

        [Tooltip("Hệ số sát thương khi trúng Tay/Chân (Limb)")]
        [SerializeField] private float _limbMultiplier = 0.75f;

        [Header("Ballistics & Hitscan (Cho RangedGun)")]
        [Tooltip("Khoảng thời gian tối thiểu giữa 2 phát bắn (giây) - dự phòng")]
        [SerializeField] private float _fireRate = 0.25f;

        [Tooltip("Tầm bắn tối đa của tia raycast (mét)")]
        [SerializeField] private float _range = 100.0f;

        [Tooltip("Số tia raycast trong 1 lần bóp cò (Shotgun = 8, các súng khác = 1)")]
        [SerializeField] private int _pelletCount = 1;

        [Tooltip("Độ lệch ngẫu nhiên của tia raycast")]
        [SerializeField] private float _spread = 0.015f;

        [Tooltip("Chế độ bắn tự động giữ chuột (SMG) hay bắn từng phát (Pistol/Rifle/Shotgun)")]
        [SerializeField] private bool _isAutomatic = false;

        [Header("Magazine & Reload (Cho RangedGun)")]
        [Tooltip("Số lượng đạn trong 1 băng")]
        [SerializeField] private int _magSize = 12;

        [Tooltip("Số lượng đạn dự trữ tối đa mang theo")]
        [SerializeField] private int _maxReserveAmmo = 120;

        [Tooltip("Thời gian nạp đạn (giây)")]
        [SerializeField] private float _reloadTime = 1.2f;

        [Header("Melee Parameters (Cho MeleeWeapon)")]
        [Tooltip("Tầm quét hình cầu của đòn đánh cận chiến (mét)")]
        [SerializeField] private float _meleeRange = 2.5f;

        [Tooltip("Góc quét hình nón trước mặt nhân vật (độ)")]
        [SerializeField] private float _meleeSwingAngle = 70.0f;

        [Header("Visual & Effects Assets")]
        [SerializeField] private GameObject _viewmodelPrefab;
        [SerializeField] private GameObject _worldModelPrefab;
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [SerializeField] private GameObject _tracerPrefab;
        [SerializeField] private GameObject _impactEffectPrefab;
        [SerializeField] private GameObject _fleshImpactEffectPrefab;
        [SerializeField] private GameObject _casingPrefab;

        [Header("Model Alignment & ADS (Góc nhìn & Ngắm bắn)")]
        [Tooltip("Độ lệch vị trí khi cầm súng thông thường")]
        [SerializeField] private Vector3 _viewmodelOffset = Vector3.zero;
        [Tooltip("Độ lệch góc xoay khi cầm súng thông thường")]
        [SerializeField] private Vector3 _viewmodelRotation = Vector3.zero;
        [Tooltip("Bật chế độ ngắm bắn qua đầu ruồi/ống ngắm (ADS)")]
        [SerializeField] private bool _hasAds = true;
        [Tooltip("Vị trí dịch chuyển cục bộ khi ngắm ADS để đưa thước ngắm về giữa màn hình")]
        [SerializeField] private Vector3 _adsOffset = new Vector3(-0.25f, 0.05f, 0.05f);
        [Tooltip("Góc nhìn FOV của camera khi ngắm ADS")]
        [Range(30f, 85f)]
        [SerializeField] private float _adsFov = 65.0f;
        [Tooltip("Hệ số thu hẹp độ phân tán đạn khi ngắm ADS (0.35 = giảm 65% spread)")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _adsSpreadMultiplier = 0.35f;

        
        [Header("=== JUICE & GAME FEEL SETTINGS ===")]
        [SerializeField] private bool _enableJuiceEffects = true;

        [Header("1. Muzzle Flash")]
        [SerializeField] private bool _useMuzzleFlash = true;
        [SerializeField] private float _flashDuration = 0.04f;

        [Header("2. Spread & Tracer")]
        [SerializeField] private float _spreadDegrees = 3.0f;
        [SerializeField] private float _tracerSpeedJitter = 0.10f; // ±10%
        [SerializeField] private float _tracerLength = 3.5f;

        [Header("3. Recoil & Pushback")]
        [SerializeField] private float _playerPushbackForce = 1.8f;

        [Header("4. Shell Ejection")]
        [SerializeField] private bool _enableShellEjection = true;
        [SerializeField] private GameObject _shellPrefab;
        [SerializeField] private Vector3 _shellEjectionVelocity = new Vector3(3f, 2.5f, 0f);

        [Header("5. Decals")]
        [SerializeField] private bool _enableDecals = true;
        [SerializeField] private GameObject _bulletDecalPrefab;

        [Header("6. Lethal Shot (Kill Juice)")]
        [SerializeField] private bool _enableKillSlowMo = true;
        [SerializeField] private float _killSlowMoScale = 0.25f;
        [SerializeField] private float _killSlowMoDuration = 0.30f;
        [SerializeField] private float _enemyDeathImpulse = 14.0f;

        [Header("Recoil & Procedural Kickback (Độ giật súng & Camera)")]
        [Tooltip("Lực nảy giật lùi về phía sau của viewmodel khi bắn (mét)")]
        [SerializeField] private float _kickbackForce = 0.05f;
        [Tooltip("Góc nảy ngửa lên trên của viewmodel khi bắn (độ)")]
        [SerializeField] private float _kickbackAngle = 2.0f;
        [Tooltip("Độ nảy ngửa góc nhìn camera khi bóp cò (độ)")]
        [SerializeField] private float _cameraRecoilPitch = 1.0f;
        [Tooltip("Độ lắc ngang ngẫu nhiên của camera khi bóp cò (độ)")]
        [SerializeField] private float _cameraRecoilYaw = 0.25f;
        [Tooltip("Tốc độ hồi tâm súng và camera về vị trí cũ")]
        [SerializeField] private float _recoilRecoverySpeed = 12.0f;

        [Header("Ballistics & Damage Falloff (Giảm sát thương theo cự ly)")]
        [Tooltip("Tầm bắn hiệu quả tối đa giữ 100% sát thương (mét)")]
        [SerializeField] private float _effectiveRange = 30.0f;
        [Tooltip("Hệ số sát thương tối thiểu khi bắn vượt quá tầm bắn hiệu quả (0.3 = 30% dame)")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _minDamageMultiplier = 0.3f;

        [Header("Audio Assets & Variations")]
        [SerializeField] private AudioClip _fireSound;
        [Tooltip("Mảng các biến thể âm thanh bắn để phát ngẫu nhiên tránh đơn điệu")]
        [SerializeField] private AudioClip[] _fireSounds;
        [SerializeField] private AudioClip _reloadSound;
        [SerializeField] private AudioClip _emptySound;
        [SerializeField] private AudioClip _meleeSwingSound;

        // Getters
        public string WeaponName => _weaponName;
        public string WeaponId => _weaponId;
        public Sprite WeaponIcon => _weaponIcon;
        public WeaponType Type => _weaponType;

        public int BaseDamage => _baseDamage;
        public float AttackRateAPS => _attackRateAPS > 0.001f ? _attackRateAPS : (_fireRate > 0.001f ? (1f / _fireRate) : 4.0f);
        public float FireRate => 1f / Mathf.Max(0.01f, AttackRateAPS);
        public float ArmorPenetration => _armorPenetration;
        public float CritChance => _critChance;
        public float CritDamageMultiplier => _critDamageMultiplier;

        public float HeadMultiplier => _headMultiplier;
        public float TorsoMultiplier => _torsoMultiplier;
        public float LimbMultiplier => _limbMultiplier;

        public float Range => _range;
        public int PelletCount => Mathf.Max(1, _pelletCount);
        public float Spread => _spread;
        public bool IsAutomatic => _isAutomatic;

        public int MagSize => _magSize;
        public int MaxReserveAmmo => _maxReserveAmmo > 0 ? _maxReserveAmmo : (_magSize * 6);
        public float ReloadTime => _reloadTime;

        public float MeleeRange => _meleeRange;
        public float MeleeSwingAngle => _meleeSwingAngle;

        public GameObject ViewmodelPrefab => _viewmodelPrefab;
        public GameObject WorldModelPrefab => _worldModelPrefab;
        public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;
        public GameObject TracerPrefab => _tracerPrefab;
        public GameObject ImpactEffectPrefab => _impactEffectPrefab;
        public GameObject FleshImpactEffectPrefab => _fleshImpactEffectPrefab;
        public GameObject CasingPrefab => _casingPrefab;

        public Vector3 ViewmodelOffset => _viewmodelOffset;
        public Vector3 ViewmodelRotation => _viewmodelRotation;
        public bool HasAds => _hasAds;
        public Vector3 AdsOffset => _adsOffset;
        public float AdsFov => _adsFov > 10f ? _adsFov : 65.0f;
        public float AdsSpreadMultiplier => Mathf.Clamp(_adsSpreadMultiplier, 0.05f, 1.0f);

        public float KickbackForce => Mathf.Max(0f, _kickbackForce);
        public float KickbackAngle => Mathf.Max(0f, _kickbackAngle);
        public float CameraRecoilPitch => Mathf.Max(0f, _cameraRecoilPitch);
        public float CameraRecoilYaw => Mathf.Max(0f, _cameraRecoilYaw);
        public float RecoilRecoverySpeed => _recoilRecoverySpeed > 0.1f ? _recoilRecoverySpeed : 12.0f;

        public bool EnableJuiceEffects => _enableJuiceEffects;
        public bool UseMuzzleFlash => _useMuzzleFlash;
        public float FlashDuration => _flashDuration > 0.005f ? _flashDuration : 0.04f;
        public float SpreadDegrees => Mathf.Max(0f, _spreadDegrees);
        public float TracerSpeedJitter => Mathf.Clamp(_tracerSpeedJitter, 0f, 0.5f);
        public float TracerLength => Mathf.Max(0.5f, _tracerLength);
        public float PlayerPushbackForce => Mathf.Max(0f, _playerPushbackForce);
        public bool EnableShellEjection => _enableShellEjection;
        public GameObject ShellPrefab => _shellPrefab != null ? _shellPrefab : _casingPrefab;
        public Vector3 ShellEjectionVelocity => _shellEjectionVelocity;
        public bool EnableDecals => _enableDecals;
        public GameObject BulletDecalPrefab => _bulletDecalPrefab;
        public bool EnableKillSlowMo => _enableKillSlowMo;
        public float KillSlowMoScale => Mathf.Clamp(_killSlowMoScale, 0.05f, 0.9f);
        public float KillSlowMoDuration => Mathf.Max(0.05f, _killSlowMoDuration);
        public float EnemyDeathImpulse => Mathf.Max(0f, _enemyDeathImpulse);

        public float EffectiveRange => _effectiveRange > 0.1f ? _effectiveRange : (_range * 0.4f);
        public float MinDamageMultiplier => Mathf.Clamp(_minDamageMultiplier, 0.1f, 1.0f);

        public AudioClip FireSound => _fireSound;
        public AudioClip[] FireSounds => _fireSounds;
        public AudioClip ReloadSound => _reloadSound;
        public AudioClip EmptySound => _emptySound;
        public AudioClip MeleeSwingSound => _meleeSwingSound;

        /// <summary>
        /// Lấy ngẫu nhiên âm thanh bắn từ danh sách hoặc trả về âm thanh mặc định.
        /// </summary>
        public AudioClip GetRandomFireSound()
        {
            if (_fireSounds != null && _fireSounds.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, _fireSounds.Length);
                if (_fireSounds[idx] != null) return _fireSounds[idx];
            }
            return _fireSound;
        }

        /// <summary>
        /// Tính toán hệ số nhân sát thương theo loại Hitbox.
        /// </summary>
        public float GetMultiplier(HitboxType hitboxType)
        {
            switch (hitboxType)
            {
                case HitboxType.Head:
                    return _headMultiplier;
                case HitboxType.Torso:
                    return _torsoMultiplier;
                case HitboxType.Limb:
                    return _limbMultiplier;
                default:
                    return 1.0f;
            }
        }
    }
}
