// Script: TimeDilationManager.cs
// Mục đích: Quản lý hiệu ứng làm chậm thời gian cục bộ (Hit-stop / Slow-Motion / Time Dilation).
//           Tạo cảm giác "Juice" cực đã khi kết liễu quái vật (Lethal Shot) mà không gây giật lag vật lý.
// Môi trường thực thi: Client-side / Offline.

using System.Collections;
using UnityEngine;

namespace Hellfire.Core
{
    [DisallowMultipleComponent]
    public class TimeDilationManager : MonoBehaviour
    {
        public static TimeDilationManager Instance { get; private set; }

        private const float DefaultFixedDelta = 0.02f;
        private Coroutine _slowMoCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static TimeDilationManager GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("[TimeDilationManager]");
                Instance = go.AddComponent<TimeDilationManager>();
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        /// <summary>
        /// Kích hoạt hiệu ứng Slow-Motion khi có đòn bắn kết liễu hoặc sự kiện quan trọng.
        /// </summary>
        /// <param name="scale">Tỉ lệ tốc độ thời gian (0.1f - 0.5f, mặc định 0.25f)</param>
        /// <param name="duration">Thời gian duy trì tính theo giây thực tế (mặc định 0.30s)</param>
        public static void TriggerSlowMo(float scale = 0.25f, float duration = 0.30f)
        {
            var manager = GetOrCreate();
            if (manager != null)
            {
                manager.StartSlowMoInternal(scale, duration);
            }
        }

        private void StartSlowMoInternal(float scale, float duration)
        {
            if (_slowMoCoroutine != null)
            {
                StopCoroutine(_slowMoCoroutine);
            }
            _slowMoCoroutine = StartCoroutine(SlowMoRoutine(scale, duration));
        }

        private IEnumerator SlowMoRoutine(float targetScale, float duration)
        {
            targetScale = Mathf.Clamp(targetScale, 0.05f, 0.9f);
            duration = Mathf.Max(0.05f, duration);

            // 1. Áp dụng ngay mức timeScale chậm
            Time.timeScale = targetScale;
            Time.fixedDeltaTime = DefaultFixedDelta * targetScale;

            // 2. Giữ nguyên trạng thái slow-motion trong khoảng 60% thời lượng bằng unscaled time
            float holdTime = duration * 0.6f;
            float elapsed = 0f;
            while (elapsed < holdTime)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 3. Nội suy mượt mà hồi phục về tốc độ bình thường (1.0f) trong 40% thời lượng còn lại
            float recoveryTime = duration * 0.4f;
            elapsed = 0f;
            while (elapsed < recoveryTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / recoveryTime);
                // Smooth step
                float smoothT = t * t * (3f - 2f * t);

                float currentScale = Mathf.Lerp(targetScale, 1.0f, smoothT);
                Time.timeScale = currentScale;
                Time.fixedDeltaTime = DefaultFixedDelta * currentScale;

                yield return null;
            }

            // 4. Khôi phục hoàn toàn
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = DefaultFixedDelta;
            _slowMoCoroutine = null;
        }

        private void OnDisable()
        {
            ResetTimeScale();
        }

        private void OnDestroy()
        {
            ResetTimeScale();
        }

        private void OnApplicationQuit()
        {
            ResetTimeScale();
        }

        private static void ResetTimeScale()
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = DefaultFixedDelta;
        }
    }
}
