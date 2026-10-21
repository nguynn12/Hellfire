// Script: ShopModalUI.cs
// Mục đích: Quản lý giao diện Cửa Hàng (Shop Modal UI) hiển thị 5 món hàng bày chung, mua riêng từng người (Mục 17.8).
//           Hỗ trợ click chuột hoặc phím tắt nhanh [1], [2], [3], [4], [5], [ESC]. Không làm pause game.
// Môi trường thực thi: Client (UI hiển thị cục bộ cho người chơi tương tác).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Items;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class ShopModalUI : MonoBehaviour
    {
        public static ShopModalUI Instance { get; private set; }

        [Header("UI Panel Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TextMeshProUGUI _headerTitleText;
        [SerializeField] private TextMeshProUGUI _headerSubtitleText;
        [SerializeField] private TextMeshProUGUI _soulsBalanceText;
        [SerializeField] private TextMeshProUGUI _toastNotificationText;
        [SerializeField] private Button _closeButton;

        [Header("Offer Cards (5 Slots)")]
        [SerializeField] private GameObject[] _cardContainers = new GameObject[5];
        [SerializeField] private TextMeshProUGUI[] _cardBadges = new TextMeshProUGUI[5];
        [SerializeField] private TextMeshProUGUI[] _cardTitles = new TextMeshProUGUI[5];
        [SerializeField] private TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[5];
        [SerializeField] private TextMeshProUGUI[] _cardPrices = new TextMeshProUGUI[5];
        [SerializeField] private Button[] _cardButtons = new Button[5];
        [SerializeField] private TextMeshProUGUI[] _cardButtonTexts = new TextMeshProUGUI[5];

        private ShopStation _currentStation;
        private bool _isOpen = false;
        private Coroutine _toastCoroutine;
        private PlayerWallet _localWallet;

        public bool IsOpen => _isOpen;
        public ShopStation CurrentStation => _currentStation;

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

        private void OnDestroy()
        {
            if (_localWallet != null)
            {
                _localWallet.OnSoulsChanged -= HandleSoulsChanged;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Bắt phím tắt nhanh 1, 2, 3, 4, 5 để mua đồ
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    TryBuyOffer(0);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    TryBuyOffer(1);
                }
                else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                {
                    TryBuyOffer(2);
                }
                else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
                {
                    TryBuyOffer(3);
                }
                else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
                {
                    TryBuyOffer(4);
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseModal();
                }
            }
        }

        /// <summary>
        /// Mở modal Shop với trạm ShopStation tương ứng.
        /// </summary>
        public void Open(ShopStation station)
        {
            EnsureUIHierarchy();

            _currentStation = station;
            _isOpen = true;

            HookLocalWallet();
            RefreshAllCards();

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }

            // Mở khóa chuột để người chơi bấm nút (Mục 17.8)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Tắt tạm xoay camera nhìn
            SetPlayerMovementLookEnabled(false);
        }

        public void CloseModal()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerMovementLookEnabled(true);

            _currentStation = null;
        }

        private void HookLocalWallet()
        {
            if (_localWallet != null)
            {
                _localWallet.OnSoulsChanged -= HandleSoulsChanged;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (playerObj != null)
                {
                    _localWallet = playerObj.GetComponent<PlayerWallet>();
                    if (_localWallet != null)
                    {
                        _localWallet.OnSoulsChanged += HandleSoulsChanged;
                    }
                }
            }
        }

        private void HandleSoulsChanged(int newSouls)
        {
            if (_isOpen)
            {
                RefreshAllCards();
            }
        }

        /// <summary>
        /// Làm mới toàn bộ 5 thẻ theo trạng thái của người chơi cục bộ.
        /// </summary>
        public void RefreshAllCards()
        {
            if (_currentStation == null || _currentStation.OfferIds == null) return;

            int currentSouls = (_localWallet != null) ? _localWallet.Souls.Value : 0;
            if (_soulsBalanceText != null)
            {
                _soulsBalanceText.text = $"Linh hồn hiện có: <color=#FFD700><b>{currentSouls}</b></color>";
            }

            WeaponController localWeaponController = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localWeaponController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<WeaponController>();
            }

            for (int i = 0; i < 5; i++)
            {
                if (i < _currentStation.OfferIds.Count)
                {
                    int itemId = _currentStation.OfferIds[i];
                    var itemInfo = _currentStation.GetItemInfo(itemId);

                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(true);
                    if (_cardBadges[i] != null)
                    {
                        _cardBadges[i].text = itemInfo.Category;
                        _cardBadges[i].color = itemInfo.ThemeColor;
                    }
                    if (_cardTitles[i] != null) _cardTitles[i].text = itemInfo.Name;
                    if (_cardDescriptions[i] != null) _cardDescriptions[i].text = itemInfo.Description;
                    if (_cardPrices[i] != null)
                    {
                        _cardPrices[i].text = $"💰 <b>{itemInfo.Price}</b> Linh hồn";
                    }

                    int offerIndex = i;
                    bool alreadyPurchased = _currentStation.IsPurchasedLocally(offerIndex);
                    bool alreadyOwnsWeapon = itemInfo.IsWeapon && itemInfo.WeaponData != null && ShopStation.HasWeapon(localWeaponController, itemInfo.WeaponData);
                    bool canAfford = currentSouls >= itemInfo.Price;

                    if (_cardButtons[i] != null && _cardButtonTexts[i] != null)
                    {
                        _cardButtons[i].onClick.RemoveAllListeners();

                        if (alreadyPurchased)
                        {
                            _cardButtons[i].interactable = false;
                            _cardButtonTexts[i].text = "ĐÃ MUA";
                            _cardButtonTexts[i].color = new Color(0.6f, 0.6f, 0.6f);
                        }
                        else if (alreadyOwnsWeapon)
                        {
                            _cardButtons[i].interactable = false;
                            _cardButtonTexts[i].text = "ĐÃ CÓ VŨ KHÍ";
                            _cardButtonTexts[i].color = new Color(0.7f, 0.5f, 0.3f);
                        }
                        else if (!canAfford)
                        {
                            _cardButtons[i].interactable = false;
                            _cardButtonTexts[i].text = "KHÔNG ĐỦ LINH HỒN";
                            _cardButtonTexts[i].color = new Color(0.9f, 0.3f, 0.3f);
                        }
                        else
                        {
                            _cardButtons[i].interactable = true;
                            _cardButtonTexts[i].text = $"MUA [{offerIndex + 1}]";
                            _cardButtonTexts[i].color = Color.white;
                            _cardButtons[i].onClick.AddListener(() => TryBuyOffer(offerIndex));
                        }
                    }
                }
                else
                {
                    if (_cardContainers[i] != null) _cardContainers[i].SetActive(false);
                }
            }
        }

        public void TryBuyOffer(int offerIndex)
        {
            if (!_isOpen || _currentStation == null) return;
            if (offerIndex < 0 || offerIndex >= _currentStation.OfferIds.Count) return;

            if (_currentStation.IsPurchasedLocally(offerIndex))
            {
                ShowToast("Bạn đã mua món này rồi!", new Color(0.9f, 0.4f, 0.4f));
                return;
            }

            int itemId = _currentStation.OfferIds[offerIndex];
            var info = _currentStation.GetItemInfo(itemId);

            int currentSouls = (_localWallet != null) ? _localWallet.Souls.Value : 0;
            if (currentSouls < info.Price)
            {
                ShowToast("Không đủ Linh hồn!", new Color(0.9f, 0.3f, 0.3f));
                return;
            }

            // Gửi ServerRpc yêu cầu mua
            _currentStation.BuyOfferServerRpc(offerIndex);
        }

        public void OnPurchaseResult(int offerIndex, bool success, string message)
        {
            if (success)
            {
                ShowToast(message, new Color(0.3f, 0.9f, 0.4f));
            }
            else
            {
                ShowToast(message, new Color(0.95f, 0.35f, 0.35f));
            }

            RefreshAllCards();
        }

        private void ShowToast(string message, Color color)
        {
            if (_toastNotificationText == null) return;

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }
            _toastCoroutine = StartCoroutine(ToastRoutine(message, color));
        }

        private IEnumerator ToastRoutine(string message, Color color)
        {
            _toastNotificationText.text = message;
            _toastNotificationText.color = color;
            _toastNotificationText.gameObject.SetActive(true);

            yield return new WaitForSeconds(2.5f);

            _toastNotificationText.gameObject.SetActive(false);
            _toastCoroutine = null;
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
        /// Tự động sinh giao diện 5 thẻ Shop nếu Scene chưa cấu hình thủ công.
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
            _panelRoot = new GameObject("ShopModalPanel");
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var bgImg = _panelRoot.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.07f, 0.90f);

            // 2. Tiêu đề
            var headerObj = new GameObject("ShopHeaderTitle");
            headerObj.transform.SetParent(_panelRoot.transform, false);
            var headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 0.90f);
            headerRect.anchorMax = new Vector2(0.5f, 0.90f);
            headerRect.sizeDelta = new Vector2(900f, 50f);
            _headerTitleText = headerObj.AddComponent<TextMeshProUGUI>();
            _headerTitleText.text = "CỬA HÀNG ĐỊA NGỤC (SHOP)";
            _headerTitleText.fontSize = 32f;
            _headerTitleText.fontStyle = FontStyles.Bold;
            _headerTitleText.alignment = TextAlignmentOptions.Center;
            _headerTitleText.color = new Color(1f, 0.85f, 0.2f);

            var subObj = new GameObject("ShopHeaderSubtitle");
            subObj.transform.SetParent(_panelRoot.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.84f);
            subRect.anchorMax = new Vector2(0.5f, 0.84f);
            subRect.sizeDelta = new Vector2(900f, 35f);
            _headerSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
            _headerSubtitleText.text = "Mua vũ khí & bùa lợi bằng Linh Hồn (Phím [1]-[5] hoặc Click, [ESC] để đóng)";
            _headerSubtitleText.fontSize = 16f;
            _headerSubtitleText.alignment = TextAlignmentOptions.Center;
            _headerSubtitleText.color = new Color(0.8f, 0.8f, 0.8f);

            // Số dư Linh hồn
            var soulsObj = new GameObject("ShopSoulsBalance");
            soulsObj.transform.SetParent(_panelRoot.transform, false);
            var soulsRect = soulsObj.AddComponent<RectTransform>();
            soulsRect.anchorMin = new Vector2(0.5f, 0.79f);
            soulsRect.anchorMax = new Vector2(0.5f, 0.79f);
            soulsRect.sizeDelta = new Vector2(600f, 35f);
            _soulsBalanceText = soulsObj.AddComponent<TextMeshProUGUI>();
            _soulsBalanceText.text = "Linh hồn hiện có: <color=#FFD700>0</color>";
            _soulsBalanceText.fontSize = 20f;
            _soulsBalanceText.alignment = TextAlignmentOptions.Center;
            _soulsBalanceText.color = Color.white;

            // Nút đóng góc phải
            var closeObj = new GameObject("ShopCloseButton");
            closeObj.transform.SetParent(_panelRoot.transform, false);
            var closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.92f, 0.90f);
            closeRect.anchorMax = new Vector2(0.92f, 0.90f);
            closeRect.sizeDelta = new Vector2(120f, 40f);
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.color = new Color(0.3f, 0.15f, 0.15f, 0.9f);
            _closeButton = closeObj.AddComponent<Button>();
            closeObj.AddComponent<UIButtonFeedback>();
            _closeButton.onClick.AddListener(CloseModal);

            var closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            var closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            var closeTmp = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "✕ Đóng [ESC]";
            closeTmp.fontSize = 15f;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // 3. Container chứa 5 thẻ
            var cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(_panelRoot.transform, false);
            var cardsRect = cardsContainer.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.5f, 0.45f);
            cardsRect.anchorMax = new Vector2(0.5f, 0.45f);
            cardsRect.sizeDelta = new Vector2(1150f, 360f);

            float cardWidth = 210f;
            float spacing = 20f;
            float startX = -((cardWidth * 5 + spacing * 4) / 2f) + cardWidth / 2f;

            for (int i = 0; i < 5; i++)
            {
                var cardObj = new GameObject($"OfferCard_{i + 1}");
                cardObj.transform.SetParent(cardsContainer.transform, false);
                var cRect = cardObj.AddComponent<RectTransform>();
                cRect.sizeDelta = new Vector2(cardWidth, 340f);
                cRect.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0f);

                var cardBg = cardObj.AddComponent<Image>();
                cardBg.color = new Color(0.10f, 0.13f, 0.20f, 0.95f);
                _cardContainers[i] = cardObj;

                // Badge / Category Chip
                var badgeObj = new GameObject("CategoryBadge");
                badgeObj.transform.SetParent(cardObj.transform, false);
                var bRect = badgeObj.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.5f, 0.91f);
                bRect.anchorMax = new Vector2(0.5f, 0.91f);
                bRect.sizeDelta = new Vector2(180f, 26f);
                var bTmp = badgeObj.AddComponent<TextMeshProUGUI>();
                bTmp.text = "VŨ KHÍ";
                bTmp.fontSize = 13f;
                bTmp.fontStyle = FontStyles.Bold;
                bTmp.alignment = TextAlignmentOptions.Center;
                _cardBadges[i] = bTmp;

                // Title
                var titleObj = new GameObject("Title");
                titleObj.transform.SetParent(cardObj.transform, false);
                var tRect = titleObj.AddComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0.5f, 0.77f);
                tRect.anchorMax = new Vector2(0.5f, 0.77f);
                tRect.sizeDelta = new Vector2(190f, 40f);
                var tTmp = titleObj.AddComponent<TextMeshProUGUI>();
                tTmp.text = "Tên Món Hàng";
                tTmp.fontSize = 17f;
                tTmp.fontStyle = FontStyles.Bold;
                tTmp.alignment = TextAlignmentOptions.Center;
                tTmp.color = Color.white;
                _cardTitles[i] = tTmp;

                // Description
                var descObj = new GameObject("Description");
                descObj.transform.SetParent(cardObj.transform, false);
                var dRect = descObj.AddComponent<RectTransform>();
                dRect.anchorMin = new Vector2(0.5f, 0.50f);
                dRect.anchorMax = new Vector2(0.5f, 0.50f);
                dRect.sizeDelta = new Vector2(190f, 100f);
                var dTmp = descObj.AddComponent<TextMeshProUGUI>();
                dTmp.text = "Mô tả chi tiết tác dụng của món đồ.";
                dTmp.fontSize = 13f;
                dTmp.alignment = TextAlignmentOptions.Top;
                dTmp.color = new Color(0.85f, 0.85f, 0.85f);
                dTmp.enableWordWrapping = true;
                _cardDescriptions[i] = dTmp;

                // Price
                var priceObj = new GameObject("Price");
                priceObj.transform.SetParent(cardObj.transform, false);
                var pRect = priceObj.AddComponent<RectTransform>();
                pRect.anchorMin = new Vector2(0.5f, 0.25f);
                pRect.anchorMax = new Vector2(0.5f, 0.25f);
                pRect.sizeDelta = new Vector2(190f, 30f);
                var pTmp = priceObj.AddComponent<TextMeshProUGUI>();
                pTmp.text = "💰 50 Linh hồn";
                pTmp.fontSize = 15f;
                pTmp.fontStyle = FontStyles.Bold;
                pTmp.alignment = TextAlignmentOptions.Center;
                pTmp.color = new Color(1f, 0.85f, 0.2f);
                _cardPrices[i] = pTmp;

                // Buy Button
                var btnObj = new GameObject("BuyButton");
                btnObj.transform.SetParent(cardObj.transform, false);
                var btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 0.10f);
                btnRect.anchorMax = new Vector2(0.5f, 0.10f);
                btnRect.sizeDelta = new Vector2(180f, 40f);
                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.15f, 0.45f, 0.75f, 0.95f);
                var btn = btnObj.AddComponent<Button>();
                btnObj.AddComponent<UIButtonFeedback>();
                _cardButtons[i] = btn;

                var btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);
                var btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;
                var btnTmp = btnTextObj.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"MUA [{i + 1}]";
                btnTmp.fontSize = 14f;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.color = Color.white;
                _cardButtonTexts[i] = btnTmp;
            }

            // 4. Toast Notification
            var toastObj = new GameObject("ShopToastText");
            toastObj.transform.SetParent(_panelRoot.transform, false);
            var toastRect = toastObj.AddComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 0.15f);
            toastRect.anchorMax = new Vector2(0.5f, 0.15f);
            toastRect.sizeDelta = new Vector2(800f, 40f);
            _toastNotificationText = toastObj.AddComponent<TextMeshProUGUI>();
            _toastNotificationText.text = "";
            _toastNotificationText.fontSize = 18f;
            _toastNotificationText.fontStyle = FontStyles.Bold;
            _toastNotificationText.alignment = TextAlignmentOptions.Center;
            _toastNotificationText.gameObject.SetActive(false);
        }
    }
}
