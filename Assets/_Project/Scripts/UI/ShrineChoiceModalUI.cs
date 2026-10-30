// Script: ShrineChoiceModalUI.cs
// Mục đích: Quản lý giao diện hiển thị 3 Thẻ Lựa Chọn Nâng Cấp khi người chơi tương tác với Tế đàn / Bàn Modifier.
//           Hỗ trợ click chuột hoặc phím tắt nhanh [1], [2], [3], [ESC]. Tự động khởi tạo UI runtime nếu chưa có sẵn trong Scene.
// Môi trường thực thi: Client (UI hiển thị cục bộ cho người chơi đang tương tác).

using System;
using System.Collections.Generic;
using Hellfire.Items;
using Hellfire.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class ShrineChoiceModalUI : MonoBehaviour
    {
        public static ShrineChoiceModalUI Instance { get; private set; }

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _headerTitleText;
        [SerializeField] private TextMeshProUGUI _headerSubtitleText;

        [Header("Choice Cards (3 Slots)")]
        [SerializeField] private GameObject[] _cardContainers = new GameObject[3];
        [SerializeField] private Image[] _cardBgImages = new Image[3];
        [SerializeField] private TextMeshProUGUI[] _cardBadges = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _cardStarTexts = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _cardTitles = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[3];
        [SerializeField] private Button[] _cardButtons = new Button[3];

        private Action<int> _onChoiceSelectedCallback;
        private Action _onRerollCallback;
        private Action _onCloseCallback;
        private bool _isOpen = false;
        private List<UpgradeOptionData> _currentOptions = new List<UpgradeOptionData>();

        private ShrineType _shrineType = ShrineType.BodyShrine;
        private int _upgradeCost = 2;
        private int _rerollCost = 1;

        private Texture2D _redCrystalTex;
        private Texture2D _greenCrystalTex;
        private Texture2D _bigBgTex;
        private Texture2D _frameMidTex;
        private Texture2D _buttonTex;
        private PlayerStats _cachedPlayerStats;

        private bool _stylesInit = false;
        private GUIStyle _fallbackTitleStyle;
        private GUIStyle _fallbackBadgeNumStyle;
        private GUIStyle _fallbackSubStyle;
        private GUIStyle _fallbackCardBadgeStyle;
        private GUIStyle _fallbackCardTitleStyle;
        private GUIStyle _fallbackCardDescStyle;
        private GUIStyle _fallbackStarStyle;
        private GUIStyle _fallbackBtnLabelStyle;
        private GUIStyle _fallbackDisabledStyle;
        private GUIStyle _fallbackRerollStyle;
        private GUIStyle _fallbackHintStyle;
        private GUIStyle _fallbackCloseStyle;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _redCrystalTex = Resources.Load<Texture2D>("GUI_Parts/RedCrystal");
            _greenCrystalTex = Resources.Load<Texture2D>("GUI_Parts/GreenCrystal");
            _bigBgTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/big_background") ?? Resources.Load<Texture2D>("GUI_Parts/Gui_parts/Frame_big");
            _frameMidTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/Frame_mid");
            _buttonTex = Resources.Load<Texture2D>("GUI_Parts/Gui_parts/button");

            EnsureUIHierarchy();
        }

        private void Start()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Bắt phím tắt nhanh 1, 2, 3 để chọn thẻ, R để đổi thẻ, ESC / E để đóng
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    TrySelectChoice(0);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    TrySelectChoice(1);
                }
                else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                {
                    TrySelectChoice(2);
                }
                else if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    TryReroll();
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseModal();
                }
            }
        }

        /// <summary>
        /// Mở modal hiển thị 3 lựa chọn nâng cấp chỉ số kèm chi phí Pha Lê.
        /// </summary>
        public void Open(
            string title, 
            string subtitle, 
            List<UpgradeOptionData> options, 
            Action<int> onSelected,
            ShrineType shrineType = ShrineType.BodyShrine,
            int upgradeCost = 2,
            int rerollCost = 1,
            Action onReroll = null,
            Action onClose = null)
        {
            EnsureUIHierarchy();

            _shrineType = shrineType;
            _upgradeCost = upgradeCost;
            _rerollCost = rerollCost;
            _currentOptions = options ?? new List<UpgradeOptionData>();
            _onChoiceSelectedCallback = onSelected;
            _onRerollCallback = onReroll;
            _onCloseCallback = onClose;

            _cachedPlayerStats = PlayerMovement.LocalInstance != null 
                ? PlayerMovement.LocalInstance.GetComponent<PlayerStats>() 
                : FindAnyObjectByType<PlayerStats>();

            if (_headerTitleText != null) _headerTitleText.text = title;
            if (_headerSubtitleText != null) _headerSubtitleText.text = subtitle;

            UpdateCardVisuals();

            _isOpen = true;
            bool hasValidUGUI = (_panelRoot != null && _cardContainers != null && _cardContainers.Length >= 3 && _cardContainers[0] != null);
            if (hasValidUGUI)
            {
                _panelRoot.SetActive(true);
            }
            else
            {
                if (_panelRoot != null) _panelRoot.SetActive(false);
            }

            // Mở khóa chuột để người chơi bấm nút
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Tắt tạm thời việc xoay camera trên PlayerMovement cục bộ
            SetPlayerMovementLookEnabled(false);
        }

        public void RefreshOptions(List<UpgradeOptionData> newOptions)
        {
            _currentOptions = newOptions ?? new List<UpgradeOptionData>();
            UpdateCardVisuals();
        }

        private void UpdateCardVisuals()
        {
            var playerStats = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.GetComponent<PlayerStats>() : FindAnyObjectByType<PlayerStats>();
            bool canAfford = playerStats != null && playerStats.CanAfford(_shrineType, _upgradeCost);

            for (int i = 0; i < 3; i++)
            {
                if (i < _currentOptions.Count)
                {
                    var opt = _currentOptions[i];
                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(true);
                    if (_cardBadges[i] != null) _cardBadges[i].text = opt.BadgeText;
                    if (_cardTitles[i] != null) _cardTitles[i].text = opt.Title;
                    if (_cardDescriptions[i] != null) _cardDescriptions[i].text = opt.Description;

                    // Xác định màu sắc theo số sao: 3 sao (Vàng kim), 2 sao (Lam sáng), 1 sao (Bạc)
                    Color starColor = opt.Stars == 3
                        ? new Color(1f, 0.85f, 0.15f)
                        : (opt.Stars == 2 ? new Color(0.35f, 0.82f, 1f) : new Color(0.85f, 0.9f, 0.95f));

                    if (_cardStarTexts[i] != null)
                    {
                        _cardStarTexts[i].text = opt.GetStarIcons();
                        _cardStarTexts[i].color = starColor;
                    }

                    if (_cardBadges[i] != null)
                    {
                        _cardBadges[i].color = starColor;
                    }

                    if (_cardBgImages[i] != null)
                    {
                        Color bgColor = opt.Stars == 3
                            ? new Color(0.24f, 0.19f, 0.05f, 0.96f)
                            : (opt.Stars == 2 ? new Color(0.08f, 0.18f, 0.28f, 0.96f) : new Color(0.12f, 0.14f, 0.22f, 0.95f));
                        _cardBgImages[i].color = bgColor;
                    }

                    int index = i;
                    if (_cardButtons[i] != null)
                    {
                        _cardButtons[i].onClick.RemoveAllListeners();
                        _cardButtons[i].onClick.AddListener(() => TrySelectChoice(index));
                        _cardButtons[i].interactable = canAfford;
                    }
                }
                else
                {
                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(false);
                }
            }
        }

        public void TrySelectChoice(int index)
        {
            if (!_isOpen) return;

            var playerStats = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.GetComponent<PlayerStats>() : FindAnyObjectByType<PlayerStats>();
            if (playerStats != null && !playerStats.CanAfford(_shrineType, _upgradeCost))
            {
                Debug.LogWarning($"[ShrineChoiceModalUI] Không đủ {_upgradeCost} Pha Lê để nâng cấp!");
                return;
            }

            _onChoiceSelectedCallback?.Invoke(index);
        }

        public void TryReroll()
        {
            if (!_isOpen) return;

            var playerStats = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.GetComponent<PlayerStats>() : FindAnyObjectByType<PlayerStats>();
            if (playerStats != null && !playerStats.CanAfford(_shrineType, _rerollCost))
            {
                Debug.LogWarning($"[ShrineChoiceModalUI] Không đủ {_rerollCost} Pha Lê để đổi thẻ!");
                return;
            }

            _onRerollCallback?.Invoke();
        }

        public void CloseModal()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerMovementLookEnabled(true);

            _onCloseCallback?.Invoke();
            _onChoiceSelectedCallback = null;
            _onRerollCallback = null;
            _onCloseCallback = null;
        }

        private void SetPlayerMovementLookEnabled(bool enabled)
        {
            var playerMove = PlayerMovement.LocalInstance;
            if (playerMove != null)
            {
                playerMove.SetCursorLock(enabled);
            }
        }

        /// <summary>
        /// Tự động sinh giao diện 3 thẻ nâng cấp nếu Scene chưa cấu hình thủ công hoặc Prefab chưa load.
        /// </summary>
        private void EnsureUIHierarchy()
        {
            if (_panelRoot != null) return;

            // 1. Thử load Prefab hoàn chỉnh từ Resources nếu có
            var modalPrefab = Resources.Load<GameObject>("UI/ShrineChoiceModal");
            if (modalPrefab != null)
            {
                var instantiated = Instantiate(modalPrefab, transform);
                var modalComp = instantiated.GetComponent<ShrineChoiceModalUI>();
                if (modalComp != null)
                {
                    _panelRoot = modalComp._panelRoot;
                    _headerTitleText = modalComp._headerTitleText;
                    _headerSubtitleText = modalComp._headerSubtitleText;
                    _cardContainers = modalComp._cardContainers;
                    _cardBgImages = modalComp._cardBgImages;
                    _cardBadges = modalComp._cardBadges;
                    _cardStarTexts = modalComp._cardStarTexts;
                    _cardTitles = modalComp._cardTitles;
                    _cardDescriptions = modalComp._cardDescriptions;
                    _cardButtons = modalComp._cardButtons;
                    if (_panelRoot != null)
                    {
                        _panelRoot.SetActive(false);
                        return;
                    }
                }
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                var canvasObj = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            transform.SetParent(canvas.transform, false);

            if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // 1. Tạo Root Panel với background từ GUI_Parts
            _panelRoot = new GameObject("ShrineChoiceModalPanel", typeof(RectTransform), typeof(Image));
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var bgImg = _panelRoot.GetComponent<Image>();
            var bigBgSprite = Resources.Load<Sprite>("GUI_Parts/Gui_parts/big_background") ?? Resources.Load<Sprite>("GUI_Parts/Gui_parts/Frame_big");
            if (bigBgSprite != null)
            {
                bgImg.sprite = bigBgSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);
            }
            else
            {
                bgImg.color = new Color(0.02f, 0.04f, 0.07f, 0.95f);
            }

            // 2. Tiêu đề kèm khung name_bar
            var headerBgObj = new GameObject("HeaderBarBg", typeof(RectTransform), typeof(Image));
            headerBgObj.transform.SetParent(_panelRoot.transform, false);
            var headerBgRect = headerBgObj.GetComponent<RectTransform>();
            headerBgRect.anchorMin = new Vector2(0.5f, 0.86f);
            headerBgRect.anchorMax = new Vector2(0.5f, 0.86f);
            headerBgRect.sizeDelta = new Vector2(620f, 65f);
            var headerBgImg = headerBgObj.GetComponent<Image>();
            var nameBarSprite = Resources.Load<Sprite>("GUI_Parts/Gui_parts/name_bar");
            if (nameBarSprite != null)
            {
                headerBgImg.sprite = nameBarSprite;
                headerBgImg.type = Image.Type.Sliced;
            }

            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(headerBgObj.transform, false);
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = Vector2.zero;
            headerRect.anchorMax = Vector2.one;
            headerRect.sizeDelta = Vector2.zero;
            _headerTitleText = headerObj.GetComponent<TextMeshProUGUI>();
            _headerTitleText.text = "BAN PHƯỚC TẾ ĐÀN";
            _headerTitleText.fontSize = 28f;
            _headerTitleText.fontStyle = FontStyles.Bold;
            _headerTitleText.alignment = TextAlignmentOptions.Center;
            _headerTitleText.color = new Color(0.0f, 0.95f, 1.0f);

            var subObj = new GameObject("HeaderSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(_panelRoot.transform, false);
            var subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.79f);
            subRect.anchorMax = new Vector2(0.5f, 0.79f);
            subRect.sizeDelta = new Vector2(900f, 32f);
            _headerSubtitleText = subObj.GetComponent<TextMeshProUGUI>();
            _headerSubtitleText.text = "Chọn 1 trong 3 nâng cấp vĩnh viễn (Bấm [1], [2], [3] hoặc Click)";
            _headerSubtitleText.fontSize = 17f;
            _headerSubtitleText.alignment = TextAlignmentOptions.Center;
            _headerSubtitleText.color = new Color(0.8f, 0.88f, 0.95f);

            // 3. Container chứa 3 thẻ
            var cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));
            cardsContainer.transform.SetParent(_panelRoot.transform, false);
            var cardsRect = cardsContainer.GetComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.5f, 0.44f);
            cardsRect.anchorMax = new Vector2(0.5f, 0.44f);
            cardsRect.sizeDelta = new Vector2(960f, 360f);

            float cardWidth = 290f;
            float spacing = 35f;
            float startX = -((cardWidth * 3 + spacing * 2) / 2f) + cardWidth / 2f;

            var frameMidSprite = Resources.Load<Sprite>("GUI_Parts/Gui_parts/Frame_mid");
            var buttonSprite = Resources.Load<Sprite>("GUI_Parts/Gui_parts/button");

            for (int i = 0; i < 3; i++)
            {
                var cardObj = new GameObject($"ChoiceCard_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                cardObj.transform.SetParent(cardsContainer.transform, false);
                var cRect = cardObj.GetComponent<RectTransform>();
                cRect.sizeDelta = new Vector2(cardWidth, 340f);
                cRect.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0f);

                var cardBg = cardObj.GetComponent<Image>();
                if (frameMidSprite != null)
                {
                    cardBg.sprite = frameMidSprite;
                    cardBg.type = Image.Type.Sliced;
                }
                cardBg.color = Color.white;
                _cardBgImages[i] = cardBg;

                var btn = cardObj.GetComponent<Button>();
                btn.transition = Selectable.Transition.None; // Không làm trắng hay đổi màu chữ khi rê chuột vào

                // Badge kèm Khung name_bar
                var badgeBarObj = new GameObject("BadgeBar", typeof(RectTransform), typeof(Image));
                badgeBarObj.transform.SetParent(cardObj.transform, false);
                var bBarRect = badgeBarObj.GetComponent<RectTransform>();
                bBarRect.anchorMin = new Vector2(0.5f, 0.90f);
                bBarRect.anchorMax = new Vector2(0.5f, 0.90f);
                bBarRect.sizeDelta = new Vector2(210f, 32f);
                var bBarImg = badgeBarObj.GetComponent<Image>();
                if (nameBarSprite != null)
                {
                    bBarImg.sprite = nameBarSprite;
                    bBarImg.type = Image.Type.Sliced;
                }

                var badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));
                badgeObj.transform.SetParent(badgeBarObj.transform, false);
                var bRect = badgeObj.GetComponent<RectTransform>();
                bRect.anchorMin = Vector2.zero;
                bRect.anchorMax = Vector2.one;
                bRect.sizeDelta = Vector2.zero;
                var bTmp = badgeObj.GetComponent<TextMeshProUGUI>();
                bTmp.text = $"[LỰA CHỌN {i + 1}]";
                bTmp.fontSize = 15f;
                bTmp.fontStyle = FontStyles.Bold;
                bTmp.alignment = TextAlignmentOptions.Center;
                bTmp.color = new Color(0.35f, 0.85f, 1f);

                // Star Rating
                var starObj = new GameObject("StarRating", typeof(RectTransform), typeof(TextMeshProUGUI));
                starObj.transform.SetParent(cardObj.transform, false);
                var sRect = starObj.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0.5f, 0.79f);
                sRect.anchorMax = new Vector2(0.5f, 0.79f);
                sRect.sizeDelta = new Vector2(260f, 28f);
                var sTmp = starObj.GetComponent<TextMeshProUGUI>();
                sTmp.text = "★★★ (200% Thưởng)";
                sTmp.fontSize = 22f;
                sTmp.fontStyle = FontStyles.Bold;
                sTmp.alignment = TextAlignmentOptions.Center;
                sTmp.color = new Color(1f, 0.85f, 0.2f);

                // Title
                var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(cardObj.transform, false);
                var tRect = titleObj.GetComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0.5f, 0.64f);
                tRect.anchorMax = new Vector2(0.5f, 0.64f);
                tRect.sizeDelta = new Vector2(260f, 48f);
                var tTmp = titleObj.GetComponent<TextMeshProUGUI>();
                tTmp.text = "Tên Chỉ Số";
                tTmp.fontSize = 22f;
                tTmp.fontStyle = FontStyles.Bold;
                tTmp.alignment = TextAlignmentOptions.Center;
                tTmp.color = Color.white;

                // Desc
                var descObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(cardObj.transform, false);
                var dRect = descObj.GetComponent<RectTransform>();
                dRect.anchorMin = new Vector2(0.5f, 0.40f);
                dRect.anchorMax = new Vector2(0.5f, 0.40f);
                dRect.sizeDelta = new Vector2(250f, 85f);
                var dTmp = descObj.GetComponent<TextMeshProUGUI>();
                dTmp.text = "+ Chi tiết nâng cấp...";
                dTmp.fontSize = 16f;
                dTmp.fontStyle = FontStyles.Bold;
                dTmp.alignment = TextAlignmentOptions.Center;
                dTmp.color = Color.white;

                // Nút bấm đồ họa từ GUI_Parts/button
                var btnVisualObj = new GameObject("ButtonVisual", typeof(RectTransform), typeof(Image));
                btnVisualObj.transform.SetParent(cardObj.transform, false);
                var btnVisualRect = btnVisualObj.GetComponent<RectTransform>();
                btnVisualRect.anchorMin = new Vector2(0.5f, 0.12f);
                btnVisualRect.anchorMax = new Vector2(0.5f, 0.12f);
                btnVisualRect.sizeDelta = new Vector2(190f, 42f);
                var btnVisualImg = btnVisualObj.GetComponent<Image>();
                if (buttonSprite != null)
                {
                    btnVisualImg.sprite = buttonSprite;
                    btnVisualImg.type = Image.Type.Sliced;
                }

                // Button Prompt
                var promptObj = new GameObject("KeyPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
                promptObj.transform.SetParent(btnVisualObj.transform, false);
                var pRect = promptObj.GetComponent<RectTransform>();
                pRect.anchorMin = Vector2.zero;
                pRect.anchorMax = Vector2.one;
                pRect.sizeDelta = Vector2.zero;
                var pTmp = promptObj.GetComponent<TextMeshProUGUI>();
                pTmp.text = "NÂNG CẤP";
                pTmp.fontSize = 15f;
                pTmp.fontStyle = FontStyles.Bold;
                pTmp.alignment = TextAlignmentOptions.Center;
                pTmp.color = Color.white;

                _cardContainers[i] = cardObj;
                _cardBadges[i] = bTmp;
                _cardStarTexts[i] = sTmp;
                _cardTitles[i] = tTmp;
                _cardDescriptions[i] = dTmp;
                _cardButtons[i] = btn;
            }

            _panelRoot.SetActive(false);
        }

        private void OnGUI()
        {
            if (!_isOpen || _currentOptions == null || _currentOptions.Count < 3) return;

            // Nếu uGUI panel đang hiển thị hợp lệ thì KHÔNG vẽ OnGUI để tránh vẽ đè 2 lần làm mờ chữ
            if (_panelRoot != null && _panelRoot.activeInHierarchy && _cardContainers != null && _cardContainers.Length >= 3 && _cardContainers[0] != null && _cardContainers[0].activeInHierarchy)
            {
                return;
            }

            DrawFallbackModalGUI();
        }

        /// <summary>
        /// Vẽ nhãn văn bản có bóng đổ nhẹ 1px giúp chữ nổi khối và sắc nét, không bị nghẹt nét chữ tiếng Việt có dấu.
        /// </summary>
        private void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color textColor, Color? shadowColor = null)
        {
            Color sc = shadowColor ?? new Color(0f, 0f, 0f, 0.90f);
            Color origColor = style.normal.textColor;

            style.normal.textColor = sc;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
            style.normal.textColor = origColor;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void DrawBorder(Rect rect, float thickness, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }

        private void InitFallbackStyles()
        {
            if (_stylesInit) return;

            _fallbackTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.0f, 0.98f, 1.0f) }
            };

            _fallbackBadgeNumStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _fallbackSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.95f, 1.0f) }
            };

            _fallbackCardBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.88f, 1f) }
            };

            _fallbackCardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _fallbackCardDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.98f, 1f) }
            };

            _fallbackStarStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _fallbackBtnLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _fallbackDisabledStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.45f, 0.45f) }
            };

            _fallbackRerollStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            _fallbackHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.88f, 0.98f) }
            };

            _fallbackCloseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _stylesInit = true;
        }

        private void DrawFallbackModalGUI()
        {
            InitFallbackStyles();

            float panelW = Mathf.Min(1020f, Screen.width * 0.90f);
            float panelH = Mathf.Min(560f, Screen.height * 0.80f);
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // 1. Vẽ nền tối toàn màn hình
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // 2. Vẽ khung chính sử dụng _bigBgTex đã cache
            GUI.color = Color.white;
            if (_bigBgTex != null)
            {
                GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _bigBgTex);
            }
            else
            {
                GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none);
            }

            // 3. Vẽ Tiêu đề với Khung hình chữ nhật trang nhã
            float barW = Mathf.Min(460f, panelW * 0.50f);
            float barH = 48f;
            float barX = panelX + (panelW - barW) * 0.5f;
            float barY = panelY + 16f;
            Rect titleRect = new Rect(barX, barY, barW, barH);

            Color titleBg = _shrineType == ShrineType.WeaponModifierShrine
                ? new Color(0.12f, 0.03f, 0.03f, 0.95f)
                : new Color(0.03f, 0.08f, 0.14f, 0.95f);
            DrawRect(titleRect, titleBg);

            Color titleBorder = _shrineType == ShrineType.WeaponModifierShrine
                ? new Color(0.85f, 0.35f, 0.15f, 0.95f)
                : new Color(0.0f, 0.85f, 1.0f, 0.95f);
            DrawBorder(titleRect, 2f, titleBorder);
            DrawBorder(new Rect(titleRect.x + 3f, titleRect.y + 3f, titleRect.width - 6f, titleRect.height - 6f), 1f, new Color(titleBorder.r, titleBorder.g, titleBorder.b, 0.35f));

            string titleStr = _shrineType == ShrineType.WeaponModifierShrine ? "BÀN CƯỜNG HÓA VŨ KHÍ" : "BAN PHƯỚC TẾ ĐÀN";
            DrawShadowLabel(titleRect, titleStr, _fallbackTitleStyle, new Color(0.0f, 0.98f, 1.0f));

            // 3b. VẼ BADGE SỐ LƯỢNG PHA LÊ (GÓC TRÊN BÊN PHẢI: CHỈ ĐỂ ẢNH VÀ SỐ LƯỢNG)
            var playerStats = _cachedPlayerStats != null ? _cachedPlayerStats : (PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.GetComponent<PlayerStats>() : null);
            Texture2D curCrystalTex = _shrineType == ShrineType.WeaponModifierShrine ? _greenCrystalTex : _redCrystalTex;
            int curCrystals = playerStats != null ? playerStats.GetCrystals(_shrineType) : 0;
            Color crystalColor = _shrineType == ShrineType.WeaponModifierShrine ? new Color(0.25f, 1f, 0.45f) : new Color(1f, 0.35f, 0.35f);

            float badgeW = 85f;
            float badgeH = 38f;
            Rect crystalBadgeRect = new Rect(panelX + panelW - badgeW - 25f, barY + 5f, badgeW, badgeH);
            DrawRect(crystalBadgeRect, new Color(0.04f, 0.08f, 0.14f, 0.92f));
            DrawBorder(crystalBadgeRect, 1.2f, crystalColor);

            if (curCrystalTex != null)
            {
                GUI.DrawTexture(new Rect(crystalBadgeRect.x + 8f, crystalBadgeRect.y + (badgeH - 26f) * 0.5f, 20f, 26f), curCrystalTex, ScaleMode.ScaleToFit);
            }
            _fallbackBadgeNumStyle.normal.textColor = crystalColor;
            DrawShadowLabel(new Rect(crystalBadgeRect.x + 34f, crystalBadgeRect.y, badgeW - 36f, badgeH), $"x{curCrystals}", _fallbackBadgeNumStyle, crystalColor);

            // Subtitle tự nhiên
            string subText = _shrineType == ShrineType.WeaponModifierShrine
                ? $"Mỗi lần nâng cấp tốn {_upgradeCost} Pha Lê  •  Làm mới tốn {_rerollCost} Pha Lê"
                : $"Mỗi lần ban phước tốn {_upgradeCost} Pha Lê  •  Làm mới tốn {_rerollCost} Pha Lê";
            DrawShadowLabel(new Rect(panelX, barY + 54f, panelW, 26f), subText, _fallbackSubStyle, new Color(0.9f, 0.95f, 1.0f));

            // 4. Vẽ 3 Thẻ lựa chọn dùng _frameMidTex & _buttonTex đã cache
            float cardW = (panelW - 80f) / 3f;
            float cardH = panelH * 0.64f;
            float startX = panelX + 25f;
            float cardY = panelY + panelH * 0.20f;

            bool canAffordUpgrade = playerStats != null && playerStats.CanAfford(_shrineType, _upgradeCost);

            for (int i = 0; i < 3 && i < _currentOptions.Count; i++)
            {
                var opt = _currentOptions[i];
                Rect cardRect = new Rect(startX + i * (cardW + 15f), cardY, cardW, cardH);
                bool isCardHovered = cardRect.Contains(Event.current.mousePosition);

                // 1. Vẽ nền thẻ bằng _frameMidTex
                if (_frameMidTex != null)
                {
                    GUI.DrawTexture(cardRect, _frameMidTex);
                }
                else
                {
                    GUI.Box(cardRect, GUIContent.none);
                }

                // 2. Highlight viền khi trỏ chuột vào:
                if (isCardHovered)
                {
                    Color highlightBorder = opt.Stars == 3 ? new Color(1f, 0.88f, 0.2f, 1f) : new Color(0.1f, 0.95f, 1f, 1f);
                    DrawBorder(cardRect, 3f, highlightBorder);
                    DrawBorder(new Rect(cardRect.x + 3f, cardRect.y + 3f, cardW - 6f, cardH - 6f), 1f, new Color(highlightBorder.r, highlightBorder.g, highlightBorder.b, 0.45f));
                }
                else
                {
                    DrawBorder(cardRect, 1f, new Color(0.35f, 0.45f, 0.55f, 0.45f));
                }

                // 3. Vẽ Badge Bar (Hình chữ nhật trang nhã)
                float cBarW = cardW * 0.65f;
                float cBarH = 26f;
                float cBarX = cardRect.x + (cardW - cBarW) * 0.5f;
                float cBarY = cardRect.y + 14f;
                Rect cBarRect = new Rect(cBarX, cBarY, cBarW, cBarH);
                DrawRect(cBarRect, new Color(0.04f, 0.08f, 0.12f, 0.88f));
                DrawBorder(cBarRect, 1.2f, new Color(0.2f, 0.7f, 0.9f, 0.75f));
                DrawShadowLabel(cBarRect, opt.BadgeText, _fallbackCardBadgeStyle, new Color(0.35f, 0.88f, 1f));

                // 4. Hàng Sao phẩm chất: ★★★ / ★★☆ / ★☆☆
                Color starColor = opt.Stars == 3 ? new Color(1f, 0.88f, 0.2f) : (opt.Stars == 2 ? new Color(0.35f, 0.85f, 1f) : new Color(0.9f, 0.95f, 1f));
                _fallbackStarStyle.normal.textColor = starColor;
                DrawShadowLabel(new Rect(cardRect.x + 10f, cardRect.y + 46f, cardW - 20f, 28f), opt.GetStarIcons(), _fallbackStarStyle, starColor);

                // 5. Tiêu đề
                DrawShadowLabel(new Rect(cardRect.x + 10f, cardRect.y + 76f, cardW - 20f, 44f), opt.Title, _fallbackCardTitleStyle, Color.white);

                // 6. Mô tả chỉ số
                DrawShadowLabel(new Rect(cardRect.x + 15f, cardRect.y + 124f, cardW - 30f, 110f), opt.Description, _fallbackCardDescStyle, new Color(0.95f, 0.98f, 1f));

                // 7. Nút Nâng Cấp kèm icon Pha Lê thật
                float btnW = cardW * 0.82f;
                float btnH = 42f;
                float btnX = cardRect.x + (cardW - btnW) * 0.5f;
                float btnY = cardRect.y + cardH - btnH - 16f;
                Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

                if (canAffordUpgrade)
                {
                    if (_buttonTex != null)
                    {
                        GUI.DrawTexture(btnRect, _buttonTex);
                    }
                    else
                    {
                        DrawRect(btnRect, new Color(0.18f, 0.08f, 0.05f, 0.9f));
                    }

                    bool isBtnHovered = btnRect.Contains(Event.current.mousePosition);
                    if (isBtnHovered)
                    {
                        DrawBorder(btnRect, 2f, new Color(1f, 0.9f, 0.3f, 1f));
                    }

                    string btnText = $"Nâng Cấp  (-{_upgradeCost})";
                    Vector2 textSize = _fallbackBtnLabelStyle.CalcSize(new GUIContent(btnText));
                    float iconSize = 22f;
                    float totalContentW = textSize.x + 4f + iconSize;
                    float contentStartX = btnRect.x + (btnRect.width - totalContentW) * 0.5f;

                    DrawShadowLabel(new Rect(contentStartX, btnRect.y, textSize.x, btnRect.height), btnText, _fallbackBtnLabelStyle, Color.white);
                    if (curCrystalTex != null)
                    {
                        GUI.DrawTexture(new Rect(contentStartX + textSize.x + 4f, btnRect.y + (btnRect.height - iconSize) * 0.5f, iconSize, iconSize), curCrystalTex, ScaleMode.ScaleToFit);
                    }
                }
                else
                {
                    // Không đủ pha lê
                    DrawRect(btnRect, new Color(0.2f, 0.05f, 0.05f, 0.8f));
                    DrawBorder(btnRect, 1.5f, new Color(0.8f, 0.2f, 0.2f, 0.8f));
                    DrawShadowLabel(btnRect, "Chưa Đủ Pha Lê", _fallbackDisabledStyle, new Color(1f, 0.45f, 0.45f));
                }

                // Xử lý Click trên thẻ hoặc nút
                if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
                {
                    if (canAffordUpgrade)
                    {
                        TrySelectChoice(i);
                    }
                    Event.current.Use();
                }
            }

            // ================= 5. THANH ĐIỀU KHIỂN DƯỚI (BOTTOM BAR: LÀM MỚI & ĐÓNG) =================
            float bottomY = panelY + panelH - 46f;

            // Nút Làm Mới (Reroll)
            float rerollW = 200f;
            float rerollH = 36f;
            Rect rerollRect = new Rect(startX, bottomY, rerollW, rerollH);
            bool canReroll = playerStats != null && playerStats.CanAfford(_shrineType, _rerollCost);

            DrawRect(rerollRect, canReroll ? new Color(0.06f, 0.12f, 0.18f, 0.92f) : new Color(0.12f, 0.06f, 0.06f, 0.85f));
            DrawBorder(rerollRect, 1.5f, canReroll ? new Color(0.0f, 0.85f, 1.0f, 0.9f) : new Color(0.7f, 0.2f, 0.2f, 0.6f));

            if (rerollRect.Contains(Event.current.mousePosition) && canReroll)
            {
                DrawBorder(rerollRect, 2.5f, new Color(1f, 0.88f, 0.2f, 1f));
            }

            _fallbackRerollStyle.alignment = canReroll ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _fallbackRerollStyle.normal.textColor = canReroll ? new Color(0.0f, 0.98f, 1.0f) : new Color(0.85f, 0.4f, 0.4f);

            if (canReroll)
            {
                string rerollText = $"Làm Mới  (-{_rerollCost})";
                Vector2 rSize = _fallbackRerollStyle.CalcSize(new GUIContent(rerollText));
                float iconSize = 20f;
                float totalContentW = rSize.x + 4f + iconSize;
                float startRerollX = rerollRect.x + (rerollRect.width - totalContentW) * 0.5f;

                DrawShadowLabel(new Rect(startRerollX, rerollRect.y, rSize.x, rerollRect.height), rerollText, _fallbackRerollStyle, _fallbackRerollStyle.normal.textColor);
                if (curCrystalTex != null)
                {
                    GUI.DrawTexture(new Rect(startRerollX + rSize.x + 4f, rerollRect.y + (rerollRect.height - iconSize) * 0.5f, iconSize, iconSize), curCrystalTex, ScaleMode.ScaleToFit);
                }
            }
            else
            {
                DrawShadowLabel(rerollRect, "Chưa Đủ Pha Lê Làm Mới", _fallbackRerollStyle, _fallbackRerollStyle.normal.textColor);
            }

            if (Event.current.type == EventType.MouseDown && rerollRect.Contains(Event.current.mousePosition))
            {
                if (canReroll)
                {
                    TryReroll();
                }
                Event.current.Use();
            }

            // Dòng hướng dẫn phím ở giữa
            DrawShadowLabel(new Rect(startX + rerollW + 10f, bottomY + 6f, panelW - rerollW - 150f, 24f), 
                "Phím tắt: 1, 2, 3 Nâng cấp • R Làm mới • ESC Đóng", _fallbackHintStyle, new Color(0.8f, 0.88f, 0.98f));

            // Nút Đóng (Close)
            float closeW = 100f;
            float closeH = 36f;
            Rect closeRect = new Rect(panelX + panelW - closeW - 25f, bottomY, closeW, closeH);
            DrawRect(closeRect, new Color(0.12f, 0.08f, 0.1f, 0.92f));
            DrawBorder(closeRect, 1.2f, new Color(0.85f, 0.45f, 0.45f, 0.8f));

            if (closeRect.Contains(Event.current.mousePosition))
            {
                DrawBorder(closeRect, 2f, new Color(1f, 0.35f, 0.35f, 1f));
            }

            DrawShadowLabel(closeRect, "Đóng", _fallbackCloseStyle, Color.white);

            if (Event.current.type == EventType.MouseDown && closeRect.Contains(Event.current.mousePosition))
            {
                CloseModal();
                Event.current.Use();
            }
        }
    }
}
