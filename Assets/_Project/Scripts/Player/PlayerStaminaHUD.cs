// Script: PlayerStaminaHUD.cs
// Mục đích: Hiển thị thanh Thể lực (Stamina Bar) và trạng thái kiệt sức của Player đang điều khiển (Giai đoạn P1).
// Môi trường thực thi: Client-side (Local Player HUD).

using UnityEngine;

namespace Hellfire.Player
{
    [DisallowMultipleComponent]
    public class PlayerStaminaHUD : MonoBehaviour
    {
        [Header("Target Player")]
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private PlayerCrouch _crouch;
        [SerializeField] private PlayerSlide _slide;

        [Header("HUD Settings")]
        [SerializeField] private bool _showOnGUI = true;
        [SerializeField] private Vector2 _screenOffset = new Vector2(40, 40);
        [SerializeField] private Vector2 _barSize = new Vector2(260, 20);

        [Header("Colors")]
        [SerializeField] private Color _staminaColor = new Color(0.2f, 0.85f, 0.3f, 0.95f);
        [SerializeField] private Color _exhaustedColor = new Color(0.95f, 0.25f, 0.2f, 0.95f);
        [SerializeField] private Color _backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);

        private Texture2D _whiteTexture;

        private void Awake()
        {
            AutoResolveReferences();
            _whiteTexture = Texture2D.whiteTexture;
        }

        private void AutoResolveReferences()
        {
            if (_stamina == null) _stamina = GetComponent<PlayerStamina>();
            if (_crouch == null) _crouch = GetComponent<PlayerCrouch>();
            if (_slide == null) _slide = GetComponent<PlayerSlide>();
        }

        private void Start()
        {
            AutoResolveReferences();
            if (_stamina == null)
            {
                FindLocalPlayerStamina();
            }
        }

        private void FindLocalPlayerStamina()
        {
            var staminas = FindObjectsByType<PlayerStamina>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in staminas)
            {
                if (!s.IsSpawned || s.IsOwner)
                {
                    _stamina = s;
                    _crouch = s.GetComponent<PlayerCrouch>();
                    _slide = s.GetComponent<PlayerSlide>();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (!_showOnGUI) return;
            if (_stamina == null)
            {
                FindLocalPlayerStamina();
                if (_stamina == null) return;
            }

            if (_stamina.IsSpawned && !_stamina.IsOwner) return;

            float maxStamina = _stamina.MaxStamina;
            float currentStamina = _stamina.CurrentStamina;
            float pct = Mathf.Clamp01(currentStamina / Mathf.Max(1f, maxStamina));

            float x = _screenOffset.x;
            float y = Screen.height - _screenOffset.y - _barSize.y;

            // Nền đen
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(x - 2, y - 2, _barSize.x + 4, _barSize.y + 4), _whiteTexture);

            // Thanh Stamina
            GUI.color = _stamina.IsExhausted ? _exhaustedColor : _staminaColor;
            GUI.DrawTexture(new Rect(x, y, _barSize.x * pct, _barSize.y), _whiteTexture);

            // Chữ hiển thị trạng thái
            GUI.color = Color.white;
            string stateText = $"STAMINA: {currentStamina:F0}/{maxStamina:F0}";
            if (_slide != null && _slide.IsSliding) stateText += " [SLIDING]";
            else if (_crouch != null && _crouch.IsCrouching) stateText += " [CROUCH]";
            else if (_stamina.IsExhausted) stateText += " [EXHAUSTED]";

            GUI.Label(new Rect(x, y - 22, 300, 20), stateText);

            // Hint phím bấm
            GUI.color = new Color(0.85f, 0.85f, 0.9f, 0.75f);
            GUI.Label(new Rect(x, y + _barSize.y + 4, 600, 20), "WASD: Di chuyển | Shift: Sprint | Ctrl: Crouch | Shift+Ctrl: Slide | V: 3rd Person");
        }
    }
}
