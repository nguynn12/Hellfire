using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hellfire.EditorTools
{
    [InitializeOnLoad]
    public static class HellfirePerformanceOptimizationTool
    {
        private const string AutoOptKey = "Hellfire_AutoPerformanceOpt_v1";

        static HellfirePerformanceOptimizationTool()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(AutoOptKey, false)) return;
            SessionState.SetBool(AutoOptKey, true);

            Debug.Log("<color=#00E5FF>[HellfirePerformanceOptimizationTool] Tự động kiểm tra và cấu hình cờ Static & tối ưu SRP Batcher...</color>");
            ConfigureSceneStaticFlags();
            OptimizePlayerCameraPlanes();
            OptimizeMaterialsForSRPBatcher();
        }

        [MenuItem("Hellfire/Optimization/Apply All Performance Optimizations (Zero-GC & Static Occlusion)", false, 20)]
        public static void ApplyAllPerformanceOptimizations()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            ConfigureSceneStaticFlags();
            OptimizePlayerCameraPlanes();
            OptimizeMaterialsForSRPBatcher();
            BakeOcclusionCulling();

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Hellfire] HOÀN TẤT TỐI ƯU HÓA HIỆU NĂNG: Zero-GC Raycast Buffer, SRP Batcher & Occlusion Culling!</color>");
        }

        [MenuItem("Hellfire/Optimization/Configure Static Flags on Environment", false, 21)]
        public static void ConfigureSceneStaticFlags()
        {
            var rootObjects = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Transform>();
            int staticCount = 0;

            var staticFlags = StaticEditorFlags.OccluderStatic |
                              StaticEditorFlags.OccludeeStatic |
                              StaticEditorFlags.BatchingStatic |
                              StaticEditorFlags.ReflectionProbeStatic;

            foreach (var t in rootObjects)
            {
                if (t == null) continue;
                string nameLower = t.gameObject.name.ToLower();

                // Chỉ gán cờ tĩnh cho địa hình, map, tường, sàn, tế đàn cố định
                if (nameLower.Contains("map") ||
                    nameLower.Contains("terrain") ||
                    nameLower.Contains("dungeon") ||
                    nameLower.Contains("wall") ||
                    nameLower.Contains("floor") ||
                    nameLower.Contains("pillar") ||
                    nameLower.Contains("altar") ||
                    nameLower.Contains("shrine") ||
                    nameLower.Contains("ground") ||
                    nameLower.Contains("environment"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, staticFlags);
                    staticCount++;
                }
            }

            Debug.Log($"[PerformanceOpt] Đã gán cờ Occluder/Occludee/Batching Static cho {staticCount} GameObject môi trường.");
        }

        [MenuItem("Hellfire/Optimization/Bake Occlusion Culling (Active Scene)", false, 22)]
        public static void BakeOcclusionCulling()
        {
            Debug.Log("[PerformanceOpt] Đang tiến hành nướng Occlusion Culling cho Scene hiện tại...");
            StaticOcclusionCulling.smallestOccluder = 4.0f;
            StaticOcclusionCulling.smallestHole = 0.5f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            StaticOcclusionCulling.Compute();
            Debug.Log("[PerformanceOpt] Đã hoàn tất nướng Occlusion Culling!");
        }

        [MenuItem("Hellfire/Optimization/Optimize Player Camera Near and Far Planes", false, 23)]
        public static void OptimizePlayerCameraPlanes()
        {
            string[] guids = AssetDatabase.FindAssets("Player t:Prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("_Project")) continue;

                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                if (prefab == null) continue;

                var cameras = prefab.GetComponentsInChildren<Camera>(true);
                bool modified = false;

                foreach (var cam in cameras)
                {
                    // Tối ưu tỉ lệ Near/Far Clip Plane để tăng độ chính xác của Z-Buffer, triệt tiêu Z-Fighting
                    if (cam.nearClipPlane < 0.1f)
                    {
                        cam.nearClipPlane = 0.1f;
                        modified = true;
                    }
                    if (cam.farClipPlane > 500f)
                    {
                        cam.farClipPlane = 500f;
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    Debug.Log($"[PerformanceOpt] Đã tối ưu NearPlane = 0.1m, FarPlane = 500m trong: {path}");
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [MenuItem("Hellfire/Optimization/Audit & Fix SRP Batcher Materials", false, 24)]
        public static void OptimizeMaterialsForSRPBatcher()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Materials" });
            int fixedCount = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // Bật GPU Instancing để SRP Batcher / GPU Instancing gộp lệnh vẽ
                if (!mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                }
            }

            Debug.Log($"[PerformanceOpt] Đã bật GPU Instancing cho {fixedCount} Materials để tối ưu SRP Batcher.");
        }
    }
}

