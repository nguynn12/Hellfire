// Script: WeaponAmmoHUD.cs
// Mục đích: Hiển thị giao diện HUD đạn dược chi tiết cho từng loại súng (Tên vũ khí, đạn trong băng, đạn dự trữ, thanh nạp đạn, cảnh báo hết đạn).
// Hoạt động độc lập bằng GUI Vector, tương thích mọi cảnh (kể cả TestShrine và Multiplayer), tự động đồng bộ theo WeaponController.

using System;
using UnityEngine;
using Hellfire.Combat;
using Hellfire.Networking;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class WeaponAmmoHUD : MonoBehaviour
    {
        [Header("Tùy Chọn Vị Trí (Layout)")]
        [Tooltip("Khoảng cách lùi từ góc phải màn hình (pixels)")]
        [SerializeField] private float _marginRight = 25f;
        [Tooltip("Khoảng cách lùi từ đáy màn hình (pixels)")]
        [SerializeField] private float _marginBottom = 25f;
        [Tooltip("Chiều rộng bảng đạn (pixels)")]
        [SerializeField] private float _panelWidth = 230f;
        [Tooltip("Chiều cao bảng đạn (pixels)")]
        [SerializeField] private float _panelHeight = 90f;

        [Header("Màu Sắc & Giao Diện (Theme)")]
        [SerializeField] private Color _panelBgColor = new Color(0.06f, 0.08f, 0.12f, 0.78f);
        [SerializeField] private Color _panelBorderColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color _normalAmmoColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color _reserveAmmoColor = new Color(0.65f, 0.70f, 0.75f, 0.85f);
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.45f, 0.1f, 1f);
        [SerializeField] private Color _emptyAmmoColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _reloadBarBgColor = new Color(0.15f, 0.18f, 0.22f, 0.9f);
        [SerializeField] private Color _reloadBarFillColor = new Color(0.2f, 0.85f, 0.5f, 1f);

        // References
        private WeaponController _weaponController;
        private Texture2D _pixelTexture;
        private GUIStyle _nameStyle;
        private GUIStyle _currentAmmoStyle;
        private GUIStyle _reserveAmmoStyle;
        private GUIStyle _reloadLabelStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _badgeStyle;

        private void Awake()
        {
            _weaponController = GetComponent<WeaponController>();
            if (_weaponController == null) _weaponController = GetComponentInParent<WeaponController>();

            _pixelTexture = new Texture2D(1, 1);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
        }

        private void InitStyles()
        {
            if (_nameStyle != null) return;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.90f, 0.98f, 0.9f) }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.35f, 0.75f, 1f, 0.85f) }
            };

            _currentAmmoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _reserveAmmoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = _reserveAmmoColor }
            };

            _reloadLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.95f, 1f, 0.95f) }
            };

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 0.85f, 0.25f, 0.9f) }
            };
        }

        private void OnGUI()
        {
            // Chỉ hiển thị cho Local Player trong chế độ Netcode
            if (_weaponController != null && Unity.Netcode.NetworkManager.Singleton != null && 
                Unity.Netcode.NetworkManager.Singleton.IsListening && !_weaponController.IsOwner)
            {
                return;
            }

            // Chỉ hiển thị khi đang trong game (cursor lock hoặc có WeaponController)
            if (_weaponController == null) return;
            WeaponData weapon = _weaponController.CurrentWeaponData;
            if (weapon == null) return;

            InitStyles();

            // Tính toán khung Panel ở góc dưới phải màn hình
            float x = Screen.width - _panelWidth - _marginRight;
            float y = Screen.height - _panelHeight - _marginBottom;
            Rect panelRect = new Rect(x, y, _panelWidth, _panelHeight);

            // 1. Vẽ nền mờ và viền panel
            DrawRect(panelRect, _panelBgColor);
            DrawBorder(panelRect, 1.5f, _panelBorderColor);

            // 2. Dòng trên: Tên vũ khí & Huy hiệu chế độ bắn (FULL-AUTO / SEMI / ADS)
            string weaponName = weapon.WeaponName.ToUpper();
            GUI.Label(new Rect(x + 12, y + 8, _panelWidth - 85, 20), weaponName, _nameStyle);

            string badgeText = weapon.IsAutomatic ? "AUTO" : "SEMI";
            if (_weaponController.IsAiming) badgeText += " | AIM";
            GUI.Label(new Rect(x + _panelWidth - 80, y + 8, 68, 20), badgeText, _badgeStyle);

            // 3. Thông số đạn: Current / Reserve
            int currentAmmo = _weaponController.CurrentAmmo;
            int magSize = weapon.MagSize;
            int reserveAmmo = _weaponController.CurrentReserveAmmo;
            bool isInfinite = OfflineManager.Instance != null && OfflineManager.Instance.InfiniteAmmo;

            // Xác định màu sắc cảnh báo đạn
            Color ammoColor = _normalAmmoColor;
            if (currentAmmo <= 0)
            {
                ammoColor = _emptyAmmoColor;
            }
            else if (currentAmmo <= Mathf.CeilToInt(magSize * 0.25f))
            {
                // Nhấp nháy màu đỏ cam cảnh báo sắp hết đạn
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                ammoColor = Color.Lerp(_lowAmmoColor, _emptyAmmoColor, pulse);
            }

            _currentAmmoStyle.normal.textColor = ammoColor;

            string currentStr = currentAmmo.ToString();
            string reserveStr = isInfinite ? " / ∞" : $" / {reserveAmmo}";

            // Vẽ số đạn to
            GUI.Label(new Rect(x + 12, y + 26, 75, 40), currentStr, _currentAmmoStyle);

            // Vẽ số đạn dự trữ nhỏ hơn
            float currentTextWidth = _currentAmmoStyle.CalcSize(new GUIContent(currentStr)).x;
            GUI.Label(new Rect(x + 16 + currentTextWidth, y + 36, 120, 26), reserveStr, _reserveAmmoStyle);

            // 4. Trạng thái nạp đạn & Thanh tiến trình nạp
            bool isReloading = _weaponController.IsReloading;
            if (isReloading)
            {
                float progress = _weaponController.ReloadProgress;
                Rect barBg = new Rect(x + 12, y + _panelHeight - 18, _panelWidth - 24, 8);
                Rect barFill = new Rect(x + 12, y + _panelHeight - 18, (_panelWidth - 24) * progress, 8);

                DrawRect(barBg, _reloadBarBgColor);
                DrawRect(barFill, _reloadBarFillColor);

                string reloadText = $"NẠP ĐẠN... {Mathf.RoundToInt(progress * 100f)}%";
                GUI.Label(new Rect(x + 12, y + 34, _panelWidth - 24, 20), reloadText, _reloadLabelStyle);
            }
            else
            {
                // Nhắc người chơi bấm R nếu đạn chưa đầy và có đạn dự trữ
                if (currentAmmo < magSize && (reserveAmmo > 0 || isInfinite))
                {
                    GUI.Label(new Rect(x + _panelWidth - 95, y + 36, 83, 24), "[R] NẠP", _promptStyle);
                }
                else if (currentAmmo <= 0 && reserveAmmo <= 0 && !isInfinite)
                {
                    GUI.Label(new Rect(x + _panelWidth - 95, y + 36, 83, 24), "HẾT ĐẠN", _promptStyle);
                }
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (_pixelTexture == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixelTexture);
            GUI.color = old;
        }

        private void DrawBorder(Rect rect, float thickness, Color color)
        {
            // Viền trên
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Viền dưới
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            // Viền trái
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Viền phải
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
