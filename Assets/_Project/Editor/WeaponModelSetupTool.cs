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
    [InitializeOnLoad]
    public static class WeaponModelSetupTool
    {
        private const string WeaponsMatDir = "Assets/_Project/Materials/Weapons";
        private const string WeaponsPrefabDir = "Assets/_Project/Prefabs/Weapons";
        private const string WeaponsDataDir = "Assets/_Project/Data/Weapons";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string TestScenePath = "Assets/_Project/Scenes/TestShrine.unity";

        private const string PistolModelPath = "Assets/_Project/Art/Models/Pistol/source/MoltenPistol.obj";
        private const string PistolTexturePath = "Assets/_Project/Art/Models/Pistol/textures/gltf_embedded_0.jpeg";
        private const string PistolMatPath = "Assets/_Project/Materials/Weapons/M_Pistol_Molten.mat";

        private const string AutoRunSessionKey = "Hellfire_AutoPistolFix_Session_v3";

        static WeaponModelSetupTool()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(AutoRunSessionKey, false)) return;
            SessionState.SetBool(AutoRunSessionKey, true);

            Debug.Log("<color=#FF6A00>[WeaponModelSetupTool] Phát hiện domain reload: Tự động cấu hình và lắp đặt Molten Pistol mới...</color>");
            SetupMoltenPistolOnly();
        }

        [MenuItem("Hellfire/Combat/Setup Molten Pistol Model (Quick)", false, 0)]
        [MenuItem("Hellfire/Combat/Fix & Setup Molten Pistol (New Model)", false, -10)]
        public static void SetupMoltenPistolOnly()
        {
            EnsureDirectories();
            Debug.Log("<color=#FF6A00>================ BẮT ĐẦU CẤU HÌNH VÀ LẮP ĐẶT MOLTEN PISTOL ================</color>");

            // 1. Tạo & kiểm tra Material phát sáng dung nham chuẩn URP Lit
            var matPistol = CreateOrGetPistolMaterial(PistolMatPath, PistolTexturePath);

            // 2. Tạo Prefab Viewmodel & WorldModel từ file MoltenPistol.obj
            var prefabPistolVM = CreatePistolPrefab(matPistol);
            var prefabPistolWorld = CreatePistolWorldPrefab(matPistol);

            // 3. Gán Viewmodel & WorldModel Prefab vào Pistol_Data.asset
            AssignPrefabToWeaponData("Pistol_Data.asset", prefabPistolVM, prefabPistolWorld, new Vector3(0.24f, -0.20f, 0.45f), Vector3.zero);

            // 4. Cập nhật Player.prefab với Pistol làm súng khởi đầu
            UpdatePlayerPrefab(isPistolDefault: true);

            // 5. Cập nhật Scene Test (TestShrine.unity) với Pistol được gắn trực tiếp vào tay, dọn sạch gun hồng mồ côi
            UpdateTestScene(prefabPistolVM, "pistol");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string logMsg = $"[WeaponModelSetupTool] HOÀN TẤT LẮP ĐẶT MODEL MOLTEN PISTOL VÀO SCENE TEST LÚC {DateTime.Now}!";
            Debug.Log($"<color=#00FF88>================ {logMsg} ================</color>");

            try
            {
                File.WriteAllText("Logs/PistolSetupExecution.log", $"{logMsg}\nPrefabVM: {prefabPistolVM != null}\nPrefabWorld: {prefabPistolWorld != null}\nMaterial: {matPistol != null}");
            }
            catch { }
        }

        [MenuItem("Hellfire/Combat/Setup All Gun Models into Scene Test", false, 1)]
        public static void SetupAllWeapons()
        {
            EnsureDirectories();

            Debug.Log("<color=#00E5FF>================ BẮT ĐẦU CẤU HÌNH VÀ LẮP ĐẶT TOÀN BỘ MODEL SÚNG ================</color>");

            // 1. Tạo Material URP Lit có texture chuẩn cho từng vũ khí
            var matRifleM4 = CreateOrGetLitMaterial(
                $"{WeaponsMatDir}/M_Rifle_M4.mat",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat0_c.jpg",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat0_n.jpg",
                0.6f, 0.5f);

            var matRifleMag = CreateOrGetLitMaterial(
                $"{WeaponsMatDir}/M_Rifle_MAG.mat",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat1_c.jpg",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat1_n.jpg",
                0.7f, 0.4f);

            var matRifleStock = CreateOrGetLitMaterial(
                $"{WeaponsMatDir}/M_Rifle_Stock.mat",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat2_c.jpg",
                "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/mat2_n.jpg",
                0.2f, 0.3f);

            var matShotgun = CreateOrGetLitMaterial(
                $"{WeaponsMatDir}/M_Shotgun.mat",
                "Assets/_Project/Art/Models/Shotgun/textures/LowPolyForBake_4800_lambert1_BaseColor.png",
                "Assets/_Project/Art/Models/Shotgun/textures/LowPolyForBake_4800_lambert1_Normal.png",
                0.5f, 0.6f);

            var matSmg = CreateOrGetLitMaterial(
                $"{WeaponsMatDir}/M_SMG.mat",
                "Assets/_Project/Art/Models/SMG/textures/P9_9smg1_BaseColor.png",
                "Assets/_Project/Art/Models/SMG/textures/P9_9smg1_Normal.png",
                0.5f, 0.5f);

            var matPistol = CreateOrGetPistolMaterial(PistolMatPath, PistolTexturePath);

            // 2. Tạo Prefab Viewmodel & WorldModel cho 4 loại súng
            var prefabRifle = CreateRiflePrefab(matRifleM4, matRifleMag, matRifleStock);
            var prefabRifleWorld = CreateRifleWorldPrefab(matRifleM4, matRifleMag, matRifleStock);

            var prefabShotgun = CreateShotgunPrefab(matShotgun);
            var prefabShotgunWorld = CreateShotgunWorldPrefab(matShotgun);

            var prefabSmg = CreateSmgPrefab(matSmg);
            var prefabSmgWorld = CreateSmgWorldPrefab(matSmg);

            var prefabPistolVM = CreatePistolPrefab(matPistol);
            var prefabPistolWorld = CreatePistolWorldPrefab(matPistol);

            // 3. Gán Viewmodel Prefab vào các ScriptableObject WeaponData
            AssignPrefabToWeaponData("Pistol_Data.asset", prefabPistolVM, prefabPistolWorld, new Vector3(0.24f, -0.20f, 0.45f), Vector3.zero);
            AssignPrefabToWeaponData("Shotgun_Data.asset", prefabShotgun, prefabShotgunWorld, new Vector3(0f, -0.05f, 0.15f), Vector3.zero);
            AssignPrefabToWeaponData("Rifle_Data.asset", prefabRifle, prefabRifleWorld, new Vector3(0f, -0.05f, 0.15f), Vector3.zero);
            AssignPrefabToWeaponData("SMG_Data.asset", prefabSmg, prefabSmgWorld, new Vector3(0f, -0.05f, 0.1f), Vector3.zero);

            // 4. Cập nhật Player.prefab
            UpdatePlayerPrefab(isPistolDefault: true);

            // 5. Cập nhật Scene Test (TestShrine.unity)
            UpdateTestScene(prefabPistolVM, "pistol");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FF88>================ HOÀN TẤT LẮP ĐẶT TOÀN BỘ MODEL SÚNG VÀO SCENE TEST! ================</color>");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            if (!AssetDatabase.IsValidFolder(WeaponsMatDir))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Weapons");
            if (!AssetDatabase.IsValidFolder(WeaponsPrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Weapons");
        }

        private static Material CreateOrGetLitMaterial(string matPath, string baseColorPath, string normalPath, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (!string.IsNullOrEmpty(baseColorPath))
            {
                var texBase = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
                if (texBase != null)
                {
                    mat.SetTexture("_BaseMap", texBase);
                    mat.SetTexture("_MainTex", texBase);
                }
            }

            if (!string.IsNullOrEmpty(normalPath))
            {
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }

                var texNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (texNormal != null)
                {
                    mat.SetTexture("_BumpMap", texNormal);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateRiflePrefab(Material matM4, Material matMag, Material matStock)
        {
            string fbxPath = "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/M4A1 Rifle.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[WeaponModelSetupTool] Không tìm thấy file FBX Rifle tại {fbxPath}");
                return null;
            }

            GameObject root = new GameObject("ViewModel_Rifle");
            GameObject model = Object.Instantiate(fbxAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 3.8f; // Chiều dài chuẩn ~0.70m

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string rName = r.gameObject.name.ToLower();
                var currentMats = r.sharedMaterials;
                for (int i = 0; i < currentMats.Length; i++)
                {
                    string mName = currentMats[i] != null ? currentMats[i].name.ToLower() : "";
                    if (mName.Contains("mag") || rName.Contains("5"))
                    {
                        currentMats[i] = matMag;
                    }
                    else if (mName.Contains("stock") || rName.Contains("mesh0") && (rName.EndsWith("0") || rName.EndsWith("9")))
                    {
                        currentMats[i] = matStock;
                    }
                    else
                    {
                        currentMats[i] = matM4;
                    }
                }
                r.sharedMaterials = currentMats;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.045f, 0.72f);

            CleanupAndSetLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/ViewModel_Rifle.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateShotgunPrefab(Material matShotgun)
        {
            string fbxPath = "Assets/_Project/Art/Models/Shotgun/source/LowPolyForBake_4800.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[WeaponModelSetupTool] Không tìm thấy file FBX Shotgun tại {fbxPath}");
                return null;
            }

            GameObject root = new GameObject("ViewModel_Shotgun");
            GameObject model = Object.Instantiate(fbxAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 2.8f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matShotgun;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.04f, 0.78f);

            CleanupAndSetLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/ViewModel_Shotgun.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSmgPrefab(Material matSmg)
        {
            string objPath = "Assets/_Project/Art/Models/SMG/source/model/P90.obj";
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (objAsset == null)
            {
                Debug.LogError($"[WeaponModelSetupTool] Không tìm thấy file OBJ SMG tại {objPath}");
                return null;
            }

            GameObject root = new GameObject("ViewModel_SMG");
            GameObject model = Object.Instantiate(objAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localScale = Vector3.one * 0.003f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matSmg;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.04f, 0.35f);

            CleanupAndSetLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/ViewModel_SMG.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateRifleWorldPrefab(Material matM4, Material matMag, Material matStock)
        {
            string fbxPath = "Assets/_Project/Art/Models/Rifle/source/M4A1 Rifle/M4A1 Rifle.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null) return null;

            GameObject root = new GameObject("WorldModel_Rifle");
            GameObject model = Object.Instantiate(fbxAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 3.0f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                string rName = r.gameObject.name.ToLower();
                var currentMats = r.sharedMaterials;
                for (int i = 0; i < currentMats.Length; i++)
                {
                    string mName = currentMats[i] != null ? currentMats[i].name.ToLower() : "";
                    if (mName.Contains("mag") || rName.Contains("5"))
                    {
                        currentMats[i] = matMag;
                    }
                    else if (mName.Contains("stock") || rName.Contains("mesh0") && (rName.EndsWith("0") || rName.EndsWith("9")))
                    {
                        currentMats[i] = matStock;
                    }
                    else
                    {
                        currentMats[i] = matM4;
                    }
                }
                r.sharedMaterials = currentMats;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.57f);

            CleanupAndSetWorldLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/WorldModel_Rifle.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateShotgunWorldPrefab(Material matShotgun)
        {
            string fbxPath = "Assets/_Project/Art/Models/Shotgun/source/LowPolyForBake_4800.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null) return null;

            GameObject root = new GameObject("WorldModel_Shotgun");
            GameObject model = Object.Instantiate(fbxAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 2.2f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matShotgun;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.031f, 0.61f);

            CleanupAndSetWorldLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/WorldModel_Shotgun.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSmgWorldPrefab(Material matSmg)
        {
            string objPath = "Assets/_Project/Art/Models/SMG/source/model/P90.obj";
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (objAsset == null) return null;

            GameObject root = new GameObject("WorldModel_SMG");
            GameObject model = Object.Instantiate(objAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localScale = Vector3.one * 0.0024f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matSmg;
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.032f, 0.28f);

            CleanupAndSetWorldLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/WorldModel_SMG.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CleanupAndSetWorldLayer(GameObject root)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) Object.DestroyImmediate(c);
            var anims = root.GetComponentsInChildren<Animator>(true);
            foreach (var a in anims) Object.DestroyImmediate(a);
            SetLayerRecursively(root, 0);
        }

        private static Material CreateOrGetPistolMaterial(string matPath, string baseColorPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (!string.IsNullOrEmpty(baseColorPath))
            {
                var texBase = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
                if (texBase != null)
                {
                    mat.SetTexture("_BaseMap", texBase);
                    mat.SetTexture("_MainTex", texBase);

                    // Bật hiệu ứng nham thạch nóng sáng rực lửa (Emission)
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", texBase);
                    mat.SetColor("_EmissionColor", new Color(2.0f, 0.75f, 0.2f) * 1.5f);
                }
            }

            mat.SetFloat("_Metallic", 0.65f);
            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreatePistolPrefab(Material matPistol)
        {
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PistolModelPath);
            if (objAsset == null)
            {
                Debug.LogWarning($"[WeaponModelSetupTool] Không tìm thấy file OBJ Pistol tại {PistolModelPath}, fallback sang procedural.");
                var pistolData = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsDataDir}/Pistol_Data.asset");
                GameObject rootProcedural = ProceduralWeaponModelBuilder.BuildProceduralGun(pistolData, false);
                rootProcedural.name = "ViewModel_Pistol";
                CleanupAndSetLayer(rootProcedural);
                string fbPath = $"{WeaponsPrefabDir}/ViewModel_Pistol.prefab";
                var fbPrefab = PrefabUtility.SaveAsPrefabAsset(rootProcedural, fbPath);
                Object.DestroyImmediate(rootProcedural);
                return fbPrefab;
            }

            GameObject root = new GameObject("ViewModel_Pistol");
            GameObject model = Object.Instantiate(objAsset, root.transform);
            model.name = "Model_Mesh";

            // Căn chỉnh trục súng ngắn Molten Pistol:
            // Đỉnh slide: +Y, Nòng súng: -X, Tay cầm: +X
            // Xoay quanh trục Y +90 độ để đưa nòng súng về phía trước (+Z forward)
            // Scale: 0.11f để súng có chiều dài thực tế ~22cm vừa vặn góc nhìn FPS
            model.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localScale = Vector3.one * 0.11f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (matPistol != null)
                {
                    var mats = new Material[r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1];
                    for (int m = 0; m < mats.Length; m++) mats[m] = matPistol;
                    r.sharedMaterials = mats;
                }
            }

            // Socket nòng súng Muzzle cho tia lửa và tia đạn
            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.17f);

            CleanupAndSetLayer(root);

            string prefabPath = $"{WeaponsPrefabDir}/ViewModel_Pistol.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[WeaponModelSetupTool] Đã tạo Prefab Molten Pistol chuẩn URP: {prefabPath}");
            return prefab;
        }

        private static GameObject CreatePistolWorldPrefab(Material matPistol)
        {
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PistolModelPath);
            if (objAsset == null) return null;

            GameObject root = new GameObject("WorldModel_Pistol");
            GameObject model = Object.Instantiate(objAsset, root.transform);
            model.name = "Model_Mesh";
            model.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            model.transform.localScale = Vector3.one * 0.085f;

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (matPistol != null)
                {
                    var mats = new Material[r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1];
                    for (int m = 0; m < mats.Length; m++) mats[m] = matPistol;
                    r.sharedMaterials = mats;
                }
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.17f) * 0.8f;

            // Xóa Collider & Animator
            var cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) Object.DestroyImmediate(c);
            var animators = root.GetComponentsInChildren<Animator>(true);
            foreach (var a in animators) Object.DestroyImmediate(a);

            // WorldModel dùng default layer (Layer 0)
            SetLayerRecursively(root, 0);

            string prefabPath = $"{WeaponsPrefabDir}/WorldModel_Pistol.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[WeaponModelSetupTool] Đã tạo Prefab WorldModel Pistol: {prefabPath}");
            return prefab;
        }

        private static void CleanupAndSetLayer(GameObject root)
        {
            // Xóa toàn bộ Collider & Animator tránh va chạm người chơi
            var cols = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                Object.DestroyImmediate(c);
            }
            var animators = root.GetComponentsInChildren<Animator>(true);
            foreach (var a in animators)
            {
                Object.DestroyImmediate(a);
            }

            // Set Layer 7 (Viewmodel)
            int vmLayer = LayerMask.NameToLayer("Viewmodel");
            if (vmLayer < 0) vmLayer = 7;
            SetLayerRecursively(root, vmLayer);
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform t in obj.transform)
            {
                if (t != null) SetLayerRecursively(t.gameObject, layer);
            }
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

            Debug.Log($"[WeaponModelSetupTool] Đã gán Prefabs cho WeaponData '{data.WeaponName}'");
        }

        private static void UpdatePlayerPrefab(bool isPistolDefault = true)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) return;

            var instance = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
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

            // 1. Dọn dẹp bất kỳ khối placeholder/viewmodel nào đang nằm mồ côi ngoài root scene (khối hồng trong scene view)
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
                    // Ẩn GunMesh placeholder cube cũ
                    var gunMesh = viewmodelHolder.Find("GunMesh");
                    if (gunMesh != null)
                    {
                        gunMesh.gameObject.SetActive(false);
                    }

                    // Xóa các model visual cũ nếu có trong WeaponViewmodel
                    for (int i = viewmodelHolder.childCount - 1; i >= 0; i--)
                    {
                        var child = viewmodelHolder.GetChild(i);
                        if (child != null && child.name != "GunMesh")
                        {
                            Object.DestroyImmediate(child.gameObject);
                        }
                    }

                    // Gắn sẵn model súng Pistol mới vào hierarchy trong Scene view để người dùng nhìn thấy ngay
                    if (defaultGunPrefab != null)
                    {
                        var mountedGun = PrefabUtility.InstantiatePrefab(defaultGunPrefab, viewmodelHolder) as GameObject;
                        mountedGun.name = $"[Model]_{gunId}";
                        mountedGun.transform.localPosition = gunId == "pistol" ? new Vector3(0f, 0f, 0.12f) : new Vector3(0f, -0.05f, 0.15f);
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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[WeaponModelSetupTool] Đã lưu cập nhật Scene Test: {TestScenePath}");
        }
    }
}
#endif
