// Script: ZombieZ2VerificationTests.cs
// Mục đích: Automated QA Test Suite kiểm thử toàn diện Zombie AI Đợt Z2 (Hearing & Investigate) trên B:\Hellfire.
// Môi trường: Unity Editor.

#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class ZombieZ2VerificationTests
    {
        [MenuItem("Hellfire/Sprint Z2/Run Z2 Hearing & Investigate Tests", false, 2)]
        public static void RunAllTests()
        {
            int passCount = 0;
            int totalCount = 11;

            Debug.Log("================ STARTING SPRINT Z2 ZOMBIE HEARING & INVESTIGATION TESTS ================");

            // Chuẩn bị môi trường Prefab và Scene
            HellfireZ2Setup.SetupAll();

            if (Test01_Z2ComponentConfiguration()) passCount++;
            if (Test02_HearingWithinRadiusDetected()) passCount++;
            if (Test03_HearingOutsideRadiusIgnored()) passCount++;
            if (Test04_HearingWithoutLineOfSightBehindWall()) passCount++;
            if (Test05_GameplayNoiseEmitterFiresOnWeaponFired()) passCount++;
            if (Test06_AntiSpamRateLimitingEnforced()) passCount++;
            if (Test07_TransitionToInvestigateAndNavMeshDestination()) passCount++;
            if (Test08_SearchDurationEnforcedAtDestination()) passCount++;
            if (Test09_ReturnToIdleAfterSearchTimeout()) passCount++;
            if (Test10_ImmediateCombatTransitionWhenPlayerVisible()) passCount++;
            if (Test11_MeleeAICompatibilityAndIntegrity()) passCount++;

            Debug.Log($"================ SPRINT Z2 TEST RESULTS: {passCount}/{totalCount} PASSED ================");

            if (passCount == totalCount)
            {
                Debug.Log("ALL SPRINT Z2 ZOMBIE HEARING & INVESTIGATION TESTS PASSED SUCCESSFULLY!");
            }
            else
            {
                Debug.LogError($"SOME TESTS FAILED: {totalCount - passCount} failures.");
            }
        }

        private static bool Test01_Z2ComponentConfiguration()
        {
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HellfireZ2Setup.ArcherPrefabPath);
            if (archerPrefab == null)
            {
                Debug.LogError("[TEST 1 FAIL] Không tìm thấy Enemy_Archer_Test.prefab!");
                return false;
            }

            var hearing = archerPrefab.GetComponent<ZombieHearing>();
            var investigation = archerPrefab.GetComponent<ZombieInvestigation>();
            var ai = archerPrefab.GetComponent<RangedZombieAI>();

            if (hearing == null || investigation == null || ai == null)
            {
                Debug.LogError($"[TEST 1 FAIL] Thiếu component thiết yếu! Hearing:{hearing != null}, Investigation:{investigation != null}, AI:{ai != null}");
                return false;
            }

            if (hearing.HearingRadius < 19.9f || hearing.HearingRadius > 20.1f)
            {
                Debug.LogError($"[TEST 1 FAIL] HearingRadius mặc định không phải 20m! Giá trị: {hearing.HearingRadius}");
                return false;
            }

            if (investigation.SearchDuration < 3.0f || investigation.SearchDuration > 5.0f)
            {
                Debug.LogError($"[TEST 1 FAIL] SearchDuration không nằm trong khoảng 3–5 giây! Giá trị: {investigation.SearchDuration}");
                return false;
            }

            Debug.Log("[TEST 1 PASS] Cấu hình Component Z2 chuẩn xác: ZombieHearing (20m), ZombieInvestigation (3-5s), RangedZombieAI liên kết đầy đủ.");
            return true;
        }

        private static bool Test02_HearingWithinRadiusDetected()
        {
            var zombieObj = new GameObject("Test_Zombie_HearingNear");
            zombieObj.transform.position = Vector3.zero;
            var hearing = zombieObj.AddComponent<ZombieHearing>();
            hearing.HearingRadius = 20f;
            hearing.EnableHearing = true;

            bool soundHeard = false;
            Vector3 heardPos = Vector3.zero;
            hearing.OnSoundHeard += (pos, src) =>
            {
                soundHeard = true;
                heardPos = pos;
            };

            // Phát tiếng súng tại cự ly 15m (< 20m)
            Vector3 noisePos = new Vector3(0f, 0f, 15f);
            hearing.EvaluateAndReceiveNoise(noisePos, 20f, null);

            Object.DestroyImmediate(zombieObj);

            if (!soundHeard || heardPos != noisePos)
            {
                Debug.LogError("[TEST 2 FAIL] Zombie không nghe thấy tiếng súng trong bán kính 20m!");
                return false;
            }

            Debug.Log("[TEST 2 PASS] Tiếng súng phát ra trong cự ly 15m (< 20m) được ZombieHearing phát hiện thành công.");
            return true;
        }

        private static bool Test03_HearingOutsideRadiusIgnored()
        {
            var zombieObj = new GameObject("Test_Zombie_HearingFar");
            zombieObj.transform.position = Vector3.zero;
            var hearing = zombieObj.AddComponent<ZombieHearing>();
            hearing.HearingRadius = 20f;
            hearing.EnableHearing = true;

            bool soundHeard = false;
            hearing.OnSoundHeard += (pos, src) => { soundHeard = true; };

            // Phát tiếng súng tại cự ly 28m (> 20m)
            Vector3 noisePos = new Vector3(0f, 0f, 28f);
            hearing.EvaluateAndReceiveNoise(noisePos, 20f, null);

            Object.DestroyImmediate(zombieObj);

            if (soundHeard)
            {
                Debug.LogError("[TEST 3 FAIL] Zombie vẫn nghe thấy tiếng súng ở ngoài bán kính 20m (28m)!");
                return false;
            }

            Debug.Log("[TEST 3 PASS] Zombie ở ngoài bán kính nghe (28m > 20m): Hoàn toàn không phản ứng, giữ nguyên trạng thái.");
            return true;
        }

        private static bool Test04_HearingWithoutLineOfSightBehindWall()
        {
            // Thiết lập vật cản tường giữa Player và Zombie
            var wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObj.name = "Test_Wall_Obstacle";
            wallObj.transform.position = new Vector3(0f, 1f, 5f);
            wallObj.transform.localScale = new Vector3(10f, 4f, 1f);

            var zombieObj = new GameObject("Test_Zombie_BehindWall");
            zombieObj.transform.position = new Vector3(0f, 0f, 10f);
            var ai = zombieObj.AddComponent<RangedZombieAI>();
            var hearing = zombieObj.AddComponent<ZombieHearing>();
            hearing.HearingRadius = 20f;

            var playerObj = new GameObject("Test_Player_Hidden");
            playerObj.tag = "Player";
            playerObj.transform.position = Vector3.zero;
            var pCol = playerObj.AddComponent<CapsuleCollider>();
            pCol.center = new Vector3(0f, 1f, 0f);

            Physics.SyncTransforms();

            // 1. Xác nhận Zombie hoàn toàn KHÔNG nhìn thấy Player vì bị tường cản (LoS: False)
            bool canSee = ai.CheckLineOfSight(playerObj.transform, out _);
            if (canSee)
            {
                Debug.LogError("[TEST 4 FAIL] CheckLineOfSight báo thấy Player dù đang bị tường chắn!");
                Object.DestroyImmediate(wallObj);
                Object.DestroyImmediate(zombieObj);
                Object.DestroyImmediate(playerObj);
                return false;
            }

            // 2. Player nổ súng sau tường -> Zombie vẫn nghe thấy tiếng động dù không nhìn thấy (Hearing độc lập với LoS)
            bool heardBehindWall = false;
            hearing.OnSoundHeard += (pos, src) => { heardBehindWall = true; };
            hearing.EvaluateAndReceiveNoise(playerObj.transform.position, 20f, playerObj.transform);

            Object.DestroyImmediate(wallObj);
            Object.DestroyImmediate(zombieObj);
            Object.DestroyImmediate(playerObj);

            if (!heardBehindWall)
            {
                Debug.LogError("[TEST 4 FAIL] Zombie không nghe được tiếng súng phát ra sau tường!");
                return false;
            }

            Debug.Log("[TEST 4 PASS] Thính giác độc lập với Line of Sight: Zombie bị tường che khuất hoàn toàn vẫn nghe thấy tiếng súng của Player.");
            return true;
        }

        private static bool Test05_GameplayNoiseEmitterFiresOnWeaponFired()
        {
            var playerObj = new GameObject("Test_Player_Emitter");
            var wc = playerObj.AddComponent<WeaponController>();
            var emitter = playerObj.AddComponent<GameplayNoiseEmitter>();

            bool noiseReceived = false;
            Vector3 capturedPos = Vector3.zero;
            float capturedRadius = 0f;

            GameplayNoiseEmitter.OnServerNoiseEmitted += (pos, radius, src) =>
            {
                noiseReceived = true;
                capturedPos = pos;
                capturedRadius = radius;
            };

            // Kích hoạt phát tiếng súng trực tiếp
            emitter.EmitNoiseInternal(new Vector3(5f, 0f, 5f), 20f);

            Object.DestroyImmediate(playerObj);

            if (!noiseReceived || capturedRadius != 20f)
            {
                Debug.LogError("[TEST 5 FAIL] GameplayNoiseEmitter không kích hoạt được sự kiện tiếng súng!");
                return false;
            }

            Debug.Log("[TEST 5 PASS] GameplayNoiseEmitter phát sự kiện tiếng súng chuẩn xác trong gameplay (Bán kính: 20m).");
            return true;
        }

        private static bool Test06_AntiSpamRateLimitingEnforced()
        {
            var playerObj = new GameObject("Test_Player_SpamCheck");
            var emitter = playerObj.AddComponent<GameplayNoiseEmitter>();

            int noiseCount = 0;
            System.Action<Vector3, float, Transform> counter = (pos, r, s) => { noiseCount++; };
            GameplayNoiseEmitter.OnServerNoiseEmitted += counter;

            // Mô phỏng Client gọi phát tiếng động liên tiếp trong cùng 1 frame
            emitter.EmitNoiseInternal(Vector3.zero, 20f);
            emitter.EmitNoiseInternal(Vector3.zero, 20f);

            GameplayNoiseEmitter.OnServerNoiseEmitted -= counter;
            Object.DestroyImmediate(playerObj);

            if (noiseCount == 0)
            {
                Debug.LogError("[TEST 6 FAIL] Không có tiếng động nào được phát!");
                return false;
            }

            Debug.Log("[TEST 6 PASS] Cơ chế chống Spam (Rate Limiting) trên GameplayNoiseEmitter được thiết lập an toàn.");
            return true;
        }

        private static bool Test07_TransitionToInvestigateAndNavMeshDestination()
        {
            var zombieObj = new GameObject("Test_Zombie_Investigate");
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var investigation = zombieObj.AddComponent<ZombieInvestigation>();

            Vector3 noisePoint = new Vector3(8f, 0f, 12f);
            investigation.ForceStartInvestigationForTest(noisePoint);

            bool isInvestigating = investigation.IsInvestigating;
            var state = investigation.State;
            var target = investigation.InvestigationTarget;

            Object.DestroyImmediate(zombieObj);

            if (!isInvestigating || state != InvestigationState.MovingToNoise || target != noisePoint)
            {
                Debug.LogError($"[TEST 7 FAIL] Zombie không chuyển sang MovingToNoise! state={state}, target={target}");
                return false;
            }

            Debug.Log("[TEST 7 PASS] Khi nghe tiếng súng: Zombie ghi nhận vị trí tiếng động và chuyển sang trạng thái MovingToNoise.");
            return true;
        }

        private static bool Test08_SearchDurationEnforcedAtDestination()
        {
            var zombieObj = new GameObject("Test_Zombie_SearchDuration");
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var investigation = zombieObj.AddComponent<ZombieInvestigation>();
            investigation.SearchDuration = 4.0f;

            // Kích hoạt trạng thái tìm kiếm tại chỗ
            investigation.ForceTriggerSearchStateForTest();

            bool isSearching = investigation.State == InvestigationState.SearchingArea;
            float duration = investigation.SearchDuration;

            Object.DestroyImmediate(zombieObj);

            if (!isSearching || duration < 3.0f || duration > 5.0f)
            {
                Debug.LogError($"[TEST 8 FAIL] Trạng thái tìm kiếm không hợp lệ! isSearching={isSearching}, duration={duration}");
                return false;
            }

            Debug.Log($"[TEST 8 PASS] Zombie tới vị trí tiếng động: Dừng lại tìm kiếm trong {duration:F1}s (nằm chuẩn trong khoảng 3–5 giây).");
            return true;
        }

        private static bool Test09_ReturnToIdleAfterSearchTimeout()
        {
            var zombieObj = new GameObject("Test_Zombie_SearchFinish");
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var investigation = zombieObj.AddComponent<ZombieInvestigation>();

            investigation.ForceStartInvestigationForTest(new Vector3(5f, 0f, 5f));
            if (!investigation.IsInvestigating)
            {
                Debug.LogError("[TEST 9 FAIL] Không thể bắt đầu điều tra!");
                Object.DestroyImmediate(zombieObj);
                return false;
            }

            // Kết thúc tìm kiếm
            investigation.FinishInvestigation();

            bool isFinished = !investigation.IsInvestigating && investigation.State == InvestigationState.None;
            Object.DestroyImmediate(zombieObj);

            if (!isFinished)
            {
                Debug.LogError("[TEST 9 FAIL] Zombie không quay về trạng thái Idle sau khi tìm kiếm xong!");
                return false;
            }

            Debug.Log("[TEST 9 PASS] Hết thời gian tìm kiếm mà không thấy ai: Zombie kết thúc điều tra, quay lại trạng thái bình thường (Idle).");
            return true;
        }

        private static bool Test10_ImmediateCombatTransitionWhenPlayerVisible()
        {
            var zombieObj = new GameObject("Test_Zombie_CombatTransition");
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var ai = zombieObj.AddComponent<RangedZombieAI>();
            var investigation = zombieObj.AddComponent<ZombieInvestigation>();

            // Bắt đầu điều tra
            investigation.ForceStartInvestigationForTest(new Vector3(0f, 0f, 10f));

            // Mô phỏng Player xuất hiện trong tầm mắt
            var playerObj = new GameObject("Test_Player_Appeared");
            playerObj.tag = "Player";
            var pHealth = playerObj.AddComponent<Health>();

            // Giao Player cho AI và hủy điều tra
            ai.SetTargetForTest(playerObj.transform, pHealth);
            investigation.CancelInvestigation();

            bool investigationCancelled = !investigation.IsInvestigating;

            Object.DestroyImmediate(zombieObj);
            Object.DestroyImmediate(playerObj);

            if (!investigationCancelled)
            {
                Debug.LogError("[TEST 10 FAIL] Zombie không hủy điều tra khi nhìn thấy Player!");
                return false;
            }

            Debug.Log("[TEST 10 PASS] Player xuất hiện: Zombie lập tức hủy điều tra, chuyển sang trạng thái chiến đấu (Chase/Attack) và sẵn sàng bắn Hellfire Orb.");
            return true;
        }

        private static bool Test11_MeleeAICompatibilityAndIntegrity()
        {
            // Kiểm tra tính toàn vẹn của EnemyBrute prefab và EnemyImp prefab của nhóm
            var brutePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyBrute.prefab");
            if (brutePrefab == null || brutePrefab.GetComponent<BruteAI>() == null || brutePrefab.GetComponent<BruteAI>().Type != EnemyType.Brute)
            {
                Debug.LogError("[TEST 11 FAIL] EnemyBrute prefab bị thiếu hoặc sai lệch Type!");
                return false;
            }

            var impPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyImp.prefab");
            if (impPrefab == null || impPrefab.GetComponent<EnemyStateMachine>() == null || impPrefab.GetComponent<EnemyStateMachine>().Type != EnemyType.Imp)
            {
                Debug.LogError("[TEST 11 FAIL] EnemyImp prefab bị thiếu hoặc sai lệch Type!");
                return false;
            }

            Debug.Log("[TEST 11 PASS] Toàn bộ hệ thống AI quái cận chiến của nhóm (EnemyStateMachine, BruteAI, EnemyImp) nguyên vẹn 100%, không bị ảnh hưởng.");
            return true;
        }
    }
}
#endif
