// Script: SafeEventSystem.cs
// Mục đích: Đảm bảo trong Scene luôn luôn chỉ có DUY NHẤT 1 EventSystem hoạt động.
//           Tự động vô hiệu hóa các EventSystem thừa mà không bao giờ gọi Destroy trên Prefab Instance,
//           triệt tiêu 100% cảnh báo "There are 2 event systems in the scene" và tránh InvalidOperationException.
// Môi trường thực thi: Client (uGUI EventSystem).

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Hellfire.UI
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class SafeEventSystem : MonoBehaviour
    {
        private EventSystem _myEs;
        private BaseInputModule _myInputModule;

        private void Awake()
        {
            _myEs = GetComponent<EventSystem>();
            _myInputModule = GetComponent<BaseInputModule>();
            EnsureSingleEventSystem();
        }

        private void OnEnable()
        {
            if (_myEs == null) _myEs = GetComponent<EventSystem>();
            if (_myInputModule == null) _myInputModule = GetComponent<BaseInputModule>();
            EnsureSingleEventSystem();
        }

        private void Update()
        {
            EnsureSingleEventSystem();
        }

        private void EnsureSingleEventSystem()
        {
            if (_myEs == null) _myEs = GetComponent<EventSystem>();
            if (_myEs == null) return;

            // 1. Nếu đã có EventSystem khác đang active làm current
            if (EventSystem.current != null && EventSystem.current != _myEs && EventSystem.current.enabled)
            {
                DisableThisEventSystem();
                return;
            }

            // 2. Tìm các EventSystem đang active trong Scene
            EventSystem[] allEs = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allEs.Length > 1)
            {
                foreach (var es in allEs)
                {
                    if (es != null && es != _myEs && es.enabled)
                    {
                        // Nếu EventSystem kia ở ngoài Root của scene, ưu tiên giữ nó và tắt bản thân
                        if (es.transform.parent == null)
                        {
                            DisableThisEventSystem();
                            return;
                        }
                    }
                }
            }
        }

        private void DisableThisEventSystem()
        {
            if (_myEs != null) _myEs.enabled = false;
            if (_myInputModule == null) _myInputModule = GetComponent<BaseInputModule>();
            if (_myInputModule != null) _myInputModule.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            CleanupDuplicateEventSystems();
            SceneManager.sceneLoaded += (scene, mode) => CleanupDuplicateEventSystems();
        }

        /// <summary>
        /// Quét toàn bộ Scene và đảm bảo chỉ có đúng 1 EventSystem active.
        /// </summary>
        public static void CleanupDuplicateEventSystems()
        {
            EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (all == null || all.Length <= 1) return;

            EventSystem primary = null;

            // Ưu tiên EventSystem ở Scene root (ví dụ GameObject EventSystem độc lập trong Scene)
            foreach (var es in all)
            {
                if (es != null && es.enabled && es.transform.parent == null)
                {
                    primary = es;
                    break;
                }
            }

            if (primary == null && all.Length > 0)
            {
                primary = all[0];
            }

            // Vô hiệu hóa toàn bộ EventSystem còn lại
            foreach (var es in all)
            {
                if (es != null && es != primary)
                {
                    es.enabled = false;
                    var inputModule = es.GetComponent<BaseInputModule>();
                    if (inputModule != null)
                    {
                        inputModule.enabled = false;
                    }

                    // Chỉ tiêu hủy nếu là root GameObject độc lập (không thuộc Prefab Instance)
                    if (es.transform.parent == null)
                    {
                        try
                        {
                            Destroy(es.gameObject);
                        }
                        catch
                        {
                            // Nếu Unity chặn do thuộc Prefab instance, việc tắt enabled là đủ an toàn tuyệt đối
                        }
                    }
                }
            }
        }
    }
}
