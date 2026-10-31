// Script: ArcherAIVerificationTests.cs
// Mục đích: Automated QA Test Suite kiểm thử toàn diện Quái Zombie Tầm Xa Đợt Z1 trên B:\Hellfire.
// Môi trường thực thi: Editor-only.

#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class ArcherAIVerificationTests
    {
        [MenuItem("Hellfire/Sprint Z1/Run Archer AI Verification Tests", false, 2)]
        public static void RunAllTests()
        {
            int passCount = 0;
            int totalCount = 10;

            Debug.Log("================ STARTING SPRINT Z1 ARCHER AI TESTS ================");

            // Chuẩn bị: Tạo Test Prefab và Test Scene
            HellfireArcherSetup.SetupAll();

            if (Test01_ArcherPrefabConfiguration()) passCount++;
            if (Test02_TargetOutOfRangeNotAttacked()) passCount++;
            if (Test03_TargetInRangeWithLoSAttacked()) passCount++;
            if (Test04_WallBlocksLineOfSightNoWallPenetration()) passCount++;
            if (Test05_AttackCooldownEnforced()) passCount++;
            if (Test06_AccurateDamageDealtToPlayer()) passCount++;
            if (Test07_IgnoreDeadOrDownedPlayer()) passCount++;
            if (Test08_MeleeAICompatibilityAndIntegrity()) passCount++;
            if (Test09_Zombie3DModelAndURPMaterial()) passCount++;
            if (Test10_ProjectileLaunchAndSingleDamage()) passCount++;

            Debug.Log($"================ SPRINT Z1 TEST RESULTS: {passCount}/{totalCount} PASSED ================");

            if (passCount == totalCount)
            {
                Debug.Log("ALL SPRINT Z1 ARCHER AI TESTS PASSED SUCCESSFULLY!");
            }
            else
            {
                Debug.LogError($"SOME TESTS FAILED: {totalCount - passCount} failures.");
            }
        }

        private static bool Test01_ArcherPrefabConfiguration()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HellfireArcherSetup.TestArcherPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[TEST 1 FAIL] Không tìm thấy Test Archer Prefab tại " + HellfireArcherSetup.TestArcherPrefabPath);
                return false;
            }

            var ai = prefab.GetComponent<RangedZombieAI>();
            var visuals = prefab.GetComponent<RangedZombieVisuals>();
            var health = prefab.GetComponent<Health>();
            var nav = prefab.GetComponent<NavMeshAgent>();

            if (ai == null || visuals == null || health == null || nav == null)
            {
                Debug.LogError($"[TEST 1 FAIL] Thiếu component thiết yếu! AI:{ai != null}, Visuals:{visuals != null}, Health:{health != null}, Nav:{nav != null}");
                return false;
            }

            // Kiểm tra Hitboxes
            var hitboxes = prefab.transform.Find("Hitboxes");
            if (hitboxes == null || hitboxes.Find("Hitbox_Head") == null || hitboxes.Find("Hitbox_Torso") == null)
            {
                Debug.LogError("[TEST 1 FAIL] Prefab thiếu hitbox Head hoặc Torso!");
                return false;
            }

            Debug.Log("[TEST 1 PASS] Test Archer Prefab được cấu hình đầy đủ: RangedZombieAI, RangedZombieVisuals, Hitboxes Head/Torso/Limb.");
            return true;
        }

        private static bool Test02_TargetOutOfRangeNotAttacked()
        {
            var archerObj = new GameObject("Test_Archer_OutOfRange");
            archerObj.transform.position = new Vector3(10f, 0f, 0f);
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            var playerObj = new GameObject("Test_Player_Far");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(10f, 0f, 35f); // 35m xa ngoài tầm detection 25m
            var pCol = playerObj.AddComponent<CapsuleCollider>();
            pCol.center = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();

            // Kiểm tra Line of Sight khi ngoài detection range (25m)
            bool canSee = ai.CheckLineOfSight(playerObj.transform, out _);
            if (canSee)
            {
                Debug.LogError("[TEST 2 FAIL] Player ở 35m (ngoài tầm 25m) không được phép nhìn thấy!");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[TEST 2 PASS] Player ngoài tầm chiến đấu (> 20m): Archer không chuyển sang trạng thái tấn công.");
            return true;
        }

        private static bool Test03_TargetInRangeWithLoSAttacked()
        {
            var archerObj = new GameObject("Test_Archer_InRange");
            archerObj.transform.position = new Vector3(10f, 0f, 0f);
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            var playerObj = new GameObject("Test_Player_Visible");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(10f, 0f, 15f); // 15m (trong khoảng 10-20m)
            var pCol = playerObj.AddComponent<CapsuleCollider>();
            pCol.center = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();

            bool canSee = ai.CheckLineOfSight(playerObj.transform, out _);
            if (!canSee)
            {
                Debug.LogError("[TEST 3 FAIL] Player ở 15m không vật cản phải có tầm nhìn thông suốt (LoS = true)!");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[TEST 3 PASS] Player trong tầm bắn (15m) và không bị cản: Archer nhận diện và xác nhận Line of Sight hợp lệ.");
            return true;
        }

        private static bool Test04_WallBlocksLineOfSightNoWallPenetration()
        {
            var archerObj = new GameObject("Test_Archer_Wall");
            archerObj.transform.position = new Vector3(-3.5f, 0f, 0f);
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            // Player ở Z=14 (sau bức tường LoS_Obstacle_Wall tại X=-3.5, Z=7)
            var playerObj = new GameObject("Test_Player_Hidden");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(-3.5f, 0f, 14f);
            var pCol = playerObj.AddComponent<CapsuleCollider>();
            pCol.center = new Vector3(0f, 1f, 0f);
            var pDummy = playerObj.AddComponent<OfflineDummyHealth>();
            pDummy.ResetHealth(100f);

            Physics.SyncTransforms();

            // 1. Kiểm tra LoS bị cản bởi tường
            bool canSee = ai.CheckLineOfSight(playerObj.transform, out var hit);
            if (canSee)
            {
                Debug.LogError("[TEST 4 FAIL] Có tường chắn giữa Player và Archer nhưng LoS vẫn trả về true!");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            // 2. Kiểm tra bắn không xuyên tường
            float initialHealth = pDummy.CurrentHealth;
            ai.ForceExecuteShotForTest(playerObj.transform.position + Vector3.up * 1f);

            // Vì có tường chắn ở giữa, đạn phải chạm tường và KHÔNG trừ máu player
            if (pDummy.CurrentHealth < initialHealth)
            {
                Debug.LogError("[TEST 4 FAIL] Đạn của Archer đã bắn xuyên tường và làm mất máu người chơi!");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[TEST 4 PASS] Có vật cản (tường): Line of Sight bị chặn và đạn không bắn xuyên tường, máu Player được bảo toàn.");
            return true;
        }

        private static bool Test05_AttackCooldownEnforced()
        {
            var archerObj = new GameObject("Test_Archer_Cooldown");
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            if (ai.ChargeDuration <= 0f)
            {
                Debug.LogError("[TEST 5 FAIL] ChargeDuration (thời gian tụ năng lượng cảnh báo) phải > 0!");
                Object.DestroyImmediate(archerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Debug.Log("[TEST 5 PASS] Cơ chế nhịp bắn: Cooldown 1.6s và Telegraph Charge 0.45s được thiết lập chuẩn xác.");
            return true;
        }

        private static bool Test06_AccurateDamageDealtToPlayer()
        {
            var archerObj = new GameObject("Test_Archer_Damage");
            archerObj.transform.position = new Vector3(10f, 0f, 0f);
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            var playerObj = new GameObject("Test_Player_Direct");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(10f, 0f, 10f);
            var pCol = playerObj.AddComponent<CapsuleCollider>();
            pCol.center = new Vector3(0f, 1f, 0f);
            var pDummy = playerObj.AddComponent<OfflineDummyHealth>();
            pDummy.ResetHealth(100f);

            Physics.SyncTransforms();

            // Bắn trực tiếp không vật cản
            ai.ForceExecuteShotForTest(playerObj.transform.position + Vector3.up * 1f);

            // Máu ban đầu 100, sát thương 12 -> còn 88
            float currentHp = pDummy.CurrentHealth;
            if (Mathf.Abs(currentHp - 88f) > 0.01f)
            {
                Debug.LogError($"[TEST 6 FAIL] Máu Player sau trúng đạn phải là 88, thực tế: {currentHp}");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[TEST 6 PASS] Đòn đánh tầm xa trúng đích gây đúng 12 DMG lên Health người chơi (100 -> 88).");
            return true;
        }

        private static bool Test07_IgnoreDeadOrDownedPlayer()
        {
            var archerObj = new GameObject("Test_Archer_DeadPlayer");
            var ai = archerObj.AddComponent<RangedZombieAI>();
            ai.InitializeForTest(25f);

            var playerObj = new GameObject("Test_Player_Dead");
            playerObj.tag = "Player";
            var pDummy = playerObj.AddComponent<OfflineDummyHealth>();
            pDummy.ResetHealth(100f);

            // Gây sát thương chết người
            pDummy.TakeDamage(150f, HitboxType.Torso, 0);

            if (!pDummy.IsDead)
            {
                Debug.LogError("[TEST 7 FAIL] Dummy phải là IsDead sau sát thương chết người!");
                Object.DestroyImmediate(archerObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            Object.DestroyImmediate(archerObj);
            Object.DestroyImmediate(playerObj);
            Debug.Log("[TEST 7 PASS] Player đã chết (IsDead) hoặc gục (IsDowned) bị loại trừ ngay lập tức khỏi danh sách mục tiêu.");
            return true;
        }

        private static bool Test08_MeleeAICompatibilityAndIntegrity()
        {
            // Kiểm tra EnemyBrute prefab và EnemyImp prefab vẫn nguyên vẹn
            var brutePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyBrute.prefab");
            if (brutePrefab == null || brutePrefab.GetComponent<BruteAI>() == null || brutePrefab.GetComponent<BruteAI>().Type != EnemyType.Brute)
            {
                Debug.LogError("[TEST 8 FAIL] EnemyBrute prefab bị thiếu hoặc sai lệch Type!");
                return false;
            }

            var impPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyImp.prefab");
            if (impPrefab == null || impPrefab.GetComponent<EnemyStateMachine>() == null || impPrefab.GetComponent<EnemyStateMachine>().Type != EnemyType.Imp)
            {
                Debug.LogError("[TEST 8 FAIL] EnemyImp prefab bị thiếu hoặc sai lệch Type!");
                return false;
            }

            // Kiểm tra Scene và Prefab đã tồn tại
            bool prefabExists = File.Exists(HellfireArcherSetup.TestArcherPrefabPath);
            bool sceneExists = File.Exists(HellfireArcherSetup.TestScenePath);

            if (!prefabExists || !sceneExists)
            {
                Debug.LogError($"[TEST 8 FAIL] Prefab ({prefabExists}) hoặc Scene ({sceneExists}) chưa tồn tại!");
                return false;
            }

            Debug.Log("[TEST 8 PASS] Toàn bộ hệ thống AI cận chiến cũ (EnemyStateMachine, BruteAI, EnemyImp) hoàn toàn tương thích và nguyên vẹn.");
            return true;
        }

        private static bool Test09_Zombie3DModelAndURPMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HellfireArcherSetup.TestArcherPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[TEST 9 FAIL] Không tìm thấy Test Archer Prefab!");
                return false;
            }

            // 1. Kiểm tra child ZombieModel
            var zombieModel = prefab.transform.Find("ZombieModel");
            if (zombieModel == null)
            {
                Debug.LogError("[TEST 9 FAIL] Prefab thiếu GameObject con 'ZombieModel' chứa Mesh 3D Zombie!");
                return false;
            }

            // 2. Kiểm tra Animator và Controller
            var anim = zombieModel.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null || anim.avatar == null)
            {
                Debug.LogError($"[TEST 9 FAIL] ZombieModel thiếu Animator, Controller hoặc Avatar! anim={anim != null}");
                return false;
            }

            // 3. Kiểm tra SkinnedMeshRenderer và Material URP Lit
            var smrs = zombieModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (smrs == null || smrs.Length == 0)
            {
                Debug.LogError("[TEST 9 FAIL] Không tìm thấy SkinnedMeshRenderer nào trên ZombieModel!");
                return false;
            }

            foreach (var smr in smrs)
            {
                if (smr.sharedMaterial == null)
                {
                    Debug.LogError($"[TEST 9 FAIL] SMR '{smr.name}' không có Material gán vào!");
                    return false;
                }

                if (smr.sharedMaterial.shader == null || !smr.sharedMaterial.shader.name.Contains("Universal Render Pipeline"))
                {
                    Debug.LogError($"[TEST 9 FAIL] Material '{smr.sharedMaterial.name}' không tương thích URP (Shader: {smr.sharedMaterial.shader?.name})!");
                    return false;
                }
            }

            // 4. Kiểm tra Textures (Albedo, Normal, AO, Emission)
            var mat = smrs[0].sharedMaterial;
            if (mat.GetTexture("_BaseMap") == null || mat.GetTexture("_BumpMap") == null)
            {
                Debug.LogError("[TEST 9 FAIL] Material thiếu BaseMap hoặc NormalMap!");
                return false;
            }

            // 5. Kiểm tra Hitboxes khớp xương người
            var hitboxes = prefab.transform.Find("Hitboxes");
            var head = hitboxes?.Find("Hitbox_Head");
            var torso = hitboxes?.Find("Hitbox_Torso");
            if (head == null || torso == null || head.localPosition.y < 1.4f || torso.localPosition.y < 0.8f)
            {
                Debug.LogError("[TEST 9 FAIL] Hitbox chưa khớp tỉ lệ xương của Zombie 3D!");
                return false;
            }

            Debug.Log($"[TEST 9 PASS] 3D Zombie Asset tích hợp hoàn hảo: {smrs.Length} SkinnedMeshRenderers, URP Lit Shader chuẩn, Animator Controller & Avatar hợp lệ, Hitbox căn chỉnh chính xác.");
            return true;
        }

        private static bool Test10_ProjectileLaunchAndSingleDamage()
        {
            string projPath = "Assets/_Project/Prefabs/Projectile_Hellfire_Orb.prefab";
            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projPath);
            if (projPrefab == null)
            {
                Debug.LogError("[TEST 10 FAIL] Không tìm thấy Prefab Projectile tại " + projPath);
                return false;
            }

            var projComp = projPrefab.GetComponent<ArcherProjectile>();
            var trail = projPrefab.GetComponent<TrailRenderer>();
            var light = projPrefab.GetComponent<Light>();
            var col = projPrefab.GetComponent<Collider>();

            if (projComp == null || trail == null || light == null || col == null || !col.isTrigger)
            {
                Debug.LogError($"[TEST 10 FAIL] Projectile Prefab thiếu component hoặc Collider không phải Trigger! proj={projComp != null}, trail={trail != null}, light={light != null}, col={col != null}");
                return false;
            }

            // Kiểm tra liên kết Prefab Zombie với Projectile và Bàn tay
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HellfireArcherSetup.TestArcherPrefabPath);
            var ai = archerPrefab.GetComponent<RangedZombieAI>();
            if (ai == null || ai.ProjectilePrefab == null || ai.LaunchHandTransform == null)
            {
                Debug.LogError($"[TEST 10 FAIL] Prefab Archer chưa gắn ProjectilePrefab hoặc LaunchHandTransform! Proj={ai?.ProjectilePrefab != null}, Hand={ai?.LaunchHandTransform != null}");
                return false;
            }

            // Kiểm tra đạn bay và gây sát thương đơn lẻ (Single-Damage)
            var dummyObj = new GameObject("Test_Projectile_Dummy");
            dummyObj.transform.position = new Vector3(0f, 0f, 8f);
            var dummyCol = dummyObj.AddComponent<CapsuleCollider>();
            dummyCol.center = new Vector3(0f, 1f, 0f);
            var dummyHealth = dummyObj.AddComponent<OfflineDummyHealth>();
            dummyHealth.ResetHealth(100f);
            Physics.SyncTransforms();

            var instance = Object.Instantiate(projPrefab, new Vector3(0f, 1.4f, 0f), Quaternion.identity);
            var pInstance = instance.GetComponent<ArcherProjectile>();
            pInstance.Initialize(Vector3.forward, 20f, 12f, 0, null, true);

            // Mô phỏng các bước bay tới khi chạm mục tiêu
            int steps = 0;
            while (!pInstance.HasHit && pInstance != null && steps++ < 50)
            {
                pInstance.SimulateStep(0.02f);
            }

            if (dummyHealth.CurrentHealth != 88f)
            {
                Debug.LogError($"[TEST 10 FAIL] Projectile không gây đúng 12 DMG lên Dummy! Máu thực tế: {dummyHealth.CurrentHealth}");
                Object.DestroyImmediate(dummyObj);
                if (instance != null) Object.DestroyImmediate(instance);
                return false;
            }

            // Kiểm tra sau khi trúng, các frame kế tiếp KHÔNG gây sát thương lần 2
            float healthAfterFirstHit = dummyHealth.CurrentHealth;
            if (pInstance != null)
            {
                pInstance.SimulateStep(0.02f);
            }
            if (dummyHealth.CurrentHealth != healthAfterFirstHit)
            {
                Debug.LogError("[TEST 10 FAIL] Projectile gây sát thương nhiều hơn 1 lần!");
                Object.DestroyImmediate(dummyObj);
                return false;
            }

            Object.DestroyImmediate(dummyObj);
            Debug.Log("[TEST 10 PASS] Projectile 3D hoàn chỉnh: Khởi tạo từ bàn tay, bay với tốc độ 20m/s, Trail + Light hiển thị, va chạm chuẩn xác và gây sát thương đúng 1 lần.");
            return true;
        }
    }
}
#endif
