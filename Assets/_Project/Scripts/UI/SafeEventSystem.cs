using UnityEngine;
using UnityEngine.EventSystems;

namespace Hellfire.UI
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class SafeEventSystem : MonoBehaviour
    {
        private void Awake()
        {
            EventSystem current = EventSystem.current;
            EventSystem myEs = GetComponent<EventSystem>();

            // Nếu đã có một EventSystem khác đang active trong Scene
            if (current != null && current != myEs)
            {
                Debug.Log($"[SafeEventSystem] Phát hiện EventSystem trùng lặp từ GameObject '{gameObject.name}'. Đang tự hủy để tránh xung đột chuột.");
                Destroy(gameObject);
            }
        }
    }
}

