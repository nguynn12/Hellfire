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
        [Tooltip("Giá mua tại Shop Station (Linh hồn) (Mục 17.8)")]
        [SerializeField] private int _shopPrice = 80;

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

        [Tooltip("Thời gian nạp đạn (giây)")]
        [SerializeField] private float _reloadTime = 1.2f;

        [Header("Melee Parameters (Cho MeleeWeapon)")]
        [Tooltip("Tầm quét hình cầu của đòn đánh cận chiến (mét)")]
        [SerializeField] private float _meleeRange = 2.5f;

        [Tooltip("Góc quét hình nón trước mặt nhân vật (độ)")]
        [SerializeField] private float _meleeSwingAngle = 70.0f;

        [Header("Visual & Audio Assets")]
        [SerializeField] private GameObject _viewmodelPrefab;
        [SerializeField] private GameObject _worldModelPrefab;
        [SerializeField] private AudioClip _fireSound;
        [SerializeField] private AudioClip _reloadSound;
        [SerializeField] private AudioClip _emptySound;

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
        public float ReloadTime => _reloadTime;
        public int ShopPrice => _shopPrice;

        public float MeleeRange => _meleeRange;
        public float MeleeSwingAngle => _meleeSwingAngle;

        public GameObject ViewmodelPrefab => _viewmodelPrefab;
        public GameObject WorldModelPrefab => _worldModelPrefab;
        public AudioClip FireSound => _fireSound;
        public AudioClip ReloadSound => _reloadSound;
        public AudioClip EmptySound => _emptySound;

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
