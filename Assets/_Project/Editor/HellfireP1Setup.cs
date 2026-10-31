// Script: HellfireP1Setup.cs
// Mục đích: Công cụ thiết lập tự động cho P1 Player Abilities:
//           - Animator Controller riêng với animation Quaternius CC0 thật (Crouch Idle, Crouch Walk, Slide Loop) và Soldier Locomotion.
//           - Prefab độc lập: Player_Abilities_Test.prefab (không sửa Animator hay Prefab gốc của nhóm).
//           - Scene kiểm thử độc lập: TestPlayerAbilities.unity với sàn, hầm trần thấp (1.35m) và dốc trượt.
// Môi trường thực thi: Unity Editor.

#if UNITY_EDITOR
using System.IO;
using Hellfire.Combat;
using Hellfire.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hellfire.Editor
{
    public static class HellfireP1Setup
    {
        public const string ControllerPath = "Assets/_Project/Art/PlayerAbilities/Player_Abilities_Controller.controller";
        public const string TestPlayerPrefabPath = "Assets/_Project/Prefabs/Player_Abilities_Test.prefab";
        public const string TestScenePath = "Assets/_Project/Scenes/TestPlayerAbilities.unity";

        // Asset paths
        public const string IdleClipPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Anims_Inplace/Soldier@Rifle_Idle.FBX";
        public const string WalkClipPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Anims_Inplace/Soldier@Rifle_Walk_F.FBX";
        public const string RunClipPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Anims_Inplace/Soldier@Rifle_Run_F.FBX";
        public const string SprintClipPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Anims_Inplace/Soldier@Rifle_Sprint_F.FBX";
        public const string SoldierPrefabPath = "Assets/SciFi_Space_Soldier/Resources/Space_Soldier/Meshes/Soldier.FBX";

        // Quaternius CC0 Animation Paths
        public const string QuaterniusCrouchIdlePath = "Assets/_Project/Art/PlayerAbilities/Animations/anim_quaternius_Crouch_Idle_Loop.anim";
        public const string QuaterniusCrouchWalkPath = "Assets/_Project/Art/PlayerAbilities/Animations/anim_quaternius_Crouch_Fwd_Loop.anim";
        public const string QuaterniusSlidePath = "Assets/_Project/Art/PlayerAbilities/Animations/anim_quaternius_Slide_Loop.anim";

        [MenuItem("Hellfire/P1 Player Abilities/Setup All (Controller, Prefab & Test Scene)", false, 1)]
        public static void SetupAll()
        {
            if (EditorApplication.isPlaying) return;

            EnsureDirectories();
            var controller = CreateOrUpdateAnimatorController();
            var playerPrefab = CreateOrUpdateTestPlayerPrefab(controller);
            CreateTestScene(playerPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HellfireP1Setup] THÀNH CÔNG: Đã thiết lập hoàn tất Player_Abilities_Controller, Player_Abilities_Test.prefab và TestPlayerAbilities.unity!");
        }

        public static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/PlayerAbilities")) AssetDatabase.CreateFolder("Assets/_Project/Art", "PlayerAbilities");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/PlayerAbilities/Animations")) AssetDatabase.CreateFolder("Assets/_Project/Art/PlayerAbilities", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes")) AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
        }

        public static AnimatorController CreateOrUpdateAnimatorController()
        {
            EnsureDirectories();

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // 1. Thêm parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Slide", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("OnGround", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Load clips
            var idleClip = LoadFirstClip(IdleClipPath);
            var walkClip = LoadFirstClip(WalkClipPath);
            var runClip = LoadFirstClip(RunClipPath);
            var sprintClip = LoadFirstClip(SprintClipPath);

            // Quaternius CC0 clips
            var crouchIdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusCrouchIdlePath);
            var crouchWalkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusCrouchWalkPath);
            var slideClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusSlidePath);

            // 2. Tạo Locomotion Blend Tree
            var locomotionState = rootStateMachine.AddState("Locomotion", new Vector3(250, 0, 0));
            var locomotionTree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };
            if (idleClip != null) locomotionTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) locomotionTree.AddChild(walkClip, 2.5f);
            if (runClip != null) locomotionTree.AddChild(runClip, 4.5f);
            if (sprintClip != null) locomotionTree.AddChild(sprintClip, 7.0f);
            locomotionState.motion = locomotionTree;
            rootStateMachine.defaultState = locomotionState;

            // 3. Tạo Crouch Blend Tree
            var crouchState = rootStateMachine.AddState("Crouch", new Vector3(250, 120, 0));
            var crouchTree = new BlendTree
            {
                name = "CrouchBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };
            if (crouchIdleClip != null) crouchTree.AddChild(crouchIdleClip, 0.0f);
            if (crouchWalkClip != null) crouchTree.AddChild(crouchWalkClip, 2.0f);
            crouchState.motion = crouchTree;

            // 4. Tạo Slide State
            var slideState = rootStateMachine.AddState("Slide", new Vector3(550, 60, 0));
            if (slideClip != null) slideState.motion = slideClip;

            // 5. Thiết lập Transitions
            // Locomotion -> Crouch
            var toCrouch = locomotionState.AddTransition(crouchState);
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.15f;
            toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            // Crouch -> Locomotion
            var toLocomotion = crouchState.AddTransition(locomotionState);
            toLocomotion.hasExitTime = false;
            toLocomotion.duration = 0.15f;
            toLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            // Locomotion -> Slide
            var locToSlide = locomotionState.AddTransition(slideState);
            locToSlide.hasExitTime = false;
            locToSlide.duration = 0.1f;
            locToSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");

            // Crouch -> Slide
            var crouchToSlide = crouchState.AddTransition(slideState);
            crouchToSlide.hasExitTime = false;
            crouchToSlide.duration = 0.1f;
            crouchToSlide.AddCondition(AnimatorConditionMode.If, 0, "Slide");

            // Slide -> Locomotion
            var slideToLoc = slideState.AddTransition(locomotionState);
            slideToLoc.hasExitTime = true;
            slideToLoc.exitTime = 0.85f;
            slideToLoc.duration = 0.2f;
            slideToLoc.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            // Slide -> Crouch
            var slideToCrouch = slideState.AddTransition(crouchState);
            slideToCrouch.hasExitTime = true;
            slideToCrouch.exitTime = 0.85f;
            slideToCrouch.duration = 0.2f;
            slideToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            return null;
        }

        public static GameObject CreateOrUpdateTestPlayerPrefab(AnimatorController controller)
        {
            // Base từ Player_CombatJuice_Test hoặc Player.prefab để giữ nguyên hệ thống vũ khí viewmodel
            string basePrefabPath = "Assets/_Project/Prefabs/Player_CombatJuice_Test.prefab";
            if (!File.Exists(basePrefabPath))
            {
                basePrefabPath = "Assets/_Project/Prefabs/Player.prefab";
            }

            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            GameObject instance;

            if (basePrefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = "Player_Abilities_Test";
            }
            else
            {
                instance = new GameObject("Player_Abilities_Test");
                instance.AddComponent<NetworkObject>();
                var cc = instance.AddComponent<CharacterController>();
                cc.radius = 0.35f;
                cc.height = 1.8f;
                cc.center = new Vector3(0, 0.9f, 0);
            }

            // 1. Thêm các components P1 độc lập
            var stamina = instance.GetComponent<PlayerStamina>() ?? instance.AddComponent<PlayerStamina>();
            var crouch = instance.GetComponent<PlayerCrouch>() ?? instance.AddComponent<PlayerCrouch>();
            var slide = instance.GetComponent<PlayerSlide>() ?? instance.AddComponent<PlayerSlide>();
            var animSync = instance.GetComponent<PlayerAnimationSync>() ?? instance.AddComponent<PlayerAnimationSync>();
            var staminaHUD = instance.GetComponent<PlayerStaminaHUD>() ?? instance.AddComponent<PlayerStaminaHUD>();
            var existingCrouchSlide = instance.GetComponent<PlayerCrouchSlide>();
            if (existingCrouchSlide != null) Object.DestroyImmediate(existingCrouchSlide);

            // 2. Gắn mô hình 3D Space_Soldier_A vào PlayerBodyMesh
            var bodyMeshTransform = instance.transform.Find("PlayerBodyMesh");
            if (bodyMeshTransform != null)
            {
                // Ẩn visual mesh của capsule cũ (không xóa Collider/MeshFilter để giữ HitboxIdentifier)
                var meshRenderer = bodyMeshTransform.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = false;

                var existingSoldier = bodyMeshTransform.Find("Space_Soldier_A");
                if (existingSoldier != null)
                {
                    Object.DestroyImmediate(existingSoldier.gameObject);
                }

                var soldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefabPath);
                if (soldierPrefab != null)
                {
                    var soldierInstance = Object.Instantiate(soldierPrefab);
                    soldierInstance.name = "Space_Soldier_A";
                    soldierInstance.transform.SetParent(bodyMeshTransform, false);

                    // PlayerBodyMesh là Capsule gốc có localPosition=(0, 0.9, 0) và localScale=(0.7, 0.9, 0.7).
                    // Để chân Soldier chạm đúng mặt đất (Root Y=0) và tỷ lệ 1:1 không bị biến dạng mà không sửa CapsuleCollider gốc:
                    Vector3 parentScale = bodyMeshTransform.localScale;
                    Vector3 parentPos = bodyMeshTransform.localPosition;
                    float invX = Mathf.Abs(parentScale.x) > 0.001f ? 1f / parentScale.x : 1f;
                    float invY = Mathf.Abs(parentScale.y) > 0.001f ? 1f / parentScale.y : 1f;
                    float invZ = Mathf.Abs(parentScale.z) > 0.001f ? 1f / parentScale.z : 1f;

                    soldierInstance.transform.localScale = new Vector3(invX, invY, invZ);
                    soldierInstance.transform.localPosition = new Vector3(-parentPos.x * invX, -parentPos.y * invY, -parentPos.z * invZ);
                    soldierInstance.transform.localRotation = Quaternion.identity;

                    // Gán layer PlayerBody cho toàn bộ cây con Space_Soldier_A để FPS Camera cull chuẩn xác, 3rd Person Camera thấy rõ
                    int playerBodyLayer = bodyMeshTransform.gameObject.layer;
                    foreach (var t in soldierInstance.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = playerBodyLayer;
                    }

                    var anim = soldierInstance.GetComponent<Animator>();
                    if (anim == null) anim = soldierInstance.AddComponent<Animator>();
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    var soAnimSync = new SerializedObject(animSync);
                    var animProp = soAnimSync.FindProperty("_animator");
                    if (animProp != null)
                    {
                        animProp.objectReferenceValue = anim;
                        soAnimSync.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // 3. Đảm bảo CameraPivot tồn tại
            var cameraPivot = instance.transform.Find("CameraPivot");
            if (cameraPivot == null)
            {
                var pivotObj = new GameObject("CameraPivot");
                pivotObj.transform.SetParent(instance.transform, false);
                pivotObj.transform.localPosition = new Vector3(0, 1.6f, 0);
                var camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(pivotObj.transform, false);
                camObj.AddComponent<Camera>();
            }

            // Lưu thành Prefab P1 độc lập
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TestPlayerPrefabPath);
            Object.DestroyImmediate(instance);

            return savedPrefab;
        }

        public static void CreateTestScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1.0f, 0.95f, 0.85f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Floor Plane
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Arena";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(8f, 1f, 8f); // 80x80m

            // 3. Low Obstacle Tunnels (Kiểm thử Crouch & Slide chống xuyên trần)
            CreateTunnelObstacle("CrouchTunnel_Low_1.35m", new Vector3(0, 0, 10), new Vector3(8, 0.4f, 6), 1.35f);
            CreateTunnelObstacle("SlideTunnel_ExtraLow_1.1m", new Vector3(12, 0, 10), new Vector3(8, 0.4f, 6), 1.1f);

            // 4. Ramps (Kiểm thử Slide dốc)
            CreateRamp("TestRamp", new Vector3(-12, 0, 12), new Vector3(6, 1, 10), 18f);

            // 5. Target Dummy để kiểm tra bắn súng/Recoil khi Crouch/Slide
            var dummyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyObj.name = "TargetDummy";
            dummyObj.transform.position = new Vector3(0f, 1f, 25f);
            dummyObj.tag = "Enemy";
            dummyObj.AddComponent<OfflineDummyHealth>();
            dummyObj.AddComponent<TargetDummyFeedback>();

            // 6. NetworkManager & LocalTestHostBootstrap
            string netManagerPrefabPath = "Assets/_Project/Prefabs/NetworkManager.prefab";
            var netManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(netManagerPrefabPath);
            if (netManagerPrefab != null)
            {
                var netMgr = (GameObject)PrefabUtility.InstantiatePrefab(netManagerPrefab);
                netMgr.name = "NetworkManager";
                var nm = netMgr.GetComponent<NetworkManager>();
                if (nm != null)
                {
                    // Đặt PlayerPrefab = null trong Scene Test để tránh NGO tự spawn thêm 1 bản sao đè lên Player_Abilities_Test đặt sẵn trong Scene
                    nm.NetworkConfig.PlayerPrefab = null;
                }
                if (netMgr.GetComponent<LocalTestHostBootstrap>() == null)
                {
                    netMgr.AddComponent<LocalTestHostBootstrap>();
                }
            }

            // 7. Spawn Player Instance
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player_Abilities_Test";
                player.transform.position = new Vector3(0f, 0.1f, -5f);

                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var cam = player.GetComponentInChildren<Camera>(true);
                if (cam != null) cam.gameObject.SetActive(true);
            }

            EditorSceneManager.SaveScene(scene, TestScenePath);
        }

        private static void CreateTunnelObstacle(string name, Vector3 center, Vector3 size, float ceilingHeight)
        {
            var root = new GameObject(name);
            root.transform.position = center;

            // Mái trần (Ceiling)
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(root.transform, false);
            ceiling.transform.localPosition = new Vector3(0, ceilingHeight + (size.y * 0.5f), 0);
            ceiling.transform.localScale = size;

            // Cột chống trái
            var leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "LeftPillar";
            leftPillar.transform.SetParent(root.transform, false);
            leftPillar.transform.localPosition = new Vector3(-size.x * 0.5f, ceilingHeight * 0.5f, 0);
            leftPillar.transform.localScale = new Vector3(0.5f, ceilingHeight, size.z);

            // Cột chống phải
            var rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "RightPillar";
            rightPillar.transform.SetParent(root.transform, false);
            rightPillar.transform.localPosition = new Vector3(size.x * 0.5f, ceilingHeight * 0.5f, 0);
            rightPillar.transform.localScale = new Vector3(0.5f, ceilingHeight, size.z);
        }

        private static void CreateRamp(string name, Vector3 position, Vector3 size, float angle)
        {
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.position = position;
            ramp.transform.rotation = Quaternion.Euler(angle, 0, 0);
            ramp.transform.localScale = size;
        }
    }
}
#endif
