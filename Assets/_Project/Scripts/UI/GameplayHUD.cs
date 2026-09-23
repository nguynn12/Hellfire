// Script: GameplayHUD.cs
// Mục đích: Quản lý toàn bộ giao diện HUD chiến đấu (Máu, Đạn, Tên vũ khí, Hitmarker, Trạng thái Gục & Hồi sinh) bằng uGUI Canvas (Mục 3, 6, 7).
// Môi trường thực thi: Client-only.

using System.Collections;
using Hellfire.Combat;
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

        [Header("Weapon & Ammo UI")]
        [SerializeField] private TextMeshProUGUI _weaponNameText;
        [SerializeField] private TextMeshProUGUI _ammoText;
        [SerializeField] private TextMeshProUGUI _reloadPromptText;

        [Header("Revive Interaction UI")]
        [SerializeField] private GameObject _revivePromptPanel;
        [SerializeField] private TextMeshProUGUI _revivePromptText;
        [SerializeField] private Slider _reviveProgressBar;

        [Header("Pause Menu")]
        [SerializeField] private GameObject _pauseMenuPanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _leaveGameButton;
        [SerializeField] private TextMeshProUGUI _playerInfoText;

        private bool _isPaused;
        private Health _localPlayerHealth;
        private WeaponController _localWeaponController;
        private PlayerRevive _localPlayerRevive;
        private Coroutine _hitmarkerCoroutine;

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

            UpdatePlayerInfo();
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

            // Phím ESC mở Pause Menu (Mục 7)
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePauseMenu();
            }
        }

        private void TryBindLocalPlayer()
        {
            if (_localPlayerHealth != null && _localWeaponController != null) return;

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
