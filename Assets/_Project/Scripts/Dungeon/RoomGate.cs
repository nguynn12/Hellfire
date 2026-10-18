// Script: RoomGate.cs
// Mục đích: Cổng sắt tại cửa phòng Combat và Boss, có collider và NavMeshObstacle đóng/mở theo lệnh Server (Mục 17.4 đặc tả kỹ thuật).
// Môi trường thực thi: Client/Host (Local geometry representation đồng bộ qua SetRoomGatesClientRpc).

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    public class RoomGate : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private BoxCollider _gateCollider;
        [SerializeField] private NavMeshObstacle _navObstacle;

        [Header("Settings")]
        [SerializeField] private float _wallHeight = 4.5f;
        [SerializeField] private float _closeDuration = 0.6f;
        [SerializeField] private float _openDuration = 0.8f;

        private Coroutine _animCoroutine;
        private bool _isClosed = false;

        public bool IsClosed => _isClosed;

        public void Initialize(Transform visualRoot, BoxCollider gateCollider, NavMeshObstacle navObstacle, float wallHeight)
        {
            _visualRoot = visualRoot;
            _gateCollider = gateCollider;
            _navObstacle = navObstacle;
            _wallHeight = wallHeight;

            // Ban đầu cổng mở (Mục 17.4: phòng Idle có cổng mở)
            SetGateImmediate(false);
        }

        public void SetClosed(bool closed)
        {
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
            }
            _animCoroutine = StartCoroutine(AnimateGate(closed));
        }

        public void SetGateImmediate(bool closed)
        {
            _isClosed = closed;

            if (_gateCollider != null)
            {
                _gateCollider.enabled = closed;
            }

            if (_navObstacle != null)
            {
                _navObstacle.carving = true;
                _navObstacle.enabled = closed;
            }

            if (_visualRoot != null)
            {
                Vector3 pos = _visualRoot.localPosition;
                pos.y = closed ? 0f : _wallHeight;
                _visualRoot.localPosition = pos;
            }
        }

        private IEnumerator AnimateGate(bool closing)
        {
            _isClosed = closing;
            float duration = closing ? _closeDuration : _openDuration;
            float startY = _visualRoot != null ? _visualRoot.localPosition.y : (closing ? _wallHeight : 0f);
            float endY = closing ? 0f : _wallHeight;

            if (closing)
            {
                // TODO(cần xác nhận): Phát âm thanh hạ cổng sắt
                // Bật va chạm và NavMeshObstacle khi bắt đầu đóng
                if (_gateCollider != null) _gateCollider.enabled = true;
                if (_navObstacle != null)
                {
                    _navObstacle.carving = true;
                    _navObstacle.enabled = true;
                }
            }
            else
            {
                // TODO(cần xác nhận): Phát âm thanh nâng cổng sắt
                // TẮT va chạm và NavMeshObstacle ngay khi mở để người chơi và AI đi qua được
                if (_gateCollider != null) _gateCollider.enabled = false;
                if (_navObstacle != null) _navObstacle.enabled = false;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (_visualRoot != null)
                {
                    Vector3 pos = _visualRoot.localPosition;
                    pos.y = Mathf.Lerp(startY, endY, smoothT);
                    _visualRoot.localPosition = pos;
                }
                yield return null;
            }

            if (_visualRoot != null)
            {
                Vector3 pos = _visualRoot.localPosition;
                pos.y = endY;
                _visualRoot.localPosition = pos;
            }

            _animCoroutine = null;
        }
    }
}
