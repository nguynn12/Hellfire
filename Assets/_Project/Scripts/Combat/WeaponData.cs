// Script: WeaponData.cs
// Mục đích: ScriptableObject định nghĩa thông số dữ liệu vũ khí theo Mục 6.1 & 6.2 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai (Dữ liệu tĩnh đọc bởi Server và Client).

using UnityEngine;

namespace Hellfire.Combat
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Hellfire/Combat/Weapon Data", order = 1)]
    public class WeaponData : ScriptableObject
    {
        [Header("General Info")]
        [SerializeField] private string _weaponName = "Súng lục (Pistol)";
        [SerializeField] private string _weaponId = "pistol";
        [SerializeField] private Sprite _weaponIcon;

        [Header("Damage & Multipliers (Mục 3.2 & 6.2)")]
        [Tooltip("Sát thương gốc mỗi viên đạn")]
        [SerializeField] private int _baseDamage = 15;

        [Tooltip("Hệ số sát thương khi trúng Đầu (Head)")]
        [SerializeField] private float _headMultiplier = 2.0f;

        [Tooltip("Hệ số sát thương khi trúng Thân (Torso)")]
        [SerializeField] private float _torsoMultiplier = 1.0f;

        [Tooltip("Hệ số sát thương khi trúng Tay/Chân (Limb)")]
        [SerializeField] private float _limbMultiplier = 0.75f;

        [Header("Ballistics & Hitscan (Mục 3.1 & 6.2)")]
        [Tooltip("Khoảng thời gian tối thiểu giữa 2 phát bắn (giây)")]
        [SerializeField] private float _fireRate = 0.25f;

        [Tooltip("Tầm bắn tối đa của tia raycast (mét)")]
        [SerializeField] private float _range = 100.0f;

        [Tooltip("Số tia raycast trong 1 lần bóp cò (Shotgun = 8, các súng khác = 1)")]
        [SerializeField] private int _pelletCount = 1;

        [Tooltip("Độ lệch ngẫu nhiên của tia raycast")]
        [SerializeField] private float _spread = 0.015f;

        [Tooltip("Chế độ bắn tự động giữ chuột (SMG) hay bắn từng phát (Pistol/Rifle/Shotgun)")]
        [SerializeField] private bool _isAutomatic = false;

        [Header("Magazine & Reload (Mục 6.2)")]
        [Tooltip("Số lượng đạn trong 1 băng")]
        [SerializeField] private int _magSize = 12;

        [Tooltip("Thời gian nạp đạn (giây)")]
        [SerializeField] private float _reloadTime = 1.2f;

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

        public int BaseDamage => _baseDamage;
        public float HeadMultiplier => _headMultiplier;
        public float TorsoMultiplier => _torsoMultiplier;
        public float LimbMultiplier => _limbMultiplier;

        public float FireRate => _fireRate;
        public float Range => _range;
        public int PelletCount => Mathf.Max(1, _pelletCount);
        public float Spread => _spread;
        public bool IsAutomatic => _isAutomatic;

        public int MagSize => _magSize;
        public float ReloadTime => _reloadTime;

        public GameObject ViewmodelPrefab => _viewmodelPrefab;
        public GameObject WorldModelPrefab => _worldModelPrefab;
        public AudioClip FireSound => _fireSound;
        public AudioClip ReloadSound => _reloadSound;
        public AudioClip EmptySound => _emptySound;

        /// <summary>
        /// Tính toán hệ số nhân sát thương theo loại Hitbox (Mục 3.2).
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
