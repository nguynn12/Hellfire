// Script: RangedZombieVisuals.cs
// Mục đích: Quản lý hiệu ứng hình ảnh (VFX) độc lập cho quái tầm xa: Đường ngắm cảnh báo (Telegraph Laser), Muzzle Flash, Bullet Tracer, và Hit Impact.
// Môi trường thực thi: Client-side (nhận sự kiện từ Server qua ClientRpc).

using System.Collections;
using UnityEngine;

namespace Hellfire.AI
{
    [DisallowMultipleComponent]
    public class RangedZombieVisuals : MonoBehaviour
    {
        [Header("Telegraph (Cảnh báo trước khi bắn)")]
        [SerializeField] private LineRenderer _laserSight;
        [SerializeField] private Color _laserColor = new Color(1f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private float _laserWidth = 0.025f;

        [Header("Tracer & Impact VFX")]
        [SerializeField] private float _tracerDuration = 0.08f;
        [SerializeField] private Color _tracerStartColor = new Color(1f, 0.2f, 0.8f, 1f);
        [SerializeField] private Color _tracerEndColor = new Color(1f, 0.1f, 0.1f, 0.4f);

        [Header("Muzzle Flash Light")]
        [SerializeField] private Color _muzzleFlashColor = new Color(1f, 0.3f, 0.1f);
        [SerializeField] private float _muzzleFlashRange = 3.5f;

        private void Awake()
        {
            EnsureLaserSight();
        }

        private void EnsureLaserSight()
        {
            if (_laserSight == null)
            {
                var laserObj = new GameObject("TelegraphLaser");
                laserObj.transform.SetParent(transform);
                laserObj.transform.localPosition = Vector3.zero;

                _laserSight = laserObj.AddComponent<LineRenderer>();
                _laserSight.startWidth = _laserWidth;
                _laserSight.endWidth = _laserWidth * 0.5f;
                _laserSight.positionCount = 2;
                _laserSight.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                _laserSight.startColor = _laserColor;
                _laserSight.endColor = new Color(_laserColor.r, _laserColor.g, _laserColor.b, 0.1f);
                _laserSight.enabled = false;
            }
        }

        public void SetTelegraphActive(bool active, Vector3 origin = default, Vector3 target = default)
        {
            EnsureLaserSight();
            if (_laserSight != null)
            {
                _laserSight.enabled = active;
                if (active)
                {
                    _laserSight.SetPosition(0, origin);
                    _laserSight.SetPosition(1, target);
                }
            }
        }

        public void PlayShootVfx(Vector3 from, Vector3 to, bool hitPlayer)
        {
            StartCoroutine(TracerRoutine(from, to, hitPlayer));
            StartCoroutine(MuzzleFlashRoutine(from));
        }

        private IEnumerator TracerRoutine(Vector3 from, Vector3 to, bool hitPlayer)
        {
            var tracerObj = new GameObject("ArcherBulletTracer");
            var lr = tracerObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.05f;
            lr.endWidth = 0.02f;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.startColor = _tracerStartColor;
            lr.endColor = _tracerEndColor;

            // Spawn impact spark tại điểm va chạm
            SpawnImpactSpark(to, hitPlayer);

            yield return new WaitForSeconds(_tracerDuration);

            if (mat != null) Destroy(mat);
            if (tracerObj != null) Destroy(tracerObj);
        }

        private void SpawnImpactSpark(Vector3 point, bool isPlayer)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ArcherImpactSpark";
            spark.transform.position = point;
            spark.transform.localScale = Vector3.one * 0.12f;

            var col = spark.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = spark.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                rend.material.color = isPlayer ? new Color(1f, 0.1f, 0.1f, 1f) : new Color(1f, 0.7f, 0.2f, 1f);
            }

            Destroy(spark, 0.12f);
        }

        private IEnumerator MuzzleFlashRoutine(Vector3 muzzlePos)
        {
            var flashObj = new GameObject("ArcherMuzzleFlash");
            flashObj.transform.position = muzzlePos;

            var light = flashObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = _muzzleFlashColor;
            light.range = _muzzleFlashRange;
            light.intensity = 4.0f;

            yield return new WaitForSeconds(0.06f);

            if (flashObj != null) Destroy(flashObj);
        }
    }
}
