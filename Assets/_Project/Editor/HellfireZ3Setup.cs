// Script: HellfireZ3Setup.cs
// Mục đích: Tự động hóa thiết lập Scene thử nghiệm TestZombieGroupAI.unity và Prefab cho Đợt Z3 (Group Alert & Horde Rage).
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

namespace Hellfire.Editor
{
    public static class HellfireZ3Setup
    {
        private const string ArcherPrefabPath = "Assets/_Project/Prefabs/Enemy_Archer_Test.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player_CombatJuice_Test.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/TestZombieGroupAI.unity";

        [MenuItem("Hellfire/Sprint Z3/Setup Z3 Prefab and Scene", false, 1)]
        public static void SetupAll()
        {
            Debug.Log("[Z3Setup] Bắt đầu thiết lập môi trường Đợt Z3: Group Alert & Horde Rage...");

            // 1. Cập nhật Prefab Zombie với ZombieGroupAlert & ZombieHordeRage
            UpdateArcherPrefabWithZ3Components();

            // 2. Tạo Scene kiểm thử TestZombieGroupAI.unity
            CreateGroupAITestScene();

            Debug.Log("[Z3Setup] Thiết lập thành công Prefabs và Scene TestZombieGroupAI.unity cho Đợt Z3!");
        }

        public static GameObject UpdateArcherPrefabWithZ3Components()
        {
            if (!File.Exists(ArcherPrefabPath))
            {
                Debug.LogError("[Z3Setup] Không tìm thấy Archer prefab tại " + ArcherPrefabPath);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(ArcherPrefabPath);
            try
            {
                // 1. Thêm ZombieGroupAlert
                var alert = contents.GetComponent<ZombieGroupAlert>();
                if (alert == null) alert = contents.AddComponent<ZombieGroupAlert>();
                alert.AlertRadius = 15f;
                alert.AlertCooldown = 4.0f;

                // 2. Thêm ZombieHordeRage
                var rage = contents.GetComponent<ZombieHordeRage>();
                if (rage == null) rage = contents.AddComponent<ZombieHordeRage>();

                // 3. Liên kết vào RangedZombieAI
                var ai = contents.GetComponent<RangedZombieAI>();
                if (ai != null)
                {
                    var soAI = new SerializedObject(ai);
                    var alertProp = soAI.FindProperty("_groupAlert");
                    if (alertProp != null) alertProp.objectReferenceValue = alert;
                    var rageProp = soAI.FindProperty("_hordeRage");
                    if (rageProp != null) rageProp.objectReferenceValue = rage;
                    soAI.ApplyModifiedProperties();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, ArcherPrefabPath);
                Debug.Log($"[Z3Setup] Đã cập nhật Enemy_Archer_Test.prefab với ZombieGroupAlert (15m) & ZombieHordeRage (<30% HP)!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
        }

        public static void CreateGroupAITestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TestZombieGroupAI";

            // 1. Ánh sáng Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.9f);
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

            // 3. Bức tường cản tầm nhìn trung tâm (để Zombie_Ally_BehindWall không nhìn thấy Player ban đầu)
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Group_Obstacle_Wall";
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

            // 5. Zombie 1 (Spotter): Đặt tại (-8, 0.1, 10) - Đứng ở rìa tường, CÓ tầm nhìn trực tiếp (LoS) tới Player
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
            if (archerPrefab != null)
            {
                var zombieSpotter = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                zombieSpotter.name = "Zombie_Spotter";
                zombieSpotter.transform.position = new Vector3(-8f, 0.1f, 10f);
                zombieSpotter.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Zombie 2 (Ally trong tầm 15m sau tường): Đặt tại (0, 0.1, 14) - Cách Spotter cự ly ~8.9m (< 15m), KHÔNG có LoS với Player
                var zombieAllyNear = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                zombieAllyNear.name = "Zombie_Ally_BehindWall";
                zombieAllyNear.transform.position = new Vector3(0f, 0.1f, 14f);
                zombieAllyNear.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Zombie 3 (Ally ngoài tầm 15m): Đặt tại (15, 0.1, 28) - Cách Spotter cự ly ~29.2m (> 15m)
                var zombieAllyFar = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                zombieAllyFar.name = "Zombie_Ally_OutOfRange";
                zombieAllyFar.transform.position = new Vector3(15f, 0.1f, 28f);
                zombieAllyFar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
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

            // 7. Lưu Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Z3Setup] Đã lưu Scene kiểm thử Đợt Z3 tại {ScenePath}!");
        }
    }
}
#endif
