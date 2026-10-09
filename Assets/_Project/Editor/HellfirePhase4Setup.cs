// Script: HellfirePhase4Setup.cs
// Mục đích: Editor Utility tự động thiết lập toàn bộ Bùa lợi (Power-up), Rương vật phẩm (Loot Chest), Game Loop Overlays (Victory, GameOver, Pause, Shield) cho Giai đoạn 4.
// Môi trường thực thi: Unity Editor only.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Hellfire.AI;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Hellfire.Items;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hellfire.Editor
{
    public static class HellfirePhase4Setup
    {
        private const string PowerUpDataDir = "Assets/_Project/Data/PowerUps";
        private const string WeaponDataDir = "Assets/_Project/Data/Weapons";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string SceneDir = "Assets/_Project/Scenes";

        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string BossPrefabPath = "Assets/_Project/Prefabs/EnemyBossHellfireLord.prefab";
        private const string NetworkManagerPrefabPath = "Assets/_Project/Prefabs/NetworkManager.prefab";
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";

        private const string ItemPickupPrefabPath = "Assets/_Project/Prefabs/ItemPickup.prefab";
        private const string LootChestPrefabPath = "Assets/_Project/Prefabs/LootChest.prefab";
        private const string BossLootChestPrefabPath = "Assets/_Project/Prefabs/BossLootChest.prefab";

        [MenuItem("Hellfire/Phase 4/Setup All (Power-ups, Loot Chests, Game Loop & UI Overlays)", false, 1)]
        public static void SetupAllPhase4()
        {
            EnsureDirectories();

            // 1. Tạo 4 PowerUpData ScriptableObjects
            var powerUps = CreatePowerUpDataAssetsInternal();

            // 2. Tải 4 WeaponData ScriptableObjects
            var weapons = LoadAllWeaponDataAssets();

            // 3. Tạo ItemPickup.prefab
            var itemPickupPrefab = CreateItemPickupPrefabInternal(powerUps, weapons);

            // 4. Tạo LootChest.prefab & BossLootChest.prefab
            var normalChestPrefab = CreateLootChestPrefabInternal("LootChest", false, powerUps, weapons, itemPickupPrefab);
            var bossChestPrefab = CreateLootChestPrefabInternal("BossLootChest", true, powerUps, weapons, itemPickupPrefab);

            // 5. Cập nhật Player.prefab với PlayerBuffManager
            UpdatePlayerPrefabInternal();

            // 6. Cập nhật Boss.prefab với BossChestPrefab
            UpdateBossPrefabInternal(bossChestPrefab);

            // 7. Đăng ký các Prefabs vào NetworkManager
            RegisterPrefabsToNetworkManagerInternal(itemPickupPrefab, normalChestPrefab, bossChestPrefab);

            // 8. Cập nhật Scene Gameplay với UI Victory/GameOver/Shield và cấu hình DungeonEnemySpawner
            UpdateGameplaySceneInternal(normalChestPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Hellfire - Hoàn thiện Giai đoạn 4 (Vật phẩm, Rương & Vòng lặp Game)",
                    "Đã hoàn thành thiết lập toàn bộ Giai đoạn 4 (MVP Final Phase):\n\n" +
                    "1. 4 Bùa lợi ScriptableObjects: Max Health Up (+25 HP & hồi đầy), Swift Boots (+20% Spd), Berserker Charm (+30% DMG), Guardian Shield (8s Bất tử).\n" +
                    "2. Prefab ItemPickup (Hiển thị 3D xoay, phát sáng, Text nhãn vật phẩm, đồng bộ qua mạng).\n" +
                    "3. 2 Rương vật phẩm (Rương thường 40% bùa / 60% súng, Rương Boss 100% bùa + 50% súng).\n" +
                    "4. PlayerBuffManager tích hợp vào Player Prefab.\n" +
                    "5. BossHellfireLord tự động rơi Boss Chest và kích hoạt Victory khi chết.\n" +
                    "6. DungeonEnemySpawner tự sinh Loot Chest trong các phòng thường.\n" +
                    "7. GameplayHUD hoàn thiện với Màn hình Chiến thắng (Victory), Thất bại (Game Over), Đếm ngược Khiên bảo vệ, và Pause Menu.\n" +
                    "8. Đăng ký toàn bộ Prefabs mới vào NetworkManager.",
                    "OK"
                );
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PowerUpDataDir)) Directory.CreateDirectory(PowerUpDataDir);
            if (!Directory.Exists(WeaponDataDir)) Directory.CreateDirectory(WeaponDataDir);
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        }

        public static List<PowerUpData> CreatePowerUpDataAssetsInternal()
        {
            EnsureDirectories();
            var list = new List<PowerUpData>();

            // 1. Max Health Up (Trái tim máu lớn - Mục 6.2)
            string hpPath = $"{PowerUpDataDir}/MaxHealthUp_Data.asset";
            var hpData = AssetDatabase.LoadAssetAtPath<PowerUpData>(hpPath);
            if (hpData == null)
            {
                hpData = ScriptableObject.CreateInstance<PowerUpData>();
                AssetDatabase.CreateAsset(hpData, hpPath);
            }
            var soHp = new SerializedObject(hpData);
            soHp.FindProperty("_powerUpType").enumValueIndex = (int)PowerUpType.MaxHealthUp;
            soHp.FindProperty("_powerUpName").stringValue = "Trái Tim Máu Lớn";
            soHp.FindProperty("_description").stringValue = "+25 HP tối đa, hồi đầy máu ngay khi nhặt";
            soHp.FindProperty("_value").floatValue = 25f;
            soHp.FindProperty("_duration").floatValue = 0f;
            soHp.FindProperty("_pickupColor").colorValue = new Color(0.1f, 0.9f, 0.3f);
            soHp.ApplyModifiedProperties();
            list.Add(hpData);

            // 2. Swift Boots (Giày tốc độ - Mục 6.2)
            string speedPath = $"{PowerUpDataDir}/SwiftBoots_Data.asset";
            var speedData = AssetDatabase.LoadAssetAtPath<PowerUpData>(speedPath);
            if (speedData == null)
            {
                speedData = ScriptableObject.CreateInstance<PowerUpData>();
                AssetDatabase.CreateAsset(speedData, speedPath);
            }
            var soSpeed = new SerializedObject(speedData);
            soSpeed.FindProperty("_powerUpType").enumValueIndex = (int)PowerUpType.SwiftBoots;
            soSpeed.FindProperty("_powerUpName").stringValue = "Giày Tốc Độ";
            soSpeed.FindProperty("_description").stringValue = "+20% tốc độ di chuyển đi/chạy/lùi";
            soSpeed.FindProperty("_value").floatValue = 0.20f;
            soSpeed.FindProperty("_duration").floatValue = 0f;
            soSpeed.FindProperty("_pickupColor").colorValue = new Color(0.1f, 0.8f, 1.0f);
            soSpeed.ApplyModifiedProperties();
            list.Add(speedData);

            // 3. Berserker Charm (Bùa sát thương - Mục 6.2)
            string dmgPath = $"{PowerUpDataDir}/BerserkerCharm_Data.asset";
            var dmgData = AssetDatabase.LoadAssetAtPath<PowerUpData>(dmgPath);
            if (dmgData == null)
            {
                dmgData = ScriptableObject.CreateInstance<PowerUpData>();
                AssetDatabase.CreateAsset(dmgData, dmgPath);
            }
            var soDmg = new SerializedObject(dmgData);
            soDmg.FindProperty("_powerUpType").enumValueIndex = (int)PowerUpType.BerserkerCharm;
            soDmg.FindProperty("_powerUpName").stringValue = "Bùa Cuồng Nộ";
            soDmg.FindProperty("_description").stringValue = "+30% sát thương súng gây ra";
            soDmg.FindProperty("_value").floatValue = 0.30f;
            soDmg.FindProperty("_duration").floatValue = 0f;
            soDmg.FindProperty("_pickupColor").colorValue = new Color(1.0f, 0.2f, 0.2f);
            soDmg.ApplyModifiedProperties();
            list.Add(dmgData);

            // 4. Guardian Shield (Khiên tạm thời - Mục 6.2)
            string shieldPath = $"{PowerUpDataDir}/GuardianShield_Data.asset";
            var shieldData = AssetDatabase.LoadAssetAtPath<PowerUpData>(shieldPath);
            if (shieldData == null)
            {
                shieldData = ScriptableObject.CreateInstance<PowerUpData>();
                AssetDatabase.CreateAsset(shieldData, shieldPath);
            }
            var soShield = new SerializedObject(shieldData);
            soShield.FindProperty("_powerUpType").enumValueIndex = (int)PowerUpType.GuardianShield;
            soShield.FindProperty("_powerUpName").stringValue = "Khiên Bảo Vệ";
            soShield.FindProperty("_description").stringValue = "Miễn nhiễm sát thương hoàn toàn trong 8 giây";
            soShield.FindProperty("_value").floatValue = 0f;
            soShield.FindProperty("_duration").floatValue = 8.0f;
            soShield.FindProperty("_pickupColor").colorValue = new Color(1.0f, 0.9f, 0.1f);
            soShield.ApplyModifiedProperties();
            list.Add(shieldData);

            Debug.Log("[HellfirePhase4Setup] Đã tạo 4 PowerUpData ScriptableObjects thành công!");
            return list;
        }

        private static List<WeaponData> LoadAllWeaponDataAssets()
        {
            var list = new List<WeaponData>();
            string[] guids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDataDir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (weapon != null) list.Add(weapon);
            }
            return list;
        }

        public static GameObject CreateItemPickupPrefabInternal(List<PowerUpData> powerUps, List<WeaponData> weapons)
        {
            var root = new GameObject("ItemPickup");

            // NetworkObject
            root.AddComponent<NetworkObject>();

            // Trigger Collider
            var collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.0f;

            // AudioSource
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;

            // Visual Mesh (Diamond/Sphere)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "PickupMesh";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            // Point Light
            var lightObj = new GameObject("PickupLight");
            lightObj.transform.SetParent(root.transform);
            lightObj.transform.localPosition = Vector3.zero;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 1.5f;

            // World Space TextMeshPro
            var textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(root.transform);
            textObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = "Vật phẩm";
            tmp.fontSize = 3.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 2f);

            // ItemPickup Component
            var pickup = root.AddComponent<ItemPickup>();
            var soPickup = new SerializedObject(pickup);

            var dbPropPowerUps = soPickup.FindProperty("_powerUpDatabase");
            dbPropPowerUps.ClearArray();
            for (int i = 0; i < powerUps.Count; i++)
            {
                dbPropPowerUps.InsertArrayElementAtIndex(i);
                dbPropPowerUps.GetArrayElementAtIndex(i).objectReferenceValue = powerUps[i];
            }

            var dbPropWeapons = soPickup.FindProperty("_weaponDatabase");
            dbPropWeapons.ClearArray();
            for (int i = 0; i < weapons.Count; i++)
            {
                dbPropWeapons.InsertArrayElementAtIndex(i);
                dbPropWeapons.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }

            soPickup.FindProperty("_meshRenderer").objectReferenceValue = visual.GetComponent<MeshRenderer>();
            soPickup.FindProperty("_pointLight").objectReferenceValue = light;
            soPickup.FindProperty("_nameLabel").objectReferenceValue = tmp;
            soPickup.FindProperty("_audioSource").objectReferenceValue = audioSource;
            soPickup.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPickupPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[HellfirePhase4Setup] Đã tạo Prefab {ItemPickupPrefabPath}");
            return prefab;
        }

        public static GameObject CreateLootChestPrefabInternal(string name, bool isBossChest, List<PowerUpData> powerUps, List<WeaponData> weapons, GameObject itemPickupPrefab)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var root = new GameObject(name);

            // NetworkObject
            root.AddComponent<NetworkObject>();

            // Box Collider
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(1.2f, 0.8f, 0.8f);

            // AudioSource
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;

            // Chest Base
            var chestBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestBase.name = "ChestBase";
            chestBase.transform.SetParent(root.transform);
            chestBase.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            chestBase.transform.localScale = new Vector3(1.0f, 0.5f, 0.7f);
            Object.DestroyImmediate(chestBase.GetComponent<Collider>());
            var baseMat = new Material(Shader.Find("Standard"));
            baseMat.color = isBossChest ? new Color(0.6f, 0.1f, 0.5f) : new Color(0.45f, 0.25f, 0.1f);
            chestBase.GetComponent<Renderer>().material = baseMat;

            // Chest Lid Pivot & Mesh
            var lidPivot = new GameObject("LidPivot");
            lidPivot.transform.SetParent(root.transform);
            lidPivot.transform.localPosition = new Vector3(0f, 0.5f, 0.35f);

            var lidMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lidMesh.name = "LidMesh";
            lidMesh.transform.SetParent(lidPivot.transform);
            lidMesh.transform.localPosition = new Vector3(0f, 0.1f, -0.35f);
            lidMesh.transform.localScale = new Vector3(1.05f, 0.2f, 0.75f);
            Object.DestroyImmediate(lidMesh.GetComponent<Collider>());
            var lidMat = new Material(Shader.Find("Standard"));
            lidMat.color = isBossChest ? new Color(0.9f, 0.2f, 0.8f) : new Color(0.7f, 0.5f, 0.15f);
            lidMesh.GetComponent<Renderer>().material = lidMat;

            // Light
            var lightObj = new GameObject("ChestLight");
            lightObj.transform.SetParent(root.transform);
            lightObj.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.0f;
            light.intensity = 1.2f;
            light.color = isBossChest ? new Color(0.9f, 0.2f, 0.8f) : new Color(0.8f, 0.6f, 0.2f);

            // World Space Text Prompt
            var promptObj = new GameObject("PromptText");
            promptObj.transform.SetParent(root.transform);
            promptObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var promptTmp = promptObj.AddComponent<TextMeshPro>();
            promptTmp.text = isBossChest ? "[E] Mở Rương Boss" : "[E] Mở Rương";
            promptTmp.fontSize = 3.5f;
            promptTmp.alignment = TextAlignmentOptions.Center;
            promptTmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            promptObj.SetActive(false);

            // Spawn Point
            var spawnPoint = new GameObject("DropSpawnPoint");
            spawnPoint.transform.SetParent(root.transform);
            spawnPoint.transform.localPosition = new Vector3(0f, 0.8f, 0.6f);

            // LootChest Component
            var lootChest = root.AddComponent<LootChest>();
            var soChest = new SerializedObject(lootChest);
            soChest.FindProperty("_isBossChest").boolValue = isBossChest;
            soChest.FindProperty("_dropSpawnPoint").objectReferenceValue = spawnPoint.transform;
            soChest.FindProperty("_itemPickupPrefab").objectReferenceValue = itemPickupPrefab;
            soChest.FindProperty("_chestLid").objectReferenceValue = lidPivot.transform;
            soChest.FindProperty("_promptText").objectReferenceValue = promptTmp;
            soChest.FindProperty("_chestLight").objectReferenceValue = light;
            soChest.FindProperty("_audioSource").objectReferenceValue = audioSource;

            var poolPowerUps = soChest.FindProperty("_powerUpPool");
            poolPowerUps.ClearArray();
            for (int i = 0; i < powerUps.Count; i++)
            {
                poolPowerUps.InsertArrayElementAtIndex(i);
                poolPowerUps.GetArrayElementAtIndex(i).objectReferenceValue = powerUps[i];
            }

            var poolWeapons = soChest.FindProperty("_weaponPool");
            poolWeapons.ClearArray();
            for (int i = 0; i < weapons.Count; i++)
            {
                poolWeapons.InsertArrayElementAtIndex(i);
                poolWeapons.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }

            soChest.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[HellfirePhase4Setup] Đã tạo Prefab {path}");
            return prefab;
        }

        public static void UpdatePlayerPrefabInternal()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) return;

            var playerRoot = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (playerRoot == null) return;

            var buffManager = playerRoot.GetComponent<PlayerBuffManager>();
            if (buffManager == null)
            {
                buffManager = playerRoot.AddComponent<PlayerBuffManager>();
            }

            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            Object.DestroyImmediate(playerRoot);
            Debug.Log("[HellfirePhase4Setup] Đã tích hợp PlayerBuffManager vào Player.prefab");
        }

        public static void UpdateBossPrefabInternal(GameObject bossChestPrefab)
        {
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null) return;

            var bossRoot = PrefabUtility.InstantiatePrefab(bossPrefab) as GameObject;
            if (bossRoot == null) return;

            var bossFSM = bossRoot.GetComponent<BossStateMachine>();
            if (bossFSM != null)
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(BossStateMachine).GetField("_bossChestPrefab", flags)?.SetValue(bossFSM, bossChestPrefab);
            }

            PrefabUtility.SaveAsPrefabAsset(bossRoot, BossPrefabPath);
            Object.DestroyImmediate(bossRoot);
            Debug.Log("[HellfirePhase4Setup] Đã cấu hình BossChestPrefab cho EnemyBossHellfireLord.prefab");
        }

        public static void RegisterPrefabsToNetworkManagerInternal(params GameObject[] prefabs)
        {
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            if (nmPrefab == null) return;

            var nmObj = PrefabUtility.InstantiatePrefab(nmPrefab) as GameObject;
            var nm = nmObj.GetComponent<NetworkManager>();

            if (nm != null && nm.NetworkConfig != null)
            {
                var list = nm.NetworkConfig.Prefabs;
                foreach (var p in prefabs)
                {
                    if (p == null) continue;
                    bool exists = false;
                    for (int i = 0; i < list.Prefabs.Count; i++)
                    {
                        if (list.Prefabs[i].Prefab == p)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        var item = new NetworkPrefab { Prefab = p };
                        list.Add(item);
                        Debug.Log($"[HellfirePhase4Setup] Đã đăng ký {p.name} vào NetworkConfig.Prefabs");
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(nmObj, NetworkManagerPrefabPath);
            Object.DestroyImmediate(nmObj);
        }

        public static void UpdateGameplaySceneInternal(GameObject lootChestPrefab)
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            // 1. Cấu hình DungeonEnemySpawner với LootChestPrefab
            var dungeonManagerObj = GameObject.Find("DungeonManager");
            if (dungeonManagerObj != null)
            {
                var spawner = dungeonManagerObj.GetComponent<DungeonEnemySpawner>();
                if (spawner != null)
                {
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    typeof(DungeonEnemySpawner).GetField("_lootChestPrefab", flags)?.SetValue(spawner, lootChestPrefab);
                }
            }

            // 2. Cập nhật HUD Canvas với Overlays và Guardian Shield Indicator
            var canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                SetupHUDOverlays(canvas.transform);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HellfirePhase4Setup] Đã cập nhật Scene Gameplay với HUD đầy đủ và DungeonEnemySpawner!");
        }

        private static void SetupHUDOverlays(Transform canvasRoot)
        {
            // A. Shield Indicator Panel (Góc trên bên trái, dưới thanh máu)
            var shieldPanel = FindChild(canvasRoot, "ShieldIndicatorPanel");
            if (shieldPanel == null)
            {
                var panelObj = new GameObject("ShieldIndicatorPanel");
                panelObj.transform.SetParent(canvasRoot, false);
                var rect = panelObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(30f, -85f);
                rect.sizeDelta = new Vector2(250f, 35f);

                var img = panelObj.AddComponent<Image>();
                img.color = new Color(0.1f, 0.2f, 0.3f, 0.8f);

                var textObj = new GameObject("ShieldTimerText");
                textObj.transform.SetParent(panelObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = "🛡️ KHIÊN BẢO VỆ: 8.0s";
                tmp.fontSize = 16f;
                tmp.color = new Color(1f, 0.9f, 0.1f);
                tmp.alignment = TextAlignmentOptions.Center;

                panelObj.SetActive(false);
            }

            // B. Buff Toast Text (Thông báo nhận bùa ở giữa màn hình)
            var buffToast = FindChild(canvasRoot, "BuffToastText");
            if (buffToast == null)
            {
                var toastObj = new GameObject("BuffToastText");
                toastObj.transform.SetParent(canvasRoot, false);
                var rect = toastObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.7f);
                rect.anchorMax = new Vector2(0.5f, 0.7f);
                rect.sizeDelta = new Vector2(600f, 60f);

                var tmp = toastObj.AddComponent<TextMeshProUGUI>();
                tmp.text = "+25 MÁU TỐI ĐA!";
                tmp.fontSize = 24f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.cyan;

                toastObj.SetActive(false);
            }

            // C. Victory Overlay Panel (Chiến thắng)
            var victoryPanel = FindChild(canvasRoot, "VictoryOverlayPanel");
            if (victoryPanel == null)
            {
                victoryPanel = CreateOverlayPanel(canvasRoot, "VictoryOverlayPanel",
                    new Color(0.05f, 0.2f, 0.05f, 0.9f),
                    "CHIẾN THẮNG!",
                    "Chúa quỷ Hellfire đã bị tiêu diệt! Bạn đã giải cứu hầm ngục.",
                    "VictoryReturnButton",
                    "VictoryWaitingText",
                    new Color(0.2f, 0.8f, 0.3f)
                );
            }

            // D. GameOver Overlay Panel (Thất bại)
            var gameOverPanel = FindChild(canvasRoot, "GameOverOverlayPanel");
            if (gameOverPanel == null)
            {
                gameOverPanel = CreateOverlayPanel(canvasRoot, "GameOverOverlayPanel",
                    new Color(0.25f, 0.05f, 0.05f, 0.92f),
                    "THẤT BẠI!",
                    "Toàn bộ người chơi trong phòng đã gục ngã.",
                    "GameOverReturnButton",
                    "GameOverWaitingText",
                    new Color(0.9f, 0.2f, 0.2f)
                );
            }
        }

        private static GameObject CreateOverlayPanel(Transform parent, string name, Color bgColor, string title, string subtitle, string buttonName, string waitingName, Color accentColor)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent, false);

            var rect = panelObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var bgImg = panelObj.AddComponent<Image>();
            bgImg.color = bgColor;

            // Title
            var titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(panelObj.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.sizeDelta = new Vector2(800f, 80f);
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.fontSize = 54f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = accentColor;

            // Subtitle
            var subObj = new GameObject("SubtitleText");
            subObj.transform.SetParent(panelObj.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.58f);
            subRect.anchorMax = new Vector2(0.5f, 0.58f);
            subRect.sizeDelta = new Vector2(700f, 60f);
            var subTmp = subObj.AddComponent<TextMeshProUGUI>();
            subTmp.text = subtitle;
            subTmp.fontSize = 20f;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = Color.white;

            // Return Button (Host only)
            var btnObj = new GameObject(buttonName);
            btnObj.transform.SetParent(panelObj.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.35f);
            btnRect.anchorMax = new Vector2(0.5f, 0.35f);
            btnRect.sizeDelta = new Vector2(260f, 60f);
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = accentColor;
            var btn = btnObj.AddComponent<Button>();

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            var btnTmp = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "VỀ SẢNH (LOBBY)";
            btnTmp.fontSize = 18f;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.color = Color.black;
            btnTmp.alignment = TextAlignmentOptions.Center;

            // Waiting Text (Client only)
            var waitObj = new GameObject(waitingName);
            waitObj.transform.SetParent(panelObj.transform, false);
            var waitRect = waitObj.AddComponent<RectTransform>();
            waitRect.anchorMin = new Vector2(0.5f, 0.35f);
            waitRect.anchorMax = new Vector2(0.5f, 0.35f);
            waitRect.sizeDelta = new Vector2(400f, 50f);
            var waitTmp = waitObj.AddComponent<TextMeshProUGUI>();
            waitTmp.text = "Đang chờ Chủ phòng quay về sảnh...";
            waitTmp.fontSize = 18f;
            waitTmp.color = Color.yellow;
            waitTmp.alignment = TextAlignmentOptions.Center;
            waitObj.SetActive(false);

            panelObj.SetActive(false);
            return panelObj;
        }

        private static GameObject FindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName) return child.gameObject;
                var found = FindChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
