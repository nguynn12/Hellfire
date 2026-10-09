// Script: PowerUpData.cs
// Mục đích: ScriptableObject chứa dữ liệu cấu hình bùa lợi theo Mục 6.1 & 6.2 đặc tả kỹ thuật.
// Môi trường thực thi: Cả hai.

using UnityEngine;

namespace Hellfire.Items
{
    [CreateAssetMenu(fileName = "NewPowerUpData", menuName = "Hellfire/PowerUp Data", order = 2)]
    public class PowerUpData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private PowerUpType _powerUpType = PowerUpType.MaxHealthUp;
        [SerializeField] private string _powerUpName = "Bùa lợi";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "Mô tả hiệu ứng bùa lợi.";
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _pickupColor = Color.cyan;

        [Header("Effect Balancing (Mục 6.2)")]
        [Tooltip("Giá trị hiệu ứng (+25 máu, +0.20 tốc độ, +0.30 sát thương)")]
        [SerializeField] private float _value = 25f;

        [Tooltip("Thời lượng hiệu ứng tính bằng giây (0 = vĩnh viễn trong lượt chơi, >0 = tạm thời)")]
        [SerializeField] private float _duration = 0f;

        public PowerUpType Type => _powerUpType;
        public string PowerUpName => _powerUpName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public Color PickupColor => _pickupColor;
        public float Value => _value;
        public float Duration => _duration;
        public bool IsPermanent => _duration <= 0f;
    }
}
