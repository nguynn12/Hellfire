// Script: HellfireArcherSetup.cs
// Mục đích: Công cụ tự động tạo Prefab và Scene kiểm thử độc lập cho Quái Zombie Tầm xa Đợt Z1 (TestArcherAI.unity).
// Môi trường thực thi: Unity Editor.

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
    public static class HellfireArcherSetup
    {
        public const string TestArcherPrefabPath = "Assets/_Project/Prefabs/Enemy_Archer_Test.prefab";
        public const string TestScenePath = "Assets/_Project/Scenes/TestArcherAI.unity";

        [MenuItem("Hellfire/Sprint Z1/Setup Archer Test Prefab & Scene", false, 1)]
        public static void SetupAll()
        {
            if (EditorApplication.isPlaying) return;

            var archerPrefab = CreateOrUpdateArcherTestPrefab();
            CreateArcherTestScene(archerPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ArcherSetup] Đã thiết lập thành công Enemy_Archer_Test.prefab và TestArcherAI.unity!");
        }

        public static GameObject CreateOrUpdateArcherTestPrefab()
        {
            var root = new GameObject("Enemy_Archer_Test");

            // 1. NetworkObject & NetworkTransform
            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            // 2. NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.stoppingDistance = 10f;
            agent.radius = 0.35f;
            agent.height = 1.6f;

            // 3. Health (25 HP theo Mục 6.2)
            var health = root.AddComponent<Health>();
            var hpProp = typeof(Health).GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpProp != null) hpProp.SetValue(health, 25f);

            // 4. Model Placeholder (Capsule thon cao màu đỏ thẫm quỷ dữ với Material Asset)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);

            var rend = body.GetComponent<MeshRenderer>();
            var archerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ArcherZombie_Test.mat");
            if (archerMat != null) rend.sharedMaterial = archerMat;

            // Xóa collider của model để dùng Hitbox chuyên biệt
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // 4b. Nhãn 3D nổi bật: ARCHER ZOMBIE – TEST (Quay mặt về phía Player)
            var labelObj = new GameObject("Label_Floating");
            labelObj.transform.SetParent(root.transform);
            labelObj.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            labelObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = "ARCHER ZOMBIE – TEST";
            textMesh.fontSize = 36;
            textMesh.characterSize = 0.12f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(1f, 0.85f, 0.1f, 1f); // Màu vàng rực

            // 4c. Điểm ngắm / Mắt quỷ phát sáng để xác định hướng quay mặt
            var eyeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyeObj.name = "Eye_Visor";
            eyeObj.transform.SetParent(root.transform);
            eyeObj.transform.localPosition = new Vector3(0f, 1.4f, 0.35f);
            eyeObj.transform.localScale = new Vector3(0.4f, 0.12f, 0.15f);
            Object.DestroyImmediate(eyeObj.GetComponent<Collider>());
            var eyeRend = eyeObj.GetComponent<MeshRenderer>();
            if (eyeRend != null && archerMat != null) eyeRend.sharedMaterial = archerMat;

            // 5. Hitboxes (Head x2.0, Torso x1.0, Limb x0.75)
            SetupHitboxes(root, new Vector3(0.8f, 1.6f, 0.8f), health);

            // 6. Visuals & AI Component
            var visuals = root.AddComponent<RangedZombieVisuals>();
            var ai = root.AddComponent<RangedZombieAI>();

            // Lưu Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TestArcherPrefabPath);
            Object.DestroyImmediate(root);

            // Đăng ký vào NetworkPrefabs
            RegisterNetworkPrefab(prefab);

            Debug.Log($"[ArcherSetup] Đã lưu Test Archer Prefab tại {TestArcherPrefabPath}");
            return prefab;
        }

        private static void SetupHitboxes(GameObject root, Vector3 scale, Health health)
        {
            var hitboxesRoot = new GameObject("Hitboxes");
            hitboxesRoot.transform.SetParent(root.transform);

            // Head (Sphere)
            var headObj = new GameObject("Hitbox_Head");
            headObj.transform.SetParent(hitboxesRoot.transform);
            headObj.transform.localPosition = new Vector3(0f, 1.36f, 0f);
            var headCol = headObj.AddComponent<SphereCollider>();
            headCol.radius = 0.28f;
            var headId = headObj.AddComponent<HitboxIdentifier>();
            headId.SetHitboxType(HitboxType.Head);
            headId.SetDamageableTarget(health);

            // Torso (Capsule)
            var torsoObj = new GameObject("Hitbox_Torso");
            torsoObj.transform.SetParent(hitboxesRoot.transform);
            torsoObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var torsoCol = torsoObj.AddComponent<CapsuleCollider>();
            torsoCol.radius = scale.x * 0.4f;
            torsoCol.height = scale.y * 0.6f;
            var torsoId = torsoObj.AddComponent<HitboxIdentifier>();
            torsoId.SetHitboxType(HitboxType.Torso);
            torsoId.SetDamageableTarget(health);

            // Limbs (Box)
            var limbsObj = new GameObject("Hitbox_Limbs");
            limbsObj.transform.SetParent(hitboxesRoot.transform);
            limbsObj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            var limbsCol = limbsObj.AddComponent<BoxCollider>();
            limbsCol.size = new Vector3(scale.x * 0.7f, scale.y * 0.3f, scale.z * 0.7f);
            var limbsId = limbsObj.AddComponent<HitboxIdentifier>();
            limbsId.SetHitboxType(HitboxType.Limb);
            limbsId.SetDamageableTarget(health);
        }

        public static void CreateArcherTestScene(GameObject archerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TestArcherAI";

            // 1. Ánh sáng
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Sàn Arena 40m x 40m
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // 40m x 40m
            var floorRend = floor.GetComponent<Renderer>();
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Arena_Floor.mat");
            if (floorRend != null && floorMat != null) floorRend.sharedMaterial = floorMat;

            // Gắn NavMeshSurface vào sàn và Bake NavMesh
            var navSurface = floor.AddComponent<NavMeshSurface>();
            navSurface.BuildNavMesh();

            // 3. Bức tường vật cản kiểm tra Line of Sight (LoS Obstacle)
            // Đặt lệch sang trái tại X=-3.5 (rộng 5m) để đường nhìn thẳng từ Player (0, 0) đến Archer (0, 14) hoàn toàn thông thoáng!
            // Khi muốn thử Line of Sight, người chơi chỉ cần di chuyển sang trái nấp sau tường.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "LoS_Obstacle_Wall";
            wall.transform.position = new Vector3(-3.5f, 1.5f, 7f);
            wall.transform.localScale = new Vector3(5f, 3f, 1f);
            var wallRend = wall.GetComponent<Renderer>();
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_LoS_Obstacle_Wall.mat");
            if (wallRend != null && wallMat != null) wallRend.sharedMaterial = wallMat;

            // 4. Sinh Player thử nghiệm (với Combat Juice & Weapon) tại (0, 0.1, 0) nhìn thẳng về +Z
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player_CombatJuice_Test.prefab")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
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
            }

            // 5. Sinh Archer Zombie thử nghiệm tại (0, 0.1, 14) đối diện trực tiếp người chơi
            if (archerPrefab != null)
            {
                var archerInstance = (GameObject)PrefabUtility.InstantiatePrefab(archerPrefab);
                archerInstance.name = "Archer_Zombie_Test";
                archerInstance.transform.position = new Vector3(0f, 0.1f, 14f);
                archerInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Quay mặt về phía Player
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
                    // Đặt PlayerPrefab = null trong scene test để tránh NGO spawn duplicate Player đè lên Player_Test
                    nm.NetworkConfig.PlayerPrefab = null;
                }
                if (nmInstance.GetComponent<LocalTestHostBootstrap>() == null)
                {
                    nmInstance.AddComponent<LocalTestHostBootstrap>();
                }
            }

            // 7. Lưu Scene
            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[ArcherSetup] Đã lưu Test Scene tại {TestScenePath}");
        }

        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            string[] guids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(assetPath);
                if (prefabsList != null && !prefabsList.Contains(prefab))
                {
                    prefabsList.Add(new NetworkPrefab { Prefab = prefab });
                    EditorUtility.SetDirty(prefabsList);
                }
            }

            // Cũng đăng ký vào NetworkManager.prefab
            string nmPath = "Assets/_Project/Prefabs/NetworkManager.prefab";
            var nmAsset = AssetDatabase.LoadAssetAtPath<GameObject>(nmPath);
            if (nmAsset != null)
            {
                var nmObj = PrefabUtility.InstantiatePrefab(nmAsset) as GameObject;
                var nm = nmObj.GetComponent<NetworkManager>();
                if (nm != null && !nm.NetworkConfig.Prefabs.Contains(prefab))
                {
                    nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                    PrefabUtility.SaveAsPrefabAsset(nmObj, nmPath);
                }
                Object.DestroyImmediate(nmObj);
            }
        }
    }
}
#endif
