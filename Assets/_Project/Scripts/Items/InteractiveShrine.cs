// Script: InteractiveShrine.cs
// Mục đích: Quản lý Tế đàn (Shrine) và Bàn Cường Hóa (Modifier Station) với 3 lựa chọn nâng cấp chỉ số.
//           Nâng cấp vũ khí áp dụng TOÀN CỤC (khi đổi súng vẫn giữ nguyên).
// Môi trường thực thi: Cả hai (Client hiển thị gợi ý 3D & mở UI 3 thẻ chọn, Server xác thực & áp dụng chỉ số).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Networking;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Items
{
    public enum ShrineType
    {
        BodyShrine,             // Tế đàn Thân Thể (3 lựa chọn Máu, Giáp, Tốc độ)
        WeaponModifierShrine,   // Bàn Cường Hóa / Modifier (3 lựa chọn Sát thương, Tốc bắn, Xuyên giáp, Chí mạng)
        HybridShrine            // Tế đàn Hỗn Hợp (3 lựa chọn ngẫu nhiên từ cả 2 nhóm)
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class InteractiveShrine : NetworkBehaviour
    {
        [Header("Shrine Identity")]
        [Tooltip("Mã ID duy nhất của tế đàn trên bản đồ")]
        [SerializeField] private int _shrineId = 1;

        [Tooltip("Loại hình nâng cấp của tế đàn")]
        [SerializeField] private ShrineType _shrineType = ShrineType.BodyShrine;

        [Tooltip("Tên hiển thị của tế đàn")]
        [SerializeField] private string _shrineName = "Tế Đàn Ban Phước";

        [Tooltip("Khoảng cách tối đa để có thể tương tác (mét)")]
        [SerializeField] private float _interactionDistance = 3.0f;

        [Header("World Space 3D Prompt hoặc UI Canvas")]
        [SerializeField] private TMP_Text _promptText;

        [Header("Local Visuals (Chỉ tắt trên máy người đã dùng)")]
        [SerializeField] private GameObject _flameVisual;
        [SerializeField] private Light _shrineLight;

        [Header("Glow & Lighting (Tùy biến màu phát sáng)")]
        [Tooltip("Thành phần điều khiển phát sáng tái sử dụng (Tùy chọn)")]
        [SerializeField] private GlowLightController _glowController;
        [Tooltip("Tự động chọn màu phát sáng rực rỡ theo loại Tế đàn: Đỏ (Máu), Tím (Vũ khí), Xanh (Hỗn hợp)")]
        [SerializeField] private bool _autoColorByType = true;
        [Tooltip("Màu phát sáng tùy chỉnh khi tắt _autoColorByType (Hỗ trợ màu HDR rực rỡ)")]
        [ColorUsage(true, true)]
        [SerializeField] private Color _activeGlowColor = new Color(0.2f, 0.8f, 1.0f, 1f);

        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _interactSound;

        [Header("First-Time Bounty Configuration")]
        [Tooltip("Có kích hoạt tặng đá cường hóa trong lần đầu mở bàn/tế đàn không")]
        [SerializeField] private bool _enableFirstOpenBounty = true;
        [Tooltip("Số lượng đá thưởng trong lần đầu mở")]
        [SerializeField] private int _firstOpenBountyAmount = 2;

        private bool _localBountyClaimed = false;

        // Chi phí Pha Lê theo cơ chế mới (Chọn thẻ: 2 Pha Lê, Đổi thẻ: 1 Pha Lê)
        public const int UpgradeCost = 2;
        public const int RerollCost = 1;

        private bool _isUsedByLocalPlayer = false;
        private bool _isLocalPlayerInRange = false;
        private List<UpgradeOptionData> _cachedOptions;

        private Collider[] _cachedColliders;
        private Camera _cachedMainCamera;
        private GameObject _cachedLocalPlayerObj;
        private GUIStyle _cachedPromptStyle;
        private GUIStyle _cachedFallbackPanelStyle;

        public int ShrineId => _shrineId;
        public ShrineType Type => _shrineType;
        public string ShrineName => _shrineName;
        public bool IsUsedByLocalPlayer => false;

        public void SetShrineId(int id)
        {
            _shrineId = id;
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            _cachedColliders = GetComponents<Collider>();

            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_glowController == null) _glowController = GetComponentInChildren<GlowLightController>();
            if (_shrineLight == null) _shrineLight = GetComponentInChildren<Light>();

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // Tự động sinh ID duy nhất dựa theo NetworkObjectId hoặc toạ độ thế giới nếu chưa được gán ID riêng (Khắc phục Lỗi 07)
            if (_shrineId <= 1)
            {
                if (NetworkObject != null && NetworkObject.IsSpawned && NetworkObjectId > 0)
                {
                    _shrineId = (int)(NetworkObjectId % 1000000) + 1;
                }
                else
                {
                    int posHash = (Mathf.RoundToInt(transform.position.x * 10f) * 73856093) ^ 
                                  (Mathf.RoundToInt(transform.position.z * 10f) * 19349663);
                    if (posHash != 0)
                    {
                        _shrineId = Mathf.Abs(posHash) % 100000 + 1;
                    }
                    else
                    {
                        _shrineId = UnityEngine.Random.Range(100, 99999);
                    }
                }
            }

            if (!IsSpawned)
            {
                GenerateOptions();
            }

            // Đảm bảo có UI modal trong scene nếu chưa có
            if (ShrineChoiceModalUI.Instance == null)
            {
                var existingUI = FindAnyObjectByType<ShrineChoiceModalUI>();
                if (existingUI == null)
                {
                    var uiObj = new GameObject("ShrineChoiceModalUI");
                    uiObj.AddComponent<ShrineChoiceModalUI>();
                }
            }

            ApplyGlowVisuals();
        }

        private void OnValidate()
        {
            ApplyGlowVisuals();
        }

        public Color ActiveGlowColor
        {
            get => GetEffectiveGlowColor();
            set
            {
                _activeGlowColor = value;
                _autoColorByType = false;
                ApplyGlowVisuals();
            }
        }

        public Color GetEffectiveGlowColor()
        {
            if (_autoColorByType)
            {
                switch (_shrineType)
                {
                    case ShrineType.BodyShrine:
                        return new Color(1.0f, 0.15f, 0.15f, 1.0f); // Đỏ Rực (Máu / Sinh mệnh)
                    case ShrineType.WeaponModifierShrine:
                        return new Color(0.15f, 1.0f, 0.35f, 1.0f); // Xanh Lá / Xanh Lục Bảo (Bàn Cường Hóa Vũ Khí)
                    case ShrineType.HybridShrine:
                    default:
                        return new Color(0.15f, 0.9f, 0.85f, 1.0f); // Xanh Lam Ngọc (Hỗn hợp)
                }
            }
            return _activeGlowColor;
        }

        /// <summary>
        /// Áp dụng màu phát sáng rực rỡ lên Point Light và Quả Cầu Lửa (FlameVisual).
        /// </summary>
        public void ApplyGlowVisuals()
        {
            if (_isUsedByLocalPlayer) return;

            Color glow = GetEffectiveGlowColor();

            // Nếu có gắn GlowLightController tái sử dụng, đồng bộ trạng thái
            if (_glowController != null)
            {
                _glowController.SetGlowColor(glow);
            }

            // 1. Đổi màu ánh sáng Point Light
            if (_shrineLight != null)
            {
                _shrineLight.color = glow;
                _shrineLight.enabled = true;
            }

            // 2. Kích hoạt ngọn lửa / hiệu ứng phát sáng FlameVisual (giữ nguyên Material gốc của model)
            if (_flameVisual != null)
            {
                _flameVisual.SetActive(true);
            }
        }

        // ================= CONTEXT MENUS (ĐỔI MÀU NHANH TRONG UNITY INSPECTOR) =================
        [ContextMenu("Set Glow: Red (Đỏ Dung Nham)")]
        public void SetGlowRed() => SetCustomGlowColor(new Color(1f, 0.15f, 0.15f, 1f));

        [ContextMenu("Set Glow: Blue (Xanh Lam Cyan)")]
        public void SetGlowBlue() => SetCustomGlowColor(new Color(0.15f, 0.85f, 1f, 1f));

        [ContextMenu("Set Glow: Green (Xanh Lục Bảo)")]
        public void SetGlowGreen() => SetCustomGlowColor(new Color(0.15f, 1f, 0.35f, 1f));

        [ContextMenu("Set Glow: Purple (Tím Ma Thuật)")]
        public void SetGlowPurple() => SetCustomGlowColor(new Color(0.85f, 0.25f, 1f, 1f));

        [ContextMenu("Set Glow: Gold (Vàng Kim)")]
        public void SetGlowGold() => SetCustomGlowColor(new Color(1f, 0.85f, 0.15f, 1f));

        public void SetCustomGlowColor(Color color)
        {
            _autoColorByType = false;
            _activeGlowColor = color;
            ApplyGlowVisuals();
        }

        public override void OnNetworkSpawn()
        {
            if (_shrineId <= 1 && NetworkObjectId > 0)
            {
                _shrineId = (int)(NetworkObjectId % 1000000) + 1;
            }
            GenerateOptions();
            StartCoroutine(CheckLocalUsageDelayed());
        }

        private IEnumerator CheckLocalUsageDelayed()
        {
            yield return new WaitForSeconds(0.5f);

            var localPlayerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayerObj != null)
            {
                var stats = localPlayerObj.GetComponent<PlayerStats>();
                if (stats != null && stats.HasUsedShrine(_shrineId))
                {
                    ApplyUsedVisualLocally();
                }
            }
        }

        private void Update()
        {
            if (_isUsedByLocalPlayer)
            {
                if (_promptText != null && _promptText.gameObject.activeSelf)
                {
                    _promptText.gameObject.SetActive(false);
                }
                return;
            }

            CheckLocalPlayerInteraction();
        }

        private void CheckLocalPlayerInteraction()
        {
            if (_cachedLocalPlayerObj == null)
            {
                if (PlayerMovement.LocalInstance != null)
                {
                    _cachedLocalPlayerObj = PlayerMovement.LocalInstance.gameObject;
                }
                else if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    _cachedLocalPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
                }
                else
                {
                    var playerMove = FindAnyObjectByType<PlayerMovement>();
                    if (playerMove != null) _cachedLocalPlayerObj = playerMove.gameObject;
                }
            }

            if (_cachedLocalPlayerObj == null) return;

            // Dùng mảng colliders đã cache thay vì gọi GetComponents mỗi frame
            if (_cachedColliders == null || _cachedColliders.Length == 0)
            {
                _cachedColliders = GetComponents<Collider>();
            }

            Vector3 playerPos = _cachedLocalPlayerObj.transform.position;
            float dist = float.MaxValue;
            if (_cachedColliders != null && _cachedColliders.Length > 0)
            {
                for (int i = 0; i < _cachedColliders.Length; i++)
                {
                    var col = _cachedColliders[i];
                    if (col != null && col.enabled)
                    {
                        float d = Vector3.Distance(col.ClosestPoint(playerPos), playerPos);
                        if (d < dist) dist = d;
                    }
                }
            }
            else
            {
                dist = Vector3.Distance(transform.position, playerPos);
            }

            // Nếu người chơi ở trong bán kính tương tác hoặc đang chạm vào collider
            _isLocalPlayerInRange = (dist <= _interactionDistance || dist <= 0.05f);

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(_isLocalPlayerInRange);
                if (_isLocalPlayerInRange)
                {
                    _promptText.text = _shrineType == ShrineType.WeaponModifierShrine
                        ? "[E] Mở Bàn Cường Hóa"
                        : "[E] Cầu Nguyện Tế Đàn";

                    // Dùng camera đã cache thay vì quét Camera.main mỗi frame
                    if (_cachedMainCamera == null) _cachedMainCamera = Camera.main;
                    if (_cachedMainCamera != null && _promptText.GetComponentInParent<Canvas>() == null)
                    {
                        _promptText.transform.rotation = Quaternion.LookRotation(_promptText.transform.position - _cachedMainCamera.transform.position);
                    }
                }
            }

            // Người chơi nhấn phím E khi ở gần tế đàn
            if (_isLocalPlayerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OpenChoiceUI();
            }

            // Xử lý phím tắt 1, 2, 3 khi mở giao diện Fallback
            if (_showFallbackChoiceGUI && Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) { _showFallbackChoiceGUI = false; OnOptionSelectedLocally(0); }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) { _showFallbackChoiceGUI = false; OnOptionSelectedLocally(1); }
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) { _showFallbackChoiceGUI = false; OnOptionSelectedLocally(2); }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    _showFallbackChoiceGUI = false;
                    var pm = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance : FindAnyObjectByType<PlayerMovement>();
                    if (pm != null) pm.SetCursorLock(true);
                }
            }
        }

        private bool _showFallbackChoiceGUI = false;

        private void OnGUI()
        {
            if (_isUsedByLocalPlayer) return;

            // 1. Luôn hiển thị gợi ý tương tác [E] giữa màn hình (dùng style đã cache)
            if (_isLocalPlayerInRange && !_showFallbackChoiceGUI && (ShrineChoiceModalUI.Instance == null || !ShrineChoiceModalUI.Instance.IsOpen))
            {
                if (_cachedPromptStyle == null)
                {
                    _cachedPromptStyle = new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.yellow }
                    };
                }

                float w = 340f;
                float h = 50f;
                float x = (Screen.width - w) / 2f;
                float y = Screen.height * 0.65f;

                string promptText = _shrineType == ShrineType.WeaponModifierShrine
                    ? "[E] Mở Bàn Cường Hóa"
                    : "[E] Cầu Nguyện Tế Đàn";

                GUI.Box(new Rect(x, y, w, h), promptText, _cachedPromptStyle);
            }

            // 2. Giao diện Fallback 3 thẻ bài nếu Canvas UI chưa sẵn sàng
            if (_showFallbackChoiceGUI && _cachedOptions != null && _cachedOptions.Count >= 3)
            {
                if (_cachedFallbackPanelStyle == null)
                {
                    _cachedFallbackPanelStyle = new GUIStyle(GUI.skin.box)
                    {
                        fontSize = 24,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperCenter
                    };
                }

                float panelW = Screen.width * 0.7f;
                float panelH = Screen.height * 0.6f;
                float panelX = (Screen.width - panelW) / 2f;
                float panelY = (Screen.height - panelH) / 2f;

                GUI.Box(new Rect(panelX, panelY, panelW, panelH), "\n=== CHỌN BAN PHƯỚC TẾ ĐÀN ===", _cachedFallbackPanelStyle);

                float cardW = (panelW - 60f) / 3f;
                float cardH = panelH * 0.6f;
                float startX = panelX + 20f;
                float cardY = panelY + panelH * 0.25f;

                for (int i = 0; i < 3; i++)
                {
                    var opt = _cachedOptions[i];
                    Rect btnRect = new Rect(startX + i * (cardW + 10f), cardY, cardW, cardH);
                    string starText = opt.GetStarIcons();
                    string label = $"{starText} ({opt.GetTierName()})\n\n{opt.Title}\n\n{opt.Description}\n\n(Bấm để chọn)";
                    if (GUI.Button(btnRect, label))
                    {
                        _showFallbackChoiceGUI = false;
                        OnOptionSelectedLocally(i);
                    }
                }
            }
        }

        private void OpenChoiceUI()
        {
            CheckAndClaimFirstOpenBounty();
            if (_cachedOptions == null || _cachedOptions.Count == 0)
            {
                GenerateOptions();
            }

            string title = _shrineType == ShrineType.WeaponModifierShrine ? "BÀN CƯỜNG HÓA VŨ KHÍ" : "BAN PHƯỚC TẾ ĐÀN";
            string subtitle = _shrineType == ShrineType.WeaponModifierShrine
                ? $"Mỗi lần nâng cấp tốn {UpgradeCost} Pha Lê • Làm mới tốn {RerollCost} Pha Lê"
                : $"Mỗi lần ban phước tốn {UpgradeCost} Pha Lê • Làm mới tốn {RerollCost} Pha Lê";

            bool uiOpened = false;

            // 1. Đảm bảo ShrineChoiceModalUI luôn tồn tại trong Scene
            if (ShrineChoiceModalUI.Instance == null)
            {
                var modal = FindAnyObjectByType<ShrineChoiceModalUI>();
                if (modal == null)
                {
                    string modalResPath = _shrineType == ShrineType.WeaponModifierShrine
                        ? "UI/WeaponWorkbenchModal"
                        : "UI/ShrineChoiceModal";
                    var modalPrefab = Resources.Load<GameObject>(modalResPath) ?? Resources.Load<GameObject>("UI/ShrineChoiceModal");
                    if (modalPrefab != null)
                    {
                        Instantiate(modalPrefab);
                    }
                    else
                    {
                        var go = new GameObject("ShrineChoiceModalUI");
                        go.AddComponent<ShrineChoiceModalUI>();
                    }
                }
            }

            if (ShrineChoiceModalUI.Instance != null)
            {
                ShrineChoiceModalUI.Instance.Open(
                    title, 
                    subtitle, 
                    _cachedOptions, 
                    OnOptionSelectedLocally,
                    _shrineType,
                    UpgradeCost,
                    RerollCost,
                    OnRerollLocally,
                    OnCloseLocally
                );
                uiOpened = ShrineChoiceModalUI.Instance.IsOpen;
            }

            // 2. Dự phòng an toàn tuyệt đối nếu Canvas bị ẩn
            if (!uiOpened)
            {
                _showFallbackChoiceGUI = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) pm.SetCursorLock(false);
            }
        }

        private void OnCloseLocally()
        {
            _showFallbackChoiceGUI = false;
        }

        private void OnOptionSelectedLocally(int choiceIndex)
        {
            if (GameModeHelper.IsOffline || !IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                ApplyUpgradeOffline(choiceIndex);
            }
            else
            {
                InteractServerRpc(choiceIndex);
            }
        }

        private void OnRerollLocally()
        {
            var playerMove = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance : FindAnyObjectByType<PlayerMovement>();
            if (playerMove == null) return;

            var playerStats = playerMove.GetComponent<PlayerStats>();
            if (playerStats == null) return;

            if (GameModeHelper.IsOffline || !IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                if (!playerStats.TrySpendCrystals(_shrineType, RerollCost))
                {
                    Debug.LogWarning($"[InteractiveShrine] Không đủ {RerollCost} Pha Lê để đổi thẻ!");
                    return;
                }

                GenerateOptions(true);
                if (ShrineChoiceModalUI.Instance != null && ShrineChoiceModalUI.Instance.IsOpen)
                {
                    ShrineChoiceModalUI.Instance.RefreshOptions(_cachedOptions);
                }
            }
            else
            {
                RerollServerRpc();
            }
        }

        private void ApplyUpgradeOffline(int choiceIndex)
        {
            var playerMove = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance : FindAnyObjectByType<PlayerMovement>();
            if (playerMove == null) return;

            var playerStats = playerMove.GetComponent<PlayerStats>();
            var weaponCtrl = playerMove.GetComponent<WeaponController>();
            if (playerStats == null) return;

            if (!playerStats.TrySpendCrystals(_shrineType, UpgradeCost))
            {
                Debug.LogWarning($"[InteractiveShrine] Không đủ {UpgradeCost} Pha Lê để nâng cấp!");
                return;
            }

            if (_cachedOptions == null || _cachedOptions.Count == 0)
            {
                GenerateOptions();
            }

            choiceIndex = Mathf.Clamp(choiceIndex, 0, _cachedOptions.Count - 1);
            var selectedOpt = _cachedOptions[choiceIndex];

            string successMsg = "";
            switch (selectedOpt.Category)
            {
                case UpgradeCategory.BodyStat:
                    if (selectedOpt.Id == "hp")
                    {
                        playerStats.TryApplyBodyBuff(selectedOpt.PrimaryValue, 0f, 0f);
                        successMsg = $"+{selectedOpt.PrimaryValue} Máu Tối Đa!";
                    }
                    else if (selectedOpt.Id == "armor")
                    {
                        playerStats.TryApplyBodyBuff(0f, selectedOpt.PrimaryValue, 0f);
                        successMsg = $"+{selectedOpt.PrimaryValue} Giáp Thân Thể!";
                    }
                    else if (selectedOpt.Id == "speed")
                    {
                        playerStats.TryApplyBodyBuff(0f, 0f, selectedOpt.PrimaryValue);
                        successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tốc Độ Di Chuyển!";
                    }
                    else if (selectedOpt.Id == "full_heal")
                    {
                        playerStats.TryApplyBodyBuff(selectedOpt.PrimaryValue, 0f, 0f);
                        var health = playerMove.GetComponent<Health>();
                        if (health != null) health.Heal(100f);
                        successMsg = $"Hồi Đầy Máu & +{selectedOpt.PrimaryValue} Máu Tối Đa!";
                    }
                    break;

                case UpgradeCategory.GlobalWeapon:
                case UpgradeCategory.SpecialModifier:
                    if (weaponCtrl != null)
                    {
                        if (selectedOpt.Id == "wpn_damage")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_aps")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusApsPercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_reload")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusReloadSpeedPercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_accuracy")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusAccuracyPercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_mag")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusMagPercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_recoil")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusRecoilReduction: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_pen")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusArmorPen: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_crit")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(
                                bonusCritChance: selectedOpt.PrimaryValue,
                                bonusCritMultiplier: selectedOpt.SecondaryValue
                            );
                        }
                        else
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0.15f, bonusApsPercent: 0.08f, bonusArmorPen: 5f);
                        }

                        successMsg = $"Đã cường hóa vũ khí: {selectedOpt.Title}!";
                    }
                    break;
            }

            // Sinh ngay 3 thẻ mới để người chơi có thể tiếp tục nâng cấp nếu còn Pha Lê
            GenerateOptions(true);
            if (ShrineChoiceModalUI.Instance != null && ShrineChoiceModalUI.Instance.IsOpen)
            {
                ShrineChoiceModalUI.Instance.RefreshOptions(_cachedOptions);
            }
            Debug.Log($"<color=#00FF88>[InteractiveShrine] (Offline) Đã tiêu hao {UpgradeCost} Pha Lê. {successMsg}</color>");
        }

        private void CheckAndClaimFirstOpenBounty()
        {
            if (!_enableFirstOpenBounty || _localBountyClaimed) return;

            var playerMove = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance : FindAnyObjectByType<PlayerMovement>();
            if (playerMove == null) return;

            var stats = playerMove.GetComponent<PlayerStats>();
            if (stats == null) return;

            if (GameModeHelper.IsOffline || !IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                // Xử lý Offline
                if (stats.TryClaimFirstOpenReward(_shrineId, _shrineType, _firstOpenBountyAmount))
                {
                    _localBountyClaimed = true;
                    TriggerFirstOpenVisualFeedback(_shrineType, _firstOpenBountyAmount);
                }
            }
            else
            {
                // Xử lý Online qua Server RPC
                ClaimFirstOpenBountyServerRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClaimFirstOpenBountyServerRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client) || client.PlayerObject == null) return;

            var stats = client.PlayerObject.GetComponent<PlayerStats>();
            if (stats != null && stats.TryClaimFirstOpenReward(_shrineId, _shrineType, _firstOpenBountyAmount))
            {
                NotifyFirstOpenBountyClientRpc(_shrineType, _firstOpenBountyAmount, GetTargetClientRpcParams(senderClientId));
            }
        }

        [ClientRpc]
        private void NotifyFirstOpenBountyClientRpc(ShrineType type, int amount, ClientRpcParams rpcParams = default)
        {
            _localBountyClaimed = true;
            TriggerFirstOpenVisualFeedback(type, amount);
        }

        private void TriggerFirstOpenVisualFeedback(ShrineType type, int amount)
        {
            _localBountyClaimed = true;

            string crystalName = type == ShrineType.WeaponModifierShrine ? "Pha Lê Xanh" : "Pha Lê Đỏ";
            string stationName = type == ShrineType.WeaponModifierShrine ? "Bàn Cường Hóa" : "Tế Đàn";
            string toastMsg = $"+{amount} {crystalName} (Khai mở {stationName} lần đầu!)";
            Color toastColor = type == ShrineType.WeaponModifierShrine ? new Color(0.15f, 1f, 0.35f) : new Color(1f, 0.25f, 0.25f);

            Debug.Log($"<color=#00FF88>[FirstOpenBounty] {toastMsg}</color>");

            if (GameplayHUD.Instance != null)
            {
                GameplayHUD.Instance.ShowFloatingToast(toastMsg, toastColor);
            }
            else
            {
                var hud = FindAnyObjectByType<GameplayHUD>();
                if (hud != null) hud.ShowFloatingToast(toastMsg, toastColor);
            }

            if (_audioSource != null && _interactSound != null)
            {
                _audioSource.PlayOneShot(_interactSound, 0.85f);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void InteractServerRpc(int choiceIndex, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client) || client.PlayerObject == null)
            {
                return;
            }

            GameObject playerObj = client.PlayerObject.gameObject;

            // 1. Kiểm tra cự ly an toàn (< 3m)
            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist > _interactionDistance + 1.5f)
            {
                NotifyFeedbackClientRpc("Bạn ở quá xa tế đàn!", false, GetTargetClientRpcParams(senderClientId));
                return;
            }

            var playerStats = playerObj.GetComponent<PlayerStats>();
            var weaponCtrl = playerObj.GetComponent<WeaponController>();

            if (playerStats == null)
            {
                return;
            }

            // 2. Kiểm tra và tiêu hao Pha Lê tương ứng
            if (!playerStats.TrySpendCrystals(_shrineType, UpgradeCost))
            {
                NotifyFeedbackClientRpc($"Không đủ {UpgradeCost} Pha Lê để nâng cấp!", false, GetTargetClientRpcParams(senderClientId));
                return;
            }

            // 3. Thực thi áp dụng nâng cấp dựa trên lựa chọn (0, 1 hoặc 2)
            if (_cachedOptions == null || _cachedOptions.Count == 0)
            {
                GenerateOptions();
            }

            choiceIndex = Mathf.Clamp(choiceIndex, 0, _cachedOptions.Count - 1);
            var selectedOpt = _cachedOptions[choiceIndex];

            string successMsg = "";
            bool success = false;

            switch (selectedOpt.Category)
            {
                case UpgradeCategory.BodyStat:
                    if (selectedOpt.Id == "hp")
                    {
                        success = playerStats.TryApplyBodyBuff(selectedOpt.PrimaryValue, 0f, 0f);
                        successMsg = $"+{selectedOpt.PrimaryValue} Máu Tối Đa!";
                    }
                    else if (selectedOpt.Id == "armor")
                    {
                        success = playerStats.TryApplyBodyBuff(0f, selectedOpt.PrimaryValue, 0f);
                        successMsg = $"+{selectedOpt.PrimaryValue} Giáp Thân Thể!";
                    }
                    else if (selectedOpt.Id == "speed")
                    {
                        success = playerStats.TryApplyBodyBuff(0f, 0f, selectedOpt.PrimaryValue);
                        successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tốc Độ Di Chuyển!";
                    }
                    else if (selectedOpt.Id == "full_heal")
                    {
                        success = playerStats.TryApplyBodyBuff(selectedOpt.PrimaryValue, 0f, 0f);
                        var health = playerObj.GetComponent<Health>();
                        if (health != null) health.Heal(100f);
                        successMsg = $"Hồi Đầy Máu & +{selectedOpt.PrimaryValue} Máu Tối Đa!";
                    }
                    break;

                case UpgradeCategory.GlobalWeapon:
                case UpgradeCategory.SpecialModifier:
                    if (weaponCtrl != null)
                    {
                        if (selectedOpt.Id == "wpn_damage")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Sát Thương (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_aps")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusApsPercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tốc Độ Bắn (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_reload")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusReloadSpeedPercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tốc Độ Nạp Đạn (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_accuracy")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusAccuracyPercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Độ Chính Xác / Giảm Tản Đạn (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_mag")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusMagPercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Dung Lượng Băng Đạn (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_recoil")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusRecoilReduction: selectedOpt.PrimaryValue);
                            successMsg = $"-{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Độ Giật Súng & Camera (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_pen")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusArmorPen: selectedOpt.PrimaryValue);
                            successMsg = $"+{selectedOpt.PrimaryValue} Xuyên Giáp (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_crit")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(
                                bonusCritChance: selectedOpt.PrimaryValue,
                                bonusCritMultiplier: selectedOpt.SecondaryValue
                            );
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tỷ Lệ Chí Mạng & +{Mathf.RoundToInt(selectedOpt.SecondaryValue * 100)}% Sát Thương Crit!";
                        }
                        else
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0.15f, bonusApsPercent: 0.08f, bonusArmorPen: 5f);
                            successMsg = "Cường Hóa Toàn Diện Hỏa Lực Cho Tất Cả Vũ Khí!";
                        }
                        success = true;
                    }
                    break;
            }

            if (success)
            {
                playerStats.RecordShrineUsed(_shrineId);
                NotifyFeedbackClientRpc(successMsg, true, GetTargetClientRpcParams(senderClientId));

                // Đổi 3 thẻ mới và gửi về cho Client tiếp tục chọn
                GenerateOptions(true);
                RefreshClientChoicesClientRpc(GetTargetClientRpcParams(senderClientId));
            }
            else
            {
                NotifyFeedbackClientRpc("Chỉ số của bạn đã đạt mức tối đa (Max Cap)!", false, GetTargetClientRpcParams(senderClientId));
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RerollServerRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client) || client.PlayerObject == null) return;
            GameObject playerObj = client.PlayerObject.gameObject;
            var playerStats = playerObj.GetComponent<PlayerStats>();
            if (playerStats == null) return;

            if (!playerStats.TrySpendCrystals(_shrineType, RerollCost))
            {
                NotifyFeedbackClientRpc($"Không đủ {RerollCost} Pha Lê để đổi thẻ!", false, GetTargetClientRpcParams(senderClientId));
                return;
            }

            GenerateOptions(true);
            RefreshClientChoicesClientRpc(GetTargetClientRpcParams(senderClientId));
        }

        [ClientRpc]
        private void RefreshClientChoicesClientRpc(ClientRpcParams rpcParams = default)
        {
            GenerateOptions(true);
            if (ShrineChoiceModalUI.Instance != null && ShrineChoiceModalUI.Instance.IsOpen)
            {
                ShrineChoiceModalUI.Instance.RefreshOptions(_cachedOptions);
            }
        }

        private void GenerateOptions(bool randomize = false)
        {
            _cachedOptions = new List<UpgradeOptionData>();
            var rng = randomize ? new System.Random() : new System.Random(_shrineId * 7919 + 17);

            if (_shrineType == ShrineType.BodyShrine)
            {
                var bodyPool = new List<System.Func<UpgradeOptionData>>
                {
                    () => CreateStarOption("hp", "Sinh Lực Bất Diệt", "THÂN THỂ", UpgradeCategory.BodyStat, 30f, 0f, "Máu Tối Đa", rng),
                    () => CreateStarOption("armor", "Giáp Hắc Thiết", "THÂN THỂ", UpgradeCategory.BodyStat, 15f, 0f, "Giáp Thân Thể", rng),
                    () => CreateStarOption("speed", "Thần Hành Bộ", "THÂN THỂ", UpgradeCategory.BodyStat, 0.12f, 0f, "Tốc Độ Chạy", rng),
                    () => CreateStarOption("hp", "Huyết Khí Tinh Hoa", "THÂN THỂ", UpgradeCategory.BodyStat, 40f, 0f, "Máu Tối Đa", rng),
                    () => CreateStarOption("armor", "Kim Cang Hộ Thể", "THÂN THỂ", UpgradeCategory.BodyStat, 20f, 0f, "Giáp Thân Thể", rng)
                };

                for (int i = bodyPool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = bodyPool[i];
                    bodyPool[i] = bodyPool[j];
                    bodyPool[j] = temp;
                }

                for (int i = 0; i < 3 && i < bodyPool.Count; i++)
                {
                    _cachedOptions.Add(bodyPool[i]());
                }
            }
            else if (_shrineType == ShrineType.WeaponModifierShrine)
            {
                var weaponPool = new List<System.Func<UpgradeOptionData>>
                {
                    () => CreateStarOption("wpn_damage", "Hỏa Lực Cuồng Nộ", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.20f, 0f, "Sát Thương Toàn Cục", rng),
                    () => CreateStarOption("wpn_aps", "Xả Đạn Thần Tốc", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.15f, 0f, "Tốc Độ Bắn", rng),
                    () => CreateStarOption("wpn_reload", "Thay Đạn Siêu Tốc", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.25f, 0f, "Tốc Độ Nạp Đạn", rng),
                    () => CreateStarOption("wpn_accuracy", "Bách Phát Bách Trúng", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.20f, 0f, "Độ Chính Xác (Giảm Tản Đạn)", rng),
                    () => CreateStarOption("wpn_mag", "Băng Đạn Mở Rộng", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.25f, 0f, "Dung Lượng Băng Đạn", rng),
                    () => CreateStarOption("wpn_recoil", "Ổn Định Báng Súng", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.25f, 0f, "Giảm Độ Giật", rng),
                    () => CreateStarOption("wpn_pen", "Đạn Phá Giáp", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 15f, 0f, "Xuyên Giáp", rng),
                    () => CreateStarOption("wpn_crit", "Điểm Huyệt Tử Thần", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.10f, 0.30f, "Chí Mạng & Sát Thương Crit", rng)
                };

                // Trộn ngẫu nhiên (Fisher-Yates Shuffle) bằng RNG deterministic
                for (int i = weaponPool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = weaponPool[i];
                    weaponPool[i] = weaponPool[j];
                    weaponPool[j] = temp;
                }

                for (int i = 0; i < 3 && i < weaponPool.Count; i++)
                {
                    _cachedOptions.Add(weaponPool[i]());
                }
            }
            else // Hybrid
            {
                var hybridPool = new List<System.Func<UpgradeOptionData>>
                {
                    () => CreateStarOption("wpn_damage", "Cường Hóa Sát Thương", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.18f, 0f, "Sát Thương Toàn Cục", rng),
                    () => CreateStarOption("hp", "Cường Hóa Sinh Lực", "THÂN THỂ", UpgradeCategory.BodyStat, 25f, 0f, "Máu Tối Đa", rng),
                    () => CreateStarOption("wpn_reload", "Thay Đạn Thần Tốc", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.20f, 0f, "Tốc Độ Nạp Đạn", rng),
                    () => CreateStarOption("armor", "Giáp Hắc Thiết", "THÂN THỂ", UpgradeCategory.BodyStat, 15f, 0f, "Giáp Thân Thể", rng),
                    () => CreateStarOption("wpn_recoil", "Ổn Định Báng Súng", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.20f, 0f, "Giảm Độ Giật", rng),
                    () => CreateStarOption("speed", "Thần Hành Bộ", "THÂN THỂ", UpgradeCategory.BodyStat, 0.10f, 0f, "Tốc Độ Chạy", rng),
                    () => CreateStarOption("wpn_accuracy", "Tập Trung Ngắm Bắn", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.18f, 0f, "Độ Chính Xác", rng),
                    () => CreateStarOption("wpn_mag", "Băng Đạn Mở Rộng", "VŨ KHÍ", UpgradeCategory.GlobalWeapon, 0.20f, 0f, "Dung Lượng Băng Đạn", rng)
                };

                for (int i = hybridPool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = hybridPool[i];
                    hybridPool[i] = hybridPool[j];
                    hybridPool[j] = temp;
                }

                for (int i = 0; i < 3 && i < hybridPool.Count; i++)
                {
                    _cachedOptions.Add(hybridPool[i]());
                }
            }
        }

        private UpgradeOptionData CreateStarOption(
            string id,
            string title,
            string badgeText,
            UpgradeCategory category,
            float basePrimary,
            float baseSecondary,
            string statName,
            System.Random rng
        )
        {
            // Tỷ lệ sao ngẫu nhiên: 1 Sao (60%), 2 Sao (30%), 3 Sao (10%)
            int roll = rng.Next(100);
            int stars = 1;
            float multiplier = 1.0f; // 100%

            if (roll >= 90)
            {
                stars = 3;
                multiplier = 2.0f; // 200% thưởng
            }
            else if (roll >= 60)
            {
                stars = 2;
                multiplier = 1.5f; // 150% thưởng
            }

            float finalPrimary = basePrimary * multiplier;
            float finalSecondary = baseSecondary * multiplier;

            string starIcons = stars == 3 ? "★★★" : (stars == 2 ? "★★☆" : "★☆☆");
            string tierName = stars == 3 ? "HUYỀN THOẠI (+200%)" : (stars == 2 ? "HIẾM (+150%)" : "CƠ BẢN (+100%)");

            string desc;
            if (id == "hp" || id == "armor" || id == "wpn_pen")
            {
                desc = $"+{Mathf.RoundToInt(finalPrimary)} {statName}";
            }
            else if (id == "wpn_crit")
            {
                desc = $"+{Mathf.RoundToInt(finalPrimary * 100f)}% Tỉ Lệ & +{Mathf.RoundToInt(finalSecondary * 100f)}% DMG Crit";
            }
            else if (id == "wpn_recoil")
            {
                desc = $"-{Mathf.RoundToInt(finalPrimary * 100f)}% Độ Giật Súng";
            }
            else if (id == "wpn_accuracy")
            {
                desc = $"-{Mathf.RoundToInt(finalPrimary * 100f)}% Độ Lệch Đạn";
            }
            else
            {
                desc = $"+{Mathf.RoundToInt(finalPrimary * 100f)}% {statName}";
            }

            return new UpgradeOptionData
            {
                Id = id,
                Title = $"{title}",
                Description = desc,
                BadgeText = $"{badgeText}",
                Category = category,
                PrimaryValue = finalPrimary,
                SecondaryValue = finalSecondary,
                Stars = stars,
                StarMultiplier = multiplier
            };
        }

        [ClientRpc]
        private void ExtinguishShrineClientRpc(ClientRpcParams rpcParams = default)
        {
            ApplyUsedVisualLocally();
        }

        private void ApplyUsedVisualLocally()
        {
            _isUsedByLocalPlayer = true;

            if (_glowController != null)
            {
                _glowController.Extinguish();
            }

            if (_flameVisual != null)
            {
                _flameVisual.SetActive(false);
            }
            if (_shrineLight != null) _shrineLight.enabled = false;
            if (_promptText != null) _promptText.gameObject.SetActive(false);

            if (_audioSource != null && _interactSound != null)
            {
                _audioSource.PlayOneShot(_interactSound);
            }
        }

        [ClientRpc]
        private void NotifyFeedbackClientRpc(string message, bool isSuccess, ClientRpcParams rpcParams = default)
        {
            if (isSuccess)
            {
                Debug.Log($"<color=#00FF88>[InteractiveShrine] {message}</color>");
            }
            else
            {
                Debug.LogWarning($"[InteractiveShrine] {message}");
            }
        }

        private ClientRpcParams GetTargetClientRpcParams(ulong targetClientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
        }
    }
}
