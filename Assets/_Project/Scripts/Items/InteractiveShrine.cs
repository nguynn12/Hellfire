// Script: InteractiveShrine.cs
// Mục đích: Quản lý Tế đàn (Shrine) và Bàn Cường Hóa (Modifier Station) với 3 lựa chọn nâng cấp chỉ số.
//           Nâng cấp vũ khí áp dụng TOÀN CỤC (khi đổi súng vẫn giữ nguyên).
// Môi trường thực thi: Cả hai (Client hiển thị gợi ý 3D & mở UI 3 thẻ chọn, Server xác thực & áp dụng chỉ số).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
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

        [Header("World Space 3D Prompt")]
        [SerializeField] private TextMeshPro _promptText;

        [Header("Local Visuals (Chỉ tắt trên máy người đã dùng)")]
        [SerializeField] private GameObject _flameVisual;
        [SerializeField] private Light _shrineLight;
        [SerializeField] private Renderer _shrineRenderer;
        [SerializeField] private Color _usedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _interactSound;

        private bool _isUsedByLocalPlayer = false;
        private bool _isLocalPlayerInRange = false;
        private List<UpgradeOptionData> _cachedOptions;

        public int ShrineId => _shrineId;
        public ShrineType Type => _shrineType;
        public bool IsUsedByLocalPlayer => _isUsedByLocalPlayer;

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
            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        public override void OnNetworkSpawn()
        {
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
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) return;

            float dist = Vector3.Distance(transform.position, localPlayerObj.transform.position);
            _isLocalPlayerInRange = (dist <= _interactionDistance);

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(_isLocalPlayerInRange);
                if (_isLocalPlayerInRange && Camera.main != null)
                {
                    _promptText.text = _shrineType == ShrineType.WeaponModifierShrine
                        ? "[E] Mở Bàn Cường Hóa"
                        : "[E] Cầu Nguyện Tế Đàn";
                    _promptText.transform.rotation = Quaternion.LookRotation(_promptText.transform.position - Camera.main.transform.position);
                }
            }

            // Người chơi nhấn phím E khi ở gần tế đàn
            if (_isLocalPlayerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OpenChoiceUI();
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

            // Mở modal 3 lựa chọn
            if (ShrineChoiceModalUI.Instance != null)
            {
                ShrineChoiceModalUI.Instance.Open(title, subtitle, _cachedOptions, OnOptionSelectedLocally);
            }
            else
            {
                // Fallback nếu chưa khởi tạo Modal UI
                var modal = FindObjectOfType<ShrineChoiceModalUI>();
                if (modal != null)
                {
                    modal.Open(title, subtitle, _cachedOptions, OnOptionSelectedLocally);
                }
                else
                {
                    // Tự động chọn lựa chọn 0 nếu không có UI
                    OnOptionSelectedLocally(0);
                }
            }
        }

        private void OnOptionSelectedLocally(int choiceIndex)
        {
            if (_isUsedByLocalPlayer) return;
            InteractServerRpc(choiceIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        private void InteractServerRpc(int choiceIndex, ServerRpcParams rpcParams = default)
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

            if (_flameVisual != null) _flameVisual.SetActive(false);
            if (_shrineLight != null) _shrineLight.enabled = false;
            if (_promptText != null) _promptText.gameObject.SetActive(false);

            if (_shrineRenderer != null)
            {
                _shrineRenderer.material.color = _usedColor;
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
