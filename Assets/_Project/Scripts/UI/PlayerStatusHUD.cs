// Script: PlayerStatusHUD.cs
// Mục đích: Hiển thị giao diện Máu/Giáp (Health & Armor HUD) ở góc dưới bên trái màn hình,
//           và Bảng theo dõi toàn bộ chỉ số nhân vật/vũ khí khi giữ hoặc bấm phím [Tab].
// Môi trường thực thi: Client-only OnGUI độc lập, tự động gắn vào người chơi, hoạt động cả Offline lẫn Online.

using System;
using Hellfire.Combat;
using Hellfire.Player;
using Hellfire.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class PlayerStatusHUD : MonoBehaviour
    {
        // ================= KÍCH THƯỚC & VỊ TRÍ HUD MÁU =================
        private const float HudWidth = 350f;
        private const float HudHeight = 110f;
        private const float MarginLeft = 24f;
        private const float MarginBottom = 24f;

        // ================= MÀU SẮC THIẾT KẾ (DARK CYBER AESTHETICS) =================
        private readonly Color _panelBgColor = new Color(0.04f, 0.07f, 0.11f, 0.92f);
        private readonly Color _panelBorderColor = new Color(0.0f, 0.92f, 1.0f, 0.85f);
        private readonly Color _hpBarBgColor = new Color(0.02f, 0.03f, 0.05f, 0.98f);
        private readonly Color _hpHealthyColor = new Color(0.0f, 1.0f, 0.65f, 1.0f);      // Xanh ngọc lục bảo rực rỡ
        private readonly Color _hpWarningColor = new Color(1.0f, 0.80f, 0.15f, 1.0f);     // Vàng cam sáng
        private readonly Color _hpCriticalColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);    // Đỏ nguy cấp
        private readonly Color _armorColor = new Color(0.35f, 0.80f, 1.0f, 1.0f);         // Xanh dương giáp sáng
        private readonly Color _shieldGlowColor = new Color(1.0f, 0.88f, 0.25f, 1.0f);    // Vàng kim khiên bất tử

        // ================= THAM CHIẾU COMPONENT =================
        private Health _health;
        private PlayerStats _playerStats;
        private PlayerBuffManager _buffManager;
        private WeaponController _weaponController;
        private PlayerMovement _playerMovement;

        // ================= HIỆU ỨNG MƯỢT MÀ =================
        private float _smoothHealthPercent = 1.0f;
        private Texture2D _pixelTexture;

        // ================= TRẠNG THÁI BẢNG CHỈ SỐ TAB =================
        private bool _isTabToggled = false;
        private bool _tabWasPressedLastFrame = false;

        // GUI Styles
        private GUIStyle _hpNumberStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _subLabelStyle;
        private GUIStyle _tabTitleStyle;
        private GUIStyle _tabSectionHeaderStyle;
        private GUIStyle _tabItemNameStyle;
        private GUIStyle _tabItemValStyle;
        private GUIStyle _tabSlotHeaderStyle;
        private GUIStyle _tabHintStyle;
        private GUIStyle _crystalCountStyle;
        private bool _stylesInitialized = false;

        // ================= TÀI NGUYÊN ĐỒ HỌA CHUYÊN NGHIỆP TỪ RESOURCES =================
        private Texture2D _hpFrameTex;
        private Texture2D _hpLineTex;
        private Texture2D _crosshairTex;
        private Texture2D _frameBigTex;
        private Texture2D _nameBarTex;
        private Texture2D _redCrystalTex;
        private Texture2D _greenCrystalTex;

        private void Awake()
        {
            _pixelTexture = new Texture2D(1, 1);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();

            // Load tài nguyên thiết kế từ Resources theo đúng yêu cầu
            _hpFrameTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/Hp_frame");
            _hpLineTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/Hp_line");
            _frameBigTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/Frame_big");
            _nameBarTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/name_bar");
            _crosshairTex = Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_01");
            _redCrystalTex = Resources.Load<Texture2D>("GUI_Parts/RedCrystal");
            _greenCrystalTex = Resources.Load<Texture2D>("GUI_Parts/GreenCrystal");
        }

        private void Start()
        {
            ResolveComponents();
        }

        private void OnDestroy()
        {
            if (_pixelTexture != null)
            {
                Destroy(_pixelTexture);
            }
        }

        private bool _componentsResolved = false;

        private void ResolveComponents()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_playerStats == null) _playerStats = GetComponent<PlayerStats>();
            if (_buffManager == null) _buffManager = GetComponent<PlayerBuffManager>();
            if (_weaponController == null) _weaponController = GetComponent<WeaponController>();
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();

            if (_health != null && _playerStats != null && _weaponController != null)
            {
                _componentsResolved = true;
            }
        }

        private void Update()
        {
            if (!_componentsResolved)
            {
                ResolveComponents();
            }

            // Cập nhật phím [Tab]: Giữ để mở, hoặc bấm nhanh để bật/tắt (Toggle)
            if (Keyboard.current != null)
            {
                bool tabIsDown = Keyboard.current.tabKey.isPressed;
                bool tabJustPressed = Keyboard.current.tabKey.wasPressedThisFrame;

                if (tabJustPressed)
                {
                    _isTabToggled = !_isTabToggled;
                }

                _tabWasPressedLastFrame = tabIsDown;
            }

            // Lerp mượt mà cho thanh máu
            if (_health != null && _health.MaxHealthValue > 0f)
            {
                float targetPercent = Mathf.Clamp01(_health.CurrentHealthValue / _health.MaxHealthValue);
                _smoothHealthPercent = Mathf.MoveTowards(_smoothHealthPercent, targetPercent, Time.deltaTime * 2.5f);
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // Kích thước chữ to, đậm và tương phản cao để chống mờ nhạt
            _hpNumberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.35f, 0.88f, 1.0f, 1.0f) }
            };

            _subLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.98f, 1.0f, 1.0f) }
            };

            _tabTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 0.98f, 1.0f, 1.0f) }
            };

            _tabSectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1.0f, 0.88f, 0.35f, 1.0f) }
            };

            _tabItemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.9f, 0.95f, 1.0f, 1.0f) }
            };

            _tabItemValStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };

            _tabSlotHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.35f, 0.85f, 1.0f, 1.0f) }
            };

            _tabHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.88f, 0.98f, 1.0f) }
            };

            _crystalCountStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _stylesInitialized = true;
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

            InitStyles();

            // 1. VẼ GIAO DIỆN MÁU & GIÁP (GÓC DƯỚI BÊN TRÁI)
            DrawHealthHUD();

            // 2. VẼ THANH PHA LÊ / VẬT PHẨM NÂNG CẤP
            DrawCrystalsHUD();

            // 3. VẼ BẢNG CHỈ SỐ NHÂN VẬT & SÚNG KHI GIỮ HOẶC TOGGLE [TAB]
            bool isTabOpen = (Keyboard.current != null && Keyboard.current.tabKey.isPressed) || _isTabToggled;
            if (isTabOpen)
            {
                DrawStatsTabOverlay();
            }
        }

        // =================================================================================
        // 1. VẼ GIAO DIỆN MÁU & GIÁP (HEALTH & ARMOR HUD)
        // =================================================================================
        private void DrawHealthHUD()
        {
            float x = MarginLeft;
            float y = Screen.height - HudHeight - MarginBottom;
            Rect panelRect = new Rect(x, y, HudWidth, HudHeight);

            // Nền đen mờ & viền Cyan cao cấp
            DrawRect(panelRect, _panelBgColor);
            DrawBorder(panelRect, 1.5f, _panelBorderColor);

            float currentHp = _health != null ? _health.CurrentHealthValue : 100f;
            float maxHp = _health != null ? _health.MaxHealthValue : 100f;
            float hpRatio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;

            // Xác định màu sắc máu & hiệu ứng nhấp nháy khi nguy cấp
            Color hpColor = _hpHealthyColor;
            if (hpRatio <= 0.25f)
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                hpColor = Color.Lerp(_hpCriticalColor, new Color(1f, 0.5f, 0.5f), pulse);
            }
            else if (hpRatio <= 0.5f)
            {
                hpColor = _hpWarningColor;
            }

            // Dòng 1: Icon HP + Số máu hiện tại / Số máu tối đa
            string hpString = $"{Mathf.CeilToInt(currentHp)}";
            string maxHpString = $" / {Mathf.CeilToInt(maxHp)} HP";

            DrawShadowLabel(new Rect(x + 14, y + 6, 95, 36), hpString, _hpNumberStyle, hpColor);
            float hpTextWidth = _hpNumberStyle.CalcSize(new GUIContent(hpString)).x;
            DrawShadowLabel(new Rect(x + 18 + hpTextWidth, y + 14, 150, 24), maxHpString, _subLabelStyle, new Color(0.95f, 0.98f, 1f, 1f));

            // Trạng thái Khiên Thần (Guardian Shield - Bất tử) nếu đang có
            bool hasShield = _buffManager != null && _buffManager.HasGuardianShield;
            if (hasShield)
            {
                float shieldTime = _buffManager.ShieldTimeRemaining;
                DrawShadowLabel(new Rect(x + HudWidth - 145, y + 12, 135, 24), $"🛡️ BẤT TỬ ({shieldTime:F1}s)", _labelStyle, _shieldGlowColor);
            }
            else if (_health != null && _health.IsDownedValue)
            {
                DrawShadowLabel(new Rect(x + HudWidth - 145, y + 12, 135, 24), "⚠️ ĐANG GỤC!", _labelStyle, _hpCriticalColor);
            }

            // Dòng 2: Thanh máu (Health Bar) sử dụng Texture Hp_frame và Hp_line chuyên nghiệp
            float barX = x + 14;
            float barY = y + 44;
            float barWidth = HudWidth - 28;
            float barHeight = 18f;

            Rect barBgRect = new Rect(barX, barY, barWidth, barHeight);

            if (_hpFrameTex != null && _hpLineTex != null)
            {
                // Nền đen bên dưới thanh máu
                DrawRect(barBgRect, new Color(0.02f, 0.03f, 0.05f, 0.95f));

                // 1. Vẽ vạch máu Hp_line theo tỷ lệ máu
                if (hpRatio > 0.001f)
                {
                    Color prevColor = GUI.color;
                    GUI.color = hpColor;
                    GUI.DrawTextureWithTexCoords(
                        new Rect(barX + 2, barY + 1, (barWidth - 4) * hpRatio, barHeight - 2),
                        _hpLineTex,
                        new Rect(0f, 0f, hpRatio, 1f)
                    );
                    GUI.color = prevColor;
                }

                // 2. Vẽ viền khung Hp_frame đè lên trên tạo chiều sâu 3D sắc nét
                GUI.DrawTexture(barBgRect, _hpFrameTex, ScaleMode.StretchToFill);
            }
            else
            {
                // Fallback nếu chưa load texture
                DrawRect(barBgRect, _hpBarBgColor);
                Rect fillRect = new Rect(barX, barY, barWidth * hpRatio, barHeight);
                DrawRect(fillRect, hpColor);
                DrawBorder(barBgRect, 1f, new Color(0.2f, 0.3f, 0.4f, 0.6f));
            }

            // Dòng 3: Chỉ số Giáp (Armor) & % Giảm sát thương
            float armorVal = _playerStats != null ? _playerStats.CurrentArmor : (_health != null ? _health.BaseArmor : 0f);
            float dmgReductionPercent = 0f;
            if (armorVal > 0f)
            {
                // Công thức chuẩn game: DamageFactor = e^(-0.01053605 * Armor)
                dmgReductionPercent = (1.0f - Mathf.Exp(-0.01053605f * armorVal)) * 100f;
            }

            string armorText = $"🛡️ GIÁP: {Mathf.RoundToInt(armorVal)}/60";
            if (dmgReductionPercent > 0.1f)
            {
                armorText += $" (-{dmgReductionPercent:F0}% Dmg)";
            }
            DrawShadowLabel(new Rect(barX, y + 74, 225, 24), armorText, _labelStyle, _armorColor);

            // Mini Bar Giáp phía dưới thanh máu
            float armorRatio = Mathf.Clamp01(armorVal / PlayerStats.MaxArmorCap);
            Rect armorBarBg = new Rect(x + HudWidth - 105, y + 80, 90, 8);
            DrawRect(armorBarBg, _hpBarBgColor);
            Rect armorBarFill = new Rect(x + HudWidth - 105, y + 80, 90 * armorRatio, 8);
            DrawRect(armorBarFill, _armorColor);
            DrawBorder(armorBarBg, 1f, new Color(0.2f, 0.5f, 0.8f, 0.8f));
        }

        // =================================================================================
        // 2. VẼ THANH PHA LÊ / VẬT PHẨM NÂNG CẤP (CRYSTAL TOKENS) - PHÓNG TO ĐẦY ĐẶN
        // =================================================================================
        private void DrawCrystalsHUD()
        {
            if (_playerStats == null) return;

            float x = MarginLeft;
            float barW = HudWidth; // Khớp chuẩn 100% bằng chiều rộng khung máu
            float barH = 44f;      // To, dày dặn, nổi bật
            float y = Screen.height - HudHeight - MarginBottom - barH - 8f;
            Rect bgRect = new Rect(x, y, barW, barH);

            DrawRect(bgRect, _panelBgColor);
            DrawBorder(bgRect, 1.5f, _panelBorderColor);

            // 1. Pha Lê Đỏ (Tế Đàn Thân Thể) - Nửa bên trái
            float redX = x + 16f;
            if (_redCrystalTex != null)
            {
                GUI.DrawTexture(new Rect(redX, y + 6f, 26f, 32f), _redCrystalTex, ScaleMode.ScaleToFit);
            }
            int redCount = _playerStats.CurrentRedCrystals;
            _crystalCountStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);
            DrawShadowLabel(new Rect(redX + 32f, y + 5f, 85f, 34f), $"x{redCount}", _crystalCountStyle, new Color(1.0f, 0.35f, 0.35f));

            // Vạch phân chia tinh tế ở giữa thanh
            DrawRect(new Rect(x + barW * 0.5f, y + 8f, 1.5f, barH - 16f), new Color(0.15f, 0.55f, 0.75f, 0.6f));

            // 2. Pha Lê Xanh (Bàn Cường Hóa Vũ Khí) - Nửa bên phải
            float greenX = x + barW * 0.5f + 20f;
            if (_greenCrystalTex != null)
            {
                GUI.DrawTexture(new Rect(greenX, y + 6f, 26f, 32f), _greenCrystalTex, ScaleMode.ScaleToFit);
            }
            int greenCount = _playerStats.CurrentGreenCrystals;
            _crystalCountStyle.normal.textColor = new Color(0.25f, 1.0f, 0.45f);
            DrawShadowLabel(new Rect(greenX + 32f, y + 5f, 85f, 34f), $"x{greenCount}", _crystalCountStyle, new Color(0.25f, 1.0f, 0.45f));
        }

        // =================================================================================
        // 3. VẼ BẢNG SOI CHỈ SỐ NHÂN VẬT & CHIẾN ĐẤU (TAB STATS OVERLAY)
        // =================================================================================
        private void DrawStatsTabOverlay()
        {
            // Lớp phủ mờ toàn màn hình
            DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.04f, 0.08f, 0.75f));

            float tabWidth = 860f;
            float tabHeight = 540f;
            float tabX = (Screen.width - tabWidth) * 0.5f;
            float tabY = (Screen.height - tabHeight) * 0.5f;
            Rect panelRect = new Rect(tabX, tabY, tabWidth, tabHeight);

            // Nền hộp & viền Frame_big từ Resources
            if (_frameBigTex != null)
            {
                DrawRect(panelRect, new Color(0.05f, 0.08f, 0.12f, 0.95f));
                GUI.DrawTexture(panelRect, _frameBigTex, ScaleMode.StretchToFill);
            }
            else
            {
                DrawRect(panelRect, new Color(0.06f, 0.09f, 0.14f, 0.96f));
                DrawBorder(panelRect, 2f, new Color(0.0f, 0.88f, 1.0f, 0.85f));
            }

            // Header Tiêu đề bằng name_bar Texture
            if (_nameBarTex != null)
            {
                GUI.DrawTexture(new Rect(tabX + 40, tabY + 6, tabWidth - 80, 48), _nameBarTex, ScaleMode.StretchToFill);
                GUI.Label(new Rect(tabX, tabY + 12, tabWidth, 32), "BẢNG CHỈ SỐ NHÂN VẬT & TRANG BỊ CHIẾN ĐẤU [TAB]", _tabTitleStyle);
            }
            else
            {
                DrawRect(new Rect(tabX, tabY, tabWidth, 44), new Color(0.03f, 0.06f, 0.1f, 0.95f));
                GUI.Label(new Rect(tabX, tabY + 8, tabWidth, 28), "BẢNG CHỈ SỐ NHÂN VẬT & TRANG BỊ CHIẾN ĐẤU [TAB]", _tabTitleStyle);
                DrawRect(new Rect(tabX, tabY + 44, tabWidth, 2), new Color(0.0f, 0.88f, 1.0f, 0.5f));
            }

            float colWidth = (tabWidth - 60f) * 0.5f;
            float col1X = tabX + 20f;
            float col2X = tabX + 40f + colWidth;
            float contentY = tabY + 54f;

            // ================= CỘT TRÁI: CHỈ SỐ SINH TỒN & THÂN THỂ =================
            DrawRect(new Rect(col1X, contentY, colWidth, 290), new Color(0.04f, 0.07f, 0.11f, 0.6f));
            DrawBorder(new Rect(col1X, contentY, colWidth, 290), 1f, new Color(0.2f, 0.35f, 0.5f, 0.4f));

            GUI.Label(new Rect(col1X + 14, contentY + 10, colWidth - 28, 22), "✦ CHỈ SỐ THÂN THỂ (SURVIVAL)", _tabSectionHeaderStyle);
            DrawRect(new Rect(col1X + 14, contentY + 34, colWidth - 28, 1), new Color(0.3f, 0.45f, 0.6f, 0.4f));

            float rowY = contentY + 42f;
            float rowSpacing = 26f;

            float maxHp = _health != null ? _health.MaxHealthValue : 100f;
            float curHp = _health != null ? _health.CurrentHealthValue : 100f;
            float bonusHp = _playerStats != null ? _playerStats.CurrentBonusMaxHealth : 0f;
            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Máu hiện tại / Tối đa", $"{curHp:F0} / {maxHp:F0}", _hpHealthyColor);
            rowY += rowSpacing;

            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Máu cộng thêm (Tế đàn)", $"+{bonusHp:F0} (Trần 150)", new Color(0.3f, 0.9f, 0.5f));
            rowY += rowSpacing;

            float armor = _playerStats != null ? _playerStats.CurrentArmor : 0f;
            float dmgRed = armor > 0 ? (1.0f - Mathf.Exp(-0.01053605f * armor)) * 100f : 0f;
            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Chỉ số Giáp bảo vệ", $"{armor:F0} / 60", _armorColor);
            rowY += rowSpacing;

            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Giảm sát thương nhận vào", $"-{dmgRed:F1}%", new Color(0.4f, 0.8f, 1f));
            rowY += rowSpacing;

            float speedMult = _playerStats != null ? _playerStats.CurrentMoveSpeedMultiplier : 1.0f;
            if (_buffManager != null) speedMult *= _buffManager.SpeedMultiplier;
            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Tốc độ di chuyển", $"{speedMult * 100f:F0}% (Cơ bản 100%)", new Color(0.95f, 0.85f, 0.3f));
            rowY += rowSpacing;

            string statusText = "Bình Thường";
            Color statusColor = Color.white;
            if (_buffManager != null && _buffManager.HasGuardianShield)
            {
                statusText = $"KHIÊN BẤT TỬ ({_buffManager.ShieldTimeRemaining:F1}s)";
                statusColor = _shieldGlowColor;
            }
            else if (_health != null && _health.IsDownedValue)
            {
                statusText = "GỤC (CẦN CỨU)";
                statusColor = _hpCriticalColor;
            }
            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Trạng thái bảo hộ", statusText, statusColor);
            rowY += rowSpacing;

            float buffDmg = _buffManager != null ? _buffManager.DamageMultiplier : 1.0f;
            DrawStatRow(col1X + 14, rowY, colWidth - 28, "Bùa Cuồng Nộ (Damage Mult)", buffDmg > 1.05f ? $"x{buffDmg:F1} SÁT THƯƠNG" : "Không có", buffDmg > 1.05f ? new Color(1f, 0.4f, 0.1f) : Color.gray);

            // ================= CỘT PHẢI: CHỈ SỐ VŨ KHÍ ĐANG TRANG BỊ =================
            DrawRect(new Rect(col2X, contentY, colWidth, 290), new Color(0.04f, 0.07f, 0.11f, 0.6f));
            DrawBorder(new Rect(col2X, contentY, colWidth, 290), 1f, new Color(0.2f, 0.35f, 0.5f, 0.4f));

            WeaponData currentWeapon = _weaponController != null ? _weaponController.CurrentWeaponData : null;
            RuntimeWeapon rw = _weaponController != null ? _weaponController.CurrentRuntimeWeapon : null;
            string weaponTitle = currentWeapon != null ? $"{currentWeapon.WeaponName.ToUpper()}" : "CHƯA TRANG BỊ";
            int starLevel = rw != null ? rw.UpgradeLevel : 0;
            string stars = starLevel > 0 ? $" ({starLevel}★)" : " (1★)";

            GUI.Label(new Rect(col2X + 14, contentY + 10, colWidth - 28, 22), $"✦ SÚNG TRÊN TAY: {weaponTitle}{stars}", _tabSectionHeaderStyle);
            DrawRect(new Rect(col2X + 14, contentY + 34, colWidth - 28, 1), new Color(0.3f, 0.45f, 0.6f, 0.4f));

            float row2Y = contentY + 42f;

            float finalDmg = _weaponController != null ? _weaponController.CurrentDamage : (currentWeapon != null ? currentWeapon.BaseDamage : 0);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Sát thương mỗi viên (Damage)", $"{finalDmg:F1}", new Color(1.0f, 0.4f, 0.3f));
            row2Y += rowSpacing;

            float finalAPS = _weaponController != null ? _weaponController.CurrentAPS : (currentWeapon != null ? currentWeapon.AttackRateAPS : 0);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Tốc độ bắn (Tấn công/giây)", $"{finalAPS:F1} viên/s", new Color(0.3f, 0.85f, 1.0f));
            row2Y += rowSpacing;

            float finalArmorPen = _weaponController != null ? _weaponController.CurrentArmorPen : (currentWeapon != null ? currentWeapon.ArmorPenetration : 0);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Xuyên giáp (Armor Penetration)", $"+{finalArmorPen:F0}", new Color(0.8f, 0.5f, 1.0f));
            row2Y += rowSpacing;

            float critChance = _weaponController != null ? _weaponController.CurrentCritChance : (currentWeapon != null ? currentWeapon.CritChance : 0);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Tỉ lệ chí mạng (Crit Chance)", $"{critChance * 100f:F1}%", new Color(1.0f, 0.85f, 0.2f));
            row2Y += rowSpacing;

            float critMult = _weaponController != null ? _weaponController.CurrentCritDamageMultiplier : (currentWeapon != null ? currentWeapon.CritDamageMultiplier : 1.5f);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Sát thương chí mạng (Crit Mult)", $"x{critMult:F2}", new Color(1.0f, 0.65f, 0.1f));
            row2Y += rowSpacing;

            int curAmmo = _weaponController != null ? _weaponController.CurrentAmmo : 0;
            int magSize = _weaponController != null ? _weaponController.CurrentMagSize : 0;
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Băng đạn / Thay đạn vô hạn", $"{curAmmo} / {magSize} (∞)", _hpHealthyColor);
            row2Y += rowSpacing;

            float reloadTime = _weaponController != null ? _weaponController.EffectiveReloadTime : (currentWeapon != null ? currentWeapon.ReloadTime : 1.2f);
            DrawStatRow(col2X + 14, row2Y, colWidth - 28, "Thời gian thay đạn (Reload)", $"{reloadTime:F2} giây", new Color(0.75f, 0.85f, 0.95f));

            // ================= KHU VỰC DƯỚI: TÚI ĐỒ 2 SLOT VŨ KHÍ =================
            float bottomY = contentY + 300f;
            float bottomHeight = 115f;
            DrawRect(new Rect(col1X, bottomY, tabWidth - 40f, bottomHeight), new Color(0.04f, 0.07f, 0.11f, 0.6f));
            DrawBorder(new Rect(col1X, bottomY, tabWidth - 40f, bottomHeight), 1f, new Color(0.2f, 0.35f, 0.5f, 0.4f));

            GUI.Label(new Rect(col1X + 14, bottomY + 8, tabWidth - 68, 20), "✦ TÚI ĐỒ VŨ KHÍ 2 SLOT (QUICK LOADOUT)", _tabSlotHeaderStyle);

            var weapons = _weaponController != null ? _weaponController.AvailableWeapons : null;
            int activeIdx = _weaponController != null ? _weaponController.CurrentWeaponIndex : 0;

            float slotWidth = (tabWidth - 70f) * 0.5f;

            // Slot 1
            Rect slot1Rect = new Rect(col1X + 14, bottomY + 32, slotWidth, 42);
            DrawSlotCard(slot1Rect, 0, weapons != null && weapons.Count > 0 ? weapons[0] : null, activeIdx == 0);

            // Slot 2
            Rect slot2Rect = new Rect(col1X + 28 + slotWidth, bottomY + 32, slotWidth, 42);
            DrawSlotCard(slot2Rect, 1, weapons != null && weapons.Count > 1 ? weapons[1] : null, activeIdx == 1);

            // Dòng hướng dẫn thao tác phím
            DrawShadowLabel(new Rect(col1X + 14, bottomY + 82, tabWidth - 68, 24), 
                "Phím tắt: [1] / [2] hoặc Cuộn chuột: Đổi súng | [R]: Nạp đạn vô hạn | [Chuột Phải]: Ngắm bắn (ADS) | [Tab]: Đóng/Mở bảng", 
                _tabHintStyle, new Color(0.85f, 0.92f, 1.0f, 1.0f));
        }

        private void DrawSlotCard(Rect rect, int slotIndex, WeaponData weapon, bool isActive)
        {
            Color bgColor = isActive ? new Color(0.0f, 0.40f, 0.65f, 0.75f) : new Color(0.06f, 0.08f, 0.12f, 0.75f);
            Color borderColor = isActive ? new Color(0.0f, 0.98f, 1.0f, 0.95f) : new Color(0.35f, 0.45f, 0.55f, 0.6f);

            DrawRect(rect, bgColor);
            DrawBorder(rect, isActive ? 2f : 1f, borderColor);

            string slotTag = $"[SLOT {slotIndex + 1}]";
            string weaponTitle = weapon != null ? weapon.WeaponName : "Trống (Nhặt thêm súng)";
            string statusTag = isActive ? "◀ ĐANG CẦM" : $"Bấm [{slotIndex + 1}]";

            DrawShadowLabel(new Rect(rect.x + 10, rect.y + 10, 75, 24), slotTag, _tabItemNameStyle, new Color(0.4f, 0.85f, 1f));
            DrawShadowLabel(new Rect(rect.x + 80, rect.y + 10, rect.width - 175, 24), weaponTitle, _tabItemValStyle, Color.white);
            DrawShadowLabel(new Rect(rect.x + rect.width - 95, rect.y + 10, 85, 24), statusTag, _subLabelStyle, isActive ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.8f, 0.9f));
        }

        private void DrawStatRow(float x, float y, float width, string name, string value, Color valColor)
        {
            DrawShadowLabel(new Rect(x, y, width - 120, 22), name, _tabItemNameStyle, new Color(0.9f, 0.95f, 1.0f, 1f));
            DrawShadowLabel(new Rect(x + width - 140, y, 140, 22), value, _tabItemValStyle, valColor);
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
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
