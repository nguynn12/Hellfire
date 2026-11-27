using TMPro;
using UnityEngine;

namespace Hellfire.Combat
{
    /// <summary>
    /// Component quản lý một chữ số sát thương bay (Floating Damage Number) trong không gian 3D.
    /// Tự động xoay mặt về phía Camera và bay lên theo đường cong mượt mà.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingDamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _textMesh;
        [SerializeField] private float _floatSpeed = 1.8f;
        [SerializeField] private float _lifetime = 0.85f;
        [SerializeField] private Vector3 _randomOffsetRange = new Vector3(0.3f, 0.2f, 0.3f);

        private float _timer;
        private Color _initialColor;
        private Camera _targetCamera;

        private void Awake()
        {
            if (_textMesh == null) _textMesh = GetComponent<TextMeshPro>();
            if (_textMesh != null)
                Hellfire.UI.InfernalTheme.Type(_textMesh, _textMesh.fontSize, Hellfire.UI.InfernalTheme.Bone, true);
        }

        public void Initialize(float damage, bool isHeadshot, Camera targetCamera = null)
        {
            if (_textMesh == null) _textMesh = GetComponent<TextMeshPro>();

            // 1. Cấu hình chữ và màu sắc
            int dmgInt = Mathf.RoundToInt(damage);
            if (isHeadshot)
            {
                _textMesh.SetText("{0:0}", dmgInt);
                _textMesh.fontSize = 5.5f;
                _initialColor = new Color(1.0f, 0.25f, 0.1f, 1.0f); // Đỏ cam rực cháy
            }
            else
            {
                _textMesh.SetText("{0:0}", dmgInt);
                _textMesh.fontSize = 4.0f;
                _initialColor = Hellfire.UI.InfernalTheme.Bone;
            }

            _textMesh.color = _initialColor;

            // 2. Thêm chút độ lệch vị trí ngẫu nhiên để khi bắn nhanh các số không bị đè lên nhau
            Vector3 offset = new Vector3(
                Random.Range(-_randomOffsetRange.x, _randomOffsetRange.x),
                Random.Range(0f, _randomOffsetRange.y),
                Random.Range(-_randomOffsetRange.z, _randomOffsetRange.z)
            );
            transform.position += offset;

            _timer = 0f;
            _targetCamera = targetCamera;
            FaceCamera();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float progress = Mathf.Clamp01(_timer / _lifetime);

            // 1. Bay nhẹ lên trên
            transform.position += Vector3.up * (_floatSpeed * (1.0f - progress * 0.5f) * Time.deltaTime);

            // 2. Mờ dần (Fade out alpha)
            if (_textMesh != null)
            {
                Color c = _initialColor;
                c.a = Mathf.Lerp(1.0f, 0.0f, progress * progress);
                _textMesh.color = c;
            }

            // 3. Luôn quay mặt vào Camera góc nhìn thứ nhất
            FaceCamera();

            // 4. Hết thời gian sống -> Tự hủy hoặc ẩn đi
            if (_timer >= _lifetime)
            {
                gameObject.SetActive(false);
            }
        }

        private void FaceCamera()
        {
            Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
            if (cam != null)
            {
                Vector3 direction = transform.position - cam.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}

