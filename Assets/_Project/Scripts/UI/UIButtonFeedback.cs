// Script: UIButtonFeedback.cs
// Mục đích: Hiệu ứng phóng to/thu nhỏ khi hover/click nút và phát âm thanh click 2D (Mục 16.5 đặc tả kỹ thuật).
// Môi trường thực thi: Client-only.

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hellfire.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animation Settings (Mục 16.5)")]
        [SerializeField] private float _hoverScale = 1.05f;
        [SerializeField] private float _hoverDuration = 0.1f;
        [SerializeField] private float _pressScale = 0.95f;
        [SerializeField] private float _pressDuration = 0.05f;

        [Header("Audio")]
        [SerializeField] private AudioClip _clickSound;

        private Button _button;
        private Vector3 _originalScale = Vector3.one;
        private Coroutine _scaleCoroutine;
        private bool _isHovered;
        private AudioSource _audioSource;

        private static AudioClip _cachedProceduralClickClip;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // Âm thanh 2D (Mục 16.5)
            }

            if (_clickSound == null)
            {
                _clickSound = GetOrCreateClickClip();
            }
        }

        private void OnEnable()
        {
            transform.localScale = _originalScale;
            _isHovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            _isHovered = true;
            AnimateScale(_hoverScale, _hoverDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            AnimateScale(1.0f, _hoverDuration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            AnimateScale(_pressScale, _pressDuration);
            PlayClickSound();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            float target = _isHovered ? _hoverScale : 1.0f;
            AnimateScale(target, _hoverDuration);
        }

        private void AnimateScale(float targetMultiplier, float duration)
        {
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
            }
            _scaleCoroutine = StartCoroutine(ScaleRoutine(_originalScale * targetMultiplier, duration));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale, float duration)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // Dùng Time.unscaledDeltaTime theo mục 16.5
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
                yield return null;
            }

            transform.localScale = targetScale;
            _scaleCoroutine = null;
        }

        private void PlayClickSound()
        {
            if (_audioSource != null && _clickSound != null)
            {
                _audioSource.PlayOneShot(_clickSound, 0.6f);
            }
        }

        /// <summary>
        /// Tạo procedural audio clip ngắn 20ms dạng click mềm mại nếu chưa có asset âm thanh.
        /// </summary>
        private static AudioClip GetOrCreateClickClip()
        {
            if (_cachedProceduralClickClip != null) return _cachedProceduralClickClip;

            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.03f); // 30ms
            float[] samples = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 120f);
                float freq = 800f * Mathf.Exp(-t * 60f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * decay;
            }

            _cachedProceduralClickClip = AudioClip.Create("UIButtonClick", lengthSamples, 1, sampleRate, false);
            _cachedProceduralClickClip.SetData(samples, 0);
            return _cachedProceduralClickClip;
        }
        /// <summary>
        /// Gắn UIButtonFeedback cho mọi Button bên dưới root nếu chưa có (Mục 16.5).
        /// </summary>
        public static void AttachAllInHierarchy(GameObject root)
        {
            if (root == null) return;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].GetComponent<UIButtonFeedback>() == null)
                {
                    buttons[i].gameObject.AddComponent<UIButtonFeedback>();
                }
            }
        }
    }
}
