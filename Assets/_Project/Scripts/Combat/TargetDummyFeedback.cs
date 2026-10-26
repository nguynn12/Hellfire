using System.Collections;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class TargetDummyFeedback : MonoBehaviour
    {
        [SerializeField] private Renderer _torsoRenderer;
        [SerializeField] private Renderer _headRenderer;

        private Health _health;
        private OfflineDummyHealth _offlineHealth;
        private Color _torsoOrigColor = Color.white;
        private Color _headOrigColor = Color.white;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _offlineHealth = GetComponent<OfflineDummyHealth>();

            if (_torsoRenderer == null)
            {
                _torsoRenderer = GetComponent<Renderer>();
            }

            if (_headRenderer == null)
            {
                var head = transform.Find("HeadHitbox");
                if (head != null) _headRenderer = head.GetComponent<Renderer>();
            }

            if (_torsoRenderer != null && _torsoRenderer.material != null)
            {
                _torsoOrigColor = _torsoRenderer.material.color;
            }

            if (_headRenderer != null && _headRenderer.material != null)
            {
                _headOrigColor = _headRenderer.material.color;
            }
        }

        private void OnEnable()
        {
            if (_offlineHealth != null)
            {
                _offlineHealth.OnDamageTaken += HandleDamageTaken;
            }
            else if (_health != null)
            {
                _health.OnDamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (_offlineHealth != null)
            {
                _offlineHealth.OnDamageTaken -= HandleDamageTaken;
            }
            else if (_health != null)
            {
                _health.OnDamageTaken -= HandleDamageTaken;
            }
        }

        private void HandleDamageTaken(float damage, HitboxType hitboxType, ulong attackerClientId)
        {
            IDamageable dmg = _health;
            float currentHp = dmg != null ? dmg.CurrentHealthValue : 0f;
            float maxHp = dmg != null ? dmg.MaxHealthValue : 100f;

            Debug.Log($"[TargetDummy] {gameObject.name} trúng đạn vùng {hitboxType}! Sát thương: -{damage:F1} HP | Máu còn: {currentHp:F1}/{maxHp:F1}");

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(hitboxType == HitboxType.Head));
        }

        private IEnumerator FlashRoutine(bool isHeadshot)
        {
            Color flashColor = isHeadshot ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.2f, 0.2f);

            if (isHeadshot && _headRenderer != null)
            {
                _headRenderer.material.color = flashColor;
            }
            else if (_torsoRenderer != null)
            {
                _torsoRenderer.material.color = flashColor;
            }

            yield return new WaitForSeconds(0.12f);

            if (_headRenderer != null) _headRenderer.material.color = _headOrigColor;
            if (_torsoRenderer != null) _torsoRenderer.material.color = _torsoOrigColor;
        }
    }
}
