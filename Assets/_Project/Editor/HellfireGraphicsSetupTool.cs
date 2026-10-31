using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hellfire.EditorTools
{
    [InitializeOnLoad]
    public static class HellfireGraphicsSetupTool
    {
        private const string AutoFixKey = "Hellfire_AutoGraphicsFix_v1";

        static HellfireGraphicsSetupTool()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(AutoFixKey, false)) return;
            SessionState.SetBool(AutoFixKey, true);

            Debug.Log("<color=#00E5FF>[HellfireGraphicsSetupTool] Phát hiện domain reload: Tự động kiểm tra và áp dụng cấu hình đồ họa sắc nét...</color>");
            ApplyAllGraphicsFixes();
        }

        [MenuItem("Hellfire/Graphics/Apply All Crisp Visual Fixes (Fix Blurry Far View)", false, 10)]
        public static void ApplyAllGraphicsFixes()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            FixVolumeProfileDepthOfField();
            FixQualitySettingsAndShadows();
            FixURPAssetSettings();
            FixPlayerCameraSettings();

            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Hellfire] ĐÃ ÁP DỤNG THÀNH CÔNG TOÀN BỘ CẤU HÌNH SỬA MỜ & TỐI ƯU TẦM NHÌN XA!</color>");
        }

        public static void FixVolumeProfileDepthOfField()
        {
            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile != null)
                {
                    if (profile.TryGet<DepthOfField>(out var dof))
                    {
                        dof.active = false; // Tắt hoàn toàn DoF làm mờ tầm xa (>10m)
                        EditorUtility.SetDirty(profile);
                        Debug.Log($"[GraphicsFix] Đã tắt Depth of Field trong: {path}");
                    }
                }
            }
        }

        public static void FixQualitySettingsAndShadows()
        {
            // Ép Anisotropic Filtering mức tối đa để không nhòe sàn ở góc xiên
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // Nâng Shadow Distance từ 40m lên 120m để không mất chiều sâu ở xa
            QualitySettings.shadowDistance = 120.0f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;

            Debug.Log("[GraphicsFix] Đã bật Anisotropic Filtering: ForceEnable và Shadow Distance: 120m.");
        }

        public static void FixURPAssetSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (urpAsset != null)
                {
                    urpAsset.shadowDistance = 120.0f;
                    urpAsset.shadowCascadeCount = 4;
                    EditorUtility.SetDirty(urpAsset);
                    Debug.Log($"[GraphicsFix] Đã cập nhật shadowDistance = 120m trong URP Asset: {path}");
                }
            }
        }

        public static void FixPlayerCameraSettings()
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
                    // Nâng nearClipPlane để tăng gấp đôi độ chính xác Z-Buffer, triệt tiêu Z-Fighting
                    if (cam.nearClipPlane < 0.1f)
                    {
                        cam.nearClipPlane = 0.1f;
                        modified = true;
                    }

                    // Cấu hình URP Additional Camera Data: Khử răng cưa SMAA High
                    var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (camData != null)
                    {
                        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        camData.antialiasingQuality = AntialiasingQuality.High;
                        camData.renderPostProcessing = true;
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    Debug.Log($"[GraphicsFix] Đã cập nhật Camera SMAA High, PostProcessing & NearClipPlane trong: {path}");
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
