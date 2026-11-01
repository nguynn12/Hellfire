// Script: ArcherProjectile.cs
// Mục đích: Quả cầu năng lượng/đạn sinh học phóng ra từ bàn tay Zombie tầm xa.
// Môi trường: Server-Authoritative tính toán va chạm và sát thương; Client hiển thị vệt sáng và hiệu ứng nổ.

using System.Collections;
using Hellfire.Combat;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class ArcherProjectile : MonoBehaviour
    {
        [Header("Projectile Flight")]
        [SerializeField] private float _speed = 20f;
        [SerializeField] private float _lifeTime = 4f;
        [SerializeField] private float _radius = 0.22f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Header("Damage Settings")]
        [SerializeField] private float _damage = 12f;

        [Header("Visual & Light")]
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private Light _pointLight;

        private Vector3 _direction;
        private ulong _attackerId;
        private Transform _ignoreRoot;
        private bool _isServerAuthoritative = true;
        private bool _hasHit = false;
        private float _spawnTime;

        public float Speed => _speed;
        public float Damage => _damage;
        public bool HasHit => _hasHit;
        public Vector3 Direction => _direction;

        public void Initialize(Vector3 direction, float speed, float damage, ulong attackerId, Transform ignoreRoot, bool isServerAuthoritative)
        {
            _direction = direction.normalized;
            _speed = speed > 0.1f ? speed : 20f;
            _damage = damage;
            _attackerId = attackerId;
            _ignoreRoot = ignoreRoot;
            _isServerAuthoritative = isServerAuthoritative;
            _hasHit = false;
            _spawnTime = Time.time;

            if (_direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(_direction);
            }
        }

        private void Start()
        {
            _spawnTime = Time.time;
            if (_direction.sqrMagnitude < 0.001f)
            {
                _direction = transform.forward;
            }

            // Đảm bảo collider ở chế độ Trigger
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (RangedZombieAI.IsDebugLoggingEnabled())
            {
                Debug.Log($"[ArcherDebug] Projectile Hellfire Orb đang bay từ {transform.position} theo hướng {_direction} (Speed: {_speed}m/s).");
            }
        }

        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        public void SimulateStep(float dt)
        {
            if (_hasHit) return;

            // Tự hủy sau khi hết thời gian bay
            if (Time.time - _spawnTime > _lifeTime)
            {
                if (RangedZombieAI.IsDebugLoggingEnabled())
                {
                    Debug.Log($"[ArcherDebug] Projectile Hellfire Orb tự hủy sau {_lifeTime}s (hết thời gian sống).");
                }
                SafeDestroy(gameObject);
                return;
            }

            float stepDist = _speed * (dt > 0.0001f ? dt : 0.02f);
            Vector3 currentPos = transform.position;

            // SphereCast từng frame để chống hiện tượng xuyên vật thể (tunneling) ở tốc độ cao
            if (Physics.SphereCast(currentPos, _radius, _direction, out RaycastHit hit, stepDist, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Bỏ qua bản thân người bắn hoặc con cháu của người bắn
                if (_ignoreRoot != null && (hit.transform == _ignoreRoot || hit.transform.IsChildOf(_ignoreRoot)))
                {
                    // Tiếp tục bay
                    transform.position += _direction * stepDist;
                    return;
                }

                // Chạm đích
                transform.position = hit.point;
                HandleImpact(hit);
                return;
            }

            // Di chuyển tịnh tiến
            transform.position += _direction * stepDist;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasHit) return;

            // Bỏ qua bản thân
            if (_ignoreRoot != null && (other.transform == _ignoreRoot || other.transform.IsChildOf(_ignoreRoot)))
            {
                return;
            }

            // Raycast ngược lại để lấy điểm tiếp xúc chuẩn xác
            RaycastHit hit;
            if (Physics.Raycast(transform.position - _direction * 0.5f, _direction, out hit, 1.0f, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                HandleImpact(hit);
            }
            else
            {
                hit = new RaycastHit
                {
                    point = transform.position,
                    normal = -_direction
                };
                HandleImpact(hit);
            }
        }

        private void HandleImpact(RaycastHit hit)
        {
            if (_hasHit) return;
            _hasHit = true;

            if (RangedZombieAI.IsDebugLoggingEnabled())
            {
                string targetName = hit.collider != null ? hit.collider.name : "Terrain";
                Debug.Log($"[ArcherDebug] Projectile Hellfire Orb va chạm {targetName} tại {hit.point}, gây {_damage} sát thương (ServerAuth: {_isServerAuthoritative}).");
            }

            // 1. Gây sát thương Server-Authoritative duy nhất một lần
            if (_isServerAuthoritative && hit.collider != null)
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    HitboxType hitboxType = HitboxType.Torso;
                    var hitboxId = hit.collider.GetComponent<HitboxIdentifier>();
                    if (hitboxId != null)
                    {
                        hitboxType = hitboxId.Type;
                    }

                    damageable.TakeDamage(_damage, hitboxType, _attackerId);
                }
            }

            // 2. Hiệu ứng va chạm (Sparks, Light burst)
            SpawnImpactEffect(hit.point, hit.normal);

            // 3. Hủy đối tượng đạn
            SafeDestroy(gameObject);
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void SpawnImpactEffect(Vector3 point, Vector3 normal)
        {
            var impactObj = new GameObject("ArcherProjectile_Impact");
            impactObj.transform.position = point;
            if (normal.sqrMagnitude > 0.001f)
            {
                impactObj.transform.rotation = Quaternion.LookRotation(normal);
            }

            // Tia sáng chớp khi nổ
            var light = impactObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.40f, 0.05f);
            light.range = 6.0f;
            light.intensity = 6.0f;

            // Khối cầu phát nổ tan biến nhanh
            var blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blast.name = "ImpactBlast";
            blast.transform.SetParent(impactObj.transform);
            blast.transform.localPosition = Vector3.zero;
            blast.transform.localScale = Vector3.one * 0.65f;

            var col = blast.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var rend = blast.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                mat.color = new Color(1f, 0.45f, 0.08f, 1f);
                rend.material = mat;
            }

            if (Application.isPlaying)
            {
                Destroy(impactObj, 0.25f);
            }
            else
            {
                DestroyImmediate(impactObj);
            }
        }
    }
}
