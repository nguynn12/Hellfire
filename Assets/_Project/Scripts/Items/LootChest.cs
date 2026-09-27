// Script: LootChest.cs
// Mục đích: Rương vật phẩm Server-Authoritative rơi bùa lợi hoặc vũ khí theo tỷ lệ đặc tả (Mục 6.3).
// Môi trường thực thi: Cả hai (Server tính toán tỷ lệ rơi & Spawn đồ, Client hiển thị hoạt ảnh mở & UI tương tác).

using System.Collections;
using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hellfire.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class LootChest : NetworkBehaviour
    {
        [Header("Chest Configuration")]
        [SerializeField] private bool _isBossChest = false;
        [SerializeField] private float _interactionDistance = 3.0f;
        [SerializeField] private Transform _dropSpawnPoint;
        [SerializeField] private GameObject _itemPickupPrefab;

        [Header("Loot Tables (Mục 6.2 & 6.3)")]
        [SerializeField] private List<PowerUpData> _powerUpPool = new List<PowerUpData>();
        [SerializeField] private List<WeaponData> _weaponPool = new List<WeaponData>();

        [Header("Visuals & Animation")]
        [SerializeField] private Transform _chestLid;
        [SerializeField] private Vector3 _lidOpenRotation = new Vector3(-65f, 0f, 0f);
        [SerializeField] private float _lidOpenSpeed = 3f;
        [SerializeField] private TextMeshPro _promptText;
        [SerializeField] private Light _chestLight;
        [SerializeField] private Color _normalChestColor = new Color(0.8f, 0.6f, 0.2f);
        [SerializeField] private Color _bossChestColor = new Color(0.9f, 0.2f, 0.8f);

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _openSound;

        // Server-Authoritative Network Variable (Mục 6.3)
        public NetworkVariable<bool> IsOpened { get; } = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private Quaternion _initialLidRotation;
        private Quaternion _targetLidRotation;
        private bool _isLocalPlayerInRange = false;

        public bool IsBossChest
        {
            get => _isBossChest;
            set => _isBossChest = value;
        }

        private void Awake()
        {
            if (_chestLid != null)
            {
                _initialLidRotation = _chestLid.localRotation;
                _targetLidRotation = _initialLidRotation * Quaternion.Euler(_lidOpenRotation);
            }

            if (_dropSpawnPoint == null)
            {
                _dropSpawnPoint = transform;
            }

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_promptText == null) _promptText = GetComponentInChildren<TextMeshPro>();
            if (_chestLight == null) _chestLight = GetComponentInChildren<Light>();
        }

        public override void OnNetworkSpawn()
        {
            IsOpened.OnValueChanged += HandleChestOpenedChanged;

            if (_chestLight != null)
            {
                _chestLight.color = _isBossChest ? _bossChestColor : _normalChestColor;
            }

            if (IsOpened.Value)
            {
                ApplyLidState(true, true);
            }
        }

        public override void OnNetworkDespawn()
        {
            IsOpened.OnValueChanged -= HandleChestOpenedChanged;
        }

        private void HandleChestOpenedChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                StartCoroutine(AnimateLidOpenRoutine());
                if (_audioSource != null && _openSound != null)
                {
                    _audioSource.PlayOneShot(_openSound);
                }

                if (_promptText != null)
                {
                    _promptText.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (IsOpened.Value) return;

            // Client kiểm tra khoảng cách người chơi cục bộ để hiển thị gợi ý nhấn phím E
            CheckLocalPlayerDistance();
        }

        private void CheckLocalPlayerDistance()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null) return;

            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) return;

            float distance = Vector3.Distance(transform.position, localPlayerObj.transform.position);
            _isLocalPlayerInRange = (distance <= _interactionDistance);

            if (_promptText != null)
            {
                _promptText.gameObject.SetActive(_isLocalPlayerInRange);
                if (_isLocalPlayerInRange && Camera.main != null)
                {
                    _promptText.text = _isBossChest ? "[E] Mở Rương Boss" : "[E] Mở Rương";
                    _promptText.transform.rotation = Quaternion.LookRotation(_promptText.transform.position - Camera.main.transform.position);
                }
            }

            // Nhấn E để mở rương
            if (_isLocalPlayerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                RequestOpenChestServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestOpenChestServerRpc(ServerRpcParams rpcParams = default)
        {
            if (IsOpened.Value) return;

            ulong callerClientId = rpcParams.Receive.SenderClientId;
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(callerClientId, out var client))
            {
                var playerObj = client.PlayerObject;
                if (playerObj != null)
                {
                    float distance = Vector3.Distance(transform.position, playerObj.transform.position);
                    if (distance > _interactionDistance + 1.5f)
                    {
                        Debug.LogWarning($"[LootChest] Client {callerClientId} ở quá xa ({distance:F1}m) để mở rương!");
                        return;
                    }

                    // Thực hiện mở rương và rơi vật phẩm
                    PerformOpenChestServer(playerObj.gameObject);
                }
            }
        }

        private void PerformOpenChestServer(GameObject openerPlayer)
        {
            IsOpened.Value = true;

            // Xác định vật phẩm rơi dựa trên tỷ lệ chốt (Mục 6.3)
            if (_isBossChest)
            {
                // RƯƠNG BOSS: 100% rơi ít nhất 1 bùa lợi
                DropRandomPowerUp(GetSpawnOffset(0));

                // TODO(cần xác nhận): tỉ lệ rơi thêm vũ khí ở rương boss tạm đặt 50% theo mục 13
                float extraWeaponRoll = Random.value;
                if (extraWeaponRoll < 0.5f)
                {
                    DropRandomWeapon(openerPlayer, GetSpawnOffset(1));
                }
            }
            else
            {
                // RƯƠNG THƯỜNG: 40% Bùa lợi, 60% Vũ khí (Mục 6.3)
                float roll = Random.value;
                if (roll < 0.40f)
                {
                    DropRandomPowerUp(GetSpawnOffset(0));
                }
                else
                {
                    DropRandomWeapon(openerPlayer, GetSpawnOffset(0));
                }
            }
        }

        private Vector3 GetSpawnOffset(int index)
        {
            Vector3 origin = _dropSpawnPoint != null ? _dropSpawnPoint.position : transform.position + Vector3.up * 0.5f;
            Vector3 offset = (index == 0) ? transform.forward * 0.8f : (transform.forward * 0.8f + transform.right * 0.8f);
            return origin + offset;
        }

        private void DropRandomPowerUp(Vector3 spawnPosition)
        {
            if (_powerUpPool == null || _powerUpPool.Count == 0 || _itemPickupPrefab == null)
            {
                Debug.LogWarning("[LootChest] Danh sách PowerUpPool rỗng hoặc thiếu ItemPickupPrefab!");
                return;
            }

            int index = Random.Range(0, _powerUpPool.Count);
            var chosenPowerUp = _powerUpPool[index];

            GameObject spawnedObj = Instantiate(_itemPickupPrefab, spawnPosition, Quaternion.identity);
            var netObj = spawnedObj.GetComponent<NetworkObject>();
            var pickup = spawnedObj.GetComponent<ItemPickup>();

            if (pickup != null)
            {
                pickup.InitializePowerUp(chosenPowerUp);
            }

            if (netObj != null)
            {
                netObj.Spawn(true);
            }

            Debug.Log($"[LootChest] Đã rơi bùa lợi: {chosenPowerUp.PowerUpName} tại {spawnPosition}");
        }

        private void DropRandomWeapon(GameObject openerPlayer, Vector3 spawnPosition)
        {
            if (_weaponPool == null || _weaponPool.Count == 0 || _itemPickupPrefab == null)
            {
                Debug.LogWarning("[LootChest] Danh sách WeaponPool rỗng hoặc thiếu ItemPickupPrefab!");
                return;
            }

            // Gợi ý loại trừ vũ khí người chơi đang cầm (Mục 6.3)
            WeaponData heldWeapon = null;
            if (openerPlayer != null)
            {
                var weaponCtrl = openerPlayer.GetComponent<WeaponController>();
                if (weaponCtrl != null)
                {
                    heldWeapon = weaponCtrl.CurrentWeaponData;
                }
            }

            var candidates = new List<WeaponData>();
            foreach (var w in _weaponPool)
            {
                if (w != null && (heldWeapon == null || w.WeaponId != heldWeapon.WeaponId))
                {
                    candidates.Add(w);
                }
            }

            // Nếu người chơi đang cầm duy nhất loại đó hoặc danh sách lọc rỗng, dùng toàn bộ pool
            if (candidates.Count == 0)
            {
                candidates.AddRange(_weaponPool);
            }

            var chosenWeapon = candidates[Random.Range(0, candidates.Count)];

            GameObject spawnedObj = Instantiate(_itemPickupPrefab, spawnPosition, Quaternion.identity);
            var netObj = spawnedObj.GetComponent<NetworkObject>();
            var pickup = spawnedObj.GetComponent<ItemPickup>();

            if (pickup != null)
            {
                pickup.InitializeWeapon(chosenWeapon);
            }

            if (netObj != null)
            {
                netObj.Spawn(true);
            }

            Debug.Log($"[LootChest] Đã rơi vũ khí: {chosenWeapon.WeaponName} tại {spawnPosition}");
        }

        private IEnumerator AnimateLidOpenRoutine()
        {
            if (_chestLid == null) yield break;

            float elapsed = 0f;
            Quaternion startRot = _chestLid.localRotation;

            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime * _lidOpenSpeed;
                _chestLid.localRotation = Quaternion.Slerp(startRot, _targetLidRotation, elapsed);
                yield return null;
            }

            _chestLid.localRotation = _targetLidRotation;
        }

        private void ApplyLidState(bool isOpen, bool instant)
        {
            if (_chestLid == null) return;

            if (isOpen)
            {
                _chestLid.localRotation = _targetLidRotation;
            }
            else
            {
                _chestLid.localRotation = _initialLidRotation;
            }
        }
    }
}
