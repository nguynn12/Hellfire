// Script: ItemPickup.cs
// Mục đích: Vật phẩm rơi trên sàn đấu (Bùa lợi hoặc Vũ khí) mà người chơi có thể nhặt (Mục 6.1, 6.2 & 6.3).
// Môi trường thực thi: Cả hai (Server quản lý nhặt và áp dụng, Client hiển thị hoạt ảnh & nhãn tên).

using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Player;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class ItemPickup : NetworkBehaviour
    {
        [Header("Databases (For Network Lookup)")]
        [SerializeField] private List<PowerUpData> _powerUpDatabase = new List<PowerUpData>();
        [SerializeField] private List<WeaponData> _weaponDatabase = new List<WeaponData>();

        [Header("Direct References (Server Override)")]
        [SerializeField] private ItemPickupType _pickupType = ItemPickupType.PowerUp;
        [SerializeField] private PowerUpData _powerUpData;
        [SerializeField] private WeaponData _weaponData;

        [Header("Visuals & UI")]
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private Light _pointLight;
        [SerializeField] private TextMeshPro _nameLabel;
        [SerializeField] private float _spinSpeed = 90f;
        [SerializeField] private float _bobFrequency = 2f;
        [SerializeField] private float _bobAmplitude = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _pickupSound;

        // Network State
        private readonly NetworkVariable<int> _netPickupType = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<FixedString64Bytes> _netItemId = new NetworkVariable<FixedString64Bytes>(
            string.Empty,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private Vector3 _initialPosition;
        private bool _hasDespawned = false;

        public ItemPickupType Type => (ItemPickupType)_netPickupType.Value;
        public PowerUpData PowerUp => _powerUpData;
        public WeaponData Weapon => _weaponData;

        private void Awake()
        {
            _initialPosition = transform.position;
            if (_meshRenderer == null) _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_pointLight == null) _pointLight = GetComponentInChildren<Light>();
            if (_nameLabel == null) _nameLabel = GetComponentInChildren<TextMeshPro>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        public override void OnNetworkSpawn()
        {
            _initialPosition = transform.position;
            _netItemId.OnValueChanged += HandleItemIdChanged;
            _netPickupType.OnValueChanged += HandlePickupTypeChanged;

            ResolveItemDataFromNetwork();
            UpdateVisuals();
        }

        public override void OnNetworkDespawn()
        {
            _netItemId.OnValueChanged -= HandleItemIdChanged;
            _netPickupType.OnValueChanged -= HandlePickupTypeChanged;
        }

        private void HandleItemIdChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
        {
            ResolveItemDataFromNetwork();
            UpdateVisuals();
        }

        private void HandlePickupTypeChanged(int oldVal, int newVal)
        {
            ResolveItemDataFromNetwork();
            UpdateVisuals();
        }

        /// <summary>
        /// Cấu hình vật phẩm trên Server trước khi Spawn ra mạng.
        /// </summary>
        public void InitializePowerUp(PowerUpData data)
        {
            if (!IsServer && NetworkObject.IsSpawned) return;

            _pickupType = ItemPickupType.PowerUp;
            _powerUpData = data;
            _weaponData = null;

            if (IsServer)
            {
                _netPickupType.Value = (int)ItemPickupType.PowerUp;
                _netItemId.Value = data != null ? data.Type.ToString() : string.Empty;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Cấu hình vũ khí trên Server trước khi Spawn ra mạng.
        /// </summary>
        public void InitializeWeapon(WeaponData data)
        {
            if (!IsServer && NetworkObject.IsSpawned) return;

            _pickupType = ItemPickupType.Weapon;
            _weaponData = data;
            _powerUpData = null;

            if (IsServer)
            {
                _netPickupType.Value = (int)ItemPickupType.Weapon;
                _netItemId.Value = data != null ? data.WeaponId : string.Empty;
            }

            UpdateVisuals();
        }

        private void ResolveItemDataFromNetwork()
        {
            string id = _netItemId.Value.ToString();
            var category = (ItemPickupType)_netPickupType.Value;

            if (category == ItemPickupType.PowerUp)
            {
                _weaponData = null;
                if (_powerUpDatabase != null)
                {
                    _powerUpData = _powerUpDatabase.Find(p => p != null && p.Type.ToString() == id);
                }
            }
            else
            {
                _powerUpData = null;
                if (_weaponDatabase != null)
                {
                    _weaponData = _weaponDatabase.Find(w => w != null && w.WeaponId == id);
                }
            }
        }

        private void UpdateVisuals()
        {
            Color displayColor = Color.white;
            string displayName = "Vật phẩm";

            if ((ItemPickupType)_netPickupType.Value == ItemPickupType.PowerUp && _powerUpData != null)
            {
                displayColor = _powerUpData.PickupColor;
                displayName = $"{_powerUpData.PowerUpName}\n<size=70%>{_powerUpData.Description}</size>";
            }
            else if ((ItemPickupType)_netPickupType.Value == ItemPickupType.Weapon && _weaponData != null)
            {
                displayColor = new Color(1f, 0.6f, 0.1f); // Màu cam vũ khí
                displayName = $"{_weaponData.WeaponName}\n<size=70%>Sát thương: {_weaponData.BaseDamage} | Băng: {_weaponData.MagSize}</size>";
            }

            if (_meshRenderer != null)
            {
                _meshRenderer.material.color = displayColor;
            }

            if (_pointLight != null)
            {
                _pointLight.color = displayColor;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = displayName;
                _nameLabel.color = displayColor;
            }
        }

        private void Update()
        {
            // Hoạt ảnh xoay và nhấp nhô cục bộ (Client & Host)
            transform.Rotate(Vector3.up * (_spinSpeed * Time.deltaTime), Space.World);

            float newY = _initialPosition.y + Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Quay text hướng về Camera người chơi
            if (_nameLabel != null && Camera.main != null)
            {
                _nameLabel.transform.rotation = Quaternion.LookRotation(_nameLabel.transform.position - Camera.main.transform.position);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Chỉ Server có thẩm quyền xác thực và trao vật phẩm (Mục 6.3)
            if (!IsServer || _hasDespawned) return;

            if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerMovement>() == null)
            {
                return;
            }

            var playerRoot = other.GetComponentInParent<NetworkObject>();
            if (playerRoot == null) return;

            var playerHealth = playerRoot.GetComponent<Health>();
            if (playerHealth != null && (playerHealth.IsDowned.Value || playerHealth.IsDead.Value))
            {
                return; // Người chơi đang gục/chết không nhặt được
            }

            var buffManager = playerRoot.GetComponent<PlayerBuffManager>();
            var weaponController = playerRoot.GetComponent<WeaponController>();

            var category = (ItemPickupType)_netPickupType.Value;

            if (category == ItemPickupType.PowerUp)
            {
                if (_powerUpData != null && buffManager != null)
                {
                    buffManager.ApplyPowerUp(_powerUpData);
                    Debug.Log($"[ItemPickup] Người chơi {playerRoot.NetworkObjectId} đã nhặt bùa: {_powerUpData.PowerUpName}");
                    PlayPickupSoundClientRpc(transform.position);
                    DespawnItem();
                }
            }
            else if (category == ItemPickupType.Weapon)
            {
                if (_weaponData != null && weaponController != null)
                {
                    weaponController.AddOrSwitchWeapon(_weaponData);
                    Debug.Log($"[ItemPickup] Người chơi {playerRoot.NetworkObjectId} đã nhặt vũ khí: {_weaponData.WeaponName}");
                    PlayPickupSoundClientRpc(transform.position);
                    DespawnItem();
                }
            }
        }

        [ClientRpc]
        private void PlayPickupSoundClientRpc(Vector3 pos)
        {
            if (_pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(_pickupSound, pos, 0.8f);
            }
        }

        private void DespawnItem()
        {
            if (_hasDespawned) return;
            _hasDespawned = true;

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
