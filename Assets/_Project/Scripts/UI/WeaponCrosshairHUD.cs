// Script: WeaponCrosshairHUD.cs
// Mục đích: Hệ thống tâm ngắm (Crosshair) tùy biến cao cho từng loại súng (Pistol, Shotgun, Rifle, SMG).
// Hỗ trợ: Đổi kiểu tâm tự động theo vũ khí, nở tâm động (Dynamic Bloom) khi bắn/di chuyển, 
//         hiệu ứng Hitmarker khi bắn trúng quái, và cho phép tùy chỉnh màu sắc, độ dày, kích thước qua Inspector.

using System;
using UnityEngine;
using Hellfire.Combat;
using Hellfire.Player;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class WeaponCrosshairHUD : MonoBehaviour
    {
        public enum CrosshairRenderMode
        {
            ClassicVectorCrosshair,   // Tâm chữ thập eSports thanh mảnh đối xứng 1:1 tuyệt đối, viền đen chống mờ (Chuẩn FPS)
            TextureCrosshair          // Tâm hình ảnh từ Assets/Resources/Crosshairs
        }

        public enum CrosshairShape
        {
            AutoByWeapon,    // Tự động chọn theo vũ khí đang cầm
            ClassicCross,    // Tâm chữ thập 4 cạnh kinh điển (Pistol / Rifle)
            ShotgunCircle,   // Tâm vòng tròn tán xạ (Shotgun)
            DotOnly,         // Chỉ 1 chấm tròn tâm (Gọn gàng)
            TStyle,          // Tâm chữ T (Bỏ thanh trên đỉnh)
            BracketStyle     // Tâm dấu ngoặc [ ] (SMG)
        }

        [Header("Chế Độ Render Tâm Ngắm")]
        [Tooltip("ClassicVector: Tâm chữ thập cân đối 1:1 hoàn hảo, có viền đen sắc nét, không bao giờ bị dãn méo. Texture: Dùng hình từ Resources.")]
        [SerializeField] private CrosshairRenderMode _renderMode = CrosshairRenderMode.ClassicVectorCrosshair;

        [Header("Tùy Chọn Kiểu Dáng (Shape)")]
        [Tooltip("Kiểu dáng tâm ngắm. Chọn AutoByWeapon để tự đổi theo súng đang cầm.")]
        [SerializeField] private CrosshairShape _shape = CrosshairShape.AutoByWeapon;

        [Header("Màu Sắc & Độ Đậm (Colors)")]
        [Tooltip("Màu sắc tâm ngắm bình thường")]
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.95f);
        [Tooltip("Màu khi bắn trúng thân quái (Torso Hit)")]
        [SerializeField] private Color _hitBodyColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Màu khi bắn trúng đầu chí mạng (Headshot Hit)")]
        [SerializeField] private Color _hitHeadColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Kích Thước & Độ Rộng (Geometry)")]
        [Tooltip("Độ dài của các vạch ngắm (pixels)")]
        [SerializeField] private float _lineLength = 9f;
        [Tooltip("Độ dày của nét vẽ (pixels)")]
        [SerializeField] private float _lineThickness = 2f;
        [Tooltip("Khoảng cách từ tâm đến các vạch khi đứng yên (pixels)")]
        [SerializeField] private float _baseGap = 5f;
        [Tooltip("Hiển thị chấm tròn ở chính giữa tâm")]
        [SerializeField] private bool _showCenterDot = true;
        [Tooltip("Kích thước chấm tròn ở giữa (pixels)")]
        [SerializeField] private float _centerDotSize = 2f;

        [Header("Hiệu Ứng Nở Tâm Động (Dynamic Bloom)")]
        [Tooltip("Bật nở tâm khi bắn hoặc di chuyển")]
        [SerializeField] private bool _enableDynamicBloom = true;
        [Tooltip("Tốc độ thu hồi tâm về trạng thái nghỉ")]
        [SerializeField] private float _bloomRecoverySpeed = 15f;

        [Header("Hitmarker Phản Hồi Bắn Trúng")]
        [Tooltip("Thời gian nháy Hitmarker khi bắn trúng (giây)")]
        [SerializeField] private float _hitmarkerDuration = 0.12f;
        [Tooltip("Kích thước vạch chéo Hitmarker (pixels)")]
        [SerializeField] private float _hitmarkerSize = 8f;

        [Header("Tài Nguyên Texture Crosshair (Từ Resources/Crosshairs)")]
        [Tooltip("Gán đè texture thủ công nếu muốn, nếu để trống sẽ tự động lấy từ Resources/Crosshairs")]
        [SerializeField] private Texture2D _customCrosshairTexture;
        [Tooltip("Kích thước cơ sở của crosshair texture (pixels)")]
        [SerializeField] private float _baseCrosshairSize = 26f;

        // References
        private WeaponController _weaponController;
        private PlayerMovement _playerMovement;

        // Textures từ Assets/Resources/Crosshairs/Art/Textures/
        private Texture2D _texPistol;
        private Texture2D _texRifle;
        private Texture2D _texShotgun;
        private Texture2D _texSmg;

        // Runtime states
        private float _currentSpreadBloom = 0f;
        private float _hitmarkerTimer = 0f;
        private bool _isHeadshotHit = false;
        private Texture2D _whitePixel;

        public CrosshairRenderMode RenderMode { get => _renderMode; set => _renderMode = value; }
        public CrosshairShape Shape { get => _shape; set => _shape = value; }
        public Color NormalColor { get => _normalColor; set => _normalColor = value; }
        public float BaseGap { get => _baseGap; set => _baseGap = value; }
        public float LineLength { get => _lineLength; set => _lineLength = value; }
        public float LineThickness { get => _lineThickness; set => _lineThickness = value; }

        private void Awake()
        {
            _weaponController = GetComponent<WeaponController>();
            if (_weaponController == null) _weaponController = GetComponentInParent<WeaponController>();

            _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null) _playerMovement = GetComponentInParent<PlayerMovement>();

            // Nạp tài nguyên hình ảnh Crosshair cân xứng chuẩn từ Assets/Resources/Crosshairs/
            // Sử dụng Crosshair_04 (chữ thập cân xứng 4 hướng tuyệt đối) thay cho 01 bị bè dẹt
            _texPistol = Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_04") ?? Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_01");
            _texRifle = Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_02");
            _texShotgun = Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_10");
            _texSmg = Resources.Load<Texture2D>("Crosshairs/Art/Textures/Crosshair_14");

            // Tạo texture 1x1 pixel trắng để vẽ vector GUI fallback
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();

            // Vô hiệu hóa tâm ngắm tĩnh CenterCrosshair trong Canvas uGUI để không bao giờ bị vẽ đè 2 lần
            DisableDuplicateCanvasCrosshair();
        }

        private void Start()
        {
            DisableDuplicateCanvasCrosshair();
        }

        private void DisableDuplicateCanvasCrosshair()
        {
            var centerObj = GameObject.Find("CenterCrosshair");
            if (centerObj != null)
            {
                centerObj.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnHitTargetConfirmed += HandleHitConfirmed;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnHitTargetConfirmed -= HandleHitConfirmed;
            }
        }

        private void HandleHitConfirmed(bool isHeadshot)
        {
            _isHeadshotHit = isHeadshot;
            _hitmarkerTimer = _hitmarkerDuration;
        }

        /// <summary>
        /// Kích hoạt nở tâm khi bắn súng (được gọi từ WeaponController khi bóp cò).
        /// </summary>
        public void TriggerFireBloom(float amount = 8f)
        {
            if (_enableDynamicBloom)
            {
                _currentSpreadBloom += amount;
                _currentSpreadBloom = Mathf.Clamp(_currentSpreadBloom, 0f, 35f);
            }
        }

        private void Update()
        {
            // Thu hồi nở tâm về 0
            if (_currentSpreadBloom > 0f)
            {
                _currentSpreadBloom = Mathf.MoveTowards(_currentSpreadBloom, 0f, Time.deltaTime * _bloomRecoverySpeed);
            }

            // Đếm lùi thời gian Hitmarker
            if (_hitmarkerTimer > 0f)
            {
                _hitmarkerTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            // Chỉ vẽ cho Local Player (tránh vẽ đè khi có nhiều người chơi trong phòng Netcode)
            if (_weaponController != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && !_weaponController.IsOwner)
                return;

            // Chỉ vẽ khi con trỏ chuột đang bị khóa trong game (đang chơi FPS)
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Xác định màu sắc hiển thị
            Color drawColor = _normalColor;
            if (_hitmarkerTimer > 0f)
            {
                drawColor = _isHeadshotHit ? _hitHeadColor : _hitBodyColor;
            }

            // Tính toán khoảng cách hở tâm (Gap) kết hợp độ giật và di chuyển
            float movementBonus = (_playerMovement != null && _playerMovement.IsMoving) ? 4f : 0f;
            float totalGap = _baseGap + _currentSpreadBloom + movementBonus;

            // Khi ngắm ADS: Thu gọn khe hở tâm và làm mờ nhẹ để nhường tầm nhìn cho điểm ruồi (Iron Sights)
            if (_weaponController != null && _weaponController.IsAiming)
            {
                float adsFactor = _weaponController.CurrentAdsFactor;
                totalGap = Mathf.Lerp(totalGap, totalGap * 0.4f, adsFactor);
                drawColor.a = Mathf.Lerp(drawColor.a, drawColor.a * 0.35f, adsFactor);
            }

            // Kiểm tra chế độ Render
            if (_renderMode == CrosshairRenderMode.TextureCrosshair)
            {
                // Chế độ 1: Dùng Texture từ Resources/Crosshairs nhưng khóa cứng tỷ lệ 1:1 không bao giờ bị méo
                Texture2D activeTex = GetActiveCrosshairTexture();
                if (activeTex != null)
                {
                    float size = Mathf.Round(_baseCrosshairSize + _currentSpreadBloom * 0.8f + movementBonus * 1.0f);
                    if (_weaponController != null && _weaponController.IsAiming)
                    {
                        size = Mathf.Round(Mathf.Lerp(size, size * 0.65f, _weaponController.CurrentAdsFactor));
                    }

                    Rect crosshairRect = new Rect(Mathf.Round(center.x - size * 0.5f), Mathf.Round(center.y - size * 0.5f), size, size);
                    Color prevColor = GUI.color;
                    GUI.color = drawColor;
                    GUI.DrawTexture(crosshairRect, activeTex, ScaleMode.ScaleToFit, true, 1.0f);
                    GUI.color = prevColor;
                }
            }
            else
            {
                // Chế độ 2: Tâm ngắm Vector eSports kinh điển (Chuẩn CS:GO/Valorant) - Cân xứng 1:1 tuyệt đối, có viền đen sắc nét
                CrosshairShape activeShape = GetEffectiveShape();
                switch (activeShape)
                {
                    case CrosshairShape.DotOnly:
                        DrawCenterDot(center, drawColor);
                        break;
                    case CrosshairShape.ShotgunCircle:
                        DrawShotgunCircle(center, totalGap + 4f, drawColor);
                        if (_showCenterDot) DrawCenterDot(center, drawColor);
                        break;
                    case CrosshairShape.TStyle:
                        DrawTCross(center, totalGap, _lineLength, _lineThickness, drawColor);
                        if (_showCenterDot) DrawCenterDot(center, drawColor);
                        break;
                    case CrosshairShape.BracketStyle:
                        DrawBrackets(center, totalGap, _lineLength, _lineThickness, drawColor);
                        if (_showCenterDot) DrawCenterDot(center, drawColor);
                        break;
                    case CrosshairShape.ClassicCross:
                    default:
                        DrawClassicCross(center, totalGap, _lineLength, _lineThickness, drawColor);
                        if (_showCenterDot) DrawCenterDot(center, drawColor);
                        break;
                }
            }

            // Vẽ hiệu ứng Hitmarker dấu chéo khi bắn trúng
            if (_hitmarkerTimer > 0f)
            {
                DrawHitmarker(center, drawColor);
            }
        }

        private Texture2D GetActiveCrosshairTexture()
        {
            if (_customCrosshairTexture != null) return _customCrosshairTexture;

            if (_weaponController != null && _weaponController.CurrentWeaponData != null)
            {
                string id = _weaponController.CurrentWeaponData.WeaponId.ToLower();
                if (id.Contains("shotgun")) return _texShotgun ?? _texPistol;
                if (id.Contains("smg")) return _texSmg ?? _texPistol;
                if (id.Contains("rifle")) return _texRifle ?? _texPistol;
                if (id.Contains("pistol")) return _texPistol;
            }

            return _texPistol;
        }

        private CrosshairShape GetEffectiveShape()
        {
            if (_shape != CrosshairShape.AutoByWeapon) return _shape;

            if (_weaponController != null && _weaponController.CurrentWeaponData != null)
            {
                string id = _weaponController.CurrentWeaponData.WeaponId.ToLower();
                if (id.Contains("shotgun")) return CrosshairShape.ShotgunCircle;
                if (id.Contains("smg")) return CrosshairShape.BracketStyle;
                if (id.Contains("rifle")) return CrosshairShape.ClassicCross;
                if (id.Contains("pistol")) return CrosshairShape.ClassicCross;
            }

            return CrosshairShape.ClassicCross;
        }

        // ================= CÁC HÀM VẼ GIAO DIỆN TÂM NGẮM VECTO (ON-GUI) =================

        private void DrawClassicCross(Vector2 center, float gap, float length, float thickness, Color color)
        {
            float halfThick = thickness * 0.5f;
            Color black = new Color(0f, 0f, 0f, 0.95f);
            float outThick = thickness + 2f;
            float halfOut = outThick * 0.5f;
            float outLen = length + 2f;

            // 1. Viền đen 4 vạch ôm bên ngoài giúp vạch ngắm cực nét trên nền trời sáng
            DrawRect(new Rect(Mathf.Round(center.x - halfOut), Mathf.Round(center.y - gap - length - 1f), outThick, outLen), black);
            DrawRect(new Rect(Mathf.Round(center.x - halfOut), Mathf.Round(center.y + gap - 1f), outThick, outLen), black);
            DrawRect(new Rect(Mathf.Round(center.x - gap - length - 1f), Mathf.Round(center.y - halfOut), outLen, outThick), black);
            DrawRect(new Rect(Mathf.Round(center.x + gap - 1f), Mathf.Round(center.y - halfOut), outLen, outThick), black);

            // 2. Bốn vạch chữ thập màu chính, đối xứng 1:1 tuyệt đối
            DrawRect(new Rect(Mathf.Round(center.x - halfThick), Mathf.Round(center.y - gap - length), thickness, length), color);
            DrawRect(new Rect(Mathf.Round(center.x - halfThick), Mathf.Round(center.y + gap), thickness, length), color);
            DrawRect(new Rect(Mathf.Round(center.x - gap - length), Mathf.Round(center.y - halfThick), length, thickness), color);
            DrawRect(new Rect(Mathf.Round(center.x + gap), Mathf.Round(center.y - halfThick), length, thickness), color);
        }

        private void DrawTCross(Vector2 center, float gap, float length, float thickness, Color color)
        {
            float halfThick = thickness * 0.5f;
            Color black = new Color(0f, 0f, 0f, 0.95f);
            float outThick = thickness + 2f;
            float halfOut = outThick * 0.5f;
            float outLen = length + 2f;

            // Viền đen
            DrawRect(new Rect(Mathf.Round(center.x - halfOut), Mathf.Round(center.y + gap - 1f), outThick, outLen), black);
            DrawRect(new Rect(Mathf.Round(center.x - gap - length - 1f), Mathf.Round(center.y - halfOut), outLen, outThick), black);
            DrawRect(new Rect(Mathf.Round(center.x + gap - 1f), Mathf.Round(center.y - halfOut), outLen, outThick), black);

            // 3 vạch chữ T
            DrawRect(new Rect(Mathf.Round(center.x - halfThick), Mathf.Round(center.y + gap), thickness, length), color);
            DrawRect(new Rect(Mathf.Round(center.x - gap - length), Mathf.Round(center.y - halfThick), length, thickness), color);
            DrawRect(new Rect(Mathf.Round(center.x + gap), Mathf.Round(center.y - halfThick), length, thickness), color);
        }

        private void DrawBrackets(Vector2 center, float gap, float length, float thickness, Color color)
        {
            float halfThick = thickness * 0.5f;
            float wing = length * 0.4f;

            // Cánh trái: [
            DrawRect(new Rect(center.x - gap - length, center.y - wing, thickness, wing * 2f), color);
            DrawRect(new Rect(center.x - gap - length, center.y - wing, wing, thickness), color);
            DrawRect(new Rect(center.x - gap - length, center.y + wing - thickness, wing, thickness), color);

            // Cánh phải: ]
            DrawRect(new Rect(center.x + gap + length - thickness, center.y - wing, thickness, wing * 2f), color);
            DrawRect(new Rect(center.x + gap + length - wing, center.y - wing, wing, thickness), color);
            DrawRect(new Rect(center.x + gap + length - wing, center.y + wing - thickness, wing, thickness), color);
        }

        private void DrawShotgunCircle(Vector2 center, float radius, Color color)
        {
            int segments = 12;
            float thickness = _lineThickness + 0.5f;
            float arcLength = (Mathf.PI * 2f * radius) / segments * 0.55f;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;

                DrawRect(new Rect(x - arcLength * 0.5f, y - thickness * 0.5f, arcLength, thickness), color);
            }
        }

        private void DrawCenterDot(Vector2 center, Color color)
        {
            float half = _centerDotSize * 0.5f;
            Color black = new Color(0f, 0f, 0f, 0.95f);
            // Viền đen quanh chấm tròn
            DrawRect(new Rect(Mathf.Round(center.x - half - 1f), Mathf.Round(center.y - half - 1f), _centerDotSize + 2f, _centerDotSize + 2f), black);
            // Chấm màu chính
            DrawRect(new Rect(Mathf.Round(center.x - half), Mathf.Round(center.y - half), _centerDotSize, _centerDotSize), color);
        }

        private void DrawHitmarker(Vector2 center, Color color)
        {
            float size = _hitmarkerSize;
            float gap = 4f;
            float thick = 2f;

            // 4 vạch chéo đỏ/vàng khi bắn trúng
            DrawDiagonalLine(center.x - gap, center.y - gap, center.x - gap - size, center.y - gap - size, thick, color);
            DrawDiagonalLine(center.x + gap, center.y - gap, center.x + gap + size, center.y - gap - size, thick, color);
            DrawDiagonalLine(center.x - gap, center.y + gap, center.x - gap - size, center.y + gap + size, thick, color);
            DrawDiagonalLine(center.x + gap, center.y + gap, center.x + gap + size, center.y + gap + size, thick, color);
        }

        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whitePixel);
            GUI.color = Color.white;
        }

        private void DrawDiagonalLine(float x1, float y1, float x2, float y2, float thickness, Color color)
        {
            Vector2 p1 = new Vector2(x1, y1);
            Vector2 p2 = new Vector2(x2, y2);
            Vector2 d = p2 - p1;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, p1);
            DrawRect(new Rect(p1.x, p1.y - thickness * 0.5f, d.magnitude, thickness), color);
            GUI.matrix = matrix;
        }
    }
}
