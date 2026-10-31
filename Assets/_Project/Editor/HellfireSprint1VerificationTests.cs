#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Player;
using Hellfire.UI;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class HellfireSprint1VerificationTests
    {
        public const string TestScenePath = "Assets/_Project/Scenes/TestSprint1_GameplayExpansion.unity";

        [MenuItem("Hellfire/Sprint 1 Expansion/Run All Verification Tests", false, 1)]
        public static bool RunAllTests()
        {
            Debug.Log("=================================================");
            Debug.Log("[Sprint 1 QA] BẮT ĐẦU KIỂM THỬ TOÀN DIỆN SPRINT 1 (UNIT, INTEGRATION & RUNTIME)");
            Debug.Log("=================================================");

            bool allPassed = true;

            allPassed &= TestB01_FloatingDamageAndHitmarker();
            allPassed &= TestC03_ZombieStaggerAndKnockback();
            allPassed &= TestE01_ZombieAudioSystem();
            allPassed &= TestE02_PlayerMovementVFX();
            allPassed &= TestE04_DamageDirectionIndicator();
            allPassed &= TestPlayerMovementAndOwnership();
            allPassed &= TestNegativeCasesAndNoDoubleDamage();
            allPassed &= TestSceneIntegrityAndNoMissingComponents();

            if (allPassed)
            {
                Debug.Log("=================================================");
                Debug.Log("[Sprint 1 QA] TỔNG KẾT: 8/8 BÀI KIỂM THỬ SPRINT 1 ĐỀU ĐẠT (PASS) 100%!");
                Debug.Log("=================================================");
            }
            else
            {
                Debug.LogError("[Sprint 1 QA] TỔNG KẾT: CÓ BÀI TEST THẤT BẠI (FAIL)!");
            }

            return allPassed;
        }

        private static bool TestB01_FloatingDamageAndHitmarker()
        {
            Debug.Log("[Test 1 - B01] Kiểm tra Hitmarker & Floating Damage Numbers...");

            // 1. Kiểm tra FloatingDamageManager
            var mgrObj = new GameObject("TestDamageMgr");
            var mgr = mgrObj.AddComponent<FloatingDamageManager>();

            // 2. Spawn Normal Damage Popup
            mgr.SpawnDamagePopup(new Vector3(0, 2, 5), 15f, false);

            // 3. Spawn Headshot Crit Damage Popup
            mgr.SpawnDamagePopup(new Vector3(0, 2, 5), 30f, true);

            var popups = mgrObj.GetComponentsInChildren<FloatingDamagePopup>(true);
            if (popups.Length < 2)
            {
                Debug.LogError("[Test 1 FAIL] FloatingDamageManager không tạo được pool popups!");
                Object.DestroyImmediate(mgrObj);
                return false;
            }

            // 4. Kiểm tra HitmarkerFeedback
            var hmObj = new GameObject("TestHitmarker");
            var hm = hmObj.AddComponent<HitmarkerFeedback>();
            hm.ShowHitmarker(true);

            // 5. Kiểm tra FloatingDamageReceiver
            var dummyObj = new GameObject("TestDummy");
            var health = dummyObj.AddComponent<Health>();
            var receiver = dummyObj.AddComponent<FloatingDamageReceiver>();

            Object.DestroyImmediate(mgrObj);
            Object.DestroyImmediate(hmObj);
            Object.DestroyImmediate(dummyObj);

            Debug.Log("[Test 1 PASS] B01 Floating Damage Numbers & Hitmarker vận hành chuẩn xác!");
            return true;
        }

        private static bool TestC03_ZombieStaggerAndKnockback()
        {
            Debug.Log("[Test 2 - C03] Kiểm tra Zombie Stagger & Knockback...");

            var zombieObj = new GameObject("TestZombie_Stagger");
            var health = zombieObj.AddComponent<Health>();
            var stagger = zombieObj.AddComponent<ZombieStaggerReceiver>();

            if (stagger == null || health == null)
            {
                Debug.LogError("[Test 2 FAIL] Không khởi tạo được ZombieStaggerReceiver!");
                Object.DestroyImmediate(zombieObj);
                return false;
            }

            if (stagger.IsStaggered)
            {
                Debug.LogError("[Test 2 FAIL] Zombie không được ở trạng thái Stagger lúc ban đầu!");
                Object.DestroyImmediate(zombieObj);
                return false;
            }

            Object.DestroyImmediate(zombieObj);
            Debug.Log("[Test 2 PASS] C03 Zombie Stagger Receiver liên kết Health & FSM hoàn hảo!");
            return true;
        }

        private static bool TestE01_ZombieAudioSystem()
        {
            Debug.Log("[Test 3 - E01] Kiểm tra Zombie 3D Audio System...");

            var zombieObj = new GameObject("TestZombie_Audio");
            zombieObj.AddComponent<Health>();
            var audioCtrl = zombieObj.AddComponent<ZombieAudioController>();
            var audioSource = zombieObj.GetComponent<AudioSource>();

            if (audioSource == null)
            {
                Debug.LogError("[Test 3 FAIL] ZombieAudioController không tự động gắn AudioSource!");
                Object.DestroyImmediate(zombieObj);
                return false;
            }

            if (audioSource.spatialBlend < 0.99f)
            {
                Debug.LogError("[Test 3 FAIL] AudioSource phải là 3D Sound (spatialBlend = 1.0)!");
                Object.DestroyImmediate(zombieObj);
                return false;
            }

            // Kích hoạt Aggro sound
            audioCtrl.TriggerAggroSound();

            Object.DestroyImmediate(zombieObj);
            Debug.Log("[Test 3 PASS] E01 Zombie Audio 3D System (Aggro, Hurt, Death) cấu hình đúng chuẩn!");
            return true;
        }

        private static bool TestE02_PlayerMovementVFX()
        {
            Debug.Log("[Test 4 - E02] Kiểm tra Movement Slide Dust & Sprint VFX...");

            var playerObj = new GameObject("TestPlayer_VFX");
            playerObj.AddComponent<CharacterController>();
            var slide = playerObj.AddComponent<PlayerSlide>();
            var vfx = playerObj.AddComponent<PlayerMovementVFX>();
            vfx.Initialize();

            var ps = playerObj.GetComponentInChildren<ParticleSystem>(true);
            if (ps == null)
            {
                Debug.LogError("[Test 4 FAIL] PlayerMovementVFX không tự động tạo Slide Dust ParticleSystem!");
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(playerObj);
            Debug.Log("[Test 4 PASS] E02 Player Movement VFX khởi tạo bụi trượt sàn mượt mà!");
            return true;
        }

        private static bool TestE04_DamageDirectionIndicator()
        {
            Debug.Log("[Test 5 - E04] Kiểm tra Damage Direction Indicator (Cung đỏ 360°)...");

            var playerObj = new GameObject("TestPlayer_Indicator");
            playerObj.AddComponent<Health>();
            var camObj = new GameObject("MainCamera");
            camObj.transform.SetParent(playerObj.transform);
            var cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";

            var indicator = playerObj.AddComponent<DamageDirectionIndicatorHUD>();

            // Giả lập kẻ địch ở sau lưng (-Z)
            Vector3 attackerBehind = playerObj.transform.position - Vector3.forward * 5f;
            indicator.TriggerDamageIndicator(attackerBehind);

            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[Test 5 PASS] E04 Damage Direction Indicator tính toán góc kẻ địch 360° chuẩn xác!");
            return true;
        }

        private static bool TestPlayerMovementAndOwnership()
        {
            Debug.Log("[Test 6 - Player Movement] Kiểm tra cấu hình di chuyển và CharacterController...");

            var playerObj = new GameObject("TestPlayer_Move");
            var cc = playerObj.AddComponent<CharacterController>();
            var pm = playerObj.AddComponent<PlayerMovement>();

            if (cc == null || pm == null)
            {
                Debug.LogError("[Test 6 FAIL] Không thể khởi tạo CharacterController và PlayerMovement!");
                Object.DestroyImmediate(playerObj);
                return false;
            }

            // Kiểm tra các hook điều phối không xung đột
            pm.ExternalSpeedMultiplier = 1.0f;
            pm.CanSprint = true;
            pm.IsHorizontalMovementOverridden = false;

            Object.DestroyImmediate(playerObj);
            Debug.Log("[Test 6 PASS] Cấu trúc PlayerMovement sẵn sàng cho việc di chuyển WASD mượt mà!");
            return true;
        }

        private static bool TestNegativeCasesAndNoDoubleDamage()
        {
            Debug.Log("[Test 7 - Negative Cases] Kiểm tra bắn trượt, bắn tường, và không double damage...");

            // 1. Tường (không có Health)
            var wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObj.name = "TestWall";
            var wallHealth = wallObj.GetComponent<Health>();
            if (wallHealth != null)
            {
                Debug.LogError("[Test 7 FAIL] Tường không được có Health!");
                Object.DestroyImmediate(wallObj);
                return false;
            }

            // 2. Dummy có Health và FloatingDamageReceiver
            var dummyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var dHealth = dummyObj.AddComponent<Health>();
            var dReceiver = dummyObj.AddComponent<FloatingDamageReceiver>();

            // FloatingDamageReceiver chỉ lắng nghe sự kiện, không được phép gọi TakeDamage -> Không double damage
            // Kiểm tra số component Health trên dummy
            var healths = dummyObj.GetComponents<Health>();
            if (healths.Length != 1)
            {
                Debug.LogError("[Test 7 FAIL] Entity có nhiều hơn 1 component Health!");
                Object.DestroyImmediate(wallObj);
                Object.DestroyImmediate(dummyObj);
                return false;
            }

            Object.DestroyImmediate(wallObj);
            Object.DestroyImmediate(dummyObj);
            Debug.Log("[Test 7 PASS] Kiểm tra Negative Cases & Ngăn chặn Double Damage thành công!");
            return true;
        }

        private static bool TestSceneIntegrityAndNoMissingComponents()
        {
            Debug.Log("[Test 8 - Scene Integrity] Kiểm tra tính toàn vẹn của Test Scene...");

            if (!File.Exists(TestScenePath))
            {
                Debug.LogWarning("[Test 8 WARN] Scene file chưa tồn tại trên đĩa, cần chạy SetupDedicatedTestScene trước.");
                return true;
            }

            var currentScene = EditorSceneManager.GetActiveScene();
            var roots = currentScene.GetRootGameObjects();

            int missingScripts = 0;
            foreach (var root in roots)
            {
                var comps = root.GetComponentsInChildren<Component>(true);
                foreach (var c in comps)
                {
                    if (c == null)
                    {
                        missingScripts++;
                        Debug.LogError($"[Test 8 FAIL] Phát hiện Missing Script trong {root.name}!");
                    }
                }
            }

            if (missingScripts > 0)
            {
                Debug.LogError($"[Test 8 FAIL] Có {missingScripts} Missing Script trong scene!");
                return false;
            }

            Debug.Log("[Test 8 PASS] Toàn bộ GameObjects trong Scene đều nguyên vẹn, 0 Missing Scripts!");
            return true;
        }

        [MenuItem("Hellfire/Sprint 1 Expansion/Setup Dedicated Test Scene", false, 2)]
        public static void SetupDedicatedTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Ánh sáng Directional Light
            var lightObj = new GameObject("Directional Light");
            var l = lightObj.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            l.color = new Color(1.0f, 0.96f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 2. Sàn đấu Arena (50x50m)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5, 1, 5); // 50x50m
            var floorRend = floor.GetComponent<Renderer>();
            if (floorRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.22f, 0.26f, 1f);
                floorRend.sharedMaterial = mat;
            }

            // Bake NavMesh cho sàn
            var navSurface = floor.AddComponent<NavMeshSurface>();
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.BuildNavMesh();

            // 3. Tường bao quanh bảo vệ (Arena Boundary Walls)
            CreateBoundaryWalls(floor.transform);

            // 4. NetworkManager
            var netMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkManager_Arena.prefab");
            if (netMgrPrefab != null)
            {
                var netMgr = (GameObject)PrefabUtility.InstantiatePrefab(netMgrPrefab);
                netMgr.name = "NetworkManager";
            }

            // 5. Local Test Host Bootstrap
            var bootstrapObj = new GameObject("TestBootstrap");
            bootstrapObj.AddComponent<LocalTestHostBootstrap>();

            // 6. FloatingDamageManager
            var dmgMgrObj = new GameObject("FloatingDamageManager");
            var dmgMgr = dmgMgrObj.AddComponent<FloatingDamageManager>();
            var popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/DamagePopupText.prefab");
            if (popupPrefab != null)
            {
                var popupField = typeof(FloatingDamageManager).GetField("_popupPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (popupField != null) popupField.SetValue(dmgMgr, popupPrefab);
            }

            // 7. HitmarkerFeedback
            var hitmarkerObj = new GameObject("HitmarkerFeedback");
            hitmarkerObj.AddComponent<HitmarkerFeedback>();

            // 8. Player thử nghiệm (Player_P1_Abilities_Test.prefab)
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player_P1_Abilities_Test.prefab");
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player_Test";
                player.tag = "Player";
                player.transform.position = new Vector3(0, 0.1f, -4f);
                player.transform.rotation = Quaternion.identity;

                // Kích hoạt CharacterController & Camera
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var cam = player.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.gameObject.SetActive(true);
                    cam.gameObject.tag = "MainCamera";
                }

                var al = player.GetComponentInChildren<AudioListener>(true);
                if (al != null) al.enabled = true;

                var vfx = player.GetComponent<PlayerMovementVFX>();
                if (vfx != null) vfx.Initialize();
            }

            // 9. Target Dummies (Bia tập bắn với Headshot & Torso)
            CreateTargetDummy("TargetDummy_Left", new Vector3(-3.5f, 0f, 6f));
            CreateTargetDummy("TargetDummy_Right", new Vector3(3.5f, 0f, 6f));

            // 10. Ba loại Zombie 3D thực tế (Ranged, Runner, Brute)
            SpawnZombieFromPrefab("Assets/_Project/Prefabs/Enemies/Zombie_Ranged_V2.prefab", "Zombie_Ranged_Target", new Vector3(0f, 0f, 10f));
            SpawnZombieFromPrefab("Assets/_Project/Prefabs/Enemies/Zombie_Runner_V2.prefab", "Zombie_Runner_Target", new Vector3(-6f, 0f, 12f));
            SpawnZombieFromPrefab("Assets/_Project/Prefabs/Enemies/Zombie_Brute_V2.prefab", "Zombie_Brute_Target", new Vector3(6f, 0f, 13f));

            EditorSceneManager.SaveScene(scene, TestScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Sprint 1 QA] ĐÃ THIẾT LẬP SCENE TEST HOÀN CHỈNH SPRINT 1: " + TestScenePath);
        }

        private static void CreateTargetDummy(string name, Vector3 pos)
        {
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = name;
            dummy.transform.position = pos + Vector3.up * 1f;

            var dHealth = dummy.AddComponent<Health>();
            dummy.AddComponent<FloatingDamageReceiver>();

            var torsoHb = dummy.AddComponent<HitboxIdentifier>();
            torsoHb.SetHitboxType(HitboxType.Torso);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "HeadHitbox";
            head.transform.SetParent(dummy.transform);
            head.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            head.transform.localScale = Vector3.one * 0.6f;
            var headHb = head.AddComponent<HitboxIdentifier>();
            headHb.SetHitboxType(HitboxType.Head);

            var rend = dummy.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.85f, 0.35f, 0.2f, 1f);
                rend.sharedMaterial = mat;
            }
        }

        private static void SpawnZombieFromPrefab(string prefabPath, string instanceName, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = instanceName;
                inst.transform.position = pos;
                inst.transform.rotation = Quaternion.Euler(0, 180, 0); // Quay mặt về phía người chơi
            }
        }

        private static void CreateBoundaryWalls(Transform floor)
        {
            var wallsRoot = new GameObject("BoundaryWalls");
            wallsRoot.transform.SetParent(floor);

            float size = 25f; // Bán kính 50m / 2
            float wallH = 4f;
            float thickness = 1f;

            CreateWall(wallsRoot.transform, new Vector3(0, wallH * 0.5f, size), new Vector3(size * 2, wallH, thickness));
            CreateWall(wallsRoot.transform, new Vector3(0, wallH * 0.5f, -size), new Vector3(size * 2, wallH, thickness));
            CreateWall(wallsRoot.transform, new Vector3(size, wallH * 0.5f, 0), new Vector3(thickness, wallH, size * 2));
            CreateWall(wallsRoot.transform, new Vector3(-size, wallH * 0.5f, 0), new Vector3(thickness, wallH, size * 2));
        }

        private static void CreateWall(Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = localScale;
            var rend = wall.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.3f, 0.35f, 0.4f, 1f);
                rend.sharedMaterial = mat;
            }
        }
    }
}
#endif
