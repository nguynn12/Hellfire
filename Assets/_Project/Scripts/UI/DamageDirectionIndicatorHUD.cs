using Hellfire.AI;
using Hellfire.Combat;
using UnityEngine;

namespace Hellfire.UI
{
    /// <summary>
    /// Component hiển thị Cung đỏ chỉ hướng sát thương (Damage Direction Indicator) độc lập trên màn hình.
    /// Giúp người chơi lập tức xác định vị trí Zombie vừa tấn công mình (phía trước, sau lưng, trái, phải).
    /// Hoàn toàn độc lập, vẽ trực tiếp qua OnGUI mà không cần can thiệp GameplayHUD của đồng đội.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageDirectionIndicatorHUD : MonoBehaviour
    {
        public static DamageDirectionIndicatorHUD Instance { get; private set; }

        [Header("Appearance")]
        [SerializeField] private float _indicatorDuration = 1.2f;
        [SerializeField] private float _arcRadius = 140f;
        [SerializeField] private float _indicatorWidth = 40f;
        [SerializeField] private float _indicatorHeight = 14f;

        private float _displayTimer;
        private float _damageAngle; // Góc lệch 0 - 360 độ so với camera
        private Texture2D _indicatorTexture;
        private Health _playerHealth;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _playerHealth = GetComponent<Health>();

            // Tạo Texture hình mũi tên / cung đỏ
            _indicatorTexture = new Texture2D(1, 1);
            _indicatorTexture.SetPixel(0, 0, new Color(1.0f, 0.15f, 0.1f, 1.0f));
            _indicatorTexture.Apply();
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void OnDestroy()
        {
            if (_indicatorTexture != null) Destroy(_indicatorTexture);
        }

        private void HandleDamageTaken(float damage, HitboxType hitbox, ulong attackerId)
        {
            // Tìm vị trí kẻ địch gần nhất hoặc quái vật gây sát thương
            Vector3 attackerPos = FindLikelyAttackerPosition(attackerId);
            TriggerDamageIndicator(attackerPos);
        }

        public void TriggerDamageIndicator(Vector3 worldAttackerPos)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam == null) return;

            // Tính vector từ người chơi đến kẻ địch
            Vector3 toAttacker = (worldAttackerPos - transform.position);
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.01f) return;

            // Chiếu lên hướng nhìn của Camera
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();

            float forwardDot = Vector3.Dot(forward, toAttacker.normalized);
            float rightDot = Vector3.Dot(right, toAttacker.normalized);

            // Góc tính theo độ: 0 = phía trước, 90 = bên phải, 180 = sau lưng, 270 = bên trái
            _damageAngle = Mathf.Atan2(rightDot, forwardDot) * Mathf.Rad2Deg;
            _displayTimer = _indicatorDuration;
        }

        private Vector3 FindLikelyAttackerPosition(ulong attackerId)
        {
            // Tìm quái vật / kẻ địch gần nhất
            var allHealths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            float closestDist = float.MaxValue;
            Vector3 bestPos = transform.position - transform.forward * 5f; // Mặc định sau lưng

            foreach (var h in allHealths)
            {
                if (h == null || h.IsPlayer || h == _playerHealth) continue;
                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestPos = h.transform.position;
                }
            }

            return bestPos;
        }

        private void Update()
        {
            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (_displayTimer <= 0f) return;

            float alpha = Mathf.Clamp01(_displayTimer / _indicatorDuration);
            GUI.color = new Color(1.0f, 0.15f, 0.1f, alpha);

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Xoay ma trận GUI theo góc _damageAngle
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(_damageAngle, center);

            // Vẽ cung đỏ phía trên tâm màn hình bán kính _arcRadius
            Rect indicatorRect = new Rect(center.x - _indicatorWidth * 0.5f, center.y - _arcRadius, _indicatorWidth, _indicatorHeight);
            GUI.DrawTexture(indicatorRect, _indicatorTexture);

            GUI.matrix = savedMatrix;
        }
    }
}
