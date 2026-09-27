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
        [Header("Crosshair & Hitmarker")]
        [SerializeField] private GameObject _crosshairObject;
        [SerializeField] private Image _crosshairImage;
        [SerializeField] private Image _hitmarkerImage;

        [Header("Health & Status UI")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TextMeshProUGUI _healthText;
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

        private bool _isPaused;
        private Health _localPlayerHealth;
        private WeaponController _localWeaponController;
        private PlayerRevive _localPlayerRevive;
        private PlayerBuffManager _localBuffManager;
        private Coroutine _hitmarkerCoroutine;
        private Coroutine _buffToastCoroutine;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            AutoResolveReferences();
            RegisterButtonListeners();

            if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(false);
            if (_hitmarkerImage != null) _hitmarkerImage.gameObject.SetActive(false);
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

            // Health
            if (_healthSlider == null) _healthSlider = FindComponentInHierarchy<Slider>(root, "HealthSlider");
            if (_healthText == null) _healthText = FindComponentInHierarchy<TextMeshProUGUI>(root, "HealthText");
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
            if (_localPlayerHealth != null && _localWeaponController != null && _localBuffManager != null) return;

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer == null) return;

            if (_localPlayerHealth == null)
            {
                _localPlayerHealth = localPlayer.GetComponent<Health>();
                if (_localPlayerHealth != null)
                {
                    _localPlayerHealth.OnHealthChanged += HandleHealthChanged;
                    _localPlayerHealth.OnDownedStateChanged += HandleDownedChanged;
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
            }

            if (_localWeaponController != null)
            {
                _localWeaponController.OnAmmoChanged -= HandleAmmoChanged;
                _localWeaponController.OnWeaponChanged -= HandleWeaponChanged;
                _localWeaponController.OnReloadStateChanged -= HandleReloadStateChanged;
                _localWeaponController.OnHitTargetConfirmed -= ShowHitmarker;
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

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
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
                _healthText.text = $"HP: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
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
                _ammoText.text = $"ĐẠN: {currentAmmo} / {magSize}";
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
            _hitmarkerCoroutine = StartCoroutine(HitmarkerRoutine(isHeadshot));
        }

        private IEnumerator HitmarkerRoutine(bool isHeadshot)
        {
            if (_hitmarkerImage != null)
            {
                _hitmarkerImage.gameObject.SetActive(true);
                _hitmarkerImage.color = isHeadshot ? Color.red : Color.white;
            }

            if (_crosshairImage != null)
            {
                _crosshairImage.color = isHeadshot ? Color.red : Color.yellow;
            }

            yield return new WaitForSeconds(0.1f);

            if (_hitmarkerImage != null)
            {
                _hitmarkerImage.gameObject.SetActive(false);
            }

            if (_crosshairImage != null)
            {
                _crosshairImage.color = new Color(1f, 1f, 1f, 0.85f);
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
    }
}
