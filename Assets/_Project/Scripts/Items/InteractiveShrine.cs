// Script: InteractiveShrine.cs
// Mục đích: Tế đàn đa năng (Body Buff / Weapon Upgrade / Dual Choice) quản lý theo cơ chế Lưu lịch sử trên người chơi (Mục I.4).
//           Mỗi tế đàn có 1 ShrineId duy nhất. Mỗi người chơi chỉ được dùng 1 lần.
//           Hiệu ứng dập lửa/đổi màu tắt đèn chỉ diễn ra cục bộ trên máy người đã sử dụng (Client-Side Visual).
// Môi trường thực thi: Server-Authoritative kiểm tra và cấp buff, Client xử lý input [E] và hiển thị hình ảnh cục bộ.

using System.Collections;
using Hellfire.Combat;
using Hellfire.Player;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Items
{
    public enum ShrineType
    {
        BodyBuff,       // Tăng chỉ số cơ thể (Máu, Giáp, Tốc độ)
        WeaponUpgrade,  // Nâng cấp cấp độ vũ khí hiện tại
        DualChoice      // Cho phép người chơi chọn 1 trong 2 loại nâng cấp
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class InteractiveShrine : NetworkBehaviour
    {
        [Header("Shrine Identity (Mục I.4)")]
        [Tooltip("Mã ID duy nhất của tế đàn trên bản đồ")]
        [SerializeField] private int _shrineId = 1;

        [Tooltip("Loại hình ban phước của tế đàn")]
        [SerializeField] private ShrineType _shrineType = ShrineType.BodyBuff;

        [Tooltip("Khoảng cách tối đa để có thể tương tác (mét)")]
        [SerializeField] private float _interactionDistance = 3.0f;

        [Header("Body Buff Values")]
        [Tooltip("Lượng máu tối đa cộng thêm")]
        [SerializeField] private float _bonusHealth = 25f;

        [Tooltip("Lượng giáp cộng thêm")]
        [SerializeField] private float _bonusArmor = 10f;

        [Tooltip("Hệ số tốc độ chạy cộng thêm (+0.10 = +10%)")]
        [SerializeField] private float _bonusSpeed = 0.10f;

        [Header("Weapon Upgrade Values")]
        [Tooltip("Số cấp độ nâng cấp vũ khí hiện tại")]
        [SerializeField] private int _weaponLevels = 1;

        [Header("Local Visuals (Chỉ tắt trên máy người đã dùng)")]
        [SerializeField] private GameObject _flameVisual;
        [SerializeField] private Light _shrineLight;
        [SerializeField] private Renderer _shrineRenderer;
        [SerializeField] private Color _usedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _interactSound;

        private bool _isUsedByLocalPlayer = false;

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
        }

        public override void OnNetworkSpawn()
        {
            // Kiểm tra trạng thái tế đàn ban đầu đối với Local Player
            StartCoroutine(CheckLocalUsageDelayed());
        }

        private IEnumerator CheckLocalUsageDelayed()
        {
            // Chờ Local Player spawn xong trên máy khách
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

        /// <summary>
        /// Người chơi gửi yêu cầu kích hoạt tế đàn lên Server khi bấm [E].
        /// </summary>
        /// <param name="choiceIndex">0 = BodyBuff, 1 = WeaponUpgrade (cho DualChoice)</param>
        public void RequestInteract(int choiceIndex = 0)
        {
            if (_isUsedByLocalPlayer)
            {
                Debug.LogWarning("[InteractiveShrine] Bạn đã sử dụng tế đàn này rồi!");
                return;
            }

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
            if (dist > _interactionDistance + 1.0f)
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

            // 2. Kiểm tra xem người chơi này đã từng dùng tế đàn này chưa (Mục I.4)
            if (playerStats.HasUsedShrine(_shrineId))
            {
                NotifyFeedbackClientRpc("Bạn đã nhận ban phước từ tế đàn này rồi!", false, GetTargetClientRpcParams(senderClientId));
                return;
            }

            // 3. Thực thi áp dụng nâng cấp dựa theo loại tế đàn
            bool applySuccess = false;
            string successMsg = "";

            if (_shrineType == ShrineType.BodyBuff || (_shrineType == ShrineType.DualChoice && choiceIndex == 0))
            {
                if (!playerStats.CanReceiveBodyBuff(_bonusHealth, _bonusArmor, _bonusSpeed))
                {
                    NotifyFeedbackClientRpc("Chỉ số cơ thể của bạn đã đạt mức tối đa (Max Cap)!", false, GetTargetClientRpcParams(senderClientId));
                    return;
                }

                applySuccess = playerStats.TryApplyBodyBuff(_bonusHealth, _bonusArmor, _bonusSpeed);
                successMsg = $"Nhận ban phước Thân Thể: +{_bonusHealth} HP, +{_bonusArmor} Giáp, +{Mathf.RoundToInt(_bonusSpeed * 100)}% Tốc Chạy!";
            }
            else if (_shrineType == ShrineType.WeaponUpgrade || (_shrineType == ShrineType.DualChoice && choiceIndex == 1))
            {
                if (weaponCtrl != null && weaponCtrl.CurrentRuntimeWeapon != null)
                {
                    weaponCtrl.UpgradeCurrentWeaponServer(_weaponLevels);
                    applySuccess = true;
                    successMsg = $"Vũ khí {weaponCtrl.CurrentWeaponData.WeaponName} đã được cường hóa thêm +{_weaponLevels} Cấp!";
                }
            }

            if (!applySuccess)
            {
                NotifyFeedbackClientRpc("Không thể kích hoạt nâng cấp tại thời điểm này!", false, GetTargetClientRpcParams(senderClientId));
                return;
            }

            // 4. Ghi nhận ID tế đàn đã dùng vào danh sách của riêng người chơi này (Server-side)
            playerStats.RecordShrineUsed(_shrineId);

            // 5. Gửi thông báo thành công và yêu cầu TẮT LỬA CỤC BỘ chỉ trên máy của riêng người chơi đó (Mục I.4)
            NotifySuccessAndExtinguishClientRpc(successMsg, GetTargetClientRpcParams(senderClientId));
        }

        private ClientRpcParams GetTargetClientRpcParams(ulong targetClientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
        }

        [ClientRpc]
        private void NotifyFeedbackClientRpc(string message, bool isSuccess, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"[InteractiveShrine] Feedback: {message}");
            // Có thể liên kết với UI HUD để hiển thị thông báo popup
        }

        [ClientRpc]
        private void NotifySuccessAndExtinguishClientRpc(string message, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"[InteractiveShrine] Thành công: {message}");
            _isUsedByLocalPlayer = true;

            // Dập tắt ngọn lửa / đổi màu ánh sáng cục bộ trên máy của người này
            ApplyUsedVisualLocally();

            if (_audioSource != null && _interactSound != null)
            {
                _audioSource.PlayOneShot(_interactSound);
            }
        }

        /// <summary>
        /// Xử lý hiệu ứng hình ảnh khi tế đàn đã được tiêu thụ trên máy cục bộ (người khác vẫn thấy cháy sáng bình thường).
        /// </summary>
        private void ApplyUsedVisualLocally()
        {
            _isUsedByLocalPlayer = true;

            // 1. Tắt GameObject ngọn lửa
            if (_flameVisual != null)
            {
                _flameVisual.SetActive(false);
            }

            // 2. Tắt ánh sáng Light
            if (_shrineLight != null)
            {
                _shrineLight.enabled = false;
            }

            // 3. Đổi màu vật liệu đá của tế đàn sang xám nguội
            if (_shrineRenderer != null && _shrineRenderer.material != null)
            {
                _shrineRenderer.material.color = _usedColor;
            }
        }
    }
}
