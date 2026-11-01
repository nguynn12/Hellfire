using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Component hiển thị Hitmarker (chữ thập phản hồi trúng đạn) độc lập trên màn hình.
    /// Vẽ trực quan qua OnGUI hoặc Canvas, hỗ trợ chớp trắng khi bắn trúng thân, chớp đỏ rực khi Headshot.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitmarkerFeedback : MonoBehaviour
    {
        public static HitmarkerFeedback Instance { get; private set; }

        [Header("Hitmarker Appearance")]
        [SerializeField] private float _displayDuration = 0.2f;
        [SerializeField] private float _lineLength = 12f;
        [SerializeField] private float _lineWidth = 2.5f;
        [SerializeField] private float _centerOffset = 6f;

        private float _hitTimer;
        private bool _isHeadshot;
        private Texture2D _whiteTexture;
        private WeaponController _subscribedWeapon;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        private void OnEnable()
        {
            TryHookWeapon();
        }

        private void OnDisable()
        {
            if (_subscribedWeapon != null)
            {
                _subscribedWeapon.OnHitTargetConfirmed -= ShowHitmarker;
                _subscribedWeapon = null;
            }
        }

        private void OnDestroy()
        {
            if (_subscribedWeapon != null)
            {
                _subscribedWeapon.OnHitTargetConfirmed -= ShowHitmarker;
                _subscribedWeapon = null;
            }

            if (_whiteTexture != null) Destroy(_whiteTexture);
        }

        public void TryHookWeapon()
        {
            if (_subscribedWeapon != null) return;

            var weapons = FindObjectsByType<WeaponController>(FindObjectsSortMode.None);
            foreach (var w in weapons)
            {
                if (w != null && (w.IsLocallyControlled || !w.IsSpawned))
                {
                    _subscribedWeapon = w;
                    _subscribedWeapon.OnHitTargetConfirmed -= ShowHitmarker;
                    _subscribedWeapon.OnHitTargetConfirmed += ShowHitmarker;
                    break;
                }
            }
        }

        public void ShowHitmarker(bool isHeadshot)
        {
            _isHeadshot = isHeadshot;
            _hitTimer = _displayDuration;
        }

        private void Update()
        {
            if (_subscribedWeapon == null)
            {
                TryHookWeapon();
            }

            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (_hitTimer <= 0f) return;

            float alpha = Mathf.Clamp01(_hitTimer / _displayDuration);
            Color hitColor = _isHeadshot ? new Color(1.0f, 0.15f, 0.1f, alpha) : new Color(1.0f, 1.0f, 1.0f, alpha);
            GUI.color = hitColor;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Vẽ 4 vạch chéo 45 độ quanh tâm màn hình
            DrawDiagonalLine(center, 1, 1);   // Góc Đông Nam
            DrawDiagonalLine(center, -1, 1);  // Góc Tây Nam
            DrawDiagonalLine(center, 1, -1);  // Góc Đông Bắc
            DrawDiagonalLine(center, -1, -1); // Góc Tây Bắc
        }

        private void DrawDiagonalLine(Vector2 center, float dirX, float dirY)
        {
            Vector2 start = center + new Vector2(dirX * _centerOffset, dirY * _centerOffset);
            Vector2 size = new Vector2(dirX * _lineLength, dirY * _lineWidth);

            GUIUtility.RotateAroundPivot(45f * (dirX * dirY > 0 ? 1 : -1), start);
            GUI.DrawTexture(new Rect(start.x, start.y, _lineLength, _lineWidth), _whiteTexture);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}

