#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class RangedZombieOfficialSetup
    {
        public const string OfficialPrefabPath = "Assets/_Project/Prefabs/EnemyRangedZombie.prefab";
        public const string ArcherMaterialPath = "Assets/_Project/Materials/M_ArcherZombie_Test.mat";
        public const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";

        [MenuItem("Hellfire/AI/Setup Official EnemyRangedZombie Prefab", false, 20)]
        public static void EnsureOfficialSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OfficialPrefabPath);
            if (prefab == null)
            {
                prefab = CreateOfficialPrefab();
            }

            if (prefab != null)
            {
                RegisterToNetworkPrefabs(prefab);
                AssignToGameplayScene(prefab);
            }

            AssetDatabase.SaveAssets();
        }

        public static GameObject CreateOfficialPrefab()
        {
            var root = new GameObject("EnemyRangedZombie");

            // 1. NetworkObject & NetworkTransform (Server Authority = 0, Interpolate = 1)
            var netObj = root.AddComponent<NetworkObject>();
            var netTrans = root.AddComponent<NetworkTransform>();
            netTrans.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTrans.Interpolate = true;

            // 2. NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.stoppingDistance = 10f;
            agent.radius = 0.35f;
            agent.height = 1.6f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;

            // 3. Health (25 HP, IsPlayer = false)
            var health = root.AddComponent<Health>();
            SetField(health, typeof(Health), "_baseMaxHealth", 25f);
            SetField(health, typeof(Health), "_isPlayer", false);

            // 4. Model (Capsule without collider)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var archerMat = AssetDatabase.LoadAssetAtPath<Material>(ArcherMaterialPath);
            var rend = body.GetComponent<MeshRenderer>();
            if (rend != null && archerMat != null) rend.sharedMaterial = archerMat;

            // 5. Visor (Eye cube without collider)
            var eyeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyeObj.name = "Eye_Visor";
            eyeObj.transform.SetParent(root.transform);
            eyeObj.transform.localPosition = new Vector3(0f, 1.4f, 0.35f);
            eyeObj.transform.localScale = new Vector3(0.4f, 0.12f, 0.15f);
            Object.DestroyImmediate(eyeObj.GetComponent<Collider>());
            var eyeRend = eyeObj.GetComponent<MeshRenderer>();
            if (eyeRend != null && archerMat != null) eyeRend.sharedMaterial = archerMat;

            // 6. Floating Label: ZOMBIE TẦM XA
            var labelObj = new GameObject("Label_Floating");
            labelObj.transform.SetParent(root.transform);
            labelObj.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            labelObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = "ZOMBIE TẦM XA";
            textMesh.fontSize = 36;
            textMesh.characterSize = 0.12f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(1f, 0.85f, 0.1f, 1f);

            // 7. Hitboxes (Head x2.0, Torso x1.0, Limb x0.75)
            SetupHitboxes(root, new Vector3(0.8f, 1.6f, 0.8f), health);

            // 8. Visuals & AI
            var visuals = root.AddComponent<RangedZombieVisuals>();
            var ai = root.AddComponent<RangedZombieAI>();

            SetField(ai, typeof(EnemyStateMachine), "_enemyType", EnemyType.HellspawnArcher);
            SetField(ai, typeof(EnemyStateMachine), "_moveSpeed", 3.5f);
            SetField(ai, typeof(EnemyStateMachine), "_attackDamage", 12f);
            SetField(ai, typeof(EnemyStateMachine), "_attackRange", 18f);
            SetField(ai, typeof(EnemyStateMachine), "_attackCooldown", 1.6f);
            SetField(ai, typeof(EnemyStateMachine), "_detectionRange", 25f);
            SetField(ai, typeof(EnemyStateMachine), "_soulsReward", 8);

            SetField(ai, typeof(RangedZombieAI), "_minComfortDistance", 10f);
            SetField(ai, typeof(RangedZombieAI), "_maxCombatDistance", 20f);
            SetField(ai, typeof(RangedZombieAI), "_chargeDuration", 0.45f);
            SetField(ai, typeof(RangedZombieAI), "_visuals", visuals);

            // Save Prefab
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, OfficialPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[RangedZombieOfficialSetup] Đã tạo prefab chính thức {OfficialPrefabPath}");
            return savedPrefab;
        }

        private static void SetField(object target, System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogError($"[RangedZombieOfficialSetup] Field {fieldName} not found on {type.Name}!");
            }
        }

        private static void SetupHitboxes(GameObject root, Vector3 scale, Health health)
        {
            var hitboxesRoot = new GameObject("Hitboxes");
            hitboxesRoot.transform.SetParent(root.transform);
            hitboxesRoot.transform.localPosition = Vector3.zero;

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
            torsoCol.direction = 1;
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

        public static void RegisterToNetworkPrefabs(GameObject prefab)
        {
            if (prefab == null) return;

            string[] guids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(assetPath);
                if (prefabsList != null && !prefabsList.Contains(prefab))
                {
                    prefabsList.Add(new NetworkPrefab { Prefab = prefab });
                    EditorUtility.SetDirty(prefabsList);
                    Debug.Log($"[RangedZombieOfficialSetup] Đã đăng ký {prefab.name} vào {assetPath}");
                }
            }

            string nmPath = "Assets/_Project/Prefabs/NetworkManager.prefab";
            var nmAsset = AssetDatabase.LoadAssetAtPath<GameObject>(nmPath);
            if (nmAsset != null)
            {
                var nmObj = PrefabUtility.InstantiatePrefab(nmAsset) as GameObject;
                if (nmObj != null)
                {
                    var nm = nmObj.GetComponent<NetworkManager>();
                    if (nm != null && !nm.NetworkConfig.Prefabs.Contains(prefab))
                    {
                        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                        PrefabUtility.SaveAsPrefabAsset(nmObj, nmPath);
                        Debug.Log($"[RangedZombieOfficialSetup] Đã đăng ký {prefab.name} vào {nmPath}");
                    }
                    Object.DestroyImmediate(nmObj);
                }
            }
        }

        public static void AssignToGameplayScene(GameObject prefab)
        {
            if (prefab == null) return;

            if (!File.Exists(GameplayScenePath)) return;

            var currentScene = EditorSceneManager.GetActiveScene();
            bool needRestore = currentScene.path != GameplayScenePath;

            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var spawner = Object.FindAnyObjectByType<DungeonEnemySpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                var rangedProp = so.FindProperty("_rangedZombiePrefab");

                bool changed = false;
                if (rangedProp != null && rangedProp.objectReferenceValue == null)
                {
                    rangedProp.objectReferenceValue = prefab;
                    changed = true;
                }

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[RangedZombieOfficialSetup] Đã gán _rangedZombiePrefab vào DungeonEnemySpawner trong Gameplay.unity");
                }
            }

            if (needRestore && !string.IsNullOrEmpty(currentScene.path) && File.Exists(currentScene.path))
            {
                EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
            }
        }
    }
}
#endif
