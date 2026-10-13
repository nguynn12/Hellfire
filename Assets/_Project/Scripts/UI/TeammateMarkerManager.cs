// Script: TeammateMarkerManager.cs
// Mục đích: Quản lý pool và cập nhật World-to-Screen cho các marker vị trí đồng đội trên Canvas Screen Space Overlay (Mục 16.3 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections.Generic;
using Hellfire.Combat;
using Hellfire.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    public class TeammateMarkerManager : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private int _poolSize = 4;
        [SerializeField] private GameObject _markerPrefab;

        private readonly List<TeammateMarkerItem> _markerPool = new List<TeammateMarkerItem>();
        private Camera _mainCamera;

        private void Start()
        {
            InitializePool();

            PlayerIdentity.OnAnyPlayerIdentityChanged += HandlePlayerListChanged;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientEvent;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientEvent;
            }

            HandlePlayerListChanged();
        }

        private void OnDestroy()
        {
            PlayerIdentity.OnAnyPlayerIdentityChanged -= HandlePlayerListChanged;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientEvent;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientEvent;
            }
        }

        private void InitializePool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var marker = CreateMarkerInstance();
                marker.Unbind();
                _markerPool.Add(marker);
            }
        }

        private TeammateMarkerItem CreateMarkerInstance()
        {
            GameObject markerObj;
            if (_markerPrefab != null)
            {
                markerObj = Instantiate(_markerPrefab, transform);
            }
            else
            {
                markerObj = BuildProceduralMarker();
                markerObj.transform.SetParent(transform, false);
            }

            var item = markerObj.GetComponent<TeammateMarkerItem>();
            if (item == null)
            {
                item = markerObj.AddComponent<TeammateMarkerItem>();
            }

            return item;
        }

        private GameObject BuildProceduralMarker()
        {
            var root = new GameObject("TeammateMarker", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(40f, 60f);

            // Mũi tên chỉ hướng (khi nằm ngoài màn hình)
            var arrowObj = new GameObject("ArrowIndicator", typeof(RectTransform), typeof(Image));
            arrowObj.transform.SetParent(root.transform, false);
            var arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(16f, 16f);
            arrowRect.anchoredPosition = new Vector2(0f, 26f);
            var arrowImg = arrowObj.GetComponent<Image>();
            arrowImg.color = Color.white;
            arrowObj.SetActive(false);

            // Icon nền tròn/vuông chứa số người chơi
            var iconObj = new GameObject("IconBg", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(root.transform, false);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
            var iconImg = iconObj.GetComponent<Image>();
            iconImg.color = Color.cyan;

            var numObj = new GameObject("NumberText", typeof(RectTransform), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(iconObj.transform, false);
            var numTmp = numObj.GetComponent<TextMeshProUGUI>();
            numTmp.fontSize = 15;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.alignment = TextAlignmentOptions.Center;
            numTmp.color = Color.black;
            var numRect = numObj.GetComponent<RectTransform>();
            numRect.anchorMin = Vector2.zero;
            numRect.anchorMax = Vector2.one;
            numRect.offsetMin = Vector2.zero;
            numRect.offsetMax = Vector2.zero;

            // Khoảng cách mét bên dưới icon
            var distObj = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            distObj.transform.SetParent(root.transform, false);
            var distTmp = distObj.GetComponent<TextMeshProUGUI>();
            distTmp.fontSize = 12;
            distTmp.fontStyle = FontStyles.Bold;
            distTmp.alignment = TextAlignmentOptions.Center;
            distTmp.color = Color.white;
            var distRect = distObj.GetComponent<RectTransform>();
            distRect.anchoredPosition = new Vector2(0f, -20f);
            distRect.sizeDelta = new Vector2(60f, 16f);

            var markerComp = root.AddComponent<TeammateMarkerItem>();
            markerComp.Configure(
                rootRect,
                iconImg,
                numTmp,
                distTmp,
                arrowRect,
                root.GetComponent<CanvasGroup>()
            );

            return root;
        }

        private void HandleClientEvent(ulong clientId)
        {
            HandlePlayerListChanged();
        }

        private void HandlePlayerListChanged()
        {
            var identities = PlayerIdentity.All;
            var teammates = new List<PlayerIdentity>();

            for (int i = 0; i < identities.Count; i++)
            {
                var id = identities[i];
                if (id != null && !id.IsOwner)
                {
                    teammates.Add(id);
                }
            }

            // Gán các đồng đội vào pool
            for (int i = 0; i < _markerPool.Count; i++)
            {
                if (i < teammates.Count)
                {
                    _markerPool[i].Bind(teammates[i]);
                }
                else
                {
                    _markerPool[i].Unbind();
                }
            }
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;

            for (int i = 0; i < _markerPool.Count; i++)
            {
                if (_markerPool[i] != null && _markerPool[i].IsBound)
                {
                    _markerPool[i].UpdateMarker(_mainCamera, screenW, screenH);
                }
            }
        }
    }
}
