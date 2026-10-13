// Script: GameplayHUD.cs
// Mục đích: Quản lý toàn bộ giao diện HUD chiến đấu, Menu Pause, Khiên bảo vệ, Màn hình Kết quả (Chiến thắng / Thất bại) bằng uGUI Canvas (Mục 3, 6, 7, 10.1 & 10.2).
// Môi trường thực thi: Client-only.

using System.Collections;
using Hellfire.Combat;
using Hellfire.Items;
using Hellfire.Networking;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Crosshair & Hitmarker (Mục 16.7)")]
        [SerializeField] private GameObject _crosshairObject;
        [SerializeField] private Image _crosshairImage;
        [SerializeField] private Image _hitmarkerImage;
        [SerializeField] private TextMeshProUGUI _killConfirmText;

        [Header("Combat Feedback: Damage Vignette & Direction (Mục 16.7)")]
        [SerializeField] private Image _damageVignetteImage;
        [SerializeField] private RectTransform _damageIndicatorArrow;

        [Header("Combat Feedback Audio (Mục 16.7)")]
        [SerializeField] private AudioSource _combatAudioSource;
        [SerializeField] private AudioClip _hitSound;
        [SerializeField] private AudioClip _headshotSound;
        [SerializeField] private AudioClip _killSound;
        [SerializeField] private AudioClip _heartbeatSound;

        [Header("Health & Status UI")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private TextMeshProUGUI _armorText;
        [SerializeField] private GameObject _downedOverlayPanel;
        [SerializeField] private TextMeshProUGUI _downedTimerText;

        [Header("Guardian Shield & Buffs (Mục 6.2)")]
        [SerializeField] private GameObject _shieldIndicatorPanel;
        [SerializeField] private TextMeshProUGUI _shieldTimerText;
        [SerializeField] private TextMeshProUGUI _buffToastText;

        [Header("Weapon & Ammo UI")]
        [SerializeField] private TextMeshProUGUI _weaponNameText;
        [SerializeField] private TextMeshProUGUI _ammoText;
        [SerializeField] private TextMeshProUGUI _reloadPromptText;

        [Header("Revive Interaction UI")]
        [SerializeField] private GameObject _revivePromptPanel;
        [SerializeField] private TextMeshProUGUI _revivePromptText;
        [SerializeField] private Slider _reviveProgressBar;

        [Header("Teammate HUD & Markers (Mục 16.2 & 16.3)")]
        [SerializeField] private TeammateHUDPanel _teammatePanel;
        [SerializeField] private TeammateMarkerManager _teammateMarkerManager;

        [Header("Pause Menu (Mục 7)")]
        [SerializeField] private GameObject _pauseMenuPanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _leaveGameButton;
        [SerializeField] private TextMeshProUGUI _playerInfoText;

        [Header("Result Screens: Victory & Game Over (Mục 10.1 & 10.2)")]
        [SerializeField] private GameObject _victoryOverlayPanel;
        [SerializeField] private Button _victoryReturnButton;
        [SerializeField] private TextMeshProUGUI _victoryWaitingText;

        [SerializeField] private GameObject _gameOverOverlayPanel;
        [SerializeField] private Button _gameOverReturnButton;
        [SerializeField] private TextMeshProUGUI _gameOverWaitingText;

        [Header("Souls (Mục 17.7)")]
        [SerializeField] private TextMeshProUGUI _soulsText;

        private bool _isPaused;
        private Health _localPlayerHealth;
        private WeaponController _localWeaponController;
        private PlayerRevive _localPlayerRevive;
        private PlayerBuffManager _localBuffManager;
        private PlayerStats _localPlayerStats;
        private PlayerWallet _localPlayerWallet;
        private Coroutine _hitmarkerCoroutine;
        private Coroutine _buffToastCoroutine;
        private float _lastHeartbeatTime = 0f;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            AutoResolveReferences();
            RegisterButtonListeners();
            AttachButtonFeedbacks();

            if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(false);
            if (_hitmarkerImage != null) _hitmarkerImage.gameObject.SetActive(false);
            if (_killConfirmText != null) _killConfirmText.gameObject.SetActive(false);
            if (_damageVignetteImage != null) _damageVignetteImage.color = new Color(1f, 0f, 0f, 0f);
            if (_damageIndicatorArrow != null) _damageIndicatorArrow.gameObject.SetActive(false);
            if (_downedOverlayPanel != null) _downedOverlayPanel.SetActive(false);
            if (_revivePromptPanel != null) _revivePromptPanel.SetActive(false);
            if (_reloadPromptText != null) _reloadPromptText.gameObject.SetActive(false);
            if (_shieldIndicatorPanel != null) _shieldIndicatorPanel.SetActive(false);
            if (_buffToastText != null) _buffToastText.gameObject.SetActive(false);
            if (_victoryOverlayPanel != null) _victoryOverlayPanel.SetActive(false);
            if (_gameOverOverlayPanel != null) _gameOverOverlayPanel.SetActive(false);

            UpdatePlayerInfo();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                HandleGameStateChanged(GameState.Lobby, GameManager.Instance.CurrentState.Value);
            }
        }

        private void AutoResolveReferences()
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform.root;

            // Crosshair
            if (_crosshairObject == null) _crosshairObject = FindInHierarchy(root, "Crosshair");
            if (_crosshairImage == null && _crosshairObject != null) _crosshairImage = _crosshairObject.GetComponent<Image>();
            if (_hitmarkerImage == null) _hitmarkerImage = FindComponentInHierarchy<Image>(root, "Hitmarker");

            // Health & Armor
            if (_healthSlider == null) _healthSlider = FindComponentInHierarchy<Slider>(root, "HealthSlider");
            if (_healthText == null) _healthText = FindComponentInHierarchy<TextMeshProUGUI>(root, "HealthText");
            if (_armorText == null) _armorText = FindComponentInHierarchy<TextMeshProUGUI>(root, "ArmorText");
            if (_downedOverlayPanel == null) _downedOverlayPanel = FindInHierarchy(root, "DownedOverlayPanel");
            if (_downedTimerText == null) _downedTimerText = FindComponentInHierarchy<TextMeshProUGUI>(root, "DownedTimerText");

            // Shield & Buffs
            if (_shieldIndicatorPanel == null) _shieldIndicatorPanel = FindInHierarchy(root, "ShieldIndicatorPanel");
            if (_shieldTimerText == null) _shieldTimerText = FindComponentInHierarchy<TextMeshProUGUI>(root, "ShieldTimerText");
            if (_buffToastText == null) _buffToastText = FindComponentInHierarchy<TextMeshProUGUI>(root, "BuffToastText");

            // Weapon & Ammo
            if (_weaponNameText == null) _weaponNameText = FindComponentInHierarchy<TextMeshProUGUI>(root, "WeaponNameText");
            if (_ammoText == null) _ammoText = FindComponentInHierarchy<TextMeshProUGUI>(root, "AmmoText");
            if (_reloadPromptText == null) _reloadPromptText = FindComponentInHierarchy<TextMeshProUGUI>(root, "ReloadPromptText");

            // Revive
            if (_revivePromptPanel == null) _revivePromptPanel = FindInHierarchy(root, "RevivePromptPanel");
            if (_revivePromptText == null) _revivePromptText = FindComponentInHierarchy<TextMeshProUGUI>(root, "RevivePromptText");
            if (_reviveProgressBar == null) _reviveProgressBar = FindComponentInHierarchy<Slider>(root, "ReviveProgressBar");

            // Pause Menu
            if (_pauseMenuPanel == null) _pauseMenuPanel = FindInHierarchy(root, "PauseMenuPanel");
            if (_resumeButton == null) _resumeButton = FindComponentInHierarchy<Button>(root, "ResumeButton");
            if (_leaveGameButton == null) _leaveGameButton = FindComponentInHierarchy<Button>(root, "LeaveGameButton");
            if (_playerInfoText == null) _playerInfoText = FindComponentInHierarchy<TextMeshProUGUI>(root, "PlayerInfoText");

            // Victory & GameOver Overlays
            if (_victoryOverlayPanel == null) _victoryOverlayPanel = FindInHierarchy(root, "VictoryOverlayPanel");
            if (_victoryReturnButton == null) _victoryReturnButton = FindComponentInHierarchy<Button>(root, "VictoryReturnButton");
            if (_victoryWaitingText == null) _victoryWaitingText = FindComponentInHierarchy<TextMeshProUGUI>(root, "VictoryWaitingText");

            if (_gameOverOverlayPanel == null) _gameOverOverlayPanel = FindInHierarchy(root, "GameOverOverlayPanel");
            if (_gameOverReturnButton == null) _gameOverReturnButton = FindComponentInHierarchy<Button>(root, "GameOverReturnButton");
            if (_gameOverWaitingText == null) _gameOverWaitingText = FindComponentInHierarchy<TextMeshProUGUI>(root, "GameOverWaitingText");

            // Souls Counter (Mục 17.7)
            if (_soulsText == null) _soulsText = FindComponentInHierarchy<TextMeshProUGUI>(root, "SoulsText");
            if (_soulsText == null && canvas != null)
            {
                _soulsText = CreateSoulsText(canvas.transform);
            }

            // Teammate Panel (Mục 16.2)
            if (_teammatePanel == null)
            {
                _teammatePanel = FindComponentInHierarchy<TeammateHUDPanel>(root, "TeammatePanel");
                if (_teammatePanel == null)
                {
                    var panelObj = new GameObject("TeammatePanel", typeof(RectTransform), typeof(TeammateHUDPanel));
                    panelObj.transform.SetParent(root, false);
                    var pRect = panelObj.GetComponent<RectTransform>();
                    pRect.anchorMin = new Vector2(0f, 0.45f);
                    pRect.anchorMax = new Vector2(0f, 0.75f);
                    pRect.pivot = new Vector2(0f, 0.5f);
                    pRect.anchoredPosition = new Vector2(25f, 0f);
                    pRect.sizeDelta = new Vector2(240f, 180f);

                    var vlg = panelObj.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 10f;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;

                    _teammatePanel = panelObj.GetComponent<TeammateHUDPanel>();
                }
            }

            // Teammate Markers (Mục 16.3)
            if (_teammateMarkerManager == null)
            {
                _teammateMarkerManager = FindComponentInHierarchy<TeammateMarkerManager>(root, "TeammateMarkerManager");
                if (_teammateMarkerManager == null)
                {
                    var markerObj = new GameObject("TeammateMarkerManager", typeof(RectTransform), typeof(TeammateMarkerManager));
                    markerObj.transform.SetParent(root, false);
                    var mRect = markerObj.GetComponent<RectTransform>();
                    mRect.anchorMin = Vector2.zero;
                    mRect.anchorMax = Vector2.one;
                    mRect.offsetMin = Vector2.zero;
                    mRect.offsetMax = Vector2.zero;

                    _teammateMarkerManager = markerObj.GetComponent<TeammateMarkerManager>();
                }
            }

            // Damage Vignette & Indicator (Mục 16.7)
            if (_damageVignetteImage == null)
            {
                var vigObj = new GameObject("DamageVignette", typeof(RectTransform), typeof(Image));
                vigObj.transform.SetParent(root, false);
                vigObj.transform.SetAsFirstSibling(); // Đặt sau cùng trong Canvas để không che UI khác
                var vRect = vigObj.GetComponent<RectTransform>();
                vRect.anchorMin = Vector2.zero;
                vRect.anchorMax = Vector2.one;
                vRect.offsetMin = Vector2.zero;
                vRect.offsetMax = Vector2.zero;
                _damageVignetteImage = vigObj.GetComponent<Image>();
                _damageVignetteImage.color = new Color(1f, 0f, 0f, 0f);
                _damageVignetteImage.raycastTarget = false;
            }

            if (_damageIndicatorArrow == null)
            {
                var arrowObj = new GameObject("DamageIndicatorArrow", typeof(RectTransform), typeof(Image));
                arrowObj.transform.SetParent(root, false);
                _damageIndicatorArrow = arrowObj.GetComponent<RectTransform>();
                _damageIndicatorArrow.sizeDelta = new Vector2(32f, 32f);
                _damageIndicatorArrow.anchoredPosition = new Vector2(0f, 120f);
                var arrowImg = arrowObj.GetComponent<Image>();
                arrowImg.color = new Color(1f, 0.1f, 0.1f, 0.9f);
                arrowImg.raycastTarget = false;
                arrowObj.SetActive(false);
            }

            // AudioSource cho phản hồi chiến đấu (Mục 16.7)
            if (_combatAudioSource == null)
            {
                _combatAudioSource = GetComponent<AudioSource>();
                if (_combatAudioSource == null)
                {
                    _combatAudioSource = gameObject.AddComponent<AudioSource>();
                    _combatAudioSource.playOnAwake = false;
                    _combatAudioSource.spatialBlend = 0f;
                }
            }
            InitProceduralAudioClips();
        }

        private TextMeshProUGUI CreateSoulsText(Transform root)
        {
            var obj = new GameObject("SoulsText");
            obj.transform.SetParent(root, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -30f);
            rect.sizeDelta = new Vector2(250f, 40f);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Linh hồn: 0";
            tmp.fontSize = 22f;
            tmp.color = new Color(0.4f, 0.9f, 1f);
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private void RegisterButtonListeners()
        {
            if (_resumeButton != null)
            {
                _resumeButton.onClick.RemoveAllListeners();
                _resumeButton.onClick.AddListener(ResumeGame);
            }

            if (_leaveGameButton != null)
            {
                _leaveGameButton.onClick.RemoveAllListeners();
                _leaveGameButton.onClick.AddListener(LeaveGame);
            }

            if (_victoryReturnButton != null)
            {
                _victoryReturnButton.onClick.RemoveAllListeners();
                _victoryReturnButton.onClick.AddListener(ReturnToLobbyFromHUD);
            }

            if (_gameOverReturnButton != null)
            {
                _gameOverReturnButton.onClick.RemoveAllListeners();
                _gameOverReturnButton.onClick.AddListener(ReturnToLobbyFromHUD);
            }
        }

        private void Update()
        {
            TryBindLocalPlayer();

            // Cập nhật bộ đếm thời gian gục (Downed)
            if (_localPlayerHealth != null && _localPlayerHealth.IsDowned.Value)
            {
                if (_downedOverlayPanel != null && !_downedOverlayPanel.activeSelf)
                {
                    _downedOverlayPanel.SetActive(true);
                }
                if (_downedTimerText != null)
                {
                    int secondsLeft = Mathf.CeilToInt(_localPlayerHealth.DownedTimer.Value);
                    _downedTimerText.text = $"BẠN ĐANG GỤC! CHỜ CỨU... ({secondsLeft}s)";
                }
            }
            else
            {
                if (_downedOverlayPanel != null && _downedOverlayPanel.activeSelf)
                {
                    _downedOverlayPanel.SetActive(false);
                }
            }

            // Cập nhật Khiên tạm thời (Guardian Shield - 8s)
            if (_localBuffManager != null && _localBuffManager.HasGuardianShield)
            {
                if (_shieldIndicatorPanel != null && !_shieldIndicatorPanel.activeSelf)
                {
                    _shieldIndicatorPanel.SetActive(true);
                }
                if (_shieldTimerText != null)
                {
                    _shieldTimerText.text = $"🛡️ KHIÊN BẢO VỆ: {_localBuffManager.ShieldTimeRemaining:F1}s";
                }
            }
            else
            {
                if (_shieldIndicatorPanel != null && _shieldIndicatorPanel.activeSelf)
                {
                    _shieldIndicatorPanel.SetActive(false);
                }
            }

            // Phím ESC mở Pause Menu (Mục 7)
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // Chỉ cho phép mở pause menu nếu không ở màn hình kết quả
                if ((_victoryOverlayPanel == null || !_victoryOverlayPanel.activeSelf) &&
                    (_gameOverOverlayPanel == null || !_gameOverOverlayPanel.activeSelf))
                {
                    TogglePauseMenu();
                }
            }
        }

        private void TryBindLocalPlayer()
        {
            if (_localPlayerHealth != null && _localWeaponController != null && _localBuffManager != null && _localPlayerWallet != null) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer == null) return;

            if (_localPlayerHealth == null)
            {
                _localPlayerHealth = localPlayer.GetComponent<Health>();
                if (_localPlayerHealth != null)
                {
                    _localPlayerHealth.OnHealthChanged += HandleHealthChanged;
                    _localPlayerHealth.OnDownedStateChanged += HandleDownedChanged;
                    _localPlayerHealth.OnDamageTaken += HandleDamageTaken;
                    _localPlayerHealth.OnPlayerAttackedBy += HandlePlayerAttacked;
                    HandleHealthChanged(_localPlayerHealth.CurrentHealth.Value, _localPlayerHealth.MaxHealth);
                }
            }

            if (_localWeaponController == null)
            {
                _localWeaponController = localPlayer.GetComponent<WeaponController>();
                if (_localWeaponController != null)
                {
                    _localWeaponController.OnAmmoChanged += HandleAmmoChanged;
                    _localWeaponController.OnWeaponChanged += HandleWeaponChanged;
                    _localWeaponController.OnReloadStateChanged += HandleReloadStateChanged;
                    _localWeaponController.OnHitTargetConfirmed += ShowHitmarker;
                    _localWeaponController.OnKillConfirmed += ShowKillConfirmation;

                    if (_localWeaponController.CurrentWeaponData != null)
                    {
                        HandleWeaponChanged(_localWeaponController.CurrentWeaponData.WeaponName);
                        HandleAmmoChanged(_localWeaponController.CurrentAmmo, _localWeaponController.CurrentWeaponData.MagSize);
                    }
                }
            }

            if (_localPlayerRevive == null)
            {
                _localPlayerRevive = localPlayer.GetComponent<PlayerRevive>();
                if (_localPlayerRevive != null)
                {
                    _localPlayerRevive.OnRevivePromptChanged += HandleRevivePromptChanged;
                    _localPlayerRevive.OnReviveProgressChanged += HandleReviveProgressChanged;
                }
            }

            if (_localBuffManager == null)
            {
                _localBuffManager = localPlayer.GetComponent<PlayerBuffManager>();
                if (_localBuffManager != null)
                {
                    _localBuffManager.OnPowerUpApplied += HandlePowerUpApplied;
                }
            }

            if (_localPlayerStats == null)
            {
                _localPlayerStats = localPlayer.GetComponent<PlayerStats>();
                if (_localPlayerStats != null)
                {
                    _localPlayerStats.OnStatsChanged += HandlePlayerStatsChanged;
                    HandlePlayerStatsChanged(_localPlayerStats.BonusMaxHealth.Value, _localPlayerStats.Armor.Value, _localPlayerStats.MoveSpeedMultiplier.Value);
                }
            }

            if (_localPlayerWallet == null)
            {
                _localPlayerWallet = localPlayer.GetComponent<PlayerWallet>();
                if (_localPlayerWallet != null)
                {
                    _localPlayerWallet.OnSoulsChanged += HandleSoulsChanged;
                    HandleSoulsChanged(_localPlayerWallet.Souls.Value);
                }
            }
        }

        private void HandleSoulsChanged(int currentSouls)
        {
            if (_soulsText != null)
            {
                _soulsText.text = $"Linh hồn: {currentSouls}";
            }
        }

        private void HandlePowerUpApplied(PowerUpType type, float value)
        {
            string msg = string.Empty;
            switch (type)
            {
                case PowerUpType.MaxHealthUp:
                    msg = $"<color=#00FF88>+ {value} MÁU TỐI ĐA & HỒI ĐẦY MÁU!</color>";
                    break;
                case PowerUpType.SwiftBoots:
                    msg = $"<color=#00FFFF>+ {(value * 100):0}% TỐC ĐỘ DI CHUYỂN!</color>";
                    break;
                case PowerUpType.BerserkerCharm:
                    msg = $"<color=#FF4444>+ {(value * 100):0}% SÁT THƯƠNG SÚNG!</color>";
                    break;
                case PowerUpType.GuardianShield:
                    msg = "<color=#FFFF00>🛡️ KHIÊN BẢO VỆ KÍCH HOẠT (8 GIÂY BẤT TỬ)!</color>";
                    break;
            }

            ShowBuffToast(msg);
        }

        public void ShowBuffToast(string message)
        {
            if (_buffToastText == null) return;

            if (_buffToastCoroutine != null)
            {
                StopCoroutine(_buffToastCoroutine);
            }
            _buffToastCoroutine = StartCoroutine(BuffToastRoutine(message));
        }

        private IEnumerator BuffToastRoutine(string message)
        {
            _buffToastText.gameObject.SetActive(true);
            _buffToastText.text = message;

            yield return new WaitForSeconds(3.0f);

            _buffToastText.gameObject.SetActive(false);
        }

        private void HandleGameStateChanged(GameState prev, GameState next)
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            if (next == GameState.Victory)
            {
                if (_victoryOverlayPanel != null) _victoryOverlayPanel.SetActive(true);
                if (_gameOverOverlayPanel != null) _gameOverOverlayPanel.SetActive(false);

                if (_victoryReturnButton != null) _victoryReturnButton.gameObject.SetActive(isHost);
                if (_victoryWaitingText != null) _victoryWaitingText.gameObject.SetActive(!isHost);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (next == GameState.GameOver)
            {
                if (_gameOverOverlayPanel != null) _gameOverOverlayPanel.SetActive(true);
                if (_victoryOverlayPanel != null) _victoryOverlayPanel.SetActive(false);

                if (_gameOverReturnButton != null) _gameOverReturnButton.gameObject.SetActive(isHost);
                if (_gameOverWaitingText != null) _gameOverWaitingText.gameObject.SetActive(!isHost);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (_victoryOverlayPanel != null) _victoryOverlayPanel.SetActive(false);
                if (_gameOverOverlayPanel != null) _gameOverOverlayPanel.SetActive(false);
            }
        }

        private void ReturnToLobbyFromHUD()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToLobby();
            }
        }

        private void OnDestroy()
        {
            if (_localPlayerHealth != null)
            {
                _localPlayerHealth.OnHealthChanged -= HandleHealthChanged;
                _localPlayerHealth.OnDownedStateChanged -= HandleDownedChanged;
                _localPlayerHealth.OnDamageTaken -= HandleDamageTaken;
                _localPlayerHealth.OnPlayerAttackedBy -= HandlePlayerAttacked;
            }

            if (_localWeaponController != null)
            {
                _localWeaponController.OnAmmoChanged -= HandleAmmoChanged;
                _localWeaponController.OnWeaponChanged -= HandleWeaponChanged;
                _localWeaponController.OnReloadStateChanged -= HandleReloadStateChanged;
                _localWeaponController.OnHitTargetConfirmed -= ShowHitmarker;
                _localWeaponController.OnKillConfirmed -= ShowKillConfirmation;
            }

            if (_localPlayerRevive != null)
            {
                _localPlayerRevive.OnRevivePromptChanged -= HandleRevivePromptChanged;
                _localPlayerRevive.OnReviveProgressChanged -= HandleReviveProgressChanged;
            }

            if (_localBuffManager != null)
            {
                _localBuffManager.OnPowerUpApplied -= HandlePowerUpApplied;
            }

            if (_localPlayerStats != null)
            {
                _localPlayerStats.OnStatsChanged -= HandlePlayerStatsChanged;
            }

            if (_localPlayerWallet != null)
            {
                _localPlayerWallet.OnSoulsChanged -= HandleSoulsChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandlePlayerStatsChanged(float bonusHealth, float armor, float speedMultiplier)
        {
            if (_armorText != null)
            {
                _armorText.text = $"GIÁP: {Mathf.RoundToInt(armor)} / {Mathf.RoundToInt(PlayerStats.MaxArmorCap)}";
                _armorText.gameObject.SetActive(armor > 0f);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_healthSlider != null)
            {
                _healthSlider.maxValue = max;
                _healthSlider.value = current;
            }

            if (_healthText != null)
            {
                _healthText.text = $"HP: {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitbox, ulong attackerId)
        {
            int roundedDamage = Mathf.RoundToInt(damage);
            // Sát thương luôn được làm tròn thành số nguyên (Mục I.3 & II.5)
            Debug.Log($"[GameplayHUD] Nhận {roundedDamage} sát thương vào {hitbox}.");
        }

        private void HandleDownedChanged(bool isDowned)
        {
            if (_downedOverlayPanel != null)
            {
                _downedOverlayPanel.SetActive(isDowned);
            }
        }

        private void HandleAmmoChanged(int currentAmmo, int magSize)
        {
            if (_ammoText != null)
            {
                _ammoText.text = $"{currentAmmo} / {magSize}";
            }
        }

        private void HandleWeaponChanged(string weaponName)
        {
            if (_weaponNameText != null)
            {
                _weaponNameText.text = weaponName;
            }
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (_reloadPromptText != null)
            {
                _reloadPromptText.gameObject.SetActive(isReloading);
                if (isReloading)
                {
                    _reloadPromptText.text = "ĐANG NẠP ĐẠN...";
                }
            }
        }

        public void ShowHitmarker(bool isHeadshot)
        {
            if (_hitmarkerCoroutine != null)
            {
                StopCoroutine(_hitmarkerCoroutine);
            }
            _hitmarkerCoroutine = StartCoroutine(HitmarkerRoutine(isHeadshot, false));
        }

        public void ShowKillConfirmation()
        {
            if (_hitmarkerCoroutine != null)
            {
                StopCoroutine(_hitmarkerCoroutine);
            }
            _hitmarkerCoroutine = StartCoroutine(HitmarkerRoutine(false, true));
        }

        private IEnumerator HitmarkerRoutine(bool isHeadshot, bool isKill)
        {
            if (_hitmarkerImage != null)
            {
                _hitmarkerImage.gameObject.SetActive(true);
                if (isKill)
                {
                    _hitmarkerImage.color = Color.red;
                    _hitmarkerImage.transform.localScale = Vector3.one * 1.5f;
                    PlayCombatSound(_killSound, 0.9f);
                }
                else if (isHeadshot)
                {
                    _hitmarkerImage.color = new Color(1f, 0.45f, 0f, 1f); // Màu cam rực
                    _hitmarkerImage.transform.localScale = Vector3.one * 1.25f;
                    PlayCombatSound(_headshotSound, 0.8f);
                }
                else
                {
                    _hitmarkerImage.color = Color.white;
                    _hitmarkerImage.transform.localScale = Vector3.one;
                    PlayCombatSound(_hitSound, 0.55f);
                }
            }

            if (_crosshairImage != null)
            {
                _crosshairImage.color = isKill ? Color.red : (isHeadshot ? new Color(1f, 0.45f, 0f, 1f) : Color.yellow);
            }

            if (isKill && _killConfirmText != null)
            {
                _killConfirmText.gameObject.SetActive(true);
            }

            // Thời lượng hitmarker trong khoảng 50-100ms theo mục 16.7
            yield return new WaitForSeconds(0.08f);

            if (_hitmarkerImage != null)
            {
                _hitmarkerImage.gameObject.SetActive(false);
                _hitmarkerImage.transform.localScale = Vector3.one;
            }

            if (_crosshairImage != null)
            {
                _crosshairImage.color = new Color(1f, 1f, 1f, 0.85f);
            }

            if (_killConfirmText != null)
            {
                _killConfirmText.gameObject.SetActive(false);
            }
        }

        private Coroutine _damageVignetteCoroutine;

        private void HandlePlayerAttacked(float damage, Vector3 attackerPos)
        {
            if (_damageVignetteCoroutine != null)
            {
                StopCoroutine(_damageVignetteCoroutine);
            }
            _damageVignetteCoroutine = StartCoroutine(DamageVignetteRoutine(damage, attackerPos));
        }

        private IEnumerator DamageVignetteRoutine(float damage, Vector3 attackerPos)
        {
            float maxAlpha = Mathf.Clamp(damage / 35f, 0.25f, 0.8f);

            // Mũi tên chỉ hướng kẻ tấn công (Mục 16.7)
            if (_damageIndicatorArrow != null && attackerPos != Vector3.zero)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 toAttacker = attackerPos - cam.transform.position;
                    toAttacker.y = 0f;
                    Vector3 fwd = cam.transform.forward;
                    fwd.y = 0f;
                    float angle = Vector3.SignedAngle(fwd, toAttacker, Vector3.up);
                    _damageIndicatorArrow.rotation = Quaternion.Euler(0f, 0f, -angle);
                    _damageIndicatorArrow.gameObject.SetActive(true);
                }
            }

            float duration = 0.4f; // Mờ dần ~0.4 giây theo mục 16.7
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(maxAlpha, 0f, elapsed / duration);
                if (_damageVignetteImage != null)
                {
                    _damageVignetteImage.color = new Color(0.85f, 0.05f, 0.05f, alpha);
                }
                yield return null;
            }

            if (_damageVignetteImage != null)
            {
                _damageVignetteImage.color = new Color(1f, 0f, 0f, 0f);
            }

            if (_damageIndicatorArrow != null)
            {
                _damageIndicatorArrow.gameObject.SetActive(false);
            }

            _damageVignetteCoroutine = null;
        }

        private void Update()
        {
            UpdatePlayerInfo();
            UpdateLowHealthPulse();
        }

        private void UpdateLowHealthPulse()
        {
            if (_localPlayerHealth == null || _damageVignetteCoroutine != null) return;

            // Nhịp máu thấp < 30% (~1.2 lần/giây) theo mục 16.7; tắt khi gục hoặc chết
            if (_localPlayerHealth.IsDowned.Value || _localPlayerHealth.IsDead.Value)
            {
                if (_damageVignetteImage != null && _damageVignetteCoroutine == null)
                {
                    _damageVignetteImage.color = new Color(1f, 0f, 0f, 0f);
                }
                return;
            }

            float maxHp = _localPlayerHealth.MaxHealth > 0 ? _localPlayerHealth.MaxHealth : 100f;
            float ratio = _localPlayerHealth.CurrentHealth.Value / maxHp;

            if (ratio < 0.3f && ratio > 0f)
            {
                float pulse = Mathf.PingPong(Time.time * 2.4f, 0.35f) + 0.15f; // Tần số 1.2Hz
                if (_damageVignetteImage != null)
                {
                    _damageVignetteImage.color = new Color(0.8f, 0f, 0f, pulse);
                }

                // Phát âm thanh tim đập ~1.2 lần/giây (Mục 16.7)
                if (Time.time - _lastHeartbeatTime >= 0.833f)
                {
                    _lastHeartbeatTime = Time.time;
                    PlayCombatSound(_heartbeatSound, 0.45f);
                }
            }
            else if (_damageVignetteImage != null && _damageVignetteCoroutine == null)
            {
                _damageVignetteImage.color = new Color(1f, 0f, 0f, 0f);
            }
        }

        private void AttachButtonFeedbacks()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].GetComponent<UIButtonFeedback>() == null)
                {
                    buttons[i].gameObject.AddComponent<UIButtonFeedback>();
                }
            }
        }

        private void HandleRevivePromptChanged(string prompt)
        {
            if (_revivePromptPanel != null)
            {
                _revivePromptPanel.SetActive(!string.IsNullOrEmpty(prompt));
            }

            if (_revivePromptText != null && !string.IsNullOrEmpty(prompt))
            {
                _revivePromptText.text = prompt;
            }
        }

        private void HandleReviveProgressChanged(float current, float max)
        {
            if (_reviveProgressBar != null)
            {
                _reviveProgressBar.gameObject.SetActive(current > 0.05f);
                _reviveProgressBar.maxValue = max;
                _reviveProgressBar.value = current;
            }
        }

        public void TogglePauseMenu()
        {
            _isPaused = !_isPaused;

            if (_pauseMenuPanel != null)
            {
                _pauseMenuPanel.SetActive(_isPaused);
            }

            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPaused;
        }

        public void ResumeGame()
        {
            _isPaused = false;
            if (_pauseMenuPanel != null)
            {
                _pauseMenuPanel.SetActive(false);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void LeaveGame()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (NetworkConnectManager.Instance != null)
            {
                NetworkConnectManager.Instance.Disconnect();
            }
        }

        private void UpdatePlayerInfo()
        {
            if (_playerInfoText != null && NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                bool isHost = NetworkManager.Singleton.IsServer;
                _playerInfoText.text = $"ID: #{localId} {(isHost ? "[Host]" : "[Client]")}";
            }
        }

        private GameObject FindInHierarchy(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent.gameObject;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindInHierarchy(parent.GetChild(i), targetName);
                if (result != null) return result;
            }
            return null;
        }

        private T FindComponentInHierarchy<T>(Transform parent, string targetName) where T : Component
        {
            var obj = FindInHierarchy(parent, targetName);
            return obj != null ? obj.GetComponent<T>() : null;
        }

        private void PlayCombatSound(AudioClip clip, float volume = 0.7f)
        {
            if (_combatAudioSource != null && clip != null)
            {
                _combatAudioSource.PlayOneShot(clip, volume);
            }
        }

        private void InitProceduralAudioClips()
        {
            if (_hitSound == null) _hitSound = CreateToneClip("Proc_Hit", 900f, 0.05f, 30f);
            if (_headshotSound == null) _headshotSound = CreateToneClip("Proc_Headshot", 1600f, 0.09f, 15f);
            if (_killSound == null) _killSound = CreateToneClip("Proc_Kill", 380f, 0.15f, 12f);
            if (_heartbeatSound == null) _heartbeatSound = CreateHeartbeatClip("Proc_Heartbeat");
        }

        private static AudioClip CreateToneClip(string name, float freq, float duration, float decay)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-decay * t);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateHeartbeatClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float thump1 = (t < 0.1f) ? Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-20f * t) : 0f;
                float thump2 = (t >= 0.1f) ? Mathf.Sin(2f * Mathf.PI * 65f * (t - 0.1f)) * Mathf.Exp(-22f * (t - 0.1f)) : 0f;
                data[i] = (thump1 * 0.5f + thump2 * 0.4f);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
