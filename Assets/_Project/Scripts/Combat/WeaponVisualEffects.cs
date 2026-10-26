using System.Collections;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponVisualEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponController _weaponController;
        [SerializeField] private Transform _muzzlePoint;
        [SerializeField] private AudioSource _audioSource;

        [Header("Tracer Settings")]
        [SerializeField] private float _tracerDuration = 0.09f;
        [SerializeField] private float _tracerStartWidth = 0.035f;
        [SerializeField] private float _tracerEndWidth = 0.015f;
        [SerializeField] private Color _tracerStartColor = new Color(1f, 0.92f, 0.45f, 1f);
        [SerializeField] private Color _tracerEndColor = new Color(1f, 0.45f, 0.1f, 0.4f);

        [Header("Muzzle Flash Settings")]
        [SerializeField] private float _flashDuration = 0.05f;
        [SerializeField] private float _flashRange = 4.0f;
        [SerializeField] private float _flashIntensity = 5.0f;
        [SerializeField] private Color _flashColor = new Color(1f, 0.85f, 0.4f);

        private Camera _playerCamera;

        private void Awake()
        {
            if (_weaponController == null)
            {
                _weaponController = GetComponentInParent<WeaponController>();
                if (_weaponController == null) _weaponController = GetComponent<WeaponController>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null) _audioSource = GetComponentInParent<AudioSource>();
            }

            _playerCamera = GetComponentInChildren<Camera>(true);
            if (_playerCamera == null) _playerCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired += HandleWeaponFired;
                _weaponController.OnTracerRequested += HandleTracerRequested;
                _weaponController.OnImpactRequested += HandleImpactRequested;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponFired -= HandleWeaponFired;
                _weaponController.OnTracerRequested -= HandleTracerRequested;
                _weaponController.OnImpactRequested -= HandleImpactRequested;
            }
        }

        public void SetMuzzlePoint(Transform muzzle)
        {
            _muzzlePoint = muzzle;
        }

        public void TriggerMuzzleFlash()
        {
            HandleWeaponFired();
        }

        public void TriggerTracer(Vector3 start, Vector3 end)
        {
            HandleTracerRequested(start, end);
        }

        public void TriggerImpact(Vector3 pos, Vector3 normal, bool isFlesh)
        {
            HandleImpactRequested(pos, normal, isFlesh);
        }

        private void HandleWeaponFired()
        {
            // 1. Âm thanh nổ súng
            if (_weaponController != null && _weaponController.CurrentWeaponData != null)
            {
                var sound = _weaponController.CurrentWeaponData.FireSound;
                if (_audioSource != null && sound != null)
                {
                    _audioSource.PlayOneShot(sound);
                }
            }

            // 2. Muzzle Flash tại đầu nòng
            Vector3 muzzlePos = GetMuzzlePosition();
            StartCoroutine(MuzzleFlashRoutine(muzzlePos));
        }

        private void HandleTracerRequested(Vector3 start, Vector3 end)
        {
            StartCoroutine(TracerRoutine(start, end));
        }

        private void HandleImpactRequested(Vector3 pos, Vector3 normal, bool isFlesh)
        {
            StartCoroutine(ImpactEffectRoutine(pos, normal, isFlesh));
        }

        private Vector3 GetMuzzlePosition()
        {
            if (_muzzlePoint != null) return _muzzlePoint.position;
            if (_playerCamera != null) return _playerCamera.transform.position + _playerCamera.transform.forward * 0.5f;
            return transform.position;
        }

        private IEnumerator MuzzleFlashRoutine(Vector3 pos)
        {
            var flashObj = new GameObject("MuzzleFlash");
            flashObj.transform.position = pos;

            var light = flashObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = _flashRange;
            light.color = _flashColor;
            light.intensity = _flashIntensity;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = pos;
            quad.transform.rotation = _playerCamera != null ? _playerCamera.transform.rotation : Quaternion.identity;
            quad.transform.localScale = Vector3.one * 0.22f;
            var col = quad.GetComponent<Collider>();
            if (col != null) SafeDestroy(col);

            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.color = new Color(1f, 0.95f, 0.6f, 1f);
                rend.material = mat;
            }

            yield return new WaitForSeconds(_flashDuration);
            SafeDestroy(flashObj);
            SafeDestroy(quad);
        }

        private IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            var tracerObj = new GameObject("Tracer");
            var line = tracerObj.AddComponent<LineRenderer>();
            line.startWidth = _tracerStartWidth;
            line.endWidth = _tracerEndWidth;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = new Color(1f, 0.9f, 0.4f, 1f);
            line.material = mat;
            line.startColor = _tracerStartColor;
            line.endColor = _tracerEndColor;

            yield return new WaitForSeconds(_tracerDuration);
            SafeDestroy(tracerObj);
        }

        private IEnumerator ImpactEffectRoutine(Vector3 pos, Vector3 normal, bool isFlesh)
        {
            int particleCount = isFlesh ? 8 : 10;
            Color color = isFlesh ? new Color(0.85f, 0.12f, 0.12f, 1f) : new Color(1f, 0.82f, 0.3f, 1f);
            var parent = new GameObject(isFlesh ? "Impact_Blood" : "Impact_Sparks");
            parent.transform.position = pos;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = color;

            for (int i = 0; i < particleCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.SetParent(parent.transform);
                p.transform.position = pos;
                p.transform.localScale = Vector3.one * (isFlesh ? 0.05f : 0.04f);
                var col = p.GetComponent<Collider>();
                if (col != null) SafeDestroy(col);
                var r = p.GetComponent<Renderer>();
                if (r != null) r.material = mat;

                var rb = p.AddComponent<Rigidbody>();
                Vector3 randomDir = (normal + Random.insideUnitSphere * 0.75f).normalized;
                rb.linearVelocity = randomDir * Random.Range(2.5f, 5.5f);
                rb.useGravity = true;
            }

            yield return new WaitForSeconds(0.25f);
            SafeDestroy(parent);
        }

        private static void SafeDestroy(Object obj)
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
    }
}
