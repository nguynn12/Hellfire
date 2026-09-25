// Script: HellfirePhase3Setup.cs
// Mục đích: Công cụ tự động thiết lập toàn bộ Prefab quái vật, Boss, DungeonManager và Scene Gameplay cho Giai đoạn 3.
// Môi trường thực thi: Unity Editor.

#if UNITY_EDITOR
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Editor
{
    public static class HellfirePhase3Setup
    {
        private const string PrefabsPath = "Assets/_Project/Prefabs";
        private const string ScenesPath = "Assets/_Project/Scenes";
        private const string MaterialsPath = "Assets/_Project/Materials";

        [MenuItem("Hellfire/Phase 3/Setup All (Dungeon Generator, NavMesh Baker, Enemy & Boss Prefabs)", false, 20)]
        public static void SetupAllPhase3()
        {
            EnsureDirectories();

            var impPrefab = CreateOrUpdateEnemyPrefab("EnemyImp", EnemyType.Imp, 30f, 5f, 10f, 1.8f, 1.0f, new Color(0.85f, 0.2f, 0.2f), new Vector3(0.8f, 1.2f, 0.8f));
            var archerPrefab = CreateOrUpdateArcherPrefab("EnemyArcher", 25f, 3.5f, 12f, new Color(0.6f, 0.2f, 0.8f), new Vector3(0.8f, 1.6f, 0.8f));
            var brutePrefab = CreateOrUpdateBrutePrefab("EnemyBrute", 120f, 2.5f, 25f, new Color(0.3f, 0.5f, 0.2f), new Vector3(1.6f, 2.2f, 1.6f));
            var bossPrefab = CreateOrUpdateBossPrefab("EnemyBossHellfireLord", 800f, 3.5f, 30f, new Color(0.9f, 0.4f, 0.1f), new Vector3(2.5f, 3.5f, 2.5f), impPrefab);

            // Đăng ký vào NetworkPrefabs
            RegisterNetworkPrefab(impPrefab);
            RegisterNetworkPrefab(archerPrefab);
            RegisterNetworkPrefab(brutePrefab);
            RegisterNetworkPrefab(bossPrefab);

            // Cập nhật scene Gameplay.unity
            SetupGameplayScene(impPrefab, archerPrefab, brutePrefab, bossPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Hellfire - Giai đoạn 3",
                "Thiết lập Giai đoạn 3 thành công!\n\n" +
                "- Prefabs tạo mới: Imp (30 HP), Archer (25 HP), Brute (120 HP), Boss Hellfire Lord (800 HP).\n" +
                "- DungeonManager & NavMeshSurface đã tích hợp vào scene Gameplay.\n" +
                "- Tự động đồng bộ Seed qua mạng và sinh hầm ngục + bake NavMesh.",
                "OK");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsPath)) Directory.CreateDirectory(PrefabsPath);
            if (!Directory.Exists(MaterialsPath)) Directory.CreateDirectory(MaterialsPath);
        }

        private static GameObject CreateOrUpdateEnemyPrefab(string name, EnemyType type, float hp, float speed, float damage, float attackRange, float cooldown, Color skinColor, Vector3 scale)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            var root = new GameObject(name);

            // 1. NetworkObject & NetworkTransform
            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            // 2. NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.stoppingDistance = attackRange * 0.8f;
            agent.radius = scale.x * 0.4f;
            agent.height = scale.y;

            // 3. Health
            var health = root.AddComponent<Health>();
            var hpProp = typeof(Health).GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpProp != null) hpProp.SetValue(health, hp);

            // 4. Mesh model
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            body.transform.localScale = scale;

            var renderer = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = skinColor;
            renderer.sharedMaterial = mat;

            // 5. Hitbox colliders
            DestroyImmediate(body.GetComponent<Collider>());
            SetupHitboxColliders(root, scale);

            // 6. FSM
            var fsm = root.AddComponent<EnemyStateMachine>();
            SetFsmFields(fsm, type, speed, damage, attackRange, cooldown);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrUpdateArcherPrefab(string name, float hp, float speed, float damage, Color skinColor, Vector3 scale)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            var root = new GameObject(name);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.stoppingDistance = 12f;
            agent.radius = scale.x * 0.4f;
            agent.height = scale.y;

            var health = root.AddComponent<Health>();
            var hpProp = typeof(Health).GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpProp != null) hpProp.SetValue(health, hp);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            body.transform.localScale = scale;

            var renderer = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = skinColor;
            renderer.sharedMaterial = mat;

            DestroyImmediate(body.GetComponent<Collider>());
            SetupHitboxColliders(root, scale);

            var fsm = root.AddComponent<HellspawnArcherAI>();
            SetFsmFields(fsm, EnemyType.HellspawnArcher, speed, damage, 18f, 1.6f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrUpdateBrutePrefab(string name, float hp, float speed, float damage, Color skinColor, Vector3 scale)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            var root = new GameObject(name);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.stoppingDistance = 2.0f;
            agent.radius = scale.x * 0.45f;
            agent.height = scale.y;

            var health = root.AddComponent<Health>();
            var hpProp = typeof(Health).GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpProp != null) hpProp.SetValue(health, hp);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            body.transform.localScale = scale;

            var renderer = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = skinColor;
            renderer.sharedMaterial = mat;

            DestroyImmediate(body.GetComponent<Collider>());
            SetupHitboxColliders(root, scale);

            var fsm = root.AddComponent<BruteAI>();
            SetFsmFields(fsm, EnemyType.Brute, speed, damage, 2.8f, 2.0f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateOrUpdateBossPrefab(string name, float hp, float speed, float damage, Color skinColor, Vector3 scale, GameObject impMinionPrefab)
        {
            string path = $"{PrefabsPath}/{name}.prefab";
            var root = new GameObject(name);

            root.AddComponent<NetworkObject>();
            root.AddComponent<Unity.Netcode.Components.NetworkTransform>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.stoppingDistance = 3.5f;
            agent.radius = scale.x * 0.45f;
            agent.height = scale.y;

            var health = root.AddComponent<Health>();
            var hpProp = typeof(Health).GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpProp != null) hpProp.SetValue(health, hp);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            body.transform.localScale = scale;

            var renderer = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = skinColor;
            renderer.sharedMaterial = mat;

            DestroyImmediate(body.GetComponent<Collider>());
            SetupHitboxColliders(root, scale);

            var fsm = root.AddComponent<BossStateMachine>();
            SetFsmFields(fsm, EnemyType.BossHellfireLord, speed, damage, 4.0f, 1.8f);

            var impField = typeof(BossStateMachine).GetField("_impMinionPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (impField != null && impMinionPrefab != null) impField.SetValue(fsm, impMinionPrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetupHitboxColliders(GameObject root, Vector3 scale)
        {
            var hitboxesRoot = new GameObject("Hitboxes");
            hitboxesRoot.transform.SetParent(root.transform);
            hitboxesRoot.transform.localPosition = Vector3.zero;

            // Head (Sphere)
            var headObj = new GameObject("Hitbox_Head");
            headObj.transform.SetParent(hitboxesRoot.transform);
            headObj.transform.localPosition = new Vector3(0f, scale.y * 0.85f, 0f);
            var headCol = headObj.AddComponent<SphereCollider>();
            headCol.radius = scale.x * 0.35f;
            var headId = headObj.AddComponent<HitboxIdentifier>();
            var headTypeField = typeof(HitboxIdentifier).GetField("_hitboxType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (headTypeField != null) headTypeField.SetValue(headId, HitboxType.Head);

            // Torso (Capsule)
            var torsoObj = new GameObject("Hitbox_Torso");
            torsoObj.transform.SetParent(hitboxesRoot.transform);
            torsoObj.transform.localPosition = new Vector3(0f, scale.y * 0.5f, 0f);
            var torsoCol = torsoObj.AddComponent<CapsuleCollider>();
            torsoCol.radius = scale.x * 0.4f;
            torsoCol.height = scale.y * 0.6f;
            var torsoId = torsoObj.AddComponent<HitboxIdentifier>();
            var torsoTypeField = typeof(HitboxIdentifier).GetField("_hitboxType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (torsoTypeField != null) torsoTypeField.SetValue(torsoId, HitboxType.Torso);

            // Limbs (Box)
            var limbsObj = new GameObject("Hitbox_Limbs");
            limbsObj.transform.SetParent(hitboxesRoot.transform);
            limbsObj.transform.localPosition = new Vector3(0f, scale.y * 0.15f, 0f);
            var limbsCol = limbsObj.AddComponent<BoxCollider>();
            limbsCol.size = new Vector3(scale.x * 0.7f, scale.y * 0.3f, scale.z * 0.7f);
            var limbsId = limbsObj.AddComponent<HitboxIdentifier>();
            var limbsTypeField = typeof(HitboxIdentifier).GetField("_hitboxType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (limbsTypeField != null) limbsTypeField.SetValue(limbsId, HitboxType.Limb);
        }

        private static void SetFsmFields(EnemyStateMachine fsm, EnemyType type, float speed, float damage, float range, float cooldown)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(EnemyStateMachine).GetField("_enemyType", flags)?.SetValue(fsm, type);
            typeof(EnemyStateMachine).GetField("_moveSpeed", flags)?.SetValue(fsm, speed);
            typeof(EnemyStateMachine).GetField("_attackDamage", flags)?.SetValue(fsm, damage);
            typeof(EnemyStateMachine).GetField("_attackRange", flags)?.SetValue(fsm, range);
            typeof(EnemyStateMachine).GetField("_attackCooldown", flags)?.SetValue(fsm, cooldown);
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
        }

        private static void SetupGameplayScene(GameObject imp, GameObject archer, GameObject brute, GameObject boss)
        {
            string scenePath = $"{ScenesPath}/Gameplay.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Kiểm tra hoặc tạo DungeonManager
            var dungeonManagerObj = GameObject.Find("DungeonManager");
            if (dungeonManagerObj == null)
            {
                dungeonManagerObj = new GameObject("DungeonManager");
            }

            if (dungeonManagerObj.GetComponent<NetworkObject>() == null)
            {
                dungeonManagerObj.AddComponent<NetworkObject>();
            }

            var generator = dungeonManagerObj.GetComponent<DungeonGenerator>() ?? dungeonManagerObj.AddComponent<DungeonGenerator>();
            var surface = dungeonManagerObj.GetComponent<NavMeshSurface>() ?? dungeonManagerObj.AddComponent<NavMeshSurface>();
            var baker = dungeonManagerObj.GetComponent<RuntimeNavMeshBaker>() ?? dungeonManagerObj.AddComponent<RuntimeNavMeshBaker>();
            var manager = dungeonManagerObj.GetComponent<DungeonManager>() ?? dungeonManagerObj.AddComponent<DungeonManager>();
            var spawner = dungeonManagerObj.GetComponent<DungeonEnemySpawner>() ?? dungeonManagerObj.AddComponent<DungeonEnemySpawner>();

            // Cấu hình spawner
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(DungeonEnemySpawner).GetField("_impPrefab", flags)?.SetValue(spawner, imp);
            typeof(DungeonEnemySpawner).GetField("_archerPrefab", flags)?.SetValue(spawner, archer);
            typeof(DungeonEnemySpawner).GetField("_brutePrefab", flags)?.SetValue(spawner, brute);
            typeof(DungeonEnemySpawner).GetField("_bossPrefab", flags)?.SetValue(spawner, boss);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
