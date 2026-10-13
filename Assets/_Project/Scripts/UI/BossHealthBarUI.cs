// Script: BossHealthBarUI.cs
// Mục đích: Thanh máu boss giữa trên màn hình khi GameState == BossFight, vạch đánh dấu 50%, banner "PHA 2" khi chuyển pha (Mục 16.6 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class BossHealthBarUI : MonoBehaviour
    {
        public static BossHealthBarUI Instance { get; private set; }

        [Header("UI Containers (Mục 16.6)")]
        [SerializeField] private GameObject _bossHealthBarRoot;
        [SerializeField] private TextMeshProUGUI _bossNameText;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TextMeshProUGUI _healthValueText;
        [SerializeField] private RectTransform _phase50Tick;

        [Header("Phase 2 Banner (Mục 16.6)")]
        [SerializeField] private GameObject _phase2BannerPanel;
        [SerializeField] private TextMeshProUGUI _phase2BannerText;

        private Health _bossHealth;
        private Coroutine _phaseBannerCoroutine;
        private Coroutine _smoothHealthCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUIHierarchyIfMissing();
        }

        private void Start()
        {
            if (_bossHealthBarRoot != null) _bossHealthBarRoot.SetActive(false);
            if (_phase2BannerPanel != null) _phase2BannerPanel.SetActive(false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                HandleGameStateChanged(GameState.Lobby, GameManager.Instance.CurrentState.Value);
            }

            BossStateMachine.OnBossSpawned += HandleBossSpawned;
            BossStateMachine.OnBossDespawned += HandleBossDespawned;

            if (BossStateMachine.ActiveBoss != null)
            {
                HandleBossSpawned(BossStateMachine.ActiveBoss);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            BossStateMachine.OnBossSpawned -= HandleBossSpawned;
            BossStateMachine.OnBossDespawned -= HandleBossDespawned;

            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged -= HandleBossHealthChanged;
            }
        }

        private void Update()
        {
            // Tự động tìm boss nếu đang trong trận BossFight mà chưa bind
            if (_bossHealth == null && GameManager.Instance != null && GameManager.Instance.CurrentState.Value == GameState.BossFight)
            {
                if (BossStateMachine.ActiveBoss != null)
                {
                    HandleBossSpawned(BossStateMachine.ActiveBoss);
                }
            }
        }

        private void HandleGameStateChanged(GameState prev, GameState next)
        {
            bool isBossFight = (next == GameState.BossFight);
            if (_bossHealthBarRoot != null)
            {
                _bossHealthBarRoot.SetActive(isBossFight && _bossHealth != null);
            }
        }

        private void HandleBossSpawned(BossStateMachine boss)
        {
            if (boss == null) return;

            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged -= HandleBossHealthChanged;
            }

            _bossHealth = boss.GetComponent<Health>();
            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged += HandleBossHealthChanged;

                if (_healthSlider != null)
                {
                    _healthSlider.minValue = 0f;
                    _healthSlider.maxValue = _bossHealth.MaxHealth;
                    _healthSlider.value = _bossHealth.CurrentHealth.Value;
                }

                UpdateHealthText(_bossHealth.CurrentHealth.Value, _bossHealth.MaxHealth);

                if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value == GameState.BossFight)
                {
                    if (_bossHealthBarRoot != null) _bossHealthBarRoot.SetActive(true);
                }
            }
        }

        private void HandleBossDespawned()
        {
            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged -= HandleBossHealthChanged;
                _bossHealth = null;
            }

            if (_bossHealthBarRoot != null)
            {
                _bossHealthBarRoot.SetActive(false);
            }
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            if (_smoothHealthCoroutine != null)
            {
                StopCoroutine(_smoothHealthCoroutine);
            }
            _smoothHealthCoroutine = StartCoroutine(SmoothHealthRoutine(current, max));
            UpdateHealthText(current, max);

            if (current <= 0f && _bossHealthBarRoot != null)
            {
                _bossHealthBarRoot.SetActive(false);
            }
        }

        private IEnumerator SmoothHealthRoutine(float target, float max)
        {
            if (_healthSlider == null) yield break;

            _healthSlider.maxValue = max;
            float start = _healthSlider.value;
            float elapsed = 0f;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _healthSlider.value = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            _healthSlider.value = target;
            _smoothHealthCoroutine = null;
        }

        private void UpdateHealthText(float current, float max)
        {
            if (_healthValueText != null)
            {
                _healthValueText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, current))} / {Mathf.CeilToInt(max)}";
            }
        }

        /// <summary>
        /// Kích hoạt biểu ngữ "PHA 2" và hiệu ứng rung (Mục 16.6 đặc tả kỹ thuật).
        /// </summary>
        public void ShowPhase2Banner()
        {
            if (_phaseBannerCoroutine != null)
            {
                StopCoroutine(_phaseBannerCoroutine);
            }
            _phaseBannerCoroutine = StartCoroutine(Phase2BannerRoutine());
        }

        private IEnumerator Phase2BannerRoutine()
        {
            if (_phase2BannerPanel != null)
            {
                _phase2BannerPanel.SetActive(true);
                _phase2BannerPanel.transform.localScale = Vector3.one * 1.3f;
            }

            float duration = 2.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (_phase2BannerPanel != null)
                {
                    _phase2BannerPanel.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, Mathf.Clamp01(t * 3f));
                }
                yield return null;
            }

            if (_phase2BannerPanel != null)
            {
                _phase2BannerPanel.SetActive(false);
                _phase2BannerPanel.transform.localScale = Vector3.one;
            }

            _phaseBannerCoroutine = null;
        }

        private void BuildUIHierarchyIfMissing()
        {
            Transform root = transform;

            if (_bossHealthBarRoot == null)
            {
                var barObj = new GameObject("BossHealthBarRoot", typeof(RectTransform));
                barObj.transform.SetParent(root, false);
                var bRect = barObj.GetComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.5f, 1f);
                bRect.anchorMax = new Vector2(0.5f, 1f);
                bRect.pivot = new Vector2(0.5f, 1f);
                bRect.anchoredPosition = new Vector2(0f, -30f);
                bRect.sizeDelta = new Vector2(520f, 65f);
                _bossHealthBarRoot = barObj;

                // Boss Name Text
                var nameObj = new GameObject("BossNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
                nameObj.transform.SetParent(barObj.transform, false);
                var nRect = nameObj.GetComponent<RectTransform>();
                nRect.anchorMin = new Vector2(0.5f, 1f);
                nRect.anchorMax = new Vector2(0.5f, 1f);
                nRect.anchoredPosition = new Vector2(0f, -10f);
                nRect.sizeDelta = new Vector2(400f, 28f);
                _bossNameText = nameObj.GetComponent<TextMeshProUGUI>();
                _bossNameText.text = "CHÚA TỂ ĐỊA NGỤC"; // Tên theo mục 16.6
                _bossNameText.fontSize = 20;
                _bossNameText.fontStyle = FontStyles.Bold;
                _bossNameText.alignment = TextAlignmentOptions.Center;
                _bossNameText.color = new Color(1f, 0.25f, 0.15f, 1f);

                // Slider
                var sliderObj = new GameObject("BossHealthSlider", typeof(RectTransform), typeof(Slider));
                sliderObj.transform.SetParent(barObj.transform, false);
                var sRect = sliderObj.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0.5f, 0f);
                sRect.anchorMax = new Vector2(0.5f, 0f);
                sRect.pivot = new Vector2(0.5f, 0f);
                sRect.anchoredPosition = new Vector2(0f, 5f);
                sRect.sizeDelta = new Vector2(480f, 22f);
                _healthSlider = sliderObj.GetComponent<Slider>();

                // Slider Background
                var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(sliderObj.transform, false);
                var bgRect = bgObj.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImg = bgObj.GetComponent<Image>();
                bgImg.color = new Color(0.12f, 0.02f, 0.02f, 0.9f);

                // Fill Area
                var fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(sliderObj.transform, false);
                var faRect = fillArea.GetComponent<RectTransform>();
                faRect.anchorMin = Vector2.zero;
                faRect.anchorMax = Vector2.one;
                faRect.offsetMin = Vector2.zero;
                faRect.offsetMax = Vector2.zero;

                var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillObj.transform.SetParent(fillArea.transform, false);
                var fRect = fillObj.GetComponent<RectTransform>();
                fRect.anchorMin = Vector2.zero;
                fRect.anchorMax = Vector2.one;
                fRect.offsetMin = Vector2.zero;
                fRect.offsetMax = Vector2.zero;
                var fImg = fillObj.GetComponent<Image>();
                fImg.color = new Color(0.9f, 0.15f, 0.1f, 1f); // Đỏ thẫm máu boss

                _healthSlider.targetGraphic = bgImg;
                _healthSlider.fillRect = fRect;

                // 50% Tick Mark (Vạch đánh dấu ở 50% theo mục 16.6)
                var tickObj = new GameObject("Phase50Tick", typeof(RectTransform), typeof(Image));
                tickObj.transform.SetParent(sliderObj.transform, false);
                _phase50Tick = tickObj.GetComponent<RectTransform>();
                _phase50Tick.anchorMin = new Vector2(0.5f, 0f);
                _phase50Tick.anchorMax = new Vector2(0.5f, 1f);
                _phase50Tick.pivot = new Vector2(0.5f, 0.5f);
                _phase50Tick.sizeDelta = new Vector2(3f, 0f);
                _phase50Tick.anchoredPosition = Vector2.zero;
                var tickImg = tickObj.GetComponent<Image>();
                tickImg.color = new Color(1f, 0.9f, 0.2f, 0.95f); // Vàng kim rõ nét

                // Health Value Text
                var valObj = new GameObject("HealthValueText", typeof(RectTransform), typeof(TextMeshProUGUI));
                valObj.transform.SetParent(sliderObj.transform, false);
                var vRect = valObj.GetComponent<RectTransform>();
                vRect.anchorMin = Vector2.zero;
                vRect.anchorMax = Vector2.one;
                vRect.offsetMin = Vector2.zero;
                vRect.offsetMax = Vector2.zero;
                _healthValueText = valObj.GetComponent<TextMeshProUGUI>();
                _healthValueText.alignment = TextAlignmentOptions.Center;
                _healthValueText.fontSize = 14;
                _healthValueText.fontStyle = FontStyles.Bold;
                _healthValueText.color = Color.white;

                barObj.SetActive(false);
            }

            if (_phase2BannerPanel == null)
            {
                var bannerObj = new GameObject("Phase2BannerPanel", typeof(RectTransform));
                bannerObj.transform.SetParent(root, false);
                var bnRect = bannerObj.GetComponent<RectTransform>();
                bnRect.anchorMin = new Vector2(0.5f, 0.7f);
                bnRect.anchorMax = new Vector2(0.5f, 0.7f);
                bnRect.pivot = new Vector2(0.5f, 0.5f);
                bnRect.sizeDelta = new Vector2(500f, 90f);
                bnRect.anchoredPosition = Vector2.zero;
                _phase2BannerPanel = bannerObj;

                var textObj = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(bannerObj.transform, false);
                var tRect = textObj.GetComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.offsetMin = Vector2.zero;
                tRect.offsetMax = Vector2.zero;
                _phase2BannerText = textObj.GetComponent<TextMeshProUGUI>();
                _phase2BannerText.text = "⚡ PHA 2 ⚡"; // Chữ PHA 2 theo mục 16.6
                _phase2BannerText.fontSize = 54;
                _phase2BannerText.fontStyle = FontStyles.Bold;
                _phase2BannerText.alignment = TextAlignmentOptions.Center;
                _phase2BannerText.color = new Color(1f, 0.2f, 0.1f, 1f);

                bannerObj.SetActive(false);
            }
        }
    }
}
