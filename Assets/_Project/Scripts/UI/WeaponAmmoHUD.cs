// Script: WeaponAmmoHUD.cs
// Mục đích: Hiển thị giao diện HUD đạn dược chi tiết cho từng loại súng (Tên vũ khí, đạn trong băng, đạn dự trữ, thanh nạp đạn, cảnh báo hết đạn).
// Hoạt động độc lập bằng GUI Vector, tương thích mọi cảnh (kể cả TestShrine và Multiplayer), tự động đồng bộ theo WeaponController.

using System;
using UnityEngine;
using Hellfire.Combat;
using Hellfire.Networking;
using Hellfire.Player;

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
        [SerializeField] private float _panelWidth = 280f;
        [Tooltip("Chiều cao bảng đạn (pixels)")]
        [SerializeField] private float _panelHeight = 100f;

        [Header("Màu Sắc & Giao Diện (Theme)")]
        [SerializeField] private Color _panelBgColor = new Color(0.04f, 0.07f, 0.11f, 0.92f);
        [SerializeField] private Color _panelBorderColor = new Color(0.0f, 0.85f, 1f, 0.75f);
        [SerializeField] private Color _normalAmmoColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _reserveAmmoColor = new Color(0.85f, 0.92f, 1.0f, 1f);
        [SerializeField] private Color _lowAmmoColor = new Color(1f, 0.65f, 0.1f, 1f);
        [SerializeField] private Color _emptyAmmoColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _reloadBarBgColor = new Color(0.12f, 0.16f, 0.22f, 0.95f);
        [SerializeField] private Color _reloadBarFillColor = new Color(0.0f, 0.95f, 0.65f, 1f);

        // References
        private WeaponController _weaponController;
        private Texture2D _pixelTexture;
        private GUIStyle _nameStyle;
        private GUIStyle _currentAmmoStyle;
        private GUIStyle _reserveAmmoStyle;
        private GUIStyle _reloadLabelStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _slotStyle;

        private void Awake()
        {
            ResolveWeaponController();

            _pixelTexture = new Texture2D(1, 1);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
        }

        private void Start()
        {
            ResolveWeaponController();
        }

        private void Update()
        {
            if (_weaponController == null)
            {
                ResolveWeaponController();
            }
        }

        private void OnDestroy()
        {
            if (_pixelTexture != null)
            {
                Destroy(_pixelTexture);
            }
        }

        private void ResolveWeaponController()
        {
            if (_weaponController != null) return;

            _weaponController = GetComponent<WeaponController>();
            if (_weaponController != null) return;

            _weaponController = GetComponentInParent<WeaponController>();
            if (_weaponController != null) return;

            if (PlayerMovement.LocalInstance != null)
            {
                _weaponController = PlayerMovement.LocalInstance.GetComponent<WeaponController>();
                if (_weaponController != null) return;
            }

            var allControllers = FindObjectsByType<WeaponController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allControllers.Length; i++)
            {
                var wc = allControllers[i];
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    if (wc.IsOwner)
                    {
                        _weaponController = wc;
                        return;
                    }
                }
                else
                {
                    _weaponController = wc;
                    return;
                }
            }
        }

        private void InitStyles()
        {
            if (_nameStyle != null) return;

            _slotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
                normal = { textColor = new Color(0.2f, 0.95f, 1f, 1f) }
            };

            _currentAmmoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };

            _reserveAmmoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft,
                wordWrap = false,
                normal = { textColor = _reserveAmmoColor }
            };

            _reloadLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.85f, 0.2f, 1f) }
            };

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.85f, 0.2f, 1f) }
            };
        }

        /// <summary>
        /// Vẽ nhãn văn bản có bóng đổ nhẹ 1px giúp chữ nổi khối và sắc nét, tiết kiệm tối đa Draw Calls cho CPU.
        /// </summary>
        private void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color textColor, Color? shadowColor = null)
        {
            Color sc = shadowColor ?? new Color(0f, 0f, 0f, 0.95f);
            Color origColor = style.normal.textColor;

            style.normal.textColor = sc;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
            style.normal.textColor = origColor;
        }

        private void OnGUI()
        {
            // Chỉ hiển thị cho Local Player trong chế độ Netcode
            if (_weaponController != null && Unity.Netcode.NetworkManager.Singleton != null && 
                Unity.Netcode.NetworkManager.Singleton.IsListening && !_weaponController.IsOwner)
            {
                return;
            }

            if (_weaponController == null)
            {
                ResolveWeaponController();
                if (_weaponController == null) return;
            }
            WeaponData weapon = _weaponController.CurrentWeaponData;
            if (weapon == null) return;

            InitStyles();

            // Tính toán khung Panel ở góc dưới phải màn hình
            float x = Screen.width - _panelWidth - _marginRight;
            float y = Screen.height - _panelHeight - _marginBottom;
            Rect panelRect = new Rect(x, y, _panelWidth, _panelHeight);

            // 0. Vẽ thanh 2 Slot vũ khí (Slot 1 & Slot 2) ngay phía trên panel đạn
            var available = _weaponController.AvailableWeapons;
            float slotBarY = y - 32f;
            float slotW = (_panelWidth - 6f) / 2f;
            float slotH = 27f;

            for (int i = 0; i < 2; i++)
            {
                float sx = x + i * (slotW + 6f);
                Rect sRect = new Rect(sx, slotBarY, slotW, slotH);
                bool isActive = (i == _weaponController.CurrentWeaponIndex);

                Color bg = isActive ? new Color(0.0f, 0.45f, 0.75f, 0.95f) : new Color(0.06f, 0.08f, 0.12f, 0.88f);
                Color border = isActive ? new Color(0.0f, 0.95f, 1f, 1f) : new Color(0.35f, 0.45f, 0.55f, 0.6f);
                DrawRect(sRect, bg);
                DrawBorder(sRect, isActive ? 2f : 1f, border);

                string slotText;
                if (available != null && i < available.Count && available[i] != null)
                {
                    string fullName = available[i].WeaponName.ToUpper();
                    // Loại bỏ phần phụ trong ngoặc đơn để tên vừa vặn trong 1 dòng không bao giờ bị đè chữ
                    int parenIdx = fullName.IndexOf('(');
                    string displayName = parenIdx > 0 ? fullName.Substring(0, parenIdx).Trim() : fullName;
                    slotText = $"[{i + 1}] {displayName}";
                }
                else
                {
                    slotText = $"[{i + 1}] TRỐNG";
                }

                Color textCol = isActive ? Color.white : new Color(0.75f, 0.82f, 0.9f, 0.9f);
                DrawShadowLabel(new Rect(sx + 2, slotBarY + 3, slotW - 4, slotH - 4), slotText, _slotStyle, textCol);
            }

            // 1. Vẽ nền mờ và viền panel
            DrawRect(panelRect, _panelBgColor);
            DrawBorder(panelRect, 1.5f, _panelBorderColor);

            // 2. Dòng trên: Tên vũ khí & Huy hiệu chế độ bắn (FULL-AUTO / SEMI / ADS)
            string weaponName = weapon.WeaponName.ToUpper();
            DrawShadowLabel(new Rect(x + 14, y + 8, _panelWidth - 95, 24), weaponName, _nameStyle, Color.white);

            string badgeText = weapon.IsAutomatic ? "AUTO" : "SEMI";
            if (_weaponController.IsAiming) badgeText += " | AIM";
            DrawShadowLabel(new Rect(x + _panelWidth - 85, y + 8, 72, 24), badgeText, _badgeStyle, new Color(0.2f, 0.95f, 1f));

            // 3. Thông số đạn: Số đạn hiện tại trong súng / Số đạn tối đa súng có thể chứa
            int currentAmmo = _weaponController.CurrentAmmo;
            int maxMag = _weaponController.CurrentMagSize;

            Color ammoColor = _normalAmmoColor;
            if (currentAmmo <= 0)
            {
                ammoColor = _emptyAmmoColor;
            }
            else if (currentAmmo <= Mathf.CeilToInt(maxMag * 0.25f))
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                ammoColor = Color.Lerp(_lowAmmoColor, _emptyAmmoColor, pulse);
            }

            string currentStr = currentAmmo.ToString();
            string maxMagStr = $" / {maxMag}";

            // Vẽ số đạn trong súng sắc nét
            DrawShadowLabel(new Rect(x + 14, y + 30, 80, 46), currentStr, _currentAmmoStyle, ammoColor);

            // Vẽ số đạn tối đa súng có thể chứa + biểu tượng thay đạn vô hạn (∞)
            float currentTextWidth = _currentAmmoStyle.CalcSize(new GUIContent(currentStr)).x;
            DrawShadowLabel(new Rect(x + 18 + currentTextWidth, y + 42, 110, 30), $"{maxMagStr} (∞)", _reserveAmmoStyle, new Color(0.85f, 0.92f, 1f));

            // 4. Trạng thái nạp đạn & Thanh tiến trình nạp
            bool isReloading = _weaponController.IsReloading;
            if (isReloading)
            {
                float progress = _weaponController.ReloadProgress;
                Rect barBg = new Rect(x + 14, y + _panelHeight - 16, _panelWidth - 28, 8);
                Rect barFill = new Rect(x + 14, y + _panelHeight - 16, (_panelWidth - 28) * progress, 8);

                DrawRect(barBg, _reloadBarBgColor);
                DrawRect(barFill, _reloadBarFillColor);
                DrawBorder(barBg, 1f, new Color(0.0f, 0.85f, 1f, 0.7f));

                // Đặt dòng chữ nạp đạn ở bên phải, hoàn toàn không đè lên số đạn bên trái
                string reloadText = $"NẠP ĐẠN {Mathf.RoundToInt(progress * 100f)}%";
                DrawShadowLabel(new Rect(x + _panelWidth - 145, y + 40, 135, 26), reloadText, _reloadLabelStyle, new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                // Nhắc người chơi bấm R nếu đạn chưa đầy
                if (currentAmmo < maxMag)
                {
                    DrawShadowLabel(new Rect(x + _panelWidth - 105, y + 40, 95, 26), "[R] NẠP", _promptStyle, new Color(1f, 0.85f, 0.2f));
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
