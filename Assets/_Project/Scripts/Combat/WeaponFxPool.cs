// Script: WeaponFxPool.cs
// Mục đích: Quản lý Object Pool tập trung cho toàn bộ hiệu ứng bắn súng (Tracers, Muzzle Flashes, Bullet Impacts).
//           Khắc phục triệt để GC Allocation do tạo/hủy GameObject liên tục và Memory Leak do clone Material.
// Môi trường thực thi: Client (hiệu ứng hình ảnh & âm thanh cục bộ).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponFxPool : MonoBehaviour
    {
        public static WeaponFxPool Instance { get; private set; }

        private static Material _sharedTracerMaterial;
        private static Material _sharedSparkMaterial;
        private static Material _sharedFleshMaterial;

        // Pools
        private readonly Queue<LineRenderer> _tracerPool = new Queue<LineRenderer>();
        private readonly Queue<GameObject> _muzzleFlashPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _sparkImpactPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _fleshImpactPool = new Queue<GameObject>();

        private Transform _poolRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _poolRoot = new GameObject("[WeaponFxPool_Root]").transform;
            _poolRoot.SetParent(transform);

            InitSharedMaterials();
            PrewarmPools();
        }

        public static WeaponFxPool GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("WeaponFxPool");
                Instance = go.AddComponent<WeaponFxPool>();
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        private static void InitSharedMaterials()
        {
            if (_sharedTracerMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _sharedTracerMaterial = new Material(s)
                {
                    color = new Color(1f, 0.85f, 0.4f, 1f)
                };
            }

            if (_sharedSparkMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _sharedSparkMaterial = new Material(s)
                {
                    color = new Color(1f, 0.8f, 0.2f, 1f)
                };
            }

            if (_sharedFleshMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _sharedFleshMaterial = new Material(s)
                {
                    color = new Color(0.9f, 0.1f, 0.1f, 1f)
                };
            }
        }

        private void PrewarmPools()
        {
            for (int i = 0; i < 20; i++)
            {
                _tracerPool.Enqueue(CreateNewTracer());
                _muzzleFlashPool.Enqueue(CreateNewMuzzleFlash());
                _sparkImpactPool.Enqueue(CreateNewSparkImpact());
                _fleshImpactPool.Enqueue(CreateNewFleshImpact());
            }
        }

        // ================= TRACER POOL =================

        private LineRenderer CreateNewTracer()
        {
            var go = new GameObject("PooledTracer");
            go.transform.SetParent(_poolRoot);
            var line = go.AddComponent<LineRenderer>();
            line.startWidth = 0.035f;
            line.endWidth = 0.01f;
            line.positionCount = 2;
            line.sharedMaterial = _sharedTracerMaterial;
            line.startColor = new Color(1f, 0.95f, 0.5f, 0.95f);
            line.endColor = new Color(1f, 0.4f, 0.1f, 0.2f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return line;
        }

        public void PlayTracer(Vector3 start, Vector3 end, float duration = 0.05f)
        {
            LineRenderer line = (_tracerPool.Count > 0) ? _tracerPool.Dequeue() : CreateNewTracer();
            if (line == null) return;

            line.gameObject.SetActive(true);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            StartCoroutine(RecycleTracerRoutine(line, duration));
        }

        private IEnumerator RecycleTracerRoutine(LineRenderer line, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (line != null)
            {
                line.gameObject.SetActive(false);
                line.transform.SetParent(_poolRoot);
                _tracerPool.Enqueue(line);
            }
        }

        // ================= MUZZLE FLASH POOL =================

        private GameObject CreateNewMuzzleFlash()
        {
            var flashObj = new GameObject("PooledMuzzleFlash");
            flashObj.transform.SetParent(_poolRoot);

            // Light flash
            var light = flashObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.4f);
            light.range = 3.5f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;

            // Flash sphere/cross
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FlashMesh";
            quad.transform.SetParent(flashObj.transform);
            quad.transform.localPosition = Vector3.forward * 0.05f;
            quad.transform.localScale = Vector3.one * 0.18f;
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedSparkMaterial;
            }

            flashObj.SetActive(false);
            return flashObj;
        }

        public void PlayMuzzleFlash(Transform muzzlePoint, GameObject customPrefab = null)
        {
            if (muzzlePoint == null) return;

            if (customPrefab != null)
            {
                var custom = Instantiate(customPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
                Destroy(custom, 0.06f);
                return;
            }

            GameObject flash = (_muzzleFlashPool.Count > 0) ? _muzzleFlashPool.Dequeue() : CreateNewMuzzleFlash();
            if (flash == null) return;

            flash.transform.SetParent(muzzlePoint);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
            flash.SetActive(true);

            StartCoroutine(RecycleMuzzleFlashRoutine(flash, 0.045f));
        }

        private IEnumerator RecycleMuzzleFlashRoutine(GameObject flash, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (flash != null)
            {
                flash.SetActive(false);
                flash.transform.SetParent(_poolRoot);
                _muzzleFlashPool.Enqueue(flash);
            }
        }

        // ================= IMPACT POOL (SPARKS & FLESH) =================

        private GameObject CreateNewSparkImpact()
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "PooledSparkImpact";
            spark.transform.SetParent(_poolRoot);
            spark.transform.localScale = Vector3.one * 0.08f;
            var col = spark.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = spark.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedSparkMaterial;
            }

            spark.SetActive(false);
            return spark;
        }

        private GameObject CreateNewFleshImpact()
        {
            var flesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flesh.name = "PooledFleshImpact";
            flesh.transform.SetParent(_poolRoot);
            flesh.transform.localScale = Vector3.one * 0.12f;
            var col = flesh.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = flesh.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedFleshMaterial;
            }

            flesh.SetActive(false);
            return flesh;
        }

        public void PlayImpact(Vector3 pos, Vector3 normal, bool isFlesh, GameObject customPrefab = null)
        {
            if (customPrefab != null)
            {
                var custom = Instantiate(customPrefab, pos, Quaternion.LookRotation(normal));
                Destroy(custom, 1.0f);
                return;
            }

            Queue<GameObject> targetPool = isFlesh ? _fleshImpactPool : _sparkImpactPool;
            GameObject impactObj = (targetPool.Count > 0) ? targetPool.Dequeue() : (isFlesh ? CreateNewFleshImpact() : CreateNewSparkImpact());
            if (impactObj == null) return;

            impactObj.transform.position = pos;
            impactObj.transform.rotation = Quaternion.LookRotation(normal);
            impactObj.SetActive(true);

            StartCoroutine(RecycleImpactRoutine(impactObj, targetPool, 0.15f));
        }

        private IEnumerator RecycleImpactRoutine(GameObject impactObj, Queue<GameObject> pool, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (impactObj != null)
            {
                impactObj.SetActive(false);
                impactObj.transform.SetParent(_poolRoot);
                pool.Enqueue(impactObj);
            }
        }
    }
}
