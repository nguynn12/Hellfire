// Script: HellfireCombatJuiceSetup.cs
// Mục đích: Editor Utility thiết lập tự động Test Prefab và Test Scene cho Đợt 1 - Combat Foundation.
// Môi trường thực thi: Editor-only.

#if UNITY_EDITOR
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
            GameObject instance;
            if (originalPrefab != null)
            {
                instance = Object.Instantiate(originalPrefab);
                instance.name = "Player_CombatJuice_Test";
            }
            else
            {
                instance = HellfirePhase1Setup.CreatePlayerPrefabInternal();
                instance.name = "Player_CombatJuice_Test";
            }

            var viewmodelTransform = instance.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
            if (viewmodelTransform == null)
            {
                Debug.LogError("[CombatJuiceSetup] Không tìm thấy CameraPivot/PlayerCamera/WeaponViewmodel trong Player prefab!");
                Object.DestroyImmediate(instance);
                return null;
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

            // Lưu Prefab thử nghiệm
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TestPrefabPath);
            Object.DestroyImmediate(instance);

            Debug.Log($"[CombatJuiceSetup] Đã lưu Test Prefab tại {TestPrefabPath}");
            return savedPrefab;
        }

        public static void CreateTestScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TestCombatJuice";

            // 1. Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Arena Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);

            // 3. Target Dummies (Cubes với Health & Hitbox để kiểm thử súng, sát thương, headshot, recoil)
            for (int i = -2; i <= 2; i++)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"TargetDummy_{i}";
                dummy.transform.position = new Vector3(i * 4f, 1f, 12f);
                dummy.AddComponent<Health>();
                var hitbox = dummy.AddComponent<HitboxIdentifier>();
                hitbox.SetHitboxType(HitboxType.Torso);

                // Head hitbox
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "HeadHitbox";
                head.transform.SetParent(dummy.transform);
                head.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                head.transform.localScale = Vector3.one * 0.6f;
                var headHitbox = head.AddComponent<HitboxIdentifier>();
                headHitbox.SetHitboxType(HitboxType.Head);
            }

            // 4. Spawn Player thử nghiệm
            if (playerPrefab != null)
            {
                var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "Player";
                playerInstance.transform.position = new Vector3(0f, 0.1f, 0f);

                // Kích hoạt Offline mode để có thể di chuyển và bắn ngay
                var pm = playerInstance.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    pm.IsOnlineMode = false;
                }
            }

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[CombatJuiceSetup] Đã lưu Test Scene tại {TestScenePath}");
        }
    }
}
#endif
