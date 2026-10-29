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
        public enum CrosshairShape
        {
            AutoByWeapon,    // Tự động chọn theo vũ khí đang cầm
            ClassicCross,    // Tâm chữ thập 4 cạnh kinh điển (Pistol / Rifle)
            ShotgunCircle,   // Tâm vòng tròn tán xạ (Shotgun)
            DotOnly,         // Chỉ 1 chấm tròn tâm (Gọn gàng)
            TStyle,          // Tâm chữ T (Bỏ thanh trên đỉnh)
            BracketStyle     // Tâm dấu ngoặc [ ] (SMG)
        }

        [Header("Tùy Chọn Kiểu Dáng (Shape)")]
        [Tooltip("Kiểu dáng tâm ngắm. Chọn AutoByWeapon để tự đổi theo súng đang cầm.")]
        [SerializeField] private CrosshairShape _shape = CrosshairShape.AutoByWeapon;

        [Header("Màu Sắc & Độ Đậm (Colors)")]
        [Tooltip("Màu sắc tâm ngắm bình thường")]
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Màu khi bắn trúng thân quái (Torso Hit)")]
        [SerializeField] private Color _hitBodyColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Màu khi bắn trúng đầu chí mạng (Headshot Hit)")]
        [SerializeField] private Color _hitHeadColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Kích Thước & Độ Rộng (Geometry)")]
        [Tooltip("Độ dài của các vạch ngắm (pixels)")]
        [SerializeField] private float _lineLength = 10f;
        [Tooltip("Độ dày của nét vẽ (pixels)")]
        [SerializeField] private float _lineThickness = 2f;
        [Tooltip("Khoảng cách từ tâm đến các vạch khi đứng yên (pixels)")]
        [SerializeField] private float _baseGap = 6f;
        [Tooltip("Hiển thị chấm tròn ở chính giữa tâm")]
        [SerializeField] private bool _showCenterDot = true;
        [Tooltip("Kích thước chấm tròn ở giữa (pixels)")]
        [SerializeField] private float _centerDotSize = 3f;

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

        // References
        private WeaponController _weaponController;
        private PlayerMovement _playerMovement;

        // Runtime states
        private float _currentSpreadBloom = 0f;
        private float _hitmarkerTimer = 0f;
        private bool _isHeadshotHit = false;
        private Texture2D _whitePixel;

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

            // Tạo texture 1x1 pixel trắng để vẽ vector GUI sắc nét
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
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

            // Xác định kiểu tâm ngắm dựa vào loại súng
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

            // Vẽ hiệu ứng Hitmarker dấu chéo khi bắn trúng
            if (_hitmarkerTimer > 0f)
            {
                DrawHitmarker(center, drawColor);
            }
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

            // Thanh trên
            DrawRect(new Rect(center.x - halfThick, center.y - gap - length, thickness, length), color);
            // Thanh dưới
            DrawRect(new Rect(center.x - halfThick, center.y + gap, thickness, length), color);
            // Thanh trái
            DrawRect(new Rect(center.x - gap - length, center.y - halfThick, length, thickness), color);
            // Thanh phải
            DrawRect(new Rect(center.x + gap, center.y - halfThick, length, thickness), color);
        }

        private void DrawTCross(Vector2 center, float gap, float length, float thickness, Color color)
        {
            float halfThick = thickness * 0.5f;

            // Thanh dưới
            DrawRect(new Rect(center.x - halfThick, center.y + gap, thickness, length), color);
            // Thanh trái
            DrawRect(new Rect(center.x - gap - length, center.y - halfThick, length, thickness), color);
            // Thanh phải
            DrawRect(new Rect(center.x + gap, center.y - halfThick, length, thickness), color);
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
            DrawRect(new Rect(center.x - half, center.y - half, _centerDotSize, _centerDotSize), color);
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
