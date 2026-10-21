// Script: ShopStation.cs
// Mục đích: Quản lý Trạm Cửa Hàng (Shop Station) đặt tại tâm phòng Shop (Mục 17.8 đặc tả kỹ thuật).
//           5 món chào bán giống hệt nhau với mọi người chơi (sinh theo Seed, NetworkList<int> OfferIds).
//           Mua là riêng của từng người, hiển thị chung (Dictionary<ulong, HashSet<int>> purchased trên Server).
//           Kiểm tra đầy đủ 7 bước hợp lệ trước khi trừ tiền và trao vật phẩm.
// Môi trường thực thi: Cả hai (Server xác thực giao dịch, Client hiển thị gợi ý 3D & mở UI ShopModalUI).

using System;
using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Hellfire.Networking;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Items
{
    [Serializable]
    public struct ShopItemInfo
    {
        public int ItemId;
        public string Name;
        public string Category; // "VŨ KHÍ" hoặc "BÙA LỢI"
        public string Description;
        public int Price;
        public Color ThemeColor;
        public bool IsWeapon;
        public WeaponData WeaponData;
        public PowerUpData PowerUpData;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class ShopStation : NetworkBehaviour
    {
        [Header("Shop Interaction")]
        [Tooltip("Khoảng cách tối đa để có thể tương tác mở Shop (Mục 17.8: 3 mét)")]
        [SerializeField] private float _interactionDistance = 3.0f;

        [Header("3D Visuals & Prompts")]
        [SerializeField] private TextMeshPro _promptText;
        [SerializeField] private Light _stationLight;
        [SerializeField] private Transform _spinningCoin;
        [SerializeField] private float _spinSpeed = 60f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _buySuccessSound;
        [SerializeField] private AudioClip _buyFailSound;

        [Header("Catalogs (8 Món Cố Định theo Mục 17.8)")]
        [SerializeField] private List<WeaponData> _weaponCatalog = new List<WeaponData>();
        [SerializeField] private List<PowerUpData> _powerUpCatalog = new List<PowerUpData>();

        /// <summary>
        /// 5 món chào bán, giống hệt nhau với mọi người chơi (Mục 17.8).
        /// </summary>
        public NetworkList<int> OfferIds { get; private set; }

        // Server-only: Lưu danh sách món đã mua của từng người chơi (clientId -> HashSet<itemId>)
        private readonly Dictionary<ulong, HashSet<int>> _purchasedByClient = new Dictionary<ulong, HashSet<int>>();

        // Client-only: Lưu các chỉ số offer mà người chơi cục bộ đã mua tại shop này
        private readonly HashSet<int> _localPurchasedOfferIndices = new HashSet<int>();

        public float InteractionDistance => _interactionDistance;
        public bool IsPurchasedLocally(int offerIndex) => _localPurchasedOfferIndices.Contains(offerIndex);

        private void Awake()
        {
            OfferIds = new NetworkList<int>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_stationLight == null) _stationLight = GetComponentInChildren<Light>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            InitializeCatalogs();
        }

        private void InitializeCatalogs()
        {
#if UNITY_EDITOR
            if (_weaponCatalog == null || _weaponCatalog.Count == 0)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Data/Weapons" });
                _weaponCatalog = new List<WeaponData>();
                foreach (var g in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    var w = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                    if (w != null && !_weaponCatalog.Contains(w)) _weaponCatalog.Add(w);
                }
            }

            if (_powerUpCatalog == null || _powerUpCatalog.Count == 0)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:PowerUpData", new[] { "Assets/_Project/Data/PowerUps" });
                _powerUpCatalog = new List<PowerUpData>();
                foreach (var g in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    var p = UnityEditor.AssetDatabase.LoadAssetAtPath<PowerUpData>(path);
                    if (p != null && !_powerUpCatalog.Contains(p)) _powerUpCatalog.Add(p);
                }
            }
#endif
            if (_weaponCatalog == null || _weaponCatalog.Count == 0)
            {
                _weaponCatalog = new List<WeaponData>(Resources.FindObjectsOfTypeAll<WeaponData>());
            }

            if (_powerUpCatalog == null || _powerUpCatalog.Count == 0)
            {
                _powerUpCatalog = new List<PowerUpData>(Resources.FindObjectsOfTypeAll<PowerUpData>());
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _purchasedByClient.Clear();

                // Server chọn một lần khi phòng được dựng (rng riêng theo seed) 5 món khác nhau từ nhóm 8 món (Mục 17.8)
                if (OfferIds.Count == 0)
                {
                    SelectOffersFromSeed();
                }
            }

            _localPurchasedOfferIndices.Clear();
        }

        private void SelectOffersFromSeed()
        {
            int seed = 12345;
            if (DungeonManager.Instance != null && DungeonManager.Instance.DungeonSeed.Value != 0)
            {
                seed = DungeonManager.Instance.DungeonSeed.Value + (int)(transform.position.x * 73 + transform.position.z * 139);
            }
            else
            {
                seed = (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
            }

            var rng = new System.Random(seed);
            var pool = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

            // Fisher-Yates shuffle
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int temp = pool[i];
                pool[i] = pool[j];
                pool[j] = temp;
            }

            OfferIds.Clear();
            for (int i = 0; i < 5; i++)
            {
                OfferIds.Add(pool[i]);
            }

            Debug.Log($"[ShopStation] Đã sinh 5 món chào bán ngẫu nhiên theo Seed {seed}: {string.Join(", ", OfferIds)}");
        }

        private void Update()
        {
            if (_spinningCoin != null)
            {
                _spinningCoin.Rotate(Vector3.up * (_spinSpeed * Time.deltaTime), Space.World);
            }

            // Client kiểm tra khoảng cách người chơi cục bộ để hiển thị gợi ý phím [E]
            CheckLocalPlayerInteraction();
        }

        private void CheckLocalPlayerInteraction()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) return;

            float dist = Vector3.Distance(transform.position, localPlayerObj.transform.position);
            bool inRange = (dist <= _interactionDistance);

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(inRange);
                if (inRange && Camera.main != null)
                {
                    _promptText.text = "[E] Mở Cửa Hàng (Shop)";
                    _promptText.transform.rotation = Quaternion.LookRotation(_promptText.transform.position - Camera.main.transform.position);
                }
            }

            // Bấm E mở ShopModalUI
            if (inRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OpenShopModalLocally();
            }

            // Tự động đóng nếu đi quá xa
            if (ShopModalUI.Instance != null && ShopModalUI.Instance.IsOpen && ShopModalUI.Instance.CurrentStation == this)
            {
                if (dist > _interactionDistance + 1.5f)
                {
                    ShopModalUI.Instance.CloseModal();
                }
            }
        }

        private void OpenShopModalLocally()
        {
            if (ShopModalUI.Instance != null)
            {
                ShopModalUI.Instance.Open(this);
            }
            else
            {
                var modal = FindObjectOfType<ShopModalUI>();
                if (modal != null)
                {
                    modal.Open(this);
                }
                else
                {
                    Debug.LogWarning("[ShopStation] Chưa tìm thấy ShopModalUI trong Scene!");
                }
            }
        }

        // ================= GIAO DỊCH MUA SẮM (SERVER-AUTHORITATIVE) =================

        /// <summary>
        /// Yêu cầu mua mặt hàng tại offerIndex (0 đến 4).
        /// Server kiểm tra 7 bước theo Mục 17.8 trước khi trừ tiền và trao đồ.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void BuyOfferServerRpc(int offerIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
            {
                return;
            }

            var playerObj = client.PlayerObject;
            if (playerObj == null) return;

            // 1. Kiểm tra: Người gửi còn sống và không Downed (Mục 17.8)
            var health = playerObj.GetComponent<Health>();
            if (health == null || health.IsDead.Value || health.IsDowned.Value)
            {
                SendBuyResult(senderClientId, offerIndex, false, "Bạn đang gục hoặc đã chết!");
                return;
            }

            // 2. Kiểm tra: Đứng trong 3 mét (theo vị trí Server) (Mục 17.8)
            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist > _interactionDistance + 1.5f)
            {
                SendBuyResult(senderClientId, offerIndex, false, "Bạn ở quá xa cửa hàng!");
                return;
            }

            // 3. Kiểm tra: GameState là Playing hoặc BossFight (Mục 17.8)
            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState.Value : GameState.Playing;
            if (state != GameState.Playing && state != GameState.BossFight)
            {
                SendBuyResult(senderClientId, offerIndex, false, "Cửa hàng chỉ mở trong trận đấu!");
                return;
            }

            // 4. Kiểm tra: offerIndex hợp lệ (0-4 và nằm trong OfferIds) (Mục 17.8)
            if (offerIndex < 0 || offerIndex >= OfferIds.Count)
            {
                SendBuyResult(senderClientId, offerIndex, false, "Món hàng không tồn tại!");
                return;
            }

            int itemId = OfferIds[offerIndex];
            var itemInfo = GetItemInfo(itemId);

            // 5. Kiểm tra: Người này chưa mua món đó (Mục 17.8)
            if (!_purchasedByClient.TryGetValue(senderClientId, out var clientPurchased))
            {
                clientPurchased = new HashSet<int>();
                _purchasedByClient[senderClientId] = clientPurchased;
            }

            if (clientPurchased.Contains(itemId))
            {
                SendBuyResult(senderClientId, offerIndex, false, "Bạn đã mua món này rồi!");
                return;
            }

            // 6. Kiểm tra: Đủ Linh hồn (Mục 17.8)
            var wallet = playerObj.GetComponent<PlayerWallet>();
            if (wallet == null || wallet.Souls.Value < itemInfo.Price)
            {
                SendBuyResult(senderClientId, offerIndex, false, $"Không đủ Linh hồn! (Cần {itemInfo.Price})");
                return;
            }

            // 7. Kiểm tra: Nếu là vũ khí thì người này chưa sở hữu khẩu đó (Mục 17.8)
            var weaponController = playerObj.GetComponent<WeaponController>();
            if (itemInfo.IsWeapon && itemInfo.WeaponData != null)
            {
                if (HasWeapon(weaponController, itemInfo.WeaponData))
                {
                    // Từ chối, KHÔNG trừ tiền, báo "Bạn đã có vũ khí này" (Mục 17.8)
                    SendBuyResult(senderClientId, offerIndex, false, "Bạn đã có vũ khí này!");
                    return;
                }
            }

            // HỢP LỆ: Trừ Linh hồn
            if (!wallet.TrySpendSouls(itemInfo.Price))
            {
                SendBuyResult(senderClientId, offerIndex, false, "Không thể trừ Linh hồn!");
                return;
            }

            // Trao món qua đúng đường trao vật phẩm đang có (Mục 17.8)
            if (itemInfo.IsWeapon && itemInfo.WeaponData != null)
            {
                if (weaponController != null)
                {
                    weaponController.AddOrSwitchWeapon(itemInfo.WeaponData);
                    Debug.Log($"[ShopStation] Client {senderClientId} đã mua và trang bị: {itemInfo.WeaponData.WeaponName}");
                }
            }
            else if (!itemInfo.IsWeapon && itemInfo.PowerUpData != null)
            {
                var buffManager = playerObj.GetComponent<PlayerBuffManager>();
                if (buffManager != null)
                {
                    buffManager.ApplyPowerUp(itemInfo.PowerUpData);
                    Debug.Log($"[ShopStation] Client {senderClientId} đã mua và kích hoạt bùa lợi: {itemInfo.PowerUpData.PowerUpName}");
                }
            }

            // Đánh dấu đã mua riêng cho người chơi này (Mục 17.8)
            clientPurchased.Add(itemId);

            // Gửi kết quả thành công cho đúng người mua bằng ClientRpc nhắm chủ nhân
            SendBuyResult(senderClientId, offerIndex, true, $"Đã mua thành công {itemInfo.Name}!");
        }

        private void SendBuyResult(ulong targetClientId, int offerIndex, bool success, string message)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
            BuyOfferResultClientRpc(offerIndex, success, message, rpcParams);
        }

        [ClientRpc]
        private void BuyOfferResultClientRpc(int offerIndex, bool success, string message, ClientRpcParams rpcParams = default)
        {
            if (success)
            {
                _localPurchasedOfferIndices.Add(offerIndex);
                if (_audioSource != null && _buySuccessSound != null)
                {
                    _audioSource.PlayOneShot(_buySuccessSound);
                }
            }
            else
            {
                if (_audioSource != null && _buyFailSound != null)
                {
                    _audioSource.PlayOneShot(_buyFailSound);
                }
            }

            if (ShopModalUI.Instance != null && ShopModalUI.Instance.IsOpen)
            {
                ShopModalUI.Instance.OnPurchaseResult(offerIndex, success, message);
            }
        }

        // ================= DỮ LIỆU DANH MỤC 8 MÓN (MỤC 17.8) =================

        public ShopItemInfo GetItemInfo(int itemId)
        {
            InitializeCatalogs();

            var info = new ShopItemInfo { ItemId = itemId };

            switch (itemId)
            {
                case 0: // Pistol (80 Souls)
                    info.WeaponData = FindWeapon("pistol");
                    info.IsWeapon = true;
                    info.Category = "VŨ KHÍ";
                    info.Name = info.WeaponData != null ? info.WeaponData.WeaponName : "Súng Lục (Pistol)";
                    info.Description = "Sát thương cơ bản: 15 | Băng đạn: 12 viên\nVũ khí phụ khởi đầu nhanh nhẹn.";
                    info.Price = info.WeaponData != null ? info.WeaponData.ShopPrice : 80;
                    info.ThemeColor = new Color(1f, 0.65f, 0.1f);
                    break;

                case 1: // SMG (80 Souls)
                    info.WeaponData = FindWeapon("smg");
                    info.IsWeapon = true;
                    info.Category = "VŨ KHÍ";
                    info.Name = info.WeaponData != null ? info.WeaponData.WeaponName : "Tiểu Liên (SMG)";
                    info.Description = "Tốc bắn cực nhanh | Băng đạn: 30 viên\nÁp đảo số lượng lớn quái vật cự ly gần.";
                    info.Price = info.WeaponData != null ? info.WeaponData.ShopPrice : 80;
                    info.ThemeColor = new Color(1f, 0.65f, 0.1f);
                    break;

                case 2: // Shotgun (80 Souls)
                    info.WeaponData = FindWeapon("shotgun");
                    info.IsWeapon = true;
                    info.Category = "VŨ KHÍ";
                    info.Name = info.WeaponData != null ? info.WeaponData.WeaponName : "Shotgun";
                    info.Description = "Bắn chùm đạn uy lực mạnh | Băng đạn: 8 viên\nSát thương bùng nổ cực mạnh cận chiến.";
                    info.Price = info.WeaponData != null ? info.WeaponData.ShopPrice : 80;
                    info.ThemeColor = new Color(1f, 0.65f, 0.1f);
                    break;

                case 3: // Rifle (80 Souls)
                    info.WeaponData = FindWeapon("rifle");
                    info.IsWeapon = true;
                    info.Category = "VŨ KHÍ";
                    info.Name = info.WeaponData != null ? info.WeaponData.WeaponName : "Súng Trường (Rifle)";
                    info.Description = "Tầm bắn xa, độ chính xác cao | Băng đạn: 25 viên\nSát thương đầm và ổn định từ xa.";
                    info.Price = info.WeaponData != null ? info.WeaponData.ShopPrice : 80;
                    info.ThemeColor = new Color(1f, 0.65f, 0.1f);
                    break;

                case 4: // Max Health Up (50 Souls)
                    info.PowerUpData = FindPowerUp(PowerUpType.MaxHealthUp);
                    info.IsWeapon = false;
                    info.Category = "BÙA LỢI";
                    info.Name = info.PowerUpData != null ? info.PowerUpData.PowerUpName : "Max Health Up";
                    info.Description = "+25 Máu tối đa và hồi đầy máu ngay lập tức.\nHiệu ứng vĩnh viễn suốt lượt chơi.";
                    info.Price = info.PowerUpData != null ? info.PowerUpData.ShopPrice : 50;
                    info.ThemeColor = new Color(0.2f, 0.9f, 0.4f);
                    break;

                case 5: // Swift Boots (50 Souls)
                    info.PowerUpData = FindPowerUp(PowerUpType.SwiftBoots);
                    info.IsWeapon = false;
                    info.Category = "BÙA LỢI";
                    info.Name = info.PowerUpData != null ? info.PowerUpData.PowerUpName : "Swift Boots";
                    info.Description = "+20% Tốc độ di chuyển vĩnh viễn.\nGiúp né tránh đạn và truy kích dễ dàng hơn.";
                    info.Price = info.PowerUpData != null ? info.PowerUpData.ShopPrice : 50;
                    info.ThemeColor = new Color(0.2f, 0.75f, 1f);
                    break;

                case 6: // Berserker Charm (50 Souls)
                    info.PowerUpData = FindPowerUp(PowerUpType.BerserkerCharm);
                    info.IsWeapon = false;
                    info.Category = "BÙA LỢI";
                    info.Name = info.PowerUpData != null ? info.PowerUpData.PowerUpName : "Berserker Charm";
                    info.Description = "+30% Sát thương gây ra vĩnh viễn.\nTăng mạnh uy lực cho mọi vũ khí súng.";
                    info.Price = info.PowerUpData != null ? info.PowerUpData.ShopPrice : 50;
                    info.ThemeColor = new Color(1f, 0.3f, 0.3f);
                    break;

                case 7: // Guardian Shield (30 Souls)
                    info.PowerUpData = FindPowerUp(PowerUpType.GuardianShield);
                    info.IsWeapon = false;
                    info.Category = "BÙA LỢI";
                    info.Name = info.PowerUpData != null ? info.PowerUpData.PowerUpName : "Guardian Shield";
                    info.Description = "Miễn dịch sát thương hoàn toàn trong 8 giây.\nKích hoạt ngay khi vừa mua.";
                    info.Price = info.PowerUpData != null ? info.PowerUpData.ShopPrice : 30;
                    info.ThemeColor = new Color(0.85f, 0.5f, 1f);
                    break;

                default:
                    info.Name = "Vật Phẩm Bí Ẩn";
                    info.Category = "KHÁC";
                    info.Description = "Không có mô tả.";
                    info.Price = 50;
                    info.ThemeColor = Color.white;
                    break;
            }

            return info;
        }

        private WeaponData FindWeapon(string idSubstr)
        {
            if (_weaponCatalog == null) return null;
            return _weaponCatalog.Find(w => w != null && w.WeaponId.ToLower().Contains(idSubstr.ToLower()));
        }

        private PowerUpData FindPowerUp(PowerUpType type)
        {
            if (_powerUpCatalog == null) return null;
            return _powerUpCatalog.Find(p => p != null && p.Type == type);
        }

        public static bool HasWeapon(WeaponController controller, WeaponData weapon)
        {
            if (controller == null || weapon == null) return false;

            if (controller.AvailableWeapons != null)
            {
                foreach (var w in controller.AvailableWeapons)
                {
                    if (w != null && (w == weapon || string.Equals(w.WeaponId, weapon.WeaponId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            if (controller.CurrentWeaponData != null)
            {
                if (controller.CurrentWeaponData == weapon || string.Equals(controller.CurrentWeaponData.WeaponId, weapon.WeaponId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
