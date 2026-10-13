// Script: LoadingScreenUI.cs
// Mục đích: Màn hình loading khi GameState == Generating, spinner 360°/s, ReadyCount x/y, mẹo chơi đổi mỗi 4s, fade chuyển cảnh 0.3s-0.5s (Mục 16.4 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections;
using Hellfire.Dungeon;
using Hellfire.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        [Header("UI Panels & Groups")]
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private CanvasGroup _loadingCanvasGroup;
        [SerializeField] private Image _fadeOverlayImage;

        [Header("Loading Elements (Mục 16.4)")]
        [SerializeField] private RectTransform _spinnerRect;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _readyStatusText;
        [SerializeField] private TextMeshProUGUI _tipText;

        [Header("Tips (Mục 16.4: tối thiểu 6 mẹo tiếng Việt)")]
        private static readonly string[] GameplayTips = new string[]
        {
            "💡 Mẹo: Bắn vào đầu (Headshot) gây sát thương gấp đôi lên hầu hết mọi loại quái vật!",
            "💡 Mẹo: Hãy luôn đi cùng đồng đội. Khi đồng đội gục, tiến lại gần và giữ phím E để cứu!",
            "💡 Mẹo: Nhặt các bùa lợi (Power-Up) trong phòng để tăng tốc độ chạy, tăng máu hoặc nhận Khiên Bất Tử 8s!",
            "💡 Mẹo: Nhấn phím Tab để bật Bản đồ hầm ngục và kiểm tra vị trí phòng Boss bất cứ lúc nào!",
            "💡 Mẹo: Súng sẽ tự động nạp khi hết đạn, nhưng chủ động bấm R nạp trước sẽ giúp bạn an toàn hơn!",
            "💡 Mẹo: Ở Pha 2, Chúa Tể Địa Ngục sẽ di chuyển nhanh hơn, bắn cầu lửa và liên tục triệu hồi tay sai!",
            "💡 Mẹo: Bạn có thể tùy chỉnh góc nhìn FOV (70-110), âm lượng và bật Giảm Rung Màn Hình trong Cài đặt!"
        };

        private int _currentTipIndex = 0;
        private float _tipTimer = 0f;
        private Coroutine _fadeCoroutine;
        private Coroutine _sceneFadeCoroutine;
        private bool _isGameManagerHooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureInstance()
        {
            if (Instance == null)
            {
                var existing = FindFirstObjectByType<LoadingScreenUI>();
                if (existing == null)
                {
                    var go = new GameObject("LoadingScreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LoadingScreenUI));
                    var canvas = go.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 999; // Trên cùng mọi UI khác

                    var scaler = go.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;

                    DontDestroyOnLoad(go);
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildUIHierarchyIfMissing();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                HandleGameStateChanged(GameState.Lobby, GameManager.Instance.CurrentState.Value);
            }
            else
            {
                // Mặc định ẩn loading nếu đang ở MainMenu hoặc Lobby
                if (_loadingPanel != null) _loadingPanel.SetActive(false);
            }

            StartSceneFadeIn(0.3f);
        }

        private void Update()
        {
            // 1. Spinner xoay 360°/giây bằng Time.unscaledDeltaTime (Mục 16.4)
            if (_spinnerRect != null && _loadingPanel != null && _loadingPanel.activeSelf)
            {
                _spinnerRect.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            }

            // 2. Cập nhật mẹo chơi đổi mỗi 4 giây (Mục 16.4)
            if (_loadingPanel != null && _loadingPanel.activeSelf)
            {
                _tipTimer += Time.unscaledDeltaTime;
                if (_tipTimer >= 4.0f)
                {
                    _tipTimer = 0f;
                    _currentTipIndex = (_currentTipIndex + 1) % GameplayTips.Length;
                    if (_tipText != null)
                    {
                        _tipText.text = GameplayTips[_currentTipIndex];
                    }
                }

                // 3. Cập nhật trạng thái ReadyCount {ReadyCount}/{Tổng số người}
                UpdateReadyStatus();
            }

            // Đảm bảo gắn kết GameManager nếu GameManager vừa được spawn
            if (GameManager.Instance != null && !_isGameManagerHooked)
            {
                _isGameManagerHooked = true;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void UpdateReadyStatus()
        {
            if (_readyStatusText == null) return;

            int ready = 0;
            int total = 1;

            if (DungeonManager.Instance != null)
            {
                ready = DungeonManager.Instance.ReadyCount.Value;
            }

            if (NetworkManager.Singleton != null)
            {
                total = Mathf.Max(1, NetworkManager.Singleton.ConnectedClients.Count);
            }

            _readyStatusText.text = $"Đang tạo hầm ngục... {ready}/{total} sẵn sàng";
        }

        private void HandleGameStateChanged(GameState prev, GameState next)
        {
            if (next == GameState.Generating)
            {
                ShowLoading();
            }
            else if (next == GameState.Playing)
            {
                FadeOutLoading(0.5f);
            }
            else
            {
                if (_loadingPanel != null && _fadeCoroutine == null)
                {
                    _loadingPanel.SetActive(false);
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartSceneFadeIn(0.3f);
        }

        public void ShowLoading()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_loadingPanel != null)
            {
                _loadingPanel.SetActive(true);
            }

            if (_loadingCanvasGroup != null)
            {
                _loadingCanvasGroup.alpha = 1f;
            }

            _tipTimer = 0f;
            _currentTipIndex = Random.Range(0, GameplayTips.Length);
            if (_tipText != null)
            {
                _tipText.text = GameplayTips[_currentTipIndex];
            }

            UpdateReadyStatus();
        }

        public void FadeOutLoading(float duration = 0.5f)
        {
            if (_loadingPanel == null || !_loadingPanel.activeSelf) return;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            float elapsed = 0f;
            float startAlpha = _loadingCanvasGroup != null ? _loadingCanvasGroup.alpha : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (_loadingCanvasGroup != null)
                {
                    _loadingCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                yield return null;
            }

            if (_loadingCanvasGroup != null)
            {
                _loadingCanvasGroup.alpha = 0f;
            }

            if (_loadingPanel != null)
            {
                _loadingPanel.SetActive(false);
            }

            _fadeCoroutine = null;
        }

        public void StartSceneFadeIn(float duration = 0.3f)
        {
            if (_fadeOverlayImage == null) return;
            if (_sceneFadeCoroutine != null) StopCoroutine(_sceneFadeCoroutine);
            _sceneFadeCoroutine = StartCoroutine(SceneFadeInRoutine(duration));
        }

        private IEnumerator SceneFadeInRoutine(float duration)
        {
            _fadeOverlayImage.gameObject.SetActive(true);
            float elapsed = 0f;
            Color color = _fadeOverlayImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                _fadeOverlayImage.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            _fadeOverlayImage.color = new Color(color.r, color.g, color.b, 0f);
            _fadeOverlayImage.gameObject.SetActive(false);
            _sceneFadeCoroutine = null;
        }

        private void BuildUIHierarchyIfMissing()
        {
            if (_loadingPanel == null)
            {
                var panelObj = new GameObject("LoadingPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                panelObj.transform.SetParent(transform, false);
                var pRect = panelObj.GetComponent<RectTransform>();
                pRect.anchorMin = Vector2.zero;
                pRect.anchorMax = Vector2.one;
                pRect.offsetMin = Vector2.zero;
                pRect.offsetMax = Vector2.zero;

                var img = panelObj.GetComponent<Image>();
                img.color = new Color(0.04f, 0.04f, 0.06f, 1f); // Nền tối hầm ngục

                _loadingCanvasGroup = panelObj.GetComponent<CanvasGroup>();
                _loadingPanel = panelObj;

                // Spinner
                var spinObj = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
                spinObj.transform.SetParent(panelObj.transform, false);
                _spinnerRect = spinObj.GetComponent<RectTransform>();
                _spinnerRect.anchorMin = new Vector2(0.5f, 0.55f);
                _spinnerRect.anchorMax = new Vector2(0.5f, 0.55f);
                _spinnerRect.pivot = new Vector2(0.5f, 0.5f);
                _spinnerRect.sizeDelta = new Vector2(72f, 72f);
                _spinnerRect.anchoredPosition = Vector2.zero;

                var spinImg = spinObj.GetComponent<Image>();
                spinImg.color = new Color(1f, 0.5f, 0.1f, 0.9f); // Màu cam lửa Hellfire

                // Title Text
                var titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(panelObj.transform, false);
                var tRect = titleObj.GetComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0.5f, 0.65f);
                tRect.anchorMax = new Vector2(0.5f, 0.65f);
                tRect.sizeDelta = new Vector2(600f, 60f);
                _titleText = titleObj.GetComponent<TextMeshProUGUI>();
                _titleText.text = "HELLFIRE";
                _titleText.fontSize = 42;
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = new Color(1f, 0.35f, 0.1f, 1f);

                // Ready Status Text
                var readyObj = new GameObject("ReadyStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
                readyObj.transform.SetParent(panelObj.transform, false);
                var rRect = readyObj.GetComponent<RectTransform>();
                rRect.anchorMin = new Vector2(0.5f, 0.45f);
                rRect.anchorMax = new Vector2(0.5f, 0.45f);
                rRect.sizeDelta = new Vector2(600f, 40f);
                _readyStatusText = readyObj.GetComponent<TextMeshProUGUI>();
                _readyStatusText.text = "Đang tạo hầm ngục... 0/1 sẵn sàng";
                _readyStatusText.fontSize = 22;
                _readyStatusText.alignment = TextAlignmentOptions.Center;
                _readyStatusText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

                // Tips Text (ở đáy màn hình)
                var tipObj = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
                tipObj.transform.SetParent(panelObj.transform, false);
                var tipRect = tipObj.GetComponent<RectTransform>();
                tipRect.anchorMin = new Vector2(0.5f, 0.15f);
                tipRect.anchorMax = new Vector2(0.5f, 0.15f);
                tipRect.sizeDelta = new Vector2(1000f, 60f);
                _tipText = tipObj.GetComponent<TextMeshProUGUI>();
                _tipText.text = GameplayTips[0];
                _tipText.fontSize = 20;
                _tipText.alignment = TextAlignmentOptions.Center;
                _tipText.color = new Color(0.75f, 0.85f, 0.95f, 0.85f);

                _loadingPanel.SetActive(false);
            }

            if (_fadeOverlayImage == null)
            {
                var fadeObj = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image));
                fadeObj.transform.SetParent(transform, false);
                var fRect = fadeObj.GetComponent<RectTransform>();
                fRect.anchorMin = Vector2.zero;
                fRect.anchorMax = Vector2.one;
                fRect.offsetMin = Vector2.zero;
                fRect.offsetMax = Vector2.zero;

                _fadeOverlayImage = fadeObj.GetComponent<Image>();
                _fadeOverlayImage.color = new Color(0f, 0f, 0f, 0f);
                _fadeOverlayImage.raycastTarget = false;
                fadeObj.SetActive(false);
            }
        }
    }
}
