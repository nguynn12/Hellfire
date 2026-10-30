#if UNITY_EDITOR
using System;
using System.IO;
using Hellfire.Items;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hellfire.Editor
{
    [InitializeOnLoad]
    public static class WeaponWorkbenchSetupTool
    {
        private const string WorkbenchModelPath = "Assets/_Project/Art/Models/WeaponWorkbench/source/untitled.fbx";
        private const string PrefabSaveDir = "Assets/_Project/Prefabs/InteractObject";
        private const string PrefabSavePath = "Assets/_Project/Prefabs/InteractObject/WeaponWorkbench.prefab";
        private const string ResourcesPrefabDir = "Assets/Resources/InteractObject";
        private const string ResourcesPrefabPath = "Assets/Resources/InteractObject/WeaponWorkbench.prefab";

        private const string AutoRunSessionKey = "Hellfire_AutoWorkbench_Created_v1";

        static WeaponWorkbenchSetupTool()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(AutoRunSessionKey, false)) return;
            SessionState.SetBool(AutoRunSessionKey, true);

            Debug.Log("<color=#00E5FF>[WeaponWorkbenchSetupTool] Phát hiện domain reload: Tự động khởi tạo Prefab Weapon Workbench...</color>");
            CreateWeaponWorkbenchPrefab();
        }

        [MenuItem("Hellfire/Combat/Create Weapon Workbench Prefab", false, 10)]
        public static GameObject CreateWeaponWorkbenchPrefab()
        {
            Debug.Log("<color=#00E5FF>================ BẮT ĐẦU TẠO PREFAB WEAPON WORKBENCH ================</color>");

            EnsureDirectories();

            // 1. Tải 3D Model từ folder Art
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WorkbenchModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[WeaponWorkbenchSetupTool] Không tìm thấy file model 3D tại: {WorkbenchModelPath}");
                return null;
            }

            // 2. Tạo Root GameObject
            var rootObj = new GameObject("WeaponWorkbench");

            // 3. Khởi tạo Model 3D làm con của root
            var visualChild = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            if (visualChild == null)
            {
                visualChild = UnityEngine.Object.Instantiate(modelAsset);
            }
            visualChild.name = "Model_Workbench";
            visualChild.transform.SetParent(rootObj.transform, false);
            visualChild.transform.localPosition = Vector3.zero;
            visualChild.transform.localRotation = Quaternion.identity;
            visualChild.transform.localScale = Vector3.one;

            // 4. Tính toán Bounds tổng thể của model để tạo Collider chính xác
            Renderer[] renderers = visualChild.GetComponentsInChildren<Renderer>();
            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.one);
            if (renderers.Length > 0)
            {
                combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }
            }

            Vector3 centerInLocal = rootObj.transform.InverseTransformPoint(combinedBounds.center);
            Vector3 sizeInLocal = combinedBounds.size;

            // Đảm bảo kích thước tối thiểu nếu bounds quá nhỏ hoặc rỗng
            if (sizeInLocal.x < 0.5f) sizeInLocal.x = 2.0f;
            if (sizeInLocal.y < 0.5f) sizeInLocal.y = 1.2f;
            if (sizeInLocal.z < 0.5f) sizeInLocal.z = 1.5f;

            // 5. Thêm Solid Collider (để người chơi không đi xuyên qua bàn)
            var solidCollider = rootObj.AddComponent<BoxCollider>();
            solidCollider.center = centerInLocal;
            solidCollider.size = sizeInLocal;
            solidCollider.isTrigger = false;

            // 6. Thêm Trigger Collider (phạm vi tương tác phím [E])
            var triggerCollider = rootObj.AddComponent<BoxCollider>();
            triggerCollider.center = centerInLocal;
            triggerCollider.size = new Vector3(sizeInLocal.x + 3.0f, Mathf.Max(sizeInLocal.y + 1.5f, 3.0f), sizeInLocal.z + 3.0f);
            triggerCollider.isTrigger = true;

            // 7. Thêm Ánh sáng (Lighting) phát sáng cho bàn chế tạo vũ khí
            var lightObj = new GameObject("WorkbenchLight");
            lightObj.transform.SetParent(rootObj.transform, false);
            lightObj.transform.localPosition = centerInLocal + Vector3.up * (sizeInLocal.y * 0.5f + 0.8f);

            var lightComp = lightObj.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = new Color(0.15f, 1.0f, 0.35f, 1.0f); // Xanh lá cây (Green)
            lightComp.intensity = 2.5f;
            lightComp.range = 6.0f;
            lightComp.shadows = LightShadows.None;

            var urpLightData = lightObj.AddComponent<UniversalAdditionalLightData>();
            if (urpLightData != null)
            {
                urpLightData.usePipelineSettings = true;
            }

            // 8. Thêm NetworkObject (Netcode for GameObjects)
            var netObj = rootObj.AddComponent<NetworkObject>();
            netObj.SynchronizeTransform = true;

            // 9. Thêm InteractiveShrine cấu hình chuẩn loại WeaponModifierShrine
            var shrineComp = rootObj.AddComponent<InteractiveShrine>();
            SetPrivateField(shrineComp, "_shrineId", 2);
            SetPrivateField(shrineComp, "_shrineType", ShrineType.WeaponModifierShrine);
            SetPrivateField(shrineComp, "_shrineName", "Bàn Cường Hóa Vũ Khí");
            SetPrivateField(shrineComp, "_interactionDistance", 3.5f);
            SetPrivateField(shrineComp, "_shrineLight", lightComp);
            SetPrivateField(shrineComp, "_autoColorByType", true);
            SetPrivateField(shrineComp, "_activeGlowColor", new Color(0.15f, 1.0f, 0.35f, 1.0f));

            // 10. Lưu Prefab ra Assets/_Project/Prefabs/InteractObject
            bool success1;
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootObj, PrefabSavePath, out success1);

            // Lưu thêm bản sao ra Assets/Resources/InteractObject để hỗ trợ nạp động bằng Resources.Load nếu cần
            bool success2;
            PrefabUtility.SaveAsPrefabAsset(rootObj, ResourcesPrefabPath, out success2);

            // Dọn dẹp object tạm trên Scene
            UnityEngine.Object.DestroyImmediate(rootObj);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (success1 && savedPrefab != null)
            {
                Debug.Log($"<color=#00FF00>[WeaponWorkbenchSetupTool] ĐÃ TẠO THÀNH CÔNG PREFAB TẠI: {PrefabSavePath}</color>", savedPrefab);
            }
            else
            {
                Debug.LogError($"[WeaponWorkbenchSetupTool] Lỗi khi lưu Prefab tại {PrefabSavePath}");
            }

            return savedPrefab;
        }

        [MenuItem("Hellfire/Combat/Place Weapon Workbench in Scene", false, 11)]
        public static void PlaceWorkbenchInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath);
            if (prefab == null)
            {
                prefab = CreateWeaponWorkbenchPrefab();
            }

            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3(3f, 0f, 0f);
                Selection.activeGameObject = instance;
                Undo.RegisterCreatedObjectUndo(instance, "Place Weapon Workbench");
                Debug.Log($"<color=#00FF00>[WeaponWorkbenchSetupTool] Đã đặt Weapon Workbench vào Scene tại {instance.transform.position}</color>");
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabSaveDir))
            {
                Directory.CreateDirectory(PrefabSaveDir);
                AssetDatabase.Refresh();
            }

            if (!Directory.Exists(ResourcesPrefabDir))
            {
                Directory.CreateDirectory(ResourcesPrefabDir);
                AssetDatabase.Refresh();
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
#endif
