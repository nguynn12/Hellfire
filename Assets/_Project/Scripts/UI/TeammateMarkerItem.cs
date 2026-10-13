// Script: TeammateMarkerItem.cs
// Mục đích: Phần tử UI marker đại diện cho 1 đồng đội trên Canvas Screen Space Overlay (Mục 16.3 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using Hellfire.Combat;
using Hellfire.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class TeammateMarkerItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private Image _iconBackground;
        [SerializeField] private TextMeshProUGUI _playerNumberText;
        [SerializeField] private TextMeshProUGUI _distanceText;
        [SerializeField] private RectTransform _arrowIndicator;
        [SerializeField] private CanvasGroup _canvasGroup;

        private PlayerIdentity _targetIdentity;
        private Health _targetHealth;
        private Transform _targetTransform;

        public bool IsBound => _targetIdentity != null;

        private void Awake()
        {
            if (_rootRect == null) _rootRect = GetComponent<RectTransform>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Configure(
            RectTransform rootRect,
            Image iconBg,
            TextMeshProUGUI numText,
            TextMeshProUGUI distText,
            RectTransform arrow,
            CanvasGroup cg)
        {
            _rootRect = rootRect;
            _iconBackground = iconBg;
            _playerNumberText = numText;
            _distanceText = distText;
            _arrowIndicator = arrow;
            _canvasGroup = cg;
        }

        public void Bind(PlayerIdentity identity)
        {
            _targetIdentity = identity;
            if (_targetIdentity != null)
            {
                _targetTransform = _targetIdentity.transform;
                _targetHealth = _targetIdentity.GetComponent<Health>();
                UpdateAppearance();
                gameObject.SetActive(true);
            }
            else
            {
                Unbind();
            }
        }

        public void Unbind()
        {
            _targetIdentity = null;
            _targetHealth = null;
            _targetTransform = null;
            gameObject.SetActive(false);
        }

        public void UpdateAppearance()
        {
            if (_targetIdentity == null) return;

            int colorIndex = _targetIdentity.ColorIndex.Value;
            Color color = PlayerPalette.GetColor(colorIndex);
            int number = PlayerPalette.GetPlayerNumber(colorIndex);

            if (_iconBackground != null)
            {
                _iconBackground.color = color;
            }

            if (_playerNumberText != null)
            {
                _playerNumberText.text = number.ToString();
            }
        }

        /// <summary>
        /// Cập nhật vị trí và trạng thái marker mỗi frame từ TeammateMarkerManager.
        /// </summary>
        public void UpdateMarker(Camera cam, float screenWidth, float screenHeight)
        {
            if (_targetIdentity == null || _targetTransform == null || cam == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // Đồng đội Chết (Dead): Bỏ marker (Mục 16.3)
            if (_targetHealth != null && _targetHealth.IsDead.Value)
            {
                gameObject.SetActive(false);
                return;
            }

            // Vị trí đầu nhân vật + 0.5m theo mục 16.3
            Vector3 worldPos = _targetTransform.position + Vector3.up * 2.3f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            float distance = Vector3.Distance(cam.transform.position, worldPos);

            // Kiểm tra phía sau camera (z < 0) -> Lật hướng
            bool isBehind = screenPos.z < 0f;
            if (isBehind)
            {
                screenPos.x = screenWidth - screenPos.x;
                screenPos.y = screenHeight - screenPos.y;
            }

            // Chừa lề 5% màn hình theo mục 16.3
            float marginX = screenWidth * 0.05f;
            float marginY = screenHeight * 0.05f;

            float minX = marginX;
            float maxX = screenWidth - marginX;
            float minY = marginY;
            float maxY = screenHeight - marginY;

            bool isOffScreen = isBehind || screenPos.x < minX || screenPos.x > maxX || screenPos.y < minY || screenPos.y > maxY;

            // Kẹp vào mép màn hình khi ngoài màn hình
            float clampedX = Mathf.Clamp(screenPos.x, minX, maxX);
            float clampedY = Mathf.Clamp(screenPos.y, minY, maxY);

            if (_rootRect != null)
            {
                _rootRect.position = new Vector3(clampedX, clampedY, 0f);
            }

            // Kích thước thu nhỏ dần theo khoảng cách (từ 1.0 ở gần xuống 0.6 từ 40m trở lên) (Mục 16.3)
            float scale = Mathf.Lerp(1.0f, 0.6f, Mathf.Clamp01(distance / 40.0f));
            if (_rootRect != null)
            {
                _rootRect.localScale = Vector3.one * scale;
            }

            // Mũi tên chỉ hướng khi nằm ngoài màn hình
            if (_arrowIndicator != null)
            {
                if (isOffScreen)
                {
                    _arrowIndicator.gameObject.SetActive(true);
                    Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
                    Vector2 dir = new Vector2(clampedX - screenCenter.x, clampedY - screenCenter.y);
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _arrowIndicator.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }
                else
                {
                    _arrowIndicator.gameObject.SetActive(false);
                }
            }

            // Xử lý trạng thái Gục (Downed): Nhấp nháy ~2 lần/giây kèm số giây còn lại (Mục 16.3)
            bool isDowned = _targetHealth != null && _targetHealth.IsDowned.Value;
            if (isDowned)
            {
                // Nhấp nháy tần số 2Hz
                float blinkAlpha = Mathf.PingPong(Time.time * 4f, 1f) > 0.5f ? 1.0f : 0.2f;
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = blinkAlpha;
                }

                if (_distanceText != null)
                {
                    float timer = Mathf.Max(0f, _targetHealth.DownedTimer.Value);
                    _distanceText.text = $"⚠ {Mathf.CeilToInt(timer)}s";
                    _distanceText.color = Color.red;
                }
            }
            else
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1.0f;
                }

                if (_distanceText != null)
                {
                    _distanceText.text = $"{Mathf.RoundToInt(distance)}m";
                    _distanceText.color = Color.white;
                }
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }
    }
}
