// Script: HellfireCombatJuiceSetup.cs
// Mục đích: Editor Utility thiết lập tự động Test Prefab và Test Scene cho Đợt 1 - Combat Foundation trên B:\Hellfire.
// Môi trường thực thi: Editor-only.

#if UNITY_EDITOR
using System.IO;
using Hellfire.Combat;
using Hellfire.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hellfire.Editor
{
    public static class HellfireCombatJuiceSetup
    {
        public const string TestPrefabPath = "Assets/_Project/Prefabs/Player_CombatJuice_Test.prefab";
        public const string OriginalPlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        public const string TestScenePath = "Assets/_Project/Scenes/TestCombatJuice.unity";
        public const string RifleModelPath = "Assets/SciFi_Space_Soldier/Resources/Weapons/Rifle/Meshes/Weapon_Rifle_1.FBX";

        [MenuItem("Hellfire/Sprint 1/Setup Test Prefab & Scene", false, 1)]
        public static void SetupAll()
        {
            var testPrefab = CreateOrUpdateTestPrefab();
            CreateTestScene(testPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CombatJuiceSetup] Đã thiết lập thành công Player_CombatJuice_Test.prefab và TestCombatJuice.unity!");
        }

        public static GameObject CreateOrUpdateTestPrefab()
        {
            var originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPlayerPrefabPath);
            if (originalPrefab == null)
            {
                Debug.LogError("[CombatJuiceSetup] Không tìm thấy Player.prefab tại " + OriginalPlayerPrefabPath);
                return null;
            }

            var instance = Object.Instantiate(originalPrefab);
            instance.name = "Player_CombatJuice_Test";

            var viewmodelTransform = instance.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
            if (viewmodelTransform == null)
            {
                Debug.LogError("[CombatJuiceSetup] Không tìm thấy CameraPivot/PlayerCamera/WeaponViewmodel trong Player prefab!");
                Object.DestroyImmediate(instance);
                return null;
            }

            // Gắn model súng Space Soldier Rifle vào Viewmodel nếu có
            Transform muzzlePoint = null;
            var rifleFbx = AssetDatabase.LoadAssetAtPath<GameObject>(RifleModelPath);
            if (rifleFbx != null)
            {
                // Tìm xem đã có RifleMesh chưa
                var existingRifle = viewmodelTransform.Find("RifleMesh");
                if (existingRifle == null)
                {
                    var rifleInstance = Object.Instantiate(rifleFbx, viewmodelTransform);
                    rifleInstance.name = "RifleMesh";
                    rifleInstance.transform.localPosition = new Vector3(0f, -0.05f, 0.1f);
                    rifleInstance.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    rifleInstance.transform.localScale = Vector3.one * 0.85f;

                    // Gán layer Viewmodel (Layer 7)
                    int vmLayer = LayerMask.NameToLayer("Viewmodel");
                    if (vmLayer >= 0)
                    {
                        foreach (var tr in rifleInstance.GetComponentsInChildren<Transform>(true))
                        {
                            tr.gameObject.layer = vmLayer;
                        }
                    }

                    // Tắt mesh khối hộp placeholder cũ nếu có
                    var oldGunMesh = viewmodelTransform.Find("GunMesh");
                    if (oldGunMesh != null)
                    {
                        var mr = oldGunMesh.GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = false;
                    }

                    // Tìm ShootFX làm nòng bắn
                    var shootFx = rifleInstance.transform.Find("ShootFX");
                    if (shootFx != null) muzzlePoint = shootFx;
                }
                else
                {
                    var shootFx = existingRifle.Find("ShootFX");
                    if (shootFx != null) muzzlePoint = shootFx;
                }
            }

            // Gắn 3 component độc lập của Đợt 1 + Coordinator vào WeaponViewmodel
            var ads = viewmodelTransform.GetComponent<WeaponAimDownSights>();
            if (ads == null) ads = viewmodelTransform.gameObject.AddComponent<WeaponAimDownSights>();

            var swayBob = viewmodelTransform.GetComponent<WeaponSwayBobbing>();
            if (swayBob == null) swayBob = viewmodelTransform.gameObject.AddComponent<WeaponSwayBobbing>();

            var recoil = viewmodelTransform.GetComponent<WeaponRecoil>();
            if (recoil == null) recoil = viewmodelTransform.gameObject.AddComponent<WeaponRecoil>();

            var coordinator = viewmodelTransform.GetComponent<WeaponMotionCoordinator>();
            if (coordinator == null) coordinator = viewmodelTransform.gameObject.AddComponent<WeaponMotionCoordinator>();

            // Auto-resolve references
            ads.AutoResolveReferences();
            swayBob.AutoResolveReferences();
            recoil.AutoResolveReferences();
            coordinator.AutoResolveReferences();

            // Cấu hình WeaponController với muzzle point
            var wc = instance.GetComponent<WeaponController>();
            if (wc != null && muzzlePoint != null)
            {
                var muzzleField = typeof(WeaponController).GetField("_weaponMuzzlePoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (muzzleField != null)
                {
                    muzzleField.SetValue(wc, muzzlePoint);
                }
            }

            // Lưu Prefab thử nghiệm riêng (không đè Player.prefab gốc)
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TestPrefabPath);
            Object.DestroyImmediate(instance);

            Debug.Log($"[CombatJuiceSetup] Đã lưu Test Prefab thành công tại {TestPrefabPath}");
            return savedPrefab;
        }

        public static void CreateTestScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TestCombatJuice";

            // 1. Ánh sáng Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Sàn đấu Arena
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50m x 50m
            var floorRend = floor.GetComponent<Renderer>();
            if (floorRend != null)
            {
                // Gán material tối màu để nổi bật vũ khí
                var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                floorMat.color = new Color(0.2f, 0.22f, 0.25f, 1f);
                floorRend.sharedMaterial = floorMat;
            }

            // 3. Bia tập bắn (Target Dummies) với Torso & Head Hitboxes
            for (int i = -3; i <= 3; i++)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"TargetDummy_{i}";
                dummy.transform.position = new Vector3(i * 3.5f, 1f, 12f);
                dummy.AddComponent<Health>();
                var torsoHitbox = dummy.AddComponent<HitboxIdentifier>();
                torsoHitbox.SetHitboxType(HitboxType.Torso);

                // Đầu (Head Hitbox)
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "HeadHitbox";
                head.transform.SetParent(dummy.transform);
                head.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                head.transform.localScale = Vector3.one * 0.6f;
                var headHitbox = head.AddComponent<HitboxIdentifier>();
                headHitbox.SetHitboxType(HitboxType.Head);

                // Màu sắc bia
                var dRend = dummy.GetComponent<Renderer>();
                if (dRend != null)
                {
                    var dummyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    dummyMat.color = new Color(0.8f, 0.3f, 0.2f, 1f);
                    dRend.sharedMaterial = dummyMat;
                }
            }

            // 4. Sinh Player thử nghiệm
            if (playerPrefab != null)
            {
                var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "Player_CombatJuice_Test";
                playerInstance.transform.position = new Vector3(0f, 0.1f, 0f);
                playerInstance.transform.rotation = Quaternion.identity;

                // Kích hoạt CharacterController & Camera để test trực tiếp trong Play Mode
                var cc = playerInstance.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var cam = playerInstance.GetComponentInChildren<Camera>(true);
                if (cam != null) cam.gameObject.SetActive(true);

                var al = playerInstance.GetComponentInChildren<AudioListener>(true);
                if (al != null) al.enabled = true;
            }

            // 5. Lưu Scene
            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[CombatJuiceSetup] Đã lưu Test Scene tại {TestScenePath}");
        }
    }
}
#endif
