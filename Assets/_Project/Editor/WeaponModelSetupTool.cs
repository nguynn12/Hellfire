#if UNITY_EDITOR
using System;
using System.IO;
using Hellfire.Combat;
using Hellfire.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hellfire.Editor
{
    public static class WeaponModelSetupTool
    {
        private const string ViewModelDir = "Assets/_Project/Prefabs/Weapons/ViewModel";
        private const string WorldModelDir = "Assets/_Project/Prefabs/Weapons/WorldModel";
        private const string WeaponsDataDir = "Assets/_Project/Data/Weapons";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string TestScenePath = "Assets/_Project/Scenes/TestShrine.unity";

        [MenuItem("Hellfire/Combat/Link Weapons from ViewModel & WorldModel Folders", false, 0)]
        public static void LinkAllWeaponsFromFolders()
        {
            Debug.Log("<color=#00E5FF>================ LIÊN KẾT TÀI NGUYÊN VŨ KHÍ TỪ VIEWMODEL & WORLDMODEL ================</color>");

            // 1. Tải các Prefabs CÓ SẴN từ 2 thư mục ViewModel và WorldModel (TUYỆT ĐỐI KHÔNG TỰ TẠO)
            var vmPistol = AssetDatabase.LoadAssetAtPath<GameObject>($"{ViewModelDir}/ViewModel_Pistol.prefab");
            var worldPistol = AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldModelDir}/WorldModel_Pistol.prefab");

            var vmRifle = AssetDatabase.LoadAssetAtPath<GameObject>($"{ViewModelDir}/ViewModel_Rifle.prefab");
            var worldRifle = AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldModelDir}/WorldModel_Rifle.prefab");

            var vmShotgun = AssetDatabase.LoadAssetAtPath<GameObject>($"{ViewModelDir}/ViewModel_Shotgun.prefab");
            var worldShotgun = AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldModelDir}/WorldModel_Shotgun.prefab");

            var vmSmg = AssetDatabase.LoadAssetAtPath<GameObject>($"{ViewModelDir}/ViewModel_SMG.prefab");
            var worldSmg = AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldModelDir}/WorldModel_SMG.prefab");

            if (vmPistol == null || worldPistol == null)
            {
                Debug.LogError($"[WeaponModelSetupTool] Không tìm thấy Prefab Pistol trong {ViewModelDir} hoặc {WorldModelDir}!");
                return;
            }

            // 2. Gán Prefab vào 4 ScriptableObject WeaponData
            AssignPrefabToWeaponData("Pistol_Data.asset", vmPistol, worldPistol, new Vector3(0.24f, -0.20f, 0.45f), Vector3.zero);
            AssignPrefabToWeaponData("Rifle_Data.asset", vmRifle, worldRifle, new Vector3(0f, -0.05f, 0.15f), Vector3.zero);
            AssignPrefabToWeaponData("Shotgun_Data.asset", vmShotgun, worldShotgun, new Vector3(0f, -0.05f, 0.15f), Vector3.zero);
            AssignPrefabToWeaponData("SMG_Data.asset", vmSmg, worldSmg, new Vector3(0f, -0.05f, 0.10f), Vector3.zero);

            // 3. Cập nhật Player.prefab
            UpdatePlayerPrefab(isPistolDefault: true);

            // 4. Cập nhật Scene Test (TestShrine.unity)
            UpdateTestScene(vmPistol, "pistol");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FF88>================ HOÀN TẤT LIÊN KẾT TÀI NGUYÊN VŨ KHÍ TỪ 2 THƯ MỤC! ================</color>");
        }

        private static void AssignPrefabToWeaponData(string assetFileName, GameObject vmPrefab, GameObject worldPrefab, Vector3 offset, Vector3 rotation)
        {
            string path = $"{WeaponsDataDir}/{assetFileName}";
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[WeaponModelSetupTool] Không tìm thấy WeaponData: {path}");
                return;
            }

            var so = new SerializedObject(data);
            if (vmPrefab != null) so.FindProperty("_viewmodelPrefab").objectReferenceValue = vmPrefab;
            if (worldPrefab != null) so.FindProperty("_worldModelPrefab").objectReferenceValue = worldPrefab;
            so.FindProperty("_viewmodelOffset").vector3Value = offset;
            so.FindProperty("_viewmodelRotation").vector3Value = rotation;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);

            Debug.Log($"[WeaponModelSetupTool] Đã liên kết Prefabs cho '{data.WeaponName}' (VM: {vmPrefab?.name}, World: {worldPrefab?.name})");
        }

        private static void UpdatePlayerPrefab(bool isPistolDefault = true)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) return;

            var instance = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (instance == null) return;

            var weaponCtrl = instance.GetComponent<WeaponController>();
            if (weaponCtrl != null)
            {
                var so = new SerializedObject(weaponCtrl);
                var viewmodel = instance.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
                if (viewmodel != null)
                {
                    viewmodel.localPosition = Vector3.zero;
                    viewmodel.localRotation = Quaternion.identity;
                    viewmodel.localScale = Vector3.one;
                    so.FindProperty("_weaponHolder").objectReferenceValue = viewmodel;
                    var gunMesh = viewmodel.Find("GunMesh");
                    if (gunMesh != null)
                    {
                        gunMesh.gameObject.SetActive(false);
                    }
                }

                var wPistol = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/Pistol_Data.asset");
                var wShotgun = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/Shotgun_Data.asset");
                var wRifle = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/Rifle_Data.asset");
                var wSmg = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/SMG_Data.asset");

                var listProp = so.FindProperty("_availableWeapons");
                listProp.arraySize = 4;
                listProp.GetArrayElementAtIndex(0).objectReferenceValue = wPistol;
                listProp.GetArrayElementAtIndex(1).objectReferenceValue = wShotgun;
                listProp.GetArrayElementAtIndex(2).objectReferenceValue = wRifle;
                listProp.GetArrayElementAtIndex(3).objectReferenceValue = wSmg;

                so.FindProperty("_currentWeapon").objectReferenceValue = isPistolDefault ? wPistol : wShotgun;
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log("[WeaponModelSetupTool] Đã cập nhật Player.prefab với WeaponHolder và Pistol mặc định");
        }

        private static void UpdateTestScene(GameObject defaultGunPrefab, string gunId = "pistol")
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var scene = activeScene;
            bool wasLoaded = activeScene.IsValid() && activeScene.path == TestScenePath;

            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid())
            {
                Debug.LogWarning($"[WeaponModelSetupTool] Không thể mở scene test tại: {TestScenePath}");
                return;
            }

            // Dọn dẹp bất kỳ khối placeholder/viewmodel nào đang nằm mồ côi ngoài root scene
            var rootObjects = scene.GetRootGameObjects();
            foreach (var rootObj in rootObjects)
            {
                if (rootObj == null) continue;
                string lowerName = rootObj.name.ToLower();
                bool isOrphanGun = lowerName.Contains("viewmodel") || 
                                   lowerName.Contains("worldmodel") || 
                                   lowerName.StartsWith("[model]") || 
                                   rootObj.name == "View" || 
                                   (lowerName.Contains("pistol") && rootObj.GetComponent<PlayerMovement>() == null);

                if (isOrphanGun)
                {
                    Debug.Log($"[WeaponModelSetupTool] Đã dọn dẹp đối tượng mồ côi ngoài Scene Root: {rootObj.name}");
                    Object.DestroyImmediate(rootObj);
                }
            }

            var player = Object.FindAnyObjectByType<PlayerMovement>();
            if (player != null)
            {
                var viewmodelHolder = player.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
                if (viewmodelHolder != null)
                {
                    var gunMesh = viewmodelHolder.Find("GunMesh");
                    if (gunMesh != null)
                    {
                        gunMesh.gameObject.SetActive(false);
                    }

                    for (int i = viewmodelHolder.childCount - 1; i >= 0; i--)
                    {
                        var child = viewmodelHolder.GetChild(i);
                        if (child != null && child.name != "GunMesh")
                        {
                            Object.DestroyImmediate(child.gameObject);
                        }
                    }

                    if (defaultGunPrefab != null)
                    {
                        var mountedGun = PrefabUtility.InstantiatePrefab(defaultGunPrefab, viewmodelHolder) as GameObject;
                        if (mountedGun != null)
                        {
                            mountedGun.name = $"[Model]_{gunId}";
                            mountedGun.transform.localPosition = gunId == "pistol" ? new Vector3(0.24f, -0.20f, 0.45f) : new Vector3(0.24f, -0.15f, 0.40f);
                            mountedGun.transform.localRotation = Quaternion.identity;

                            var weaponCtrl = player.GetComponent<WeaponController>();
                            if (weaponCtrl != null)
                            {
                                var so = new SerializedObject(weaponCtrl);
                                so.FindProperty("_weaponHolder").objectReferenceValue = viewmodelHolder;
                                var muzzle = mountedGun.transform.Find("Muzzle");
                                if (muzzle != null)
                                {
                                    so.FindProperty("_weaponMuzzlePoint").objectReferenceValue = muzzle;
                                }
                                var pistolData = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/Pistol_Data.asset");
                                if (pistolData != null)
                                {
                                    so.FindProperty("_currentWeapon").objectReferenceValue = pistolData;
                                }
                                so.ApplyModifiedProperties();
                            }
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[WeaponModelSetupTool] Đã lưu cập nhật Scene Test: {TestScenePath}");
        }
    }
}
#endif
