// Script: TeammateHUDPanel.cs
// Mục đích: Quản lý panel thông tin đồng đội ở cạnh trái HUD (Mục 16.2 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Player;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class TeammateHUDPanel : MonoBehaviour
    {
        [Header("Container")]
        [SerializeField] private RectTransform _container;
        [SerializeField] private GameObject _itemPrefab;

        private readonly List<TeammateHUDItemSlot> _slots = new List<TeammateHUDItemSlot>();

        private void Awake()
        {
            if (_container == null)
            {
                _container = GetComponent<RectTransform>();
            }
        }

        private void Start()
        {
            PlayerIdentity.OnAnyPlayerIdentityChanged += RefreshTeammates;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientEvent;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientEvent;
            }

            RefreshTeammates();
        }

        private void OnDestroy()
        {
            PlayerIdentity.OnAnyPlayerIdentityChanged -= RefreshTeammates;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientEvent;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientEvent;
            }

            ClearSlots();
        }

        private void HandleClientEvent(ulong clientId)
        {
            RefreshTeammates();
        }

        /// <summary>
        /// Làm mới danh sách đồng đội (tối đa 3 người, ẩn khi chơi một mình).
        /// Cập nhật thông qua callback NetworkVariable, không dò mỗi frame (Mục 16.2).
        /// </summary>
        public void RefreshTeammates()
        {
            var identities = PlayerIdentity.All;
            var teammates = new List<PlayerIdentity>();

            for (int i = 0; i < identities.Count; i++)
            {
                var identity = identities[i];
                if (identity != null && !identity.IsOwner)
                {
                    teammates.Add(identity);
                }
            }

            // Ẩn toàn bộ panel khi chơi một mình (0 đồng đội) theo mục 16.2
            if (teammates.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Điều chỉnh số lượng slot trong pool phù hợp với số lượng đồng đội (tối đa 3)
            int targetCount = Mathf.Min(teammates.Count, 3);
            while (_slots.Count < targetCount)
            {
                var slot = CreateSlot();
                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < targetCount)
                {
                    _slots[i].gameObject.SetActive(true);
                    _slots[i].Bind(teammates[i]);
                }
                else
                {
                    _slots[i].Unbind();
                    _slots[i].gameObject.SetActive(false);
                }
            }
        }

        private TeammateHUDItemSlot CreateSlot()
        {
            GameObject slotObj;
            if (_itemPrefab != null)
            {
                slotObj = Instantiate(_itemPrefab, _container != null ? _container : transform);
            }
            else
            {
                slotObj = BuildProceduralSlot();
                slotObj.transform.SetParent(_container != null ? _container : transform, false);
            }

            var slotComponent = slotObj.GetComponent<TeammateHUDItemSlot>();
            if (slotComponent == null)
            {
                slotComponent = slotObj.AddComponent<TeammateHUDItemSlot>();
            }

            return slotComponent;
        }

        private GameObject BuildProceduralSlot()
        {
            var root = new GameObject("TeammateSlot", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(230f, 48f);

            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.08f, 0.1f, 0.85f);

            // Chip màu + số
            var chipObj = new GameObject("ColorChip", typeof(RectTransform), typeof(Image));
            chipObj.transform.SetParent(root.transform, false);
            var chipRect = chipObj.GetComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(0f, 0.5f);
            chipRect.anchorMax = new Vector2(0f, 0.5f);
            chipRect.anchoredPosition = new Vector2(20f, 0f);
            chipRect.sizeDelta = new Vector2(28f, 28f);

            var numObj = new GameObject("NumberText", typeof(RectTransform), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(chipObj.transform, false);
            var numTmp = numObj.GetComponent<TextMeshProUGUI>();
            numTmp.fontSize = 16;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.alignment = TextAlignmentOptions.Center;
            numTmp.color = Color.black;
            var numRect = numObj.GetComponent<RectTransform>();
            numRect.anchorMin = Vector2.zero;
            numRect.anchorMax = Vector2.one;
            numRect.offsetMin = Vector2.zero;
            numRect.offsetMax = Vector2.zero;

            // Tên người chơi
            var nameObj = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(root.transform, false);
            var nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
            nameTmp.fontSize = 13;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = Color.white;
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(40f, -4f);
            nameRect.sizeDelta = new Vector2(-46f, 18f);

            // Thanh máu
            var barObj = new GameObject("HealthBar", typeof(RectTransform), typeof(Image));
            barObj.transform.SetParent(root.transform, false);
            var barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0f, 0f);
            barRect.anchoredPosition = new Vector2(40f, 6f);
            barRect.sizeDelta = new Vector2(-46f, 16f);
            var barImg = barObj.GetComponent<Image>();
            barImg.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);

            var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(barObj.transform, false);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillObj.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1.0f;
            fillImg.color = new Color(0.15f, 0.75f, 0.25f, 1f);

            // Overlay khi Gục (Downed)
            var downedObj = new GameObject("DownedOverlay", typeof(RectTransform));
            downedObj.transform.SetParent(root.transform, false);
            var downedRect = downedObj.GetComponent<RectTransform>();
            downedRect.anchorMin = new Vector2(0f, 0f);
            downedRect.anchorMax = new Vector2(1f, 0f);
            downedRect.pivot = new Vector2(0f, 0f);
            downedRect.anchoredPosition = new Vector2(40f, 6f);
            downedRect.sizeDelta = new Vector2(-46f, 16f);

            var downedTextObj = new GameObject("DownedTimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            downedTextObj.transform.SetParent(downedObj.transform, false);
            var downedTmp = downedTextObj.GetComponent<TextMeshProUGUI>();
            downedTmp.fontSize = 12;
            downedTmp.fontStyle = FontStyles.Bold;
            downedTmp.alignment = TextAlignmentOptions.Center;
            downedTmp.color = Color.red;
            var dtRect = downedTextObj.GetComponent<RectTransform>();
            dtRect.anchorMin = Vector2.zero;
            dtRect.anchorMax = Vector2.one;
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = Vector2.zero;

            // Overlay khi Chết (Dead)
            var deadObj = new GameObject("DeadIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
            deadObj.transform.SetParent(root.transform, false);
            var deadTmp = deadObj.GetComponent<TextMeshProUGUI>();
            deadTmp.text = "✖ ĐÃ CHẾT";
            deadTmp.fontSize = 12;
            deadTmp.fontStyle = FontStyles.Bold;
            deadTmp.alignment = TextAlignmentOptions.Center;
            deadTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            var deadRect = deadObj.GetComponent<RectTransform>();
            deadRect.anchorMin = new Vector2(0f, 0f);
            deadRect.anchorMax = new Vector2(1f, 0f);
            deadRect.pivot = new Vector2(0f, 0f);
            deadRect.anchoredPosition = new Vector2(40f, 6f);
            deadRect.sizeDelta = new Vector2(-46f, 16f);

            var slotComp = root.AddComponent<TeammateHUDItemSlot>();
            slotComp.ConfigureReferences(
                chipObj.GetComponent<Image>(),
                numTmp,
                nameTmp,
                barObj,
                fillImg,
                downedObj,
                downedTmp,
                deadObj,
                root.GetComponent<CanvasGroup>()
            );

            return root;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                {
                    _slots[i].Unbind();
                }
            }
            _slots.Clear();
        }
    }

    /// <summary>
    /// Đại diện cho 1 slot hiển thị của 1 đồng đội trên TeammateHUDPanel.
    /// Tự động cập nhật qua OnValueChanged của Health & PlayerIdentity.
    /// </summary>
    public class TeammateHUDItemSlot : MonoBehaviour
    {
        [SerializeField] private Image _colorChip;
        [SerializeField] private TextMeshProUGUI _playerNumberText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private GameObject _healthBarRoot;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private GameObject _downedRoot;
        [SerializeField] private TextMeshProUGUI _downedTimerText;
        [SerializeField] private GameObject _deadRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        private PlayerIdentity _boundIdentity;
        private Health _boundHealth;

        public void ConfigureReferences(
            Image chip,
            TextMeshProUGUI numText,
            TextMeshProUGUI nameText,
            GameObject healthBarRoot,
            Image healthFill,
            GameObject downedRoot,
            TextMeshProUGUI downedTimerText,
            GameObject deadRoot,
            CanvasGroup canvasGroup)
        {
            _colorChip = chip;
            _playerNumberText = numText;
            _nameText = nameText;
            _healthBarRoot = healthBarRoot;
            _healthFillImage = healthFill;
            _downedRoot = downedRoot;
            _downedTimerText = downedTimerText;
            _deadRoot = deadRoot;
            _canvasGroup = canvasGroup;
        }

        public void Bind(PlayerIdentity identity)
        {
            if (_boundIdentity == identity) return;

            Unbind();

            _boundIdentity = identity;
            if (_boundIdentity == null) return;

            _boundHealth = _boundIdentity.GetComponent<Health>();

            // Đăng ký event NetworkVariable theo mục 16.2
            _boundIdentity.PlayerName.OnValueChanged += HandleNameChanged;
            _boundIdentity.ColorIndex.OnValueChanged += HandleColorIndexChanged;

            if (_boundHealth != null)
            {
                _boundHealth.CurrentHealth.OnValueChanged += HandleHealthChanged;
                _boundHealth.IsDowned.OnValueChanged += HandleDownedChanged;
                _boundHealth.IsDead.OnValueChanged += HandleDeadChanged;
                _boundHealth.DownedTimer.OnValueChanged += HandleDownedTimerChanged;
            }

            UpdateDisplay();
        }

        public void Unbind()
        {
            if (_boundIdentity != null)
            {
                _boundIdentity.PlayerName.OnValueChanged -= HandleNameChanged;
                _boundIdentity.ColorIndex.OnValueChanged -= HandleColorIndexChanged;
                _boundIdentity = null;
            }

            if (_boundHealth != null)
            {
                _boundHealth.CurrentHealth.OnValueChanged -= HandleHealthChanged;
                _boundHealth.IsDowned.OnValueChanged -= HandleDownedChanged;
                _boundHealth.IsDead.OnValueChanged -= HandleDeadChanged;
                _boundHealth.DownedTimer.OnValueChanged -= HandleDownedTimerChanged;
                _boundHealth = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => UpdateDisplay();
        private void HandleColorIndexChanged(int oldVal, int newVal) => UpdateDisplay();
        private void HandleHealthChanged(float oldVal, float newVal) => UpdateDisplay();
        private void HandleDownedChanged(bool oldVal, bool newVal) => UpdateDisplay();
        private void HandleDeadChanged(bool oldVal, bool newVal) => UpdateDisplay();
        private void HandleDownedTimerChanged(float oldVal, float newVal) => UpdateDownedTimer();

        private void UpdateDisplay()
        {
            if (_boundIdentity == null) return;

            int colorIndex = _boundIdentity.ColorIndex.Value;
            Color playerColor = PlayerPalette.GetColor(colorIndex);
            int playerNumber = PlayerPalette.GetPlayerNumber(colorIndex);

            if (_colorChip != null)
            {
                _colorChip.color = playerColor;
            }

            if (_playerNumberText != null)
            {
                _playerNumberText.text = playerNumber.ToString();
            }

            if (_nameText != null)
            {
                _nameText.text = _boundIdentity.PlayerName.Value.ToString();
            }

            bool isDead = _boundHealth != null && _boundHealth.IsDead.Value;
            bool isDowned = _boundHealth != null && _boundHealth.IsDowned.Value;

            // 1. Trạng thái Chết (Dead): Làm xám, hiện dấu X (Mục 16.2)
            if (isDead)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0.45f;
                if (_healthBarRoot != null) _healthBarRoot.SetActive(false);
                if (_downedRoot != null) _downedRoot.SetActive(false);
                if (_deadRoot != null) _deadRoot.SetActive(true);
                return;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1.0f;
            if (_deadRoot != null) _deadRoot.SetActive(false);

            // 2. Trạng thái Gục (Downed): Thay thanh máu bằng đếm ngược DownedTimer (Mục 16.2)
            if (isDowned)
            {
                if (_healthBarRoot != null) _healthBarRoot.SetActive(false);
                if (_downedRoot != null) _downedRoot.SetActive(true);
                UpdateDownedTimer();
                return;
            }

            // 3. Trạng thái Bình thường (Normal): Hiện thanh máu
            if (_downedRoot != null) _downedRoot.SetActive(false);
            if (_healthBarRoot != null) _healthBarRoot.SetActive(true);

            if (_healthFillImage != null && _boundHealth != null)
            {
                float maxHp = _boundHealth.MaxHealth > 0f ? _boundHealth.MaxHealth : 100f;
                float curHp = Mathf.Max(0f, _boundHealth.CurrentHealth.Value);
                _healthFillImage.fillAmount = Mathf.Clamp01(curHp / maxHp);
            }
        }

        private void UpdateDownedTimer()
        {
            if (_boundHealth == null || !_boundHealth.IsDowned.Value) return;

            float timer = Mathf.Max(0f, _boundHealth.DownedTimer.Value);
            int minutes = Mathf.FloorToInt(timer / 60f);
            int seconds = Mathf.FloorToInt(timer % 60f);

            if (_downedTimerText != null)
            {
                _downedTimerText.text = $"⚠ GỤC: {minutes:00}:{seconds:00}";
            }
        }
    }
}
