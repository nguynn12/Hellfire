// Script: DifficultyScalingData.cs
// Mục đích: Cấu hình ScriptableObject cân bằng độ khó theo số lượng người chơi (Mục 15.3 đặc tả kỹ thuật).
// Môi trường thực thi: Server đọc để tính toán hệ số quái và máu boss.

using UnityEngine;

namespace Hellfire.Dungeon
{
    [CreateAssetMenu(fileName = "DifficultyScalingData", menuName = "Hellfire/Difficulty Scaling Data")]
    public class DifficultyScalingData : ScriptableObject
    {
        [Header("Normal Enemy Count Multipliers (1, 2, 3, 4 players)")]
        [Tooltip("Hệ số số lượng quái thường theo 1/2/3/4 người chơi: mặc định [0.7, 1.0, 1.4, 1.8]")]
        [SerializeField] private float[] _enemyCountMultipliers = new float[4] { 0.7f, 1.0f, 1.4f, 1.8f };

        [Header("Boss Max Health Multipliers (1, 2, 3, 4 players)")]
        [Tooltip("Hệ số máu tối đa của boss theo 1/2/3/4 người chơi: mặc định [0.6, 1.0, 1.5, 2.0]")]
        [SerializeField] private float[] _bossHealthMultipliers = new float[4] { 0.6f, 1.0f, 1.5f, 2.0f };

        public float GetEnemyCountMultiplier(int playerCount)
        {
            if (_enemyCountMultipliers == null || _enemyCountMultipliers.Length == 0) return 1.0f;
            int index = Mathf.Clamp(playerCount - 1, 0, _enemyCountMultipliers.Length - 1);
            return _enemyCountMultipliers[index];
        }

        public float GetBossHealthMultiplier(int playerCount)
        {
            if (_bossHealthMultipliers == null || _bossHealthMultipliers.Length == 0) return 1.0f;
            int index = Mathf.Clamp(playerCount - 1, 0, _bossHealthMultipliers.Length - 1);
            return _bossHealthMultipliers[index];
        }
    }
}
