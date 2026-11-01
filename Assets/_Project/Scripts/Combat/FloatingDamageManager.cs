using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Quản lý Object Pool cho các chữ số sát thương bay (Floating Damage Numbers).
    /// Đảm bảo không tạo Garbage Collection (GC) khi người chơi xả đạn liên thanh.
    /// Hoàn toàn độc lập, có thể gọi từ bất kỳ đâu qua FloatingDamageManager.Instance.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingDamageManager : MonoBehaviour
    {
        public static FloatingDamageManager Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 25;
        [SerializeField] private GameObject _popupPrefab;

        private readonly List<FloatingDamagePopup> _pool = new List<FloatingDamagePopup>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateNewPopupInstance();
            }
        }

        private FloatingDamagePopup CreateNewPopupInstance()
        {
            GameObject obj;
            if (_popupPrefab != null)
            {
                obj = Instantiate(_popupPrefab, transform);
            }
            else
            {
                // Fallback tạo procedural GameObject nếu chưa gán prefab
                obj = new GameObject("DamagePopup_Pooled");
                obj.transform.SetParent(transform);
                var tmp = obj.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                obj.AddComponent<FloatingDamagePopup>();
            }

            obj.SetActive(false);
            var popup = obj.GetComponent<FloatingDamagePopup>();
            _pool.Add(popup);
            return popup;
        }

        /// <summary>
        /// Kích hoạt một chữ số sát thương bay tại vị trí va chạm trúng đạn.
        /// </summary>
        public void SpawnDamagePopup(Vector3 worldPosition, float damage, bool isHeadshot, Camera targetCamera = null)
        {
            FloatingDamagePopup availablePopup = null;

            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeInHierarchy)
                {
                    availablePopup = _pool[i];
                    break;
                }
            }

            if (availablePopup == null)
            {
                availablePopup = CreateNewPopupInstance();
            }

            availablePopup.transform.position = worldPosition;
            availablePopup.gameObject.SetActive(true);
            availablePopup.Initialize(damage, isHeadshot, targetCamera);
        }
    }
}

