// Script: RuntimeNavMeshBaker.cs
// Mục đích: Nướng (bake) NavMesh tại runtime sau khi dựng xong hình học bản đồ (Mục 4.2 đặc tả kỹ thuật).
// Môi trường thực thi: Cả hai (Mỗi máy tự bake cục bộ của riêng mình, không truyền dữ liệu NavMesh qua mạng).

using System;
using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Dungeon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        [Header("NavMesh Configuration")]
        [SerializeField] private NavMeshSurface _navMeshSurface;

        public bool IsBaking { get; private set; }
        public bool IsBakeComplete { get; private set; }

        public event Action OnBakeCompleted;

        private void Reset()
        {
            _navMeshSurface = GetComponent<NavMeshSurface>();
        }

        private void Awake()
        {
            if (_navMeshSurface == null)
            {
                _navMeshSurface = GetComponent<NavMeshSurface>();
            }
        }

        /// <summary>
        /// Bake NavMesh đồng bộ hoặc chờ 1 frame để đảm bảo colliders đã cập nhật trong Physics scene.
        /// </summary>
        public void BakeNavMesh(Action onComplete = null)
        {
            StartCoroutine(BakeRoutine(onComplete));
        }

        private IEnumerator BakeRoutine(Action onComplete)
        {
            IsBaking = true;
            IsBakeComplete = false;

            // Chờ cuối frame để physics engine cập nhật toàn bộ collider mới vừa Instantiate
            yield return new WaitForEndOfFrame();

            if (_navMeshSurface != null)
            {
                // Mục 17.0: bake only generated static geometry, never player/viewmodel meshes.
                var root = GetComponent<DungeonGenerator>()?.GeometryRoot;
                if (root == null) throw new InvalidOperationException("DungeonGeometryRoot missing before bake.");
                _navMeshSurface.enabled = false;
                var generatedSurface = root.GetComponent<NavMeshSurface>() ?? root.gameObject.AddComponent<NavMeshSurface>();
                generatedSurface.agentTypeID = _navMeshSurface.agentTypeID;
                generatedSurface.collectObjects = CollectObjects.Children;
                generatedSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                generatedSurface.BuildNavMesh();
            }

            IsBaking = false;
            IsBakeComplete = true;

            onComplete?.Invoke();
            OnBakeCompleted?.Invoke();
        }
    }
}
