// Script: ItemPickup.cs
// Mục đích: Vật phẩm rơi trên sàn đấu (Bùa lợi hoặc Vũ khí) mà người chơi có thể nhặt (Mục 6.1, 6.2 & 6.3).
// Môi trường thực thi: Cả hai (Server quản lý nhặt và áp dụng, Client hiển thị hoạt ảnh & nhãn tên).

using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Networking;
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

        private MaterialPropertyBlock _itemPropBlock;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorPropId = Shader.PropertyToID("_Color");

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
            if (GameModeHelper.IsOffline)
            {
                _pickupType = ItemPickupType.PowerUp;
                _powerUpData = data;
                _weaponData = null;
                UpdateVisuals();
                return;
            }

            bool isServerAuthorized = IsServer || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
            if (!isServerAuthorized && NetworkObject != null && NetworkObject.IsSpawned) return;

            _pickupType = ItemPickupType.PowerUp;
            _powerUpData = data;
            _weaponData = null;

            if (isServerAuthorized)
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
            if (GameModeHelper.IsOffline)
            {
                _pickupType = ItemPickupType.Weapon;
                _weaponData = data;
                _powerUpData = null;
                UpdateVisuals();
                return;
            }

            bool isServerAuthorized = IsServer || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
            if (!isServerAuthorized && NetworkObject != null && NetworkObject.IsSpawned) return;

            _pickupType = ItemPickupType.Weapon;
            _weaponData = data;
            _powerUpData = null;

            if (isServerAuthorized)
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

            ItemPickupType activeType = GameModeHelper.IsOffline ? _pickupType : (ItemPickupType)_netPickupType.Value;

            if (activeType == ItemPickupType.PowerUp && _powerUpData != null)
            {
                displayColor = _powerUpData.PickupColor;
                displayName = $"{_powerUpData.PowerUpName}\n<size=70%>{_powerUpData.Description}</size>";
            }
            else if (activeType == ItemPickupType.Weapon && _weaponData != null)
            {
                displayColor = new Color(1f, 0.6f, 0.1f); // Màu cam vũ khí
                displayName = $"{_weaponData.WeaponName}\n<size=70%>Sát thương: {_weaponData.BaseDamage} | Băng: {_weaponData.MagSize}</size>";
            }

            if (_meshRenderer != null)
            {
                if (_itemPropBlock == null) _itemPropBlock = new MaterialPropertyBlock();
                _meshRenderer.GetPropertyBlock(_itemPropBlock);
                _itemPropBlock.SetColor(BaseColorPropId, displayColor);
                _itemPropBlock.SetColor(LegacyColorPropId, displayColor);
                _meshRenderer.SetPropertyBlock(_itemPropBlock);
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

        private static Camera _cachedMainCamera;

        private void Update()
        {
            // Hoạt ảnh xoay và nhấp nhô cục bộ (Client & Host)
            transform.Rotate(Vector3.up * (_spinSpeed * Time.deltaTime), Space.World);

            float newY = _initialPosition.y + Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Quay text hướng về Camera người chơi (ưu tiên camera local cache để tối ưu CPU)
            if (_nameLabel != null)
            {
                Camera cam = null;
                if (PlayerMovement.LocalInstance != null && PlayerMovement.LocalInstance.PlayerCamera != null)
                {
                    cam = PlayerMovement.LocalInstance.PlayerCamera;
                }
                else
                {
                    if (_cachedMainCamera == null)
                    {
                        _cachedMainCamera = Camera.main;
                    }
                    cam = _cachedMainCamera;
                }

                if (cam != null)
                {
                    _nameLabel.transform.rotation = Quaternion.LookRotation(_nameLabel.transform.position - cam.transform.position);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Server hoặc Offline có thẩm quyền xác thực và trao vật phẩm (Mục 6.3)
            if (!GameModeHelper.IsServerOrOffline || _hasDespawned) return;

            if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerMovement>() == null)
            {
                return;
            }

            var playerMove = other.GetComponentInParent<PlayerMovement>();
            if (playerMove == null) return;

            var playerHealth = playerMove.GetComponent<Health>();
            if (playerHealth != null && (playerHealth.IsDowned.Value || playerHealth.IsDead.Value))
            {
                return; // Người chơi đang gục/chết không nhặt được
            }

            var buffManager = playerMove.GetComponent<PlayerBuffManager>();
            var weaponController = playerMove.GetComponent<WeaponController>();

            var category = GameModeHelper.IsOffline ? _pickupType : (ItemPickupType)_netPickupType.Value;

            if (category == ItemPickupType.PowerUp)
            {
                if (_powerUpData != null && buffManager != null)
                {
                    buffManager.ApplyPowerUp(_powerUpData);
                    Debug.Log($"[ItemPickup] Người chơi đã nhặt bùa: {_powerUpData.PowerUpName}");
                    if (!GameModeHelper.IsOffline)
                    {
                        PlayPickupSoundClientRpc(transform.position);
                    }
                    DespawnItem();
                }
            }
            else if (category == ItemPickupType.Weapon)
            {
                if (_weaponData != null && weaponController != null)
                {
                    weaponController.AddOrSwitchWeapon(_weaponData);
                    Debug.Log($"[ItemPickup] Người chơi đã nhặt vũ khí: {_weaponData.WeaponName}");
                    if (!GameModeHelper.IsOffline)
                    {
                        PlayPickupSoundClientRpc(transform.position);
                    }
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

            if (GameModeHelper.IsOffline)
            {
                Destroy(gameObject);
            }
            else if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
