// Script: PlayerP1VerificationTests.cs
// Mục đích: Bộ kiểm thử tự động xác minh toàn diện hệ thống Asset, Quaternius CC0 Animation, Stamina, Crouch, Slide, AnimationSync cho Giai đoạn P1.
// Môi trường thực thi: Unity Editor.

#if UNITY_EDITOR
using System.IO;
using Hellfire.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hellfire.Editor
{
    public static class PlayerP1VerificationTests
    {
        [MenuItem("Hellfire/P1 Player Abilities/Run Verification Tests", false, 2)]
        public static bool RunAllTests()
        {
            Debug.Log("=================================================");
            Debug.Log("[P1-QA] BẮT ĐẦU KIỂM THỬ TOÀN DIỆN PLAYER ABILITIES P1");
            Debug.Log("=================================================");

            bool allPassed = true;

            allPassed &= TestAssetAndAnimationInventory();
            allPassed &= TestAnimatorControllerStructure();
            allPassed &= TestPlayerStamina();
            allPassed &= TestPlayerCrouchAndCeilingCheck();
            allPassed &= TestPlayerSlideMechanic();
            allPassed &= TestPlayerPrefabAndSceneStructure();

            if (allPassed)
            {
                Debug.Log("=================================================");
                Debug.Log("[P1-QA] KẾT QUẢ: TẤT CẢ 6 BỘ KIỂM THỬ P1 ĐỀU ĐẠT (PASS) 100%!");
                Debug.Log("=================================================");
            }
            else
            {
                Debug.LogError("[P1-QA] KẾT QUẢ: CÓ BÀI TEST THẤT BẠI (FAIL). VUI LÒNG KIỂM TRA LOGS!");
            }

            return allPassed;
        }

        private static bool TestAssetAndAnimationInventory()
        {
            Debug.Log("[Test 1] Kiểm tra kiểm kê Asset & Quaternius CC0 Animations...");

            // 1. Kiểm tra Model Soldier.FBX
            string soldierFbxPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Soldier.FBX";
            var soldierModel = AssetDatabase.LoadAssetAtPath<GameObject>(soldierFbxPath);
            if (soldierModel == null)
            {
                Debug.LogError("[Test 1 FAIL] Không tìm thấy Soldier.FBX tại: " + soldierFbxPath);
                return false;
            }

            // 2. Kiểm tra Source FBX Quaternius UAL 1 & 2
            string ual1Path = "Assets/_Project/Art/PlayerAbilities/Quaternius_UAL1.fbx";
            string ual2Path = "Assets/_Project/Art/PlayerAbilities/Quaternius_UAL2.fbx";
            if (!File.Exists(ual1Path) || !File.Exists(ual2Path))
            {
                Debug.LogError("[Test 1 FAIL] Thiếu Quaternius FBX sources trong Assets/_Project/Art/PlayerAbilities/");
                return false;
            }

            // 3. Kiểm tra các Animation clips thực tế
            string[] requiredClips = new[]
            {
                HellfireP1Setup.IdleClipPath,
                HellfireP1Setup.WalkClipPath,
                HellfireP1Setup.RunClipPath,
                HellfireP1Setup.SprintClipPath,
                HellfireP1Setup.QuaterniusCrouchIdlePath,
                HellfireP1Setup.QuaterniusCrouchWalkPath,
                HellfireP1Setup.QuaterniusSlidePath
            };

            foreach (var clipPath in requiredClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    // Thử load sub-asset nếu là FBX
                    var assets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
                    bool found = false;
                    foreach (var a in assets)
                    {
                        if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        Debug.LogError("[Test 1 FAIL] Không tìm thấy AnimationClip hợp lệ tại: " + clipPath);
                        return false;
                    }
                }
            }

            Debug.Log("[Test 1 PASS] Toàn bộ Animation clips (Idle, Walk, Run, Sprint, Quaternius Crouch Idle, Crouch Walk, Slide) đều hợp lệ!");
            return true;
        }

        private static bool TestAnimatorControllerStructure()
        {
            Debug.Log("[Test 2] Kiểm tra Animator Controller Player_Abilities_Controller...");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HellfireP1Setup.ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[Test 2 FAIL] Không tìm thấy Animator Controller tại: " + HellfireP1Setup.ControllerPath);
                return false;
            }

            // Kiểm tra parameters
            string[] requiredParams = new[] { "Speed", "IsCrouching", "IsSprinting", "IsSliding", "Slide", "OnGround" };
            foreach (var p in requiredParams)
            {
                bool exists = false;
                foreach (var param in controller.parameters)
                {
                    if (param.name == p) { exists = true; break; }
                }
                if (!exists)
                {
                    Debug.LogError("[Test 2 FAIL] Controller thiếu parameter: " + p);
                    return false;
                }
            }

            // Kiểm tra States
            var sm = controller.layers[0].stateMachine;
            bool hasLocomotion = false;
            bool hasCrouch = false;
            bool hasSlide = false;

            foreach (var state in sm.states)
            {
                if (state.state.name == "Locomotion") hasLocomotion = true;
                if (state.state.name == "Crouch") hasCrouch = true;
                if (state.state.name == "Slide") hasSlide = true;
            }

            if (!hasLocomotion || !hasCrouch || !hasSlide)
            {
                Debug.LogError($"[Test 2 FAIL] Controller thiếu states! Locomotion: {hasLocomotion}, Crouch: {hasCrouch}, Slide: {hasSlide}");
                return false;
            }

            Debug.Log("[Test 2 PASS] Animator Controller cấu trúc chuẩn xác với đầy đủ Parameters, Locomotion, Crouch và Slide states!");
            return true;
        }

        private static bool TestPlayerStamina()
        {
            Debug.Log("[Test 3] Kiểm tra logic PlayerStamina...");

            var go = new GameObject("TestStaminaObj");
            var stamina = go.AddComponent<PlayerStamina>();

            // 1. Kiểm tra ban đầu
            if (stamina.CurrentStamina < 99.9f || stamina.IsExhausted || !stamina.CanSprint || !stamina.CanSlide)
            {
                Debug.LogError("[Test 3 FAIL] Trạng thái thể lực ban đầu không chính xác!");
                Object.DestroyImmediate(go);
                return false;
            }

            // 2. Tiêu hao Sprint
            stamina.DrainSprintStamina(1.0f); // Tiêu hao 18 điểm
            if (stamina.CurrentStamina >= 100f)
            {
                Debug.LogError("[Test 3 FAIL] Tiêu hao Sprint không làm giảm thể lực!");
                Object.DestroyImmediate(go);
                return false;
            }

            // 3. Tiêu hao Slide
            bool slideConsumed = stamina.TryConsumeSlideCost(); // Tiêu hao 20 điểm
            if (!slideConsumed)
            {
                Debug.LogError("[Test 3 FAIL] Không thể tiêu hao thể lực Slide!");
                Object.DestroyImmediate(go);
                return false;
            }

            // 4. Tiêu hao cạn kiệt
            stamina.DrainSprintStamina(10.0f); // Drain hết
            if (!stamina.IsExhausted || stamina.CanSprint)
            {
                Debug.LogError("[Test 3 FAIL] Thể lực về 0 nhưng không kích hoạt Exhausted!");
                Object.DestroyImmediate(go);
                return false;
            }

            // 5. Reset thể lực
            stamina.ResetStamina();
            if (stamina.CurrentStamina < 99.9f || stamina.IsExhausted)
            {
                Debug.LogError("[Test 3 FAIL] ResetStamina không phục hồi đầy đủ!");
                Object.DestroyImmediate(go);
                return false;
            }

            Object.DestroyImmediate(go);
            Debug.Log("[Test 3 PASS] PlayerStamina vận hành chính xác (Drain, Consume, Exhaustion, Reset)!");
            return true;
        }

        private static bool TestPlayerCrouchAndCeilingCheck()
        {
            Debug.Log("[Test 4] Kiểm tra PlayerCrouch và kiểm tra trần nhà (Ceiling Clearance)...");

            var go = new GameObject("TestCrouchObj");
            go.transform.position = new Vector3(500f, 0f, 500f);
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            var crouch = go.AddComponent<PlayerCrouch>();

            // Khi ở không gian mở
            if (!crouch.HasCeilingClearance())
            {
                Debug.LogError("[Test 4 FAIL] Không gian trống nhưng HasCeilingClearance lại báo false!");
                Object.DestroyImmediate(go);
                return false;
            }

            // Tạo vật cản trần thấp 1.35m phía trên
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = go.transform.position + Vector3.up * 1.35f;
            ceiling.transform.localScale = new Vector3(2f, 0.2f, 2f);

            Physics.SyncTransforms();

            // Lúc này trên đầu có vật cản ở 1.35m (< 1.8m), đứng dậy sẽ bị va chạm
            bool hasClearance = crouch.HasCeilingClearance();
            if (hasClearance)
            {
                Debug.LogError("[Test 4 FAIL] Có trần thấp 1.35m nhưng HasCeilingClearance vẫn báo true!");
                Object.DestroyImmediate(ceiling);
                Object.DestroyImmediate(go);
                return false;
            }

            Object.DestroyImmediate(ceiling);
            Object.DestroyImmediate(go);

            Debug.Log("[Test 4 PASS] PlayerCrouch và SphereCast Ceiling Clearance chống đứng xuyên trần hoạt động chuẩn xác!");
            return true;
        }

        private static bool TestPlayerSlideMechanic()
        {
            Debug.Log("[Test 5] Kiểm tra PlayerSlide và cơ chế trượt né...");

            var go = new GameObject("TestSlideObj");
            go.AddComponent<CharacterController>();
            var stamina = go.AddComponent<PlayerStamina>();
            var crouch = go.AddComponent<PlayerCrouch>();
            var slide = go.AddComponent<PlayerSlide>();

            // Kiểm tra thông số thiết lập
            if (slide.SlideInitialSpeed < 8.0f || slide.SlideDuration <= 0.5f || slide.SlideCooldown <= 0.3f)
            {
                Debug.LogError("[Test 5 FAIL] Thông số Slide không hợp lệ!");
                Object.DestroyImmediate(go);
                return false;
            }

            // Kích hoạt Slide
            bool eventTriggered = false;
            slide.OnSlideStarted += () => eventTriggered = true;
            slide.StartSlide();

            if (!slide.IsSliding || !eventTriggered || !crouch.IsCrouching)
            {
                Debug.LogError("[Test 5 FAIL] StartSlide không kích hoạt trạng thái trượt hoặc không đồng bộ Crouch!");
                Object.DestroyImmediate(go);
                return false;
            }

            slide.StopSlide();
            if (slide.IsSliding)
            {
                Debug.LogError("[Test 5 FAIL] StopSlide không hủy trạng thái trượt!");
                Object.DestroyImmediate(go);
                return false;
            }

            Object.DestroyImmediate(go);
            Debug.Log("[Test 5 PASS] PlayerSlide vận hành chuẩn xác (StartSlide, Cooldown, StopSlide)!");
            return true;
        }

        private static bool TestPlayerPrefabAndSceneStructure()
        {
            Debug.Log("[Test 6] Kiểm tra Prefab Player_Abilities_Test và Scene TestPlayerAbilities...");

            // 1. Kiểm tra Prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HellfireP1Setup.TestPlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Test 6 FAIL] Không tìm thấy Prefab tại: " + HellfireP1Setup.TestPlayerPrefabPath);
                return false;
            }

            if (prefab.GetComponent<CharacterController>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu CharacterController!");
                return false;
            }

            if (prefab.GetComponent<PlayerMovement>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu PlayerMovement!");
                return false;
            }

            if (prefab.GetComponent<PlayerStamina>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu PlayerStamina!");
                return false;
            }

            if (prefab.GetComponent<PlayerCrouch>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu PlayerCrouch!");
                return false;
            }

            if (prefab.GetComponent<PlayerSlide>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu PlayerSlide!");
                return false;
            }

            if (prefab.GetComponent<PlayerAnimationSync>() == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab thiếu PlayerAnimationSync!");
                return false;
            }

            // Kiểm tra Animator trên mô hình thân nhân vật
            var anims = prefab.GetComponentsInChildren<Animator>(true);
            Animator bodyAnim = null;
            foreach (var a in anims)
            {
                if (a.runtimeAnimatorController != null)
                {
                    bodyAnim = a;
                    break;
                }
            }

            if (bodyAnim == null)
            {
                Debug.LogError("[Test 6 FAIL] Prefab không có Animator được gán runtimeAnimatorController trên mô hình!");
                return false;
            }

            if (bodyAnim.applyRootMotion)
            {
                Debug.LogError("[Test 6 FAIL] Animator không được bật applyRootMotion (phải bằng false để In-Place)!");
                return false;
            }

            // 2. Kiểm tra Scene
            if (!File.Exists(HellfireP1Setup.TestScenePath))
            {
                Debug.LogError("[Test 6 FAIL] Không tìm thấy Scene tại: " + HellfireP1Setup.TestScenePath);
                return false;
            }

            Debug.Log("[Test 6 PASS] Prefab Player_Abilities_Test và Scene TestPlayerAbilities đạt 100% tiêu chuẩn cấu trúc!");
            return true;
        }
    }
}
#endif
