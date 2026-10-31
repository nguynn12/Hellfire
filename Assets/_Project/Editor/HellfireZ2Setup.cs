// Script: HellfireZ2Setup.cs
// Mục đích: Thiết lập tự động Prefab và Scene kiểm thử độc lập cho Zombie AI Đợt Z2 (Hearing & Investigate).
// Môi trường: Unity Editor.

#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class HellfireZ2Setup
    {
        public const string ArcherPrefabPath = "Assets/_Project/Prefabs/Enemy_Archer_Test.prefab";
        public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player_CombatJuice_Test.prefab";
        public const string HearingTestScenePath = "Assets/_Project/Scenes/TestZombieHearing.unity";

        [MenuItem("Hellfire/Sprint Z2/Setup Z2 Hearing Prefabs & Scene", false, 1)]
        public static void SetupAll()
        {
            if (EditorApplication.isPlaying) return;

            UpdateArcherPrefabWithHearing();
            UpdatePlayerPrefabWithNoiseEmitter();
            CreateHearingTestScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Z2Setup] Thiết lập thành công Prefabs và Scene TestZombieHearing.unity cho Đợt Z2!");
        }

        public static GameObject UpdateArcherPrefabWithHearing()
        {
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
            if (archerPrefab == null)
            {
                // Nếu chưa có thì tạo mới từ Z1 setup
                archerPrefab = HellfireArcherSetup.CreateOrUpdateArcherTestPrefab();
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(ArcherPrefabPath);
            try
            {
                // 1. Thêm ZombieHearing nếu chưa có
                var hearing = contents.GetComponent<ZombieHearing>();
                if (hearing == null) hearing = contents.AddComponent<ZombieHearing>();
                hearing.HearingRadius = 20f;
                hearing.EnableHearing = true;

                // 2. Thêm ZombieInvestigation nếu chưa có
                var investigation = contents.GetComponent<ZombieInvestigation>();
                if (investigation == null) investigation = contents.AddComponent<ZombieInvestigation>();
                investigation.SearchDuration = 4.0f;

                // 3. Liên kết vào RangedZombieAI
                var ai = contents.GetComponent<RangedZombieAI>();
                if (ai != null)
                {
                    var soAI = new SerializedObject(ai);
                    var invProp = soAI.FindProperty("_investigation");
                    if (invProp != null) invProp.objectReferenceValue = investigation;
                    var hearProp = soAI.FindProperty("_hearing");
                    if (hearProp != null) hearProp.objectReferenceValue = hearing;
                    soAI.ApplyModifiedProperties();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, ArcherPrefabPath);
                Debug.Log($"[Z2Setup] Đã cập nhật Enemy_Archer_Test.prefab với ZombieHearing (20m) & ZombieInvestigation (4s)!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
        }

        public static GameObject UpdatePlayerPrefabWithNoiseEmitter()
        {
            if (!File.Exists(PlayerPrefabPath))
            {
                Debug.LogError("[Z2Setup] Không tìm thấy Player prefab tại " + PlayerPrefabPath);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var emitter = contents.GetComponent<GameplayNoiseEmitter>();
                if (emitter == null) emitter = contents.AddComponent<GameplayNoiseEmitter>();
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log($"[Z2Setup] Đã gắn GameplayNoiseEmitter vào {PlayerPrefabPath}!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        }

        public static void CreateHearingTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TestZombieHearing";

            // 1. Ánh sáng Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Sàn Arena 50m x 50m
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            var floorRend = floor.GetComponent<Renderer>();
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Arena_Floor.mat");
            if (floorRend != null && floorMat != null) floorRend.sharedMaterial = floorMat;

            // Bake NavMesh cho sàn
            var navSurface = floor.AddComponent<NavMeshSurface>();
            navSurface.BuildNavMesh();

            // 3. Bức tường cản tầm nhìn trung tâm (Bắt buộc cho Tình huống 1: Zombie đứng sau tường, không thấy Player)
            // Tường rộng 12m, cao 3.5m đặt tại Z=7m chắn hoàn toàn đường nhìn thẳng giữa Player (Z=0) và Zombie (Z=14)
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Hearing_Obstacle_Wall";
            wall.transform.position = new Vector3(0f, 1.75f, 7f);
            wall.transform.localScale = new Vector3(12f, 3.5f, 1f);
            var wallRend = wall.GetComponent<Renderer>();
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_LoS_Obstacle_Wall.mat");
            if (wallRend != null && wallMat != null) wallRend.sharedMaterial = wallMat;

            // 4. Sinh Player tại (0, 0.1, 0)
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "Player_Test";
                playerInstance.transform.position = new Vector3(0f, 0.1f, 0f);
                playerInstance.transform.rotation = Quaternion.identity;

                var cc = playerInstance.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var cam = playerInstance.GetComponentInChildren<Camera>(true);
                if (cam != null) cam.gameObject.SetActive(true);

                if (playerInstance.GetComponent<GameplayNoiseEmitter>() == null)
                {
                    playerInstance.AddComponent<GameplayNoiseEmitter>();
                }
            }

            // 5. Zombie 1: Quái sau tường trong tầm nghe (Cự ly 14m < 20m, KHÔNG nhìn thấy Player)
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
            if (archerPrefab != null)
            {
                var zombieNear = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                zombieNear.name = "Zombie_InHearingRange_BehindWall";
                zombieNear.transform.position = new Vector3(0f, 0.1f, 14f);
                zombieNear.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Zombie 2: Quái ngoài tầm nghe (Cự ly 28m > 20m, để kiểm thử Tình huống 7: Ngoài bán kính không phản ứng)
                var zombieFar = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                zombieFar.name = "Zombie_OutOfHearingRange";
                zombieFar.transform.position = new Vector3(10f, 0.1f, 28f);
                zombieFar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // 6. NetworkManager với LocalTestHostBootstrap
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkManager.prefab");
            if (nmPrefab != null)
            {
                var nmInstance = (GameObject)PrefabUtility.InstantiatePrefab(nmPrefab);
                nmInstance.name = "NetworkManager";
                var nm = nmInstance.GetComponent<NetworkManager>();
                if (nm != null)
                {
                    nm.NetworkConfig.PlayerPrefab = null; // Tránh spawn duplicate
                }
                if (nmInstance.GetComponent<LocalTestHostBootstrap>() == null)
                {
                    nmInstance.AddComponent<LocalTestHostBootstrap>();
                }
            }

            EditorSceneManager.SaveScene(scene, HearingTestScenePath);
            Debug.Log($"[Z2Setup] Đã lưu Scene kiểm thử Đợt Z2 tại {HearingTestScenePath}!");
        }
    }
}
#endif
