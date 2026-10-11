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
        [SerializeField] private TextMeshProUGUI[] _cardBadges = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _cardTitles = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[3];
        [SerializeField] private Button[] _cardButtons = new Button[3];

        private Action<int> _onChoiceSelectedCallback;
        private bool _isOpen = false;
        private List<UpgradeOptionData> _currentOptions = new List<UpgradeOptionData>();

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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

            // Bắt phím tắt nhanh 1, 2, 3 để chọn thẻ
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    SelectChoice(0);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    SelectChoice(1);
                }
                else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                {
                    SelectChoice(2);
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseModal();
                }
            }
        }

        /// <summary>
        /// Mở modal hiển thị 3 lựa chọn nâng cấp chỉ số.
        /// </summary>
        public void Open(string title, string subtitle, List<UpgradeOptionData> options, Action<int> onSelected)
        {
            EnsureUIHierarchy();

            _currentOptions = options ?? new List<UpgradeOptionData>();
            _onChoiceSelectedCallback = onSelected;

            if (_headerTitleText != null) _headerTitleText.text = title;
            if (_headerSubtitleText != null) _headerSubtitleText.text = subtitle;

            for (int i = 0; i < 3; i++)
            {
                if (i < _currentOptions.Count)
                {
                    var opt = _currentOptions[i];
                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(true);
                    if (_cardBadges[i] != null) _cardBadges[i].text = opt.BadgeText;
                    if (_cardTitles[i] != null) _cardTitles[i].text = opt.Title;
                    if (_cardDescriptions[i] != null) _cardDescriptions[i].text = opt.Description;

                    int index = i;
                    if (_cardButtons[i] != null)
                    {
                        _cardButtons[i].onClick.RemoveAllListeners();
                        _cardButtons[i].onClick.AddListener(() => SelectChoice(index));
                    }
                }
                else
                {
                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(false);
                }
            }

            _isOpen = true;
            if (_panelRoot != null) _panelRoot.SetActive(true);

            // Mở khóa chuột để người chơi bấm nút
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Tắt tạm thời việc xoay camera trên PlayerMovement cục bộ
            SetPlayerMovementLookEnabled(false);
        }

        public void SelectChoice(int index)
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerMovementLookEnabled(true);

            _onChoiceSelectedCallback?.Invoke(index);
            _onChoiceSelectedCallback = null;
        }

        public void CloseModal()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerMovementLookEnabled(true);

            _onChoiceSelectedCallback = null;
        }

        private void SetPlayerMovementLookEnabled(bool enabled)
        {
            var playerMove = FindObjectOfType<PlayerMovement>();
            if (playerMove != null)
            {
                playerMove.SetCursorLock(enabled);
            }
        }

        /// <summary>
        /// Tự động sinh giao diện 3 thẻ nâng cấp nếu Scene chưa cấu hình thủ công.
        /// </summary>
        private void EnsureUIHierarchy()
        {
            if (_panelRoot != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    transform.SetParent(canvas.transform, false);
                }
            }

            if (canvas == null) return;

            // 1. Tạo Root Panel tối mờ nền
            _panelRoot = new GameObject("ShrineChoiceModalPanel");
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var bgImg = _panelRoot.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.05f, 0.85f);

            // 2. Tiêu đề
            var headerObj = new GameObject("HeaderTitle");
            headerObj.transform.SetParent(_panelRoot.transform, false);
            var headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 0.85f);
            headerRect.anchorMax = new Vector2(0.5f, 0.85f);
            headerRect.sizeDelta = new Vector2(800f, 60f);
            _headerTitleText = headerObj.AddComponent<TextMeshProUGUI>();
            _headerTitleText.text = "BAN PHƯỚC TẾ ĐÀN";
            _headerTitleText.fontSize = 32f;
            _headerTitleText.fontStyle = FontStyles.Bold;
            _headerTitleText.alignment = TextAlignmentOptions.Center;
            _headerTitleText.color = new Color(1f, 0.85f, 0.3f);

            var subObj = new GameObject("HeaderSubtitle");
            subObj.transform.SetParent(_panelRoot.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.78f);
            subRect.anchorMax = new Vector2(0.5f, 0.78f);
            subRect.sizeDelta = new Vector2(800f, 40f);
            _headerSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
            _headerSubtitleText.text = "Chọn 1 trong 3 nâng cấp vĩnh viễn (Phím [1], [2], [3] hoặc Click)";
            _headerSubtitleText.fontSize = 18f;
            _headerSubtitleText.alignment = TextAlignmentOptions.Center;
            _headerSubtitleText.color = new Color(0.8f, 0.8f, 0.8f);

            // 3. Container chứa 3 thẻ
            var cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(_panelRoot.transform, false);
            var cardsRect = cardsContainer.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.5f, 0.45f);
            cardsRect.anchorMax = new Vector2(0.5f, 0.45f);
            cardsRect.sizeDelta = new Vector2(950f, 320f);

            float cardWidth = 280f;
            float spacing = 35f;
            float startX = -((cardWidth * 3 + spacing * 2) / 2f) + cardWidth / 2f;

            for (int i = 0; i < 3; i++)
            {
                var cardObj = new GameObject($"ChoiceCard_{i + 1}");
                cardObj.transform.SetParent(cardsContainer.transform, false);
                var cRect = cardObj.AddComponent<RectTransform>();
                cRect.sizeDelta = new Vector2(cardWidth, 300f);
                cRect.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0f);

                var cardBg = cardObj.AddComponent<Image>();
                cardBg.color = new Color(0.12f, 0.14f, 0.22f, 0.95f);

                var btn = cardObj.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.2f, 0.35f, 0.55f, 1f);
                colors.pressedColor = new Color(0.1f, 0.5f, 0.8f, 1f);
                btn.colors = colors;

                // Badge
                var badgeObj = new GameObject("Badge");
                badgeObj.transform.SetParent(cardObj.transform, false);
                var bRect = badgeObj.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.5f, 0.9f);
                bRect.anchorMax = new Vector2(0.5f, 0.9f);
                bRect.sizeDelta = new Vector2(240f, 30f);
                var bTmp = badgeObj.AddComponent<TextMeshProUGUI>();
                bTmp.text = $"[LỰA CHỌN {i + 1}]";
                bTmp.fontSize = 14f;
                bTmp.alignment = TextAlignmentOptions.Center;
                bTmp.color = new Color(0.4f, 0.8f, 1f);

                // Title
                var titleObj = new GameObject("Title");
                titleObj.transform.SetParent(cardObj.transform, false);
                var tRect = titleObj.AddComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0.5f, 0.68f);
                tRect.anchorMax = new Vector2(0.5f, 0.68f);
                tRect.sizeDelta = new Vector2(250f, 60f);
                var tTmp = titleObj.AddComponent<TextMeshProUGUI>();
                tTmp.text = "Tên Nâng Cấp";
                tTmp.fontSize = 18f;
                tTmp.fontStyle = FontStyles.Bold;
                tTmp.alignment = TextAlignmentOptions.Center;
                tTmp.color = Color.white;

                // Desc
                var descObj = new GameObject("Description");
                descObj.transform.SetParent(cardObj.transform, false);
                var dRect = descObj.AddComponent<RectTransform>();
                dRect.anchorMin = new Vector2(0.5f, 0.35f);
                dRect.anchorMax = new Vector2(0.5f, 0.35f);
                dRect.sizeDelta = new Vector2(240f, 100f);
                var dTmp = descObj.AddComponent<TextMeshProUGUI>();
                dTmp.text = "+ Chỉ số chi tiết";
                dTmp.fontSize = 15f;
                dTmp.alignment = TextAlignmentOptions.Center;
                dTmp.color = new Color(0.9f, 0.9f, 0.9f);

                // Button Prompt
                var promptObj = new GameObject("KeyPrompt");
                promptObj.transform.SetParent(cardObj.transform, false);
                var pRect = promptObj.AddComponent<RectTransform>();
                pRect.anchorMin = new Vector2(0.5f, 0.1f);
                pRect.anchorMax = new Vector2(0.5f, 0.1f);
                pRect.sizeDelta = new Vector2(200f, 30f);
                var pTmp = promptObj.AddComponent<TextMeshProUGUI>();
                pTmp.text = $"Bấm [{i + 1}] để chọn";
                pTmp.fontSize = 14f;
                pTmp.alignment = TextAlignmentOptions.Center;
                pTmp.color = new Color(1f, 0.85f, 0.3f);

                _cardContainers[i] = cardObj;
                _cardBadges[i] = bTmp;
                _cardTitles[i] = tTmp;
                _cardDescriptions[i] = dTmp;
                _cardButtons[i] = btn;
            }

            _panelRoot.SetActive(false);
        }
    }
}
