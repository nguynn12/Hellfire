// Script: CosmeticBullet.cs
// Mục đích: Điều khiển viên đạn bay 3D trực quan (Cosmetic Bullet) kèm vệt sáng (Trail) từ nòng súng đến mục tiêu.
// Không can thiệp vào logic mạng hay trừ máu của Hitscan, tăng trải nghiệm thị giác và độ đầm khi bắn.

using System;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class CosmeticBullet : MonoBehaviour
    {
        private Vector3 _startPos;
        private Vector3 _targetPos;
        private float _speed = 220f;
        private float _travelTime;
        private float _elapsedTime;
        private Action _onHitCallback;
        private bool _hasArrived;
        private TrailRenderer _trail;
        private MeshRenderer _meshRenderer;

        /// <summary>
        /// Khởi tạo đường bay của viên đạn từ đầu nòng đến điểm trúng đích.
        /// </summary>
        public void Launch(
            Vector3 startPos,
            Vector3 targetPos,
            float speed,
            Material bulletMat,
            Material trailMat,
            Color coreColor,
            Color trailColor,
            Action onHit = null
        )
        {
            _startPos = startPos;
            _targetPos = targetPos;
            _speed = Mathf.Max(50f, speed);
            _onHitCallback = onHit;
            _elapsedTime = 0f;
            _hasArrived = false;

            transform.position = startPos;

            Vector3 toTarget = targetPos - startPos;
            float distance = toTarget.magnitude;

            if (distance > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }

            _travelTime = distance / _speed;

            // 1. Tạo Mesh đầu đạn (Viên đạn hình con thoi / capsule nhỏ phát sáng)
            SetupBulletMesh(bulletMat, coreColor);

            // 2. Tạo TrailRenderer (Vệt sáng xé gió sau đuôi đạn)
            SetupTrail(trailMat, coreColor, trailColor);

            // Tự hủy dự phòng sau 4 giây nếu bắn vào không gian vô tận
            Destroy(gameObject, 4.0f);
        }

        private void SetupBulletMesh(Material mat, Color color)
        {
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Tạo mesh capsule hoặc sphere kéo dẹt
            var tempObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshFilter.sharedMesh = tempObj.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempObj);

            transform.localScale = new Vector3(0.022f, 0.022f, 0.09f);

            if (mat != null)
            {
                _meshRenderer.sharedMaterial = mat;
            }
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        private void SetupTrail(Material mat, Color startCol, Color endCol)
        {
            _trail = gameObject.GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();

            _trail.time = 0.05f; // Đuôi đạn ngắn và sắc nét
            _trail.minVertexDistance = 0.05f;
            _trail.startWidth = 0.035f;
            _trail.endWidth = 0.0f;

            if (mat != null)
            {
                _trail.sharedMaterial = mat;
            }

            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(startCol, 0.0f),
                    new GradientColorKey(endCol, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            _trail.colorGradient = gradient;
        }

        private void Update()
        {
            if (_hasArrived) return;

            _elapsedTime += Time.deltaTime;

            if (_travelTime <= 0.0001f || _elapsedTime >= _travelTime)
            {
                ArriveAtTarget();
                return;
            }

            float t = _elapsedTime / _travelTime;
            transform.position = Vector3.Lerp(_startPos, _targetPos, t);
        }

        private void ArriveAtTarget()
        {
            _hasArrived = true;
            transform.position = _targetPos;

            // Kích hoạt hiệu ứng va chạm tại điểm trúng
            _onHitCallback?.Invoke();

            // Ẩn đầu đạn, chờ vệt sáng đuôi mờ dần rồi xóa hẳn
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            if (_trail != null) _trail.autodestruct = true;

            Destroy(gameObject, 0.06f);
        }
    }
}

