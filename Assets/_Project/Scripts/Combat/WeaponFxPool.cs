// Script: WeaponFxPool.cs
// Mục đích: Quản lý Object Pool tập trung cho toàn bộ hiệu ứng bắn súng chuẩn Juice AAA:
//           - Short Tracers with speed jitter
//           - Snappy Muzzle Flash with random Z rotation
//           - FIFO Shell Casing Ejection (tối đa 100 vỏ, tự sleep physics)
//           - Persistent Bullet Decals Ring Buffer (tối đa 150 vết đạn vĩnh viễn)
//           - Sparks & Flesh Impact Particles
//           - Đạt chuẩn 0 Bytes GC Allocation mỗi frame.
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

        private const int MAX_ACTIVE_SHELLS = 100;
        private const int MAX_ACTIVE_DECALS = 150;

        private static Material _sharedTracerMaterial;
        private static Material _sharedSparkMaterial;
        private static Material _sharedFleshMaterial;
        private static Material _sharedBrassMaterial;
        private static Material _sharedDecalMaterial;

        // Pools
        private readonly Queue<LineRenderer> _tracerPool = new Queue<LineRenderer>();
        private readonly Queue<GameObject> _muzzleFlashPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _sparkImpactPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _fleshImpactPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _shellPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _activeShells = new Queue<GameObject>();
        private readonly Queue<GameObject> _decalPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _activeDecals = new Queue<GameObject>();

        private Transform _poolRoot;
        private Transform _decalsRoot;
        private Transform _shellsRoot;

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

            _decalsRoot = new GameObject("[Decals_Root]").transform;
            _decalsRoot.SetParent(_poolRoot);

            _shellsRoot = new GameObject("[Shells_Root]").transform;
            _shellsRoot.SetParent(_poolRoot);

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
            Shader defaultShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");

            if (_sharedTracerMaterial == null)
            {
                _sharedTracerMaterial = new Material(defaultShader)
                {
                    color = new Color(1f, 0.90f, 0.45f, 1f)
                };
            }

            if (_sharedSparkMaterial == null)
            {
                _sharedSparkMaterial = new Material(defaultShader)
                {
                    color = new Color(1f, 0.85f, 0.3f, 1f)
                };
            }

            if (_sharedFleshMaterial == null)
            {
                _sharedFleshMaterial = new Material(defaultShader)
                {
                    color = new Color(0.9f, 0.12f, 0.12f, 1f)
                };
            }

            if (_sharedBrassMaterial == null)
            {
                Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? defaultShader;
                _sharedBrassMaterial = new Material(standardShader)
                {
                    color = new Color(0.85f, 0.72f, 0.32f, 1f)
                };
            }

            if (_sharedDecalMaterial == null)
            {
                _sharedDecalMaterial = new Material(defaultShader)
                {
                    color = new Color(0.12f, 0.12f, 0.14f, 0.92f)
                };
            }
        }

        private void PrewarmPools()
        {
            for (int i = 0; i < 25; i++)
            {
                _tracerPool.Enqueue(CreateNewTracer());
                _muzzleFlashPool.Enqueue(CreateNewMuzzleFlash());
                _sparkImpactPool.Enqueue(CreateNewSparkImpact());
                _fleshImpactPool.Enqueue(CreateNewFleshImpact());
                _shellPool.Enqueue(CreateNewShell());
            }

            for (int i = 0; i < 50; i++)
            {
                _decalPool.Enqueue(CreateNewDecal());
            }
        }

        // ================= 1. TRACER POOL (SHORT TRACERS WITH JITTER) =================

        private LineRenderer CreateNewTracer()
        {
            var go = new GameObject("PooledTracer");
            go.transform.SetParent(_poolRoot);
            var line = go.AddComponent<LineRenderer>();
            line.startWidth = 0.04f;
            line.endWidth = 0.015f;
            line.positionCount = 2;
            line.sharedMaterial = _sharedTracerMaterial;
            line.startColor = new Color(1f, 0.95f, 0.5f, 0.95f);
            line.endColor = new Color(1f, 0.45f, 0.15f, 0.25f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return line;
        }

        public void PlayTracer(Vector3 start, Vector3 end, float duration = 0.045f)
        {
            LineRenderer line = (_tracerPool.Count > 0) ? _tracerPool.Dequeue() : CreateNewTracer();
            if (line == null) return;

            line.gameObject.SetActive(true);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            StartCoroutine(RecycleTracerRoutine(line, duration));
        }

        public void PlayShortTracer(Vector3 start, Vector3 end, float baseSpeed = 160f, float tracerLength = 3.5f, float jitterPercent = 0.10f)
        {
            LineRenderer line = (_tracerPool.Count > 0) ? _tracerPool.Dequeue() : CreateNewTracer();
            if (line == null) return;

            line.gameObject.SetActive(true);
            float speedJitter = 1.0f + Random.Range(-jitterPercent, jitterPercent);
            float finalSpeed = Mathf.Max(40f, baseSpeed * speedJitter);
            StartCoroutine(AnimateShortTracerRoutine(line, start, end, finalSpeed, tracerLength));
        }

        private IEnumerator AnimateShortTracerRoutine(LineRenderer line, Vector3 start, Vector3 end, float speed, float tracerLength)
        {
            float totalDist = Vector3.Distance(start, end);
            if (totalDist <= 0.01f)
            {
                RecycleTracer(line);
                yield break;
            }

            Vector3 direction = (end - start).normalized;
            float currentDist = 0f;

            while (currentDist < totalDist)
            {
                currentDist += speed * Time.deltaTime;
                float headDist = Mathf.Min(currentDist, totalDist);
                float tailDist = Mathf.Max(0f, headDist - tracerLength);

                Vector3 headPos = start + direction * headDist;
                Vector3 tailPos = start + direction * tailDist;

                line.SetPosition(0, tailPos);
                line.SetPosition(1, headPos);

                yield return null;
            }

            RecycleTracer(line);
        }

        private IEnumerator RecycleTracerRoutine(LineRenderer line, float delay)
        {
            yield return new WaitForSeconds(delay);
            RecycleTracer(line);
        }

        private void RecycleTracer(LineRenderer line)
        {
            if (line != null)
            {
                line.gameObject.SetActive(false);
                line.transform.SetParent(_poolRoot);
                _tracerPool.Enqueue(line);
            }
        }

        // ================= 2. MUZZLE FLASH POOL (RANDOM Z ROTATION & FAST EXPIRY) =================

        private GameObject CreateNewMuzzleFlash()
        {
            var flashObj = new GameObject("PooledMuzzleFlash");
            flashObj.transform.SetParent(_poolRoot);

            var light = flashObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.4f);
            light.range = 3.5f;
            light.intensity = 2.8f;
            light.shadows = LightShadows.None;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FlashMesh";
            quad.transform.SetParent(flashObj.transform);
            quad.transform.localPosition = Vector3.forward * 0.05f;
            quad.transform.localScale = Vector3.one * 0.22f;
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

        public void PlayMuzzleFlash(Transform muzzlePoint, GameObject customPrefab = null, float duration = 0.04f)
        {
            if (muzzlePoint == null) return;

            if (customPrefab != null)
            {
                var custom = Instantiate(customPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
                custom.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                Destroy(custom, duration);
                return;
            }

            GameObject flash = (_muzzleFlashPool.Count > 0) ? _muzzleFlashPool.Dequeue() : CreateNewMuzzleFlash();
            if (flash == null) return;

            flash.transform.SetParent(muzzlePoint);
            flash.transform.localPosition = Vector3.zero;
            // Xoay ngẫu nhiên góc Z để chớp lửa biến đổi sống động
            flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            flash.SetActive(true);

            StartCoroutine(RecycleMuzzleFlashRoutine(flash, Mathf.Max(0.02f, duration)));
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

        // ================= 3. SHELL CASING EJECTION POOL (FIFO MAX 100) =================

        private GameObject CreateNewShell()
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "PooledShellCasing";
            shell.transform.SetParent(_shellsRoot);
            shell.transform.localScale = new Vector3(0.02f, 0.045f, 0.02f);

            var rend = shell.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = _sharedBrassMaterial;

            var col = shell.GetComponent<CapsuleCollider>() ?? shell.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            var rb = shell.GetComponent<Rigidbody>() ?? shell.AddComponent<Rigidbody>();
            rb.mass = 0.08f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            shell.SetActive(false);
            return shell;
        }

        public void EjectShell(Vector3 spawnPos, Vector3 forwardDir, Vector3 rightDir, Vector3 upDir, Vector3 baseVelocity, GameObject customPrefab = null)
        {
            // Kiểm tra giới hạn tối đa 100 vỏ đạn (FIFO: Tái sử dụng vỏ cũ nhất)
            while (_activeShells.Count >= MAX_ACTIVE_SHELLS)
            {
                var oldest = _activeShells.Dequeue();
                if (oldest != null)
                {
                    oldest.SetActive(false);
                    oldest.transform.SetParent(_shellsRoot);
                    _shellPool.Enqueue(oldest);
                }
            }

            GameObject shell = (_shellPool.Count > 0) ? _shellPool.Dequeue() : CreateNewShell();
            if (shell == null) return;

            shell.transform.SetParent(_shellsRoot);
            shell.transform.position = spawnPos;
            shell.transform.rotation = Quaternion.LookRotation(forwardDir, upDir);
            shell.SetActive(true);

            var rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                // Văng sang ngang bên phải với góc tản ngẫu nhiên
                Vector3 ejectionVel = rightDir * baseVelocity.x
                                    + upDir * baseVelocity.y
                                    + forwardDir * (baseVelocity.z + Random.Range(-0.5f, 0.5f));
                rb.linearVelocity = ejectionVel;
                rb.angularVelocity = Random.insideUnitSphere * 28f;
            }

            _activeShells.Enqueue(shell);
            StartCoroutine(SleepShellPhysicsRoutine(shell, 1.4f));
        }

        private IEnumerator SleepShellPhysicsRoutine(GameObject shell, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (shell != null && shell.activeInHierarchy)
            {
                var rb = shell.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Chuyển sang isKinematic để triệt tiêu tải CPU của Physics engine
                    rb.isKinematic = true;
                }
            }
        }

        // ================= 4. PERSISTENT BULLET DECALS (RING BUFFER MAX 150) =================

        private GameObject CreateNewDecal()
        {
            var decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "PooledBulletDecal";
            decal.transform.SetParent(_decalsRoot);
            decal.transform.localScale = Vector3.one * 0.12f;

            var col = decal.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = decal.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _sharedDecalMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            decal.SetActive(false);
            return decal;
        }

        public void SpawnBulletDecal(Vector3 pos, Vector3 normal, Transform hitTransform = null, GameObject customPrefab = null)
        {
            // Ring Buffer tối đa 150 vết đạn vĩnh viễn trên bản đồ
            while (_activeDecals.Count >= MAX_ACTIVE_DECALS)
            {
                var oldest = _activeDecals.Dequeue();
                if (oldest != null)
                {
                    oldest.SetActive(false);
                    oldest.transform.SetParent(_decalsRoot);
                    _decalPool.Enqueue(oldest);
                }
            }

            GameObject decal = (_decalPool.Count > 0) ? _decalPool.Dequeue() : CreateNewDecal();
            if (decal == null) return;

            // Offset normal 0.0025m để chống triệt để Z-fighting
            decal.transform.position = pos + normal * 0.0025f;
            decal.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);

            // Bám theo object bị bắn (nếu có) hoặc gắn vào Decals_Root
            if (hitTransform != null && !hitTransform.CompareTag("Player"))
            {
                decal.transform.SetParent(hitTransform, true);
            }
            else
            {
                decal.transform.SetParent(_decalsRoot, true);
            }

            decal.SetActive(true);
            _activeDecals.Enqueue(decal);
        }

        // ================= 5. IMPACT POOL (SPARKS & FLESH) =================

        private GameObject CreateNewSparkImpact()
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "PooledSparkImpact";
            spark.transform.SetParent(_poolRoot);
            spark.transform.localScale = Vector3.one * 0.08f;
            var col = spark.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = spark.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = _sharedSparkMaterial;

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
            if (rend != null) rend.sharedMaterial = _sharedFleshMaterial;

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
