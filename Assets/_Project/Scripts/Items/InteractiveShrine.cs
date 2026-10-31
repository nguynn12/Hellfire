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
        [SerializeField] private Renderer _shrineRenderer;
        [SerializeField] private Color _usedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

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

        private MaterialPropertyBlock _shrinePropBlock;
        private MaterialPropertyBlock _flamePropBlock;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorPropId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");

        private bool _isUsedByLocalPlayer = false;
        private bool _isLocalPlayerInRange = false;
        private List<UpgradeOptionData> _cachedOptions;

        // Đồng bộ ShrineId qua mạng để client nhận đúng ID xác định từ phòng Upgrade (Mục 17.6)
        private readonly NetworkVariable<int> _networkShrineId = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public int ShrineId => IsSpawned ? _networkShrineId.Value : _shrineId;
        public ShrineType Type => _shrineType;
        public string ShrineName => _shrineName;
        public bool IsUsedByLocalPlayer => _isUsedByLocalPlayer;

        public void SetShrineId(int id)
        {
            _shrineId = id;
            if (IsServer)
            {
                _networkShrineId.Value = id;
            }
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
            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_glowController == null) _glowController = GetComponentInChildren<GlowLightController>();

            // Tự động tạo 3D Text Prompt nếu chưa có
            if (_promptText == null)
            {
                var textObj = new GameObject("PromptText");
                textObj.transform.SetParent(transform, false);
                textObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                _promptText = textObj.AddComponent<TextMeshPro>();
                _promptText.fontSize = 4;
                _promptText.alignment = TextAlignmentOptions.Center;
                _promptText.color = Color.yellow;
                _promptText.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // Tự động sinh ID duy nhất dựa theo toạ độ thế giới nếu chưa được gán ID riêng (Khắc phục Lỗi 07)
            if (_shrineId == 1)
            {
                int posHash = (Mathf.RoundToInt(transform.position.x * 10f) * 73856093) ^ 
                              (Mathf.RoundToInt(transform.position.z * 10f) * 19349663);
                if (posHash != 0)
                {
                    _shrineId = Mathf.Abs(posHash) % 100000 + 1;
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
                        return new Color(0.85f, 0.25f, 1.0f, 1.0f); // Tím Huyền Bí (Vũ khí / Ma thuật)
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

            // 2. Đổi màu và phát sáng quả cầu lửa FlameVisual (Emission)
            if (_flameVisual != null)
            {
                _flameVisual.SetActive(true);
                var flameRenderer = _flameVisual.GetComponent<Renderer>();
                if (flameRenderer != null)
                {
                    if (_flamePropBlock == null) _flamePropBlock = new MaterialPropertyBlock();
                    flameRenderer.GetPropertyBlock(_flamePropBlock);
                    _flamePropBlock.SetColor(BaseColorPropId, glow);
                    _flamePropBlock.SetColor(LegacyColorPropId, glow);
                    _flamePropBlock.SetColor(EmissionColorPropId, glow * 2.5f); // Phát sáng HDR
                    flameRenderer.SetPropertyBlock(_flamePropBlock);
                }
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
            if (IsServer)
            {
                _networkShrineId.Value = _shrineId;
            }
            else
            {
                _shrineId = _networkShrineId.Value;
            }

            _networkShrineId.OnValueChanged += HandleShrineIdChanged;

            GenerateOptions();
            StartCoroutine(CheckLocalUsageDelayed());
        }

        public override void OnNetworkDespawn()
        {
            _networkShrineId.OnValueChanged -= HandleShrineIdChanged;
        }

        private void HandleShrineIdChanged(int previousValue, int newValue)
        {
            _shrineId = newValue;
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
            GameObject localPlayerObj = null;

            if (PlayerMovement.LocalInstance != null)
            {
                localPlayerObj = PlayerMovement.LocalInstance.gameObject;
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            }
            else
            {
                // Fallback khi test Offline: tìm Player trong Scene
                var playerMove = FindAnyObjectByType<PlayerMovement>();
                if (playerMove != null)
                {
                    localPlayerObj = playerMove.gameObject;
                }
            }

            if (localPlayerObj == null) return;

            // Tính khoảng cách từ người chơi đến bề mặt gần nhất của Collider (nếu có), thay vì chỉ đo từ tâm
            var colliders = GetComponents<Collider>();
            float dist = float.MaxValue;
            if (colliders != null && colliders.Length > 0)
            {
                foreach (var col in colliders)
                {
                    if (col.enabled)
                    {
                        float d = Vector3.Distance(col.ClosestPoint(localPlayerObj.transform.position), localPlayerObj.transform.position);
                        if (d < dist) dist = d;
                    }
                }
            }
            else
            {
                dist = Vector3.Distance(transform.position, localPlayerObj.transform.position);
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

                    // Chỉ xoay LookRotation nếu là 3D World Space Text (không nằm trong Canvas UI)
                    var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                    if (cam != null && _promptText.GetComponentInParent<Canvas>() == null)
                    {
                        _promptText.transform.rotation = Quaternion.LookRotation(_promptText.transform.position - cam.transform.position);
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
                    var pm = FindAnyObjectByType<PlayerMovement>();
                    if (pm != null) pm.SetCursorLock(true);
                }
            }
        }

        private bool _showFallbackChoiceGUI = false;

        private void OnGUI()
        {
            if (_isUsedByLocalPlayer) return;

            // 1. Luôn hiển thị gợi ý tương tác [E] giữa màn hình (đảm bảo 100% người chơi luôn thấy)
            if (_isLocalPlayerInRange && !_showFallbackChoiceGUI && (ShrineChoiceModalUI.Instance == null || !ShrineChoiceModalUI.Instance.IsOpen))
            {
                GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 22;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.alignment = TextAnchor.MiddleCenter;
                promptStyle.normal.textColor = Color.yellow;

                float w = 340f;
                float h = 50f;
                float x = (Screen.width - w) / 2f;
                float y = Screen.height * 0.65f;

                string promptText = _shrineType == ShrineType.WeaponModifierShrine
                    ? "[E] Mở Bàn Cường Hóa"
                    : "[E] Cầu Nguyện Tế Đàn";

                GUI.Box(new Rect(x, y, w, h), promptText, promptStyle);
            }

            // 2. Giao diện Fallback 3 thẻ bài nếu Canvas UI chưa sẵn sàng
            if (_showFallbackChoiceGUI && _cachedOptions != null && _cachedOptions.Count >= 3)
            {
                GUIStyle panelStyle = new GUIStyle(GUI.skin.box);
                panelStyle.fontSize = 24;
                panelStyle.fontStyle = FontStyle.Bold;
                panelStyle.alignment = TextAnchor.UpperCenter;

                float panelW = Screen.width * 0.7f;
                float panelH = Screen.height * 0.6f;
                float panelX = (Screen.width - panelW) / 2f;
                float panelY = (Screen.height - panelH) / 2f;

                GUI.Box(new Rect(panelX, panelY, panelW, panelH), "\n=== CHỌN BAN PHƯỚC TẾ ĐÀN ===", panelStyle);

                float cardW = (panelW - 60f) / 3f;
                float cardH = panelH * 0.6f;
                float startX = panelX + 20f;
                float cardY = panelY + panelH * 0.25f;

                for (int i = 0; i < 3; i++)
                {
                    var opt = _cachedOptions[i];
                    Rect btnRect = new Rect(startX + i * (cardW + 10f), cardY, cardW, cardH);
                    string label = $"[PHÍM {i + 1}]\n\n{opt.Title}\n\n{opt.Description}\n\n(Bấm để chọn)";
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
            if (_isUsedByLocalPlayer) return;

            if (_cachedOptions == null || _cachedOptions.Count == 0)
            {
                GenerateOptions();
            }

            string title = _shrineType == ShrineType.WeaponModifierShrine ? "BÀN CƯỜNG HÓA VŨ KHÍ" : "BAN PHƯỚC TẾ ĐÀN";
            string subtitle = _shrineType == ShrineType.WeaponModifierShrine
                ? "Nâng cấp hỏa lực cho TẤT CẢ vũ khí (giữ nguyên khi đổi súng)"
                : "Chọn 1 trong 3 ban phước vĩnh viễn (Phím [1], [2], [3] hoặc Click)";

            bool uiOpened = false;

            // 1. Thử mở qua Singleton Canvas UI
            if (ShrineChoiceModalUI.Instance != null)
            {
                ShrineChoiceModalUI.Instance.Open(title, subtitle, _cachedOptions, OnOptionSelectedLocally);
                uiOpened = ShrineChoiceModalUI.Instance.IsOpen;
            }

            // 2. Thử tìm Canvas UI trong Scene
            if (!uiOpened)
            {
                var modal = FindAnyObjectByType<ShrineChoiceModalUI>();
                if (modal != null)
                {
                    modal.Open(title, subtitle, _cachedOptions, OnOptionSelectedLocally);
                    uiOpened = modal.IsOpen;
                }
            }

            // 3. Nếu chưa có Canvas UI, tự động kích hoạt Fallback OnGUI
            if (!uiOpened)
            {
                _showFallbackChoiceGUI = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) pm.SetCursorLock(false);
            }
        }

        private void OnOptionSelectedLocally(int choiceIndex)
        {
            if (_isUsedByLocalPlayer) return;

            if (!GameModeHelper.IsOffline && IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                InteractServerRpc(choiceIndex);
            }
            else
            {
                ApplyUpgradeOffline(choiceIndex);
            }
        }

        private void ApplyUpgradeOffline(int choiceIndex)
        {
            var playerMove = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance : FindAnyObjectByType<PlayerMovement>();
            if (playerMove == null) return;

            var playerStats = playerMove.GetComponent<PlayerStats>();
            var weaponCtrl = playerMove.GetComponent<WeaponController>();
            if (playerStats == null) return;

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
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0f, bonusApsPercent: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_pen")
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0f, bonusApsPercent: 0f, bonusArmorPen: selectedOpt.PrimaryValue);
                        else if (selectedOpt.Id == "wpn_crit")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(
                                bonusDamagePercent: 0f,
                                bonusApsPercent: 0f,
                                bonusArmorPen: 0f,
                                bonusCritChance: selectedOpt.PrimaryValue,
                                bonusCritMultiplier: selectedOpt.SecondaryValue
                            );
                        }
                        else
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(0.15f, 0.08f, 5f, 0.05f, 0.2f);
                        }

                        successMsg = $"Đã cường hóa vũ khí: {selectedOpt.Title}!";
                    }
                    break;
            }

            _isUsedByLocalPlayer = true;
            ApplyUsedVisualLocally();
            Debug.Log($"<color=#00FF88>[InteractiveShrine] (Offline) {successMsg}</color>");
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

            // 2. Kiểm tra xem người chơi này đã từng dùng tế đàn này chưa
            if (playerStats.HasUsedShrine(_shrineId))
            {
                NotifyFeedbackClientRpc("Bạn đã nhận ban phước từ tế đàn này rồi!", false, GetTargetClientRpcParams(senderClientId));
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
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0f, bonusApsPercent: selectedOpt.PrimaryValue);
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tốc Độ Bắn (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_pen")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(bonusDamagePercent: 0f, bonusApsPercent: 0f, bonusArmorPen: selectedOpt.PrimaryValue);
                            successMsg = $"+{selectedOpt.PrimaryValue} Xuyên Giáp (Áp dụng TẤT CẢ vũ khí)!";
                        }
                        else if (selectedOpt.Id == "wpn_crit")
                        {
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(
                                bonusDamagePercent: 0f,
                                bonusApsPercent: 0f,
                                bonusArmorPen: 0f,
                                bonusCritChance: selectedOpt.PrimaryValue,
                                bonusCritMultiplier: selectedOpt.SecondaryValue
                            );
                            successMsg = $"+{Mathf.RoundToInt(selectedOpt.PrimaryValue * 100)}% Tỷ Lệ Chí Mạng & +{Mathf.RoundToInt(selectedOpt.SecondaryValue * 100)}% Sát Thương Crit!";
                        }
                        else
                        {
                            // Nâng cấp tổng hợp toàn diện
                            weaponCtrl.ApplyGlobalWeaponUpgradeServer(0.15f, 0.08f, 5f, 0.05f, 0.2f);
                            successMsg = "Cường Hóa Toàn Diện Hỏa Lực Cho Tất Cả Vũ Khí!";
                        }
                        success = true;
                    }
                    break;
            }

            if (success)
            {
                // Ghi nhận lịch sử người chơi đã dùng tế đàn này
                playerStats.RecordShrineUsed(_shrineId);

                // Dập lửa và tắt visual tế đàn riêng cho máy người này
                ExtinguishShrineClientRpc(GetTargetClientRpcParams(senderClientId));
                NotifyFeedbackClientRpc(successMsg, true, GetTargetClientRpcParams(senderClientId));
            }
            else
            {
                NotifyFeedbackClientRpc("Chỉ số của bạn đã đạt mức tối đa (Max Cap)!", false, GetTargetClientRpcParams(senderClientId));
            }
        }

        private void GenerateOptions()
        {
            _cachedOptions = new List<UpgradeOptionData>();
            var rng = new System.Random(_shrineId * 7919 + 17);

            if (_shrineType == ShrineType.BodyShrine)
            {
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "hp",
                    Title = "Sinh Lực Bất Diệt",
                    Description = "+30 Máu Tối Đa\nTăng lượng máu tối đa vĩnh viễn.",
                    BadgeText = "[THÂN THỂ]",
                    Category = UpgradeCategory.BodyStat,
                    PrimaryValue = 30f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "armor",
                    Title = "Giáp Hắc Thiết",
                    Description = "+15 Giáp Thân Thể\nGiảm đáng kể mọi sát thương nhận vào.",
                    BadgeText = "[THÂN THỂ]",
                    Category = UpgradeCategory.BodyStat,
                    PrimaryValue = 15f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "speed",
                    Title = "Thần Hành Bộ",
                    Description = "+12% Tốc Độ Di Chuyển\nTăng tốc độ chạy, né đòn cơ động.",
                    BadgeText = "[THÂN THỂ]",
                    Category = UpgradeCategory.BodyStat,
                    PrimaryValue = 0.12f
                });
            }
            else if (_shrineType == ShrineType.WeaponModifierShrine)
            {
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "wpn_damage",
                    Title = "Hỏa Lực Cuồng Nộ",
                    Description = "+20% Sát Thương Toàn Cục\nÁp dụng cho TẤT CẢ vũ khí (giữ nguyên khi đổi súng).",
                    BadgeText = "[VŨ KHÍ]",
                    Category = UpgradeCategory.GlobalWeapon,
                    PrimaryValue = 0.20f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "wpn_aps",
                    Title = "Xả Đạn Thần Tốc",
                    Description = "+15% Tốc Độ Bắn\nTăng nhịp bắn cho mọi loại súng.",
                    BadgeText = "[VŨ KHÍ]",
                    Category = UpgradeCategory.GlobalWeapon,
                    PrimaryValue = 0.15f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "wpn_crit",
                    Title = "Điểm Huyệt Tử Thần",
                    Description = "+10% Chí Mạng & +30% Sát Thương Crit\nÁp dụng cho tất cả các loại súng.",
                    BadgeText = "[VŨ KHÍ]",
                    Category = UpgradeCategory.GlobalWeapon,
                    PrimaryValue = 0.10f,
                    SecondaryValue = 0.30f
                });
            }
            else // Hybrid
            {
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "wpn_damage",
                    Title = "Cường Hóa Sát Thương",
                    Description = "+15% Sát Thương cho tất cả vũ khí.",
                    BadgeText = "[VŨ KHÍ]",
                    Category = UpgradeCategory.GlobalWeapon,
                    PrimaryValue = 0.15f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "hp",
                    Title = "Cường Hóa Máu",
                    Description = "+25 Máu Tối Đa cho người chơi.",
                    BadgeText = "[THÂN THỂ]",
                    Category = UpgradeCategory.BodyStat,
                    PrimaryValue = 25f
                });
                _cachedOptions.Add(new UpgradeOptionData
                {
                    Id = "wpn_pen",
                    Title = "Đạn Phá Giáp",
                    Description = "+15 Xuyên Giáp cho tất cả vũ khí.",
                    BadgeText = "[VŨ KHÍ]",
                    Category = UpgradeCategory.GlobalWeapon,
                    PrimaryValue = 15f
                });
            }
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

            if (_flameVisual != null) _flameVisual.SetActive(false);
            if (_shrineLight != null) _shrineLight.enabled = false;
            if (_promptText != null) _promptText.gameObject.SetActive(false);

            if (_shrineRenderer != null)
            {
                if (_shrinePropBlock == null) _shrinePropBlock = new MaterialPropertyBlock();
                _shrineRenderer.GetPropertyBlock(_shrinePropBlock);
                _shrinePropBlock.SetColor(BaseColorPropId, _usedColor);
                _shrinePropBlock.SetColor(LegacyColorPropId, _usedColor);
                _shrineRenderer.SetPropertyBlock(_shrinePropBlock);
            }

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
