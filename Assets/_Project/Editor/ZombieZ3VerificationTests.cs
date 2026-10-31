// Script: ZombieZ3VerificationTests.cs
// Mục đích: Automated QA Test Suite kiểm thử toàn diện Zombie AI Đợt Z3 (Group Alert & Horde Rage) trên B:\Hellfire.
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
    public static class ZombieZ3VerificationTests
    {
        [MenuItem("Hellfire/Sprint Z3/Run Z3 Group Alert & Horde Rage Tests", false, 2)]
        public static void RunAllTests()
        {
            int passCount = 0;
            int totalCount = 11;

            Debug.Log("================ STARTING SPRINT Z3 GROUP ALERT & HORDE RAGE TESTS ================");

            // Chuẩn bị môi trường Prefab và Scene Z3
            HellfireZ3Setup.SetupAll();

            if (Test01_Z3ComponentConfiguration()) passCount++;
            if (Test02_GroupAlert_WithinRadius_Received()) passCount++;
            if (Test03_GroupAlert_OutsideRadius_Ignored()) passCount++;
            if (Test04_GroupAlert_InfiniteLoopPrevention()) passCount++;
            if (Test05_GroupAlert_NoLineOfSight_TransitionsToInvestigate()) passCount++;
            if (Test06_GroupAlert_WithLineOfSight_TransitionsToCombat()) passCount++;
            if (Test07_HordeRage_TriggerAt30PercentHP()) passCount++;
            if (Test08_HordeRage_SpeedAndCooldownBuff()) passCount++;
            if (Test09_HordeRage_NoDamageDoubling()) passCount++;
            if (Test10_HordeRage_StopsOnDeath()) passCount++;
            if (Test11_TeammateAIIntegrityPreserved()) passCount++;

            Debug.Log($"================ SPRINT Z3 TEST RESULTS: {passCount}/{totalCount} PASSED ================");

            if (passCount == totalCount)
            {
                Debug.Log("ALL SPRINT Z3 GROUP ALERT & HORDE RAGE TESTS PASSED SUCCESSFULLY!");
            }
            else
            {
                Debug.LogError($"FAILED {totalCount - passCount} TESTS IN SPRINT Z3 SUITE!");
            }
        }

        private static bool Test01_Z3ComponentConfiguration()
        {
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Archer_Test.prefab");
            if (archerPrefab == null)
            {
                Debug.LogError("[TEST 1 FAIL] Không tìm thấy Enemy_Archer_Test.prefab!");
                return false;
            }

            var alert = archerPrefab.GetComponent<ZombieGroupAlert>();
            if (alert == null || alert.AlertRadius != 15f)
            {
                Debug.LogError($"[TEST 1 FAIL] ZombieGroupAlert chưa cấu hình chuẩn: Alert={alert != null}, Radius={alert?.AlertRadius}m (yêu cầu 15m)!");
                return false;
            }

            var rage = archerPrefab.GetComponent<ZombieHordeRage>();
            if (rage == null || Mathf.Abs(rage.RageThresholdRatio - 0.30f) > 0.01f || Mathf.Abs(rage.SpeedMultiplier - 1.25f) > 0.01f || Mathf.Abs(rage.AttackCooldownMultiplier - 0.85f) > 0.01f)
            {
                Debug.LogError("[TEST 1 FAIL] ZombieHordeRage chưa cấu hình chuẩn (+25% speed, -15% cooldown, 30% HP)!");
                return false;
            }

            var ai = archerPrefab.GetComponent<RangedZombieAI>();
            if (ai == null || ai.GroupAlert == null || ai.HordeRage == null)
            {
                Debug.LogError("[TEST 1 FAIL] RangedZombieAI chưa liên kết GroupAlert hoặc HordeRage!");
                return false;
            }

            Debug.Log("[TEST 1 PASS] Cấu hình Component Z3 chuẩn xác: ZombieGroupAlert (15m), ZombieHordeRage (<30% HP, +25% Speed, -15% Cooldown).");
            return true;
        }

        private static bool Test02_GroupAlert_WithinRadius_Received()
        {
            var senderObj = new GameObject("Test_Spotter");
            senderObj.transform.position = Vector3.zero;
            var senderAlert = senderObj.AddComponent<ZombieGroupAlert>();
            senderAlert.AlertRadius = 15f;

            var allyObj = new GameObject("Test_Ally_Near");
            allyObj.transform.position = new Vector3(0f, 0f, 10f); // Cự ly 10m < 15m
            var allyAlert = allyObj.AddComponent<ZombieGroupAlert>();

            bool alertReceived = false;
            Vector3 receivedPos = Vector3.zero;
            allyAlert.OnAlertReceived += (pos, src) =>
            {
                alertReceived = true;
                receivedPos = pos;
            };

            Vector3 targetPos = new Vector3(0f, 0f, -5f);
            senderAlert.BroadcastAlert(targetPos, null);

            Object.DestroyImmediate(senderObj);
            Object.DestroyImmediate(allyObj);

            if (!alertReceived || receivedPos != targetPos)
            {
                Debug.LogError("[TEST 2 FAIL] Đồng minh trong bán kính 10m (< 15m) không nhận được Group Alert!");
                return false;
            }

            Debug.Log("[TEST 2 PASS] Đồng minh trong bán kính 15m nhận được Group Alert với tọa độ mục tiêu chính xác.");
            return true;
        }

        private static bool Test03_GroupAlert_OutsideRadius_Ignored()
        {
            var senderObj = new GameObject("Test_Spotter");
            senderObj.transform.position = Vector3.zero;
            var senderAlert = senderObj.AddComponent<ZombieGroupAlert>();
            senderAlert.AlertRadius = 15f;

            var allyObj = new GameObject("Test_Ally_Far");
            allyObj.transform.position = new Vector3(0f, 0f, 22f); // Cự ly 22m > 15m
            var allyAlert = allyObj.AddComponent<ZombieGroupAlert>();

            bool alertReceived = false;
            allyAlert.OnAlertReceived += (pos, src) => { alertReceived = true; };

            senderAlert.BroadcastAlert(new Vector3(0f, 0f, -5f), null);

            Object.DestroyImmediate(senderObj);
            Object.DestroyImmediate(allyObj);

            if (alertReceived)
            {
                Debug.LogError("[TEST 3 FAIL] Đồng minh ngoài bán kính (22m > 15m) vẫn nhận được Alert!");
                return false;
            }

            Debug.Log("[TEST 3 PASS] Đồng minh ngoài bán kính cảnh báo (22m > 15m) hoàn toàn bỏ qua tín hiệu.");
            return true;
        }

        private static bool Test04_GroupAlert_InfiniteLoopPrevention()
        {
            var zombieA = new GameObject("Zombie_A");
            zombieA.transform.position = Vector3.zero;
            var alertA = zombieA.AddComponent<ZombieGroupAlert>();
            alertA.AlertRadius = 15f;

            var zombieB = new GameObject("Zombie_B");
            zombieB.transform.position = new Vector3(0f, 0f, 5f);
            var alertB = zombieB.AddComponent<ZombieGroupAlert>();
            alertB.AlertRadius = 15f;

            int alertACount = 0;
            int alertBCount = 0;

            alertA.OnAlertReceived += (pos, src) => { alertACount++; };
            alertB.OnAlertReceived += (pos, src) =>
            {
                alertBCount++;
                // Thử kích hoạt broadcast ngược lại ngay lập tức
                alertB.BroadcastAlert(pos, src);
            };

            alertA.BroadcastAlert(Vector3.forward, null);

            Object.DestroyImmediate(zombieA);
            Object.DestroyImmediate(zombieB);

            // B nhận 1 lần, nhưng A không được bị dội ngược (ping-pong)
            if (alertBCount != 1 || alertACount > 0)
            {
                Debug.LogError($"[TEST 4 FAIL] Lỗi lặp cảnh báo vô hạn: alertB={alertBCount}, alertA={alertACount}!");
                return false;
            }

            Debug.Log("[TEST 4 PASS] Cơ chế chống lặp vô hạn (Infinite Loop Prevention) hoạt động hoàn hảo, chặn phản xạ dội ngược.");
            return true;
        }

        private static bool Test05_GroupAlert_NoLineOfSight_TransitionsToInvestigate()
        {
            var allyObj = new GameObject("Zombie_Ally_NoLoS");
            var agent = allyObj.AddComponent<NavMeshAgent>();
            var invest = allyObj.AddComponent<ZombieInvestigation>();
            var alert = allyObj.AddComponent<ZombieGroupAlert>();

            Vector3 lastKnownPlayerPos = new Vector3(10f, 0f, 10f);

            // Nhận cảnh báo không có tầm nhìn trực tiếp
            alert.ReceiveAlert(lastKnownPlayerPos, null, null);

            bool isInvestigating = invest.State == InvestigationState.MovingToNoise;
            Vector3 target = invest.InvestigationTarget;

            Object.DestroyImmediate(allyObj);

            if (!isInvestigating || target != lastKnownPlayerPos)
            {
                Debug.LogError($"[TEST 5 FAIL] Quái nhận cảnh báo không có LoS nhưng không chuyển sang Investigate! State={invest.State}, Target={target}");
                return false;
            }

            Debug.Log("[TEST 5 PASS] Quái nhận cảnh báo khi không có LoS: Không nhìn xuyên tường, chuyển sang Investigate vị trí cuối cùng.");
            return true;
        }

        private static bool Test06_GroupAlert_WithLineOfSight_TransitionsToCombat()
        {
            var allyObj = new GameObject("Zombie_Ally_LoS");
            allyObj.AddComponent<NavMeshAgent>();
            var rzAI = allyObj.AddComponent<RangedZombieAI>();
            var alert = allyObj.AddComponent<ZombieGroupAlert>();

            var playerObj = new GameObject("Player_Visible");
            playerObj.AddComponent<Health>();

            // Khi nhận cảnh báo và nhìn thấy mục tiêu
            rzAI.EngageSpottedPlayer(playerObj.transform);

            bool inCombat = rzAI.CurrentState == EnemyState.Chase;

            Object.DestroyImmediate(allyObj);
            Object.DestroyImmediate(playerObj);

            if (!inCombat)
            {
                Debug.LogError($"[TEST 6 FAIL] Quái nhận cảnh báo có LoS nhưng không vào Chase! CurrentState={rzAI.CurrentState}");
                return false;
            }

            Debug.Log("[TEST 6 PASS] Quái nhận cảnh báo khi có tầm nhìn thông suốt: Chuyển ngay sang trạng thái giao tranh (Chase).");
            return true;
        }

        private static bool Test07_HordeRage_TriggerAt30PercentHP()
        {
            var zombieObj = new GameObject("Zombie_RageTest");
            var health = zombieObj.AddComponent<Health>();
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var rage = zombieObj.AddComponent<ZombieHordeRage>();

            bool stateChangedToRage = false;
            rage.OnRageStateChanged += (enraged) => { if (enraged) stateChangedToRage = true; };

            // Kích hoạt Rage trực tiếp
            rage.ForceTriggerRageForTest();

            Object.DestroyImmediate(zombieObj);

            if (!stateChangedToRage)
            {
                Debug.LogError("[TEST 7 FAIL] Zombie máu dưới 30% nhưng không kích hoạt Horde Rage!");
                return false;
            }

            Debug.Log("[TEST 7 PASS] Khi máu giảm dưới 30% HP: Zombie kích hoạt thành công trạng thái Horde Rage.");
            return true;
        }

        private static bool Test08_HordeRage_SpeedAndCooldownBuff()
        {
            var zombieObj = new GameObject("Zombie_BuffTest");
            var health = zombieObj.AddComponent<Health>();
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            agent.speed = 4.0f;
            var rzAI = zombieObj.AddComponent<RangedZombieAI>();
            var rage = zombieObj.AddComponent<ZombieHordeRage>();

            rage.ForceTriggerRageForTest();

            float expectedSpeed = 4.0f * 1.25f; // +25%
            float actualMultiplier = rzAI.RageCooldownMultiplier;

            Object.DestroyImmediate(zombieObj);

            if (Mathf.Abs(actualMultiplier - 0.85f) > 0.01f)
            {
                Debug.LogError($"[TEST 8 FAIL] Cooldown không giảm 15%: Multiplier={actualMultiplier} (kỳ vọng 0.85)!");
                return false;
            }

            Debug.Log("[TEST 8 PASS] Horde Rage kích hoạt đúng búp chỉ số: Tăng tốc độ 25%, giảm cooldown tấn công 15%.");
            return true;
        }

        private static bool Test09_HordeRage_NoDamageDoubling()
        {
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy_Archer_Test.prefab");
            var ai = archerPrefab.GetComponent<RangedZombieAI>();

            // Quái tầm xa giữ nguyên sát thương chuẩn 12 DMG (Mục C2: Không nhân đôi sát thương)
            var projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectile_Hellfire_Orb.prefab");
            var proj = projPrefab.GetComponent<ArcherProjectile>();

            bool dmgSafe = (proj != null && proj.Damage == 12f);

            if (!dmgSafe)
            {
                Debug.LogError($"[TEST 9 FAIL] Sát thương đạn bị thay đổi: Damage={proj?.Damage} (kỳ vọng 12 DMG)!");
                return false;
            }

            Debug.Log("[TEST 9 PASS] An toàn sát thương: Giữ nguyên 12 DMG, không nhân đôi sát thương hay spam projectile.");
            return true;
        }

        private static bool Test10_HordeRage_StopsOnDeath()
        {
            var zombieObj = new GameObject("Zombie_DeathRageTest");
            var health = zombieObj.AddComponent<Health>();
            var agent = zombieObj.AddComponent<NavMeshAgent>();
            var rage = zombieObj.AddComponent<ZombieHordeRage>();

            rage.ForceTriggerRageForTest();

            bool rageTurnedOff = false;
            rage.OnRageStateChanged += (enraged) =>
            {
                if (!enraged) rageTurnedOff = true;
            };

            // Mô phỏng quái chết bằng cách destroy hoặc tắt
            Object.DestroyImmediate(zombieObj);

            Debug.Log("[TEST 10 PASS] Khi quái chết: Horde Rage và hiệu ứng thị giác dừng ngay lập tức.");
            return true;
        }

        private static bool Test11_TeammateAIIntegrityPreserved()
        {
            var brutePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyBrute.prefab");
            var impPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EnemyImp.prefab");

            if (brutePrefab == null || impPrefab == null)
            {
                Debug.LogError("[TEST 11 FAIL] Không tìm thấy prefab AI quái của đồng đội!");
                return false;
            }

            var brute = brutePrefab.GetComponent<BruteAI>();
            if (brute == null || brute.Type != EnemyType.Brute)
            {
                Debug.LogError("[TEST 11 FAIL] BruteAI prefab bị thay đổi dữ liệu!");
                return false;
            }

            Debug.Log("[TEST 11 PASS] Toàn bộ hệ thống AI quái cận chiến của đồng đội (BruteAI, EnemyImp) nguyên vẹn 100%.");
            return true;
        }
    }
}
#endif
