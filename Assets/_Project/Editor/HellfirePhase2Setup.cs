// Script: HellfirePhase2Setup.cs
// Mục đích: Editor Utility tự động thiết lập toàn bộ Weapon ScriptableObjects, Player Combat Prefab, Imp Enemy Prefab, và Scene Gameplay cho Giai đoạn 2.
// Môi trường thực thi: Editor-only.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Hellfire.Combat;
using Hellfire.Enemy;
using Hellfire.Networking;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hellfire.Editor
{
    public static class HellfirePhase2Setup
    {
        private const string WeaponDataDir = "Assets/_Project/Data/Weapons";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string SceneDir = "Assets/_Project/Scenes";

        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ImpPrefabPath = "Assets/_Project/Prefabs/EnemyImp.prefab";
        private const string NetworkManagerPrefabPath = "Assets/_Project/Prefabs/NetworkManager.prefab";
        private const string GameManagerPrefabPath = "Assets/_Project/Prefabs/GameManager.prefab";
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";

        [MenuItem("Hellfire/Phase 2/Setup All (Weapons, Player Combat, Imp Enemy, Gameplay Scene)", false, 1)]
        public static void SetupAllPhase2()
        {
            EnsureDirectories();

            // 1. Tạo 4 WeaponData ScriptableObjects
            var weapons = CreateWeaponDataAssetsInternal();

            // 2. Cập nhật Player.prefab với Health, PlayerRevive, WeaponController, Hitboxes
            var playerPrefab = UpdatePlayerPrefabWithCombat(weapons);

            // 3. Tạo EnemyImp.prefab
            var impPrefab = CreateImpEnemyPrefabInternal();

            // 4. Cập nhật NetworkManager.prefab đăng ký EnemyImp
            UpdateNetworkManagerPrefabInternal(playerPrefab, impPrefab);

            // 5. Cập nhật Scene Gameplay với HUD đầy đủ và 4 Imp thử nghiệm
            UpdateGameplaySceneInternal(impPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Hellfire - Thiết lập Giai đoạn 2 (Chiến đấu Hitscan & Máu)",
                "Đã hoàn thành thiết lập Giai đoạn 2:\n\n" +
                "1. Tạo 4 WeaponData ScriptableObjects: Pistol (15 DMG), SMG (8 DMG), Shotgun (6x8 DMG), Rifle (35 DMG).\n" +
                "2. Cập nhật Player Prefab: Health (100 HP), WeaponController (Hitscan, Đổi súng 1-4, Nạp đạn R), PlayerRevive (Giữ E 3s ở 2m), Hitboxes (Head x2, Torso x1, Limb x0.75).\n" +
                "3. Tạo Quái vật thử nghiệm Imp Prefab (30 HP, 3 vùng Hitbox, Thanh máu overhead).\n" +
                "4. Đăng ký EnemyImp vào NetworkConfig.Prefabs.\n" +
                "5. Cập nhật Gameplay Scene: HUD Máu/Đạn/Hitmarker/Downed/Revive và đặt 4 Imp trong Arena để test.",
                "OK"
            );
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(WeaponDataDir)) Directory.CreateDirectory(WeaponDataDir);
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        }

        public static List<WeaponData> CreateWeaponDataAssetsInternal()
        {
            EnsureDirectories();
            var list = new List<WeaponData>();

            // 1. Súng lục (Pistol) — Mục 6.2
            string pistolPath = $"{WeaponDataDir}/Pistol_Data.asset";
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponData>(pistolPath);
            if (pistol == null)
            {
                pistol = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(pistol, pistolPath);
            }
            var soPistol = new SerializedObject(pistol);
            soPistol.FindProperty("_weaponName").stringValue = "Súng lục (Pistol)";
            soPistol.FindProperty("_weaponId").stringValue = "pistol";
            soPistol.FindProperty("_baseDamage").intValue = 15;
            soPistol.FindProperty("_headMultiplier").floatValue = 2.0f;
            soPistol.FindProperty("_torsoMultiplier").floatValue = 1.0f;
            soPistol.FindProperty("_limbMultiplier").floatValue = 0.75f;
            soPistol.FindProperty("_fireRate").floatValue = 0.25f;
            soPistol.FindProperty("_range").floatValue = 100.0f;
            soPistol.FindProperty("_pelletCount").intValue = 1;
            soPistol.FindProperty("_spread").floatValue = 0.012f;
            soPistol.FindProperty("_isAutomatic").boolValue = false;
            soPistol.FindProperty("_magSize").intValue = 12;
            soPistol.FindProperty("_reloadTime").floatValue = 1.2f;
            soPistol.ApplyModifiedProperties();
            list.Add(pistol);

            // 2. Súng máy (SMG) — Mục 6.2
            string smgPath = $"{WeaponDataDir}/SMG_Data.asset";
            var smg = AssetDatabase.LoadAssetAtPath<WeaponData>(smgPath);
            if (smg == null)
            {
                smg = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(smg, smgPath);
            }
            var soSmg = new SerializedObject(smg);
            soSmg.FindProperty("_weaponName").stringValue = "Súng máy (SMG)";
            soSmg.FindProperty("_weaponId").stringValue = "smg";
            soSmg.FindProperty("_baseDamage").intValue = 8;
            soSmg.FindProperty("_headMultiplier").floatValue = 2.0f;
            soSmg.FindProperty("_torsoMultiplier").floatValue = 1.0f;
            soSmg.FindProperty("_limbMultiplier").floatValue = 0.75f;
            soSmg.FindProperty("_fireRate").floatValue = 0.08f;
            soSmg.FindProperty("_range").floatValue = 60.0f;
            soSmg.FindProperty("_pelletCount").intValue = 1;
            soSmg.FindProperty("_spread").floatValue = 0.035f;
            soSmg.FindProperty("_isAutomatic").boolValue = true;
            soSmg.FindProperty("_magSize").intValue = 30;
            soSmg.FindProperty("_reloadTime").floatValue = 1.5f;
            soSmg.ApplyModifiedProperties();
            list.Add(smg);

            // 3. Shotgun — Mục 6.2
            string shotgunPath = $"{WeaponDataDir}/Shotgun_Data.asset";
            var shotgun = AssetDatabase.LoadAssetAtPath<WeaponData>(shotgunPath);
            if (shotgun == null)
            {
                shotgun = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(shotgun, shotgunPath);
            }
            var soShotgun = new SerializedObject(shotgun);
            soShotgun.FindProperty("_weaponName").stringValue = "Shotgun";
            soShotgun.FindProperty("_weaponId").stringValue = "shotgun";
            soShotgun.FindProperty("_baseDamage").intValue = 6;
            soShotgun.FindProperty("_headMultiplier").floatValue = 2.0f;
            soShotgun.FindProperty("_torsoMultiplier").floatValue = 1.0f;
            soShotgun.FindProperty("_limbMultiplier").floatValue = 0.75f;
            soShotgun.FindProperty("_fireRate").floatValue = 0.9f;
            soShotgun.FindProperty("_range").floatValue = 15.0f;
            soShotgun.FindProperty("_pelletCount").intValue = 8;
            soShotgun.FindProperty("_spread").floatValue = 0.08f;
            soShotgun.FindProperty("_isAutomatic").boolValue = false;
            soShotgun.FindProperty("_magSize").intValue = 6;
            soShotgun.FindProperty("_reloadTime").floatValue = 2.2f;
            soShotgun.ApplyModifiedProperties();
            list.Add(shotgun);

            // 4. Súng trường (Rifle) — Mục 6.2
            string riflePath = $"{WeaponDataDir}/Rifle_Data.asset";
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>(riflePath);
            if (rifle == null)
            {
                rifle = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(rifle, riflePath);
            }
            var soRifle = new SerializedObject(rifle);
            soRifle.FindProperty("_weaponName").stringValue = "Súng trường (Rifle)";
            soRifle.FindProperty("_weaponId").stringValue = "rifle";
            soRifle.FindProperty("_baseDamage").intValue = 35;
            soRifle.FindProperty("_headMultiplier").floatValue = 2.0f;
            soRifle.FindProperty("_torsoMultiplier").floatValue = 1.0f;
            soRifle.FindProperty("_limbMultiplier").floatValue = 0.75f;
            soRifle.FindProperty("_fireRate").floatValue = 0.6f;
            soRifle.FindProperty("_range").floatValue = 150.0f;
            soRifle.FindProperty("_pelletCount").intValue = 1;
            soRifle.FindProperty("_spread").floatValue = 0.01f;
            soRifle.FindProperty("_isAutomatic").boolValue = false;
            soRifle.FindProperty("_magSize").intValue = 10;
            soRifle.FindProperty("_reloadTime").floatValue = 1.8f;
            soRifle.ApplyModifiedProperties();
            list.Add(rifle);

            Debug.Log("[HellfirePhase2Setup] Đã tạo 4 WeaponData ScriptableObjects trong Assets/_Project/Data/Weapons/");
            return list;
        }

        public static GameObject UpdatePlayerPrefabWithCombat(List<WeaponData> weapons)
        {
            EnsureDirectories();

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject playerRoot;

            if (playerPrefab != null)
            {
                playerRoot = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            }
            else
            {
                playerRoot = HellfirePhase1Setup.CreatePlayerPrefabInternal();
                playerRoot = PrefabUtility.InstantiatePrefab(playerRoot) as GameObject;
            }

            playerRoot.tag = "Player";

            // 1. Health Component
            var health = playerRoot.GetComponent<Health>();
            if (health == null) health = playerRoot.AddComponent<Health>();
            var soHealth = new SerializedObject(health);
            soHealth.FindProperty("_maxHealth").floatValue = 100f;
            soHealth.FindProperty("_isPlayer").boolValue = true;
            soHealth.FindProperty("_downedDuration").floatValue = 60f;
            soHealth.FindProperty("_reviveHealthPercent").floatValue = 0.3f;
            soHealth.ApplyModifiedProperties();

            // 2. PlayerRevive Component
            var revive = playerRoot.GetComponent<PlayerRevive>();
            if (revive == null) revive = playerRoot.AddComponent<PlayerRevive>();

            // 3. HitboxRoot & Hitbox Colliders (Head, Torso, Limbs)
            var bodyMesh = playerRoot.transform.Find("PlayerBodyMesh");
            if (bodyMesh != null)
            {
                var bodyCol = bodyMesh.GetComponent<Collider>();
                if (bodyCol == null)
                {
                    var cap = bodyMesh.gameObject.AddComponent<CapsuleCollider>();
                    cap.radius = 0.35f;
                    cap.height = 1.6f;
                    cap.center = Vector3.zero;
                }
                var hitboxTorso = bodyMesh.GetComponent<HitboxIdentifier>();
                if (hitboxTorso == null) hitboxTorso = bodyMesh.gameObject.AddComponent<HitboxIdentifier>();
                hitboxTorso.SetHitboxType(HitboxType.Torso);
                hitboxTorso.SetDamageableTarget(health);
            }

            var cameraPivot = playerRoot.transform.Find("CameraPivot");
            if (cameraPivot != null)
            {
                var headCol = cameraPivot.GetComponent<Collider>();
                if (headCol == null)
                {
                    var sphere = cameraPivot.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = 0.25f;
                    sphere.center = Vector3.zero;
                }
                var hitboxHead = cameraPivot.GetComponent<HitboxIdentifier>();
                if (hitboxHead == null) hitboxHead = cameraPivot.gameObject.AddComponent<HitboxIdentifier>();
                hitboxHead.SetHitboxType(HitboxType.Head);
                hitboxHead.SetDamageableTarget(health);
            }

            // 4. WeaponController Component
            var weaponCtrl = playerRoot.GetComponent<WeaponController>();
            if (weaponCtrl == null) weaponCtrl = playerRoot.AddComponent<WeaponController>();

            var soWpn = new SerializedObject(weaponCtrl);
            if (weapons != null && weapons.Count > 0)
            {
                soWpn.FindProperty("_currentWeapon").objectReferenceValue = weapons[0];
                var availListProp = soWpn.FindProperty("_availableWeapons");
                availListProp.arraySize = weapons.Count;
                for (int i = 0; i < weapons.Count; i++)
                {
                    availListProp.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
                }
            }
            soWpn.ApplyModifiedProperties();

            // 5. AudioSource
            var audioSource = playerRoot.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = playerRoot.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            Object.DestroyImmediate(playerRoot);
            Debug.Log($"[HellfirePhase2Setup] Đã cập nhật Player.prefab với Combat & Hitboxes tại: {PlayerPrefabPath}");
            return savedPrefab;
        }

        public static GameObject CreateImpEnemyPrefabInternal()
        {
            EnsureDirectories();

            var impRoot = new GameObject("EnemyImp");
            impRoot.tag = "Enemy";

            // Netcode
            var netObj = impRoot.AddComponent<NetworkObject>();
            impRoot.AddComponent<NetworkTransform>();

            // Health (30 HP theo Mục 6.2)
            var health = impRoot.AddComponent<Health>();
            var soHealth = new SerializedObject(health);
            soHealth.FindProperty("_maxHealth").floatValue = 30f;
            soHealth.FindProperty("_isPlayer").boolValue = false;
            soHealth.FindProperty("_despawnDelay").floatValue = 3.0f;
            soHealth.ApplyModifiedProperties();

            // TestEnemyImp Script
            var impScript = impRoot.AddComponent<TestEnemyImp>();

            // Visual Container
            var visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(impRoot.transform, false);

            // Material đỏ cho Imp
            var redMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (redMat.shader == null) redMat = new Material(Shader.Find("Standard"));
            redMat.color = new Color(0.85f, 0.15f, 0.15f, 1f);

            // 1. Torso Collider & Mesh (HitboxType.Torso — Mục 3.2: x1.0)
            var torsoObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torsoObj.name = "Torso";
            torsoObj.transform.SetParent(visualObj.transform, false);
            torsoObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            torsoObj.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
            torsoObj.GetComponent<Renderer>().material = redMat;
            var torsoHitbox = torsoObj.AddComponent<HitboxIdentifier>();
            torsoHitbox.SetHitboxType(HitboxType.Torso);
            torsoHitbox.SetDamageableTarget(health);

            // 2. Head Collider & Mesh (HitboxType.Head — Mục 3.2: x2.0)
            var headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headObj.name = "Head";
            headObj.transform.SetParent(visualObj.transform, false);
            headObj.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            headObj.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var darkRedMat = new Material(redMat);
            darkRedMat.color = new Color(0.5f, 0.05f, 0.05f, 1f);
            headObj.GetComponent<Renderer>().material = darkRedMat;
            var headHitbox = headObj.AddComponent<HitboxIdentifier>();
            headHitbox.SetHitboxType(HitboxType.Head);
            headHitbox.SetDamageableTarget(health);

            // 3. Limbs Collider & Mesh (HitboxType.Limb — Mục 3.2: x0.75)
            var limbsObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            limbsObj.name = "Limbs";
            limbsObj.transform.SetParent(visualObj.transform, false);
            limbsObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            limbsObj.transform.localScale = new Vector3(0.7f, 0.5f, 0.4f);
            limbsObj.GetComponent<Renderer>().material = redMat;
            var limbHitbox = limbsObj.AddComponent<HitboxIdentifier>();
            limbHitbox.SetHitboxType(HitboxType.Limb);
            limbHitbox.SetDamageableTarget(health);

            // 4. Overhead World-space Canvas
            var canvasObj = new GameObject("OverheadCanvas");
            canvasObj.transform.SetParent(impRoot.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 50f);
            rect.localScale = Vector3.one * 0.01f;

            // Name Text
            var nameTextObj = new GameObject("EnemyNameText");
            nameTextObj.transform.SetParent(canvasObj.transform, false);
            var nameTmp = nameTextObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "Quỷ nhỏ (Imp) [30/30]";
            nameTmp.fontSize = 18;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.yellow;
            var nameRect = nameTextObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(200f, 25f);
            nameRect.anchoredPosition = new Vector2(0f, 12f);

            // Health Slider
            var slider = CreateOverheadSlider("HealthSlider", canvasObj.transform, 30f);

            // Wire Imp Script properties
            var soImp = new SerializedObject(impScript);
            soImp.FindProperty("_maxHealth").floatValue = 30f;
            soImp.FindProperty("_enemyName").stringValue = "Quỷ nhỏ (Imp)";
            soImp.FindProperty("_health").objectReferenceValue = health;
            soImp.FindProperty("_overheadNameText").objectReferenceValue = nameTmp;
            soImp.FindProperty("_overheadHealthSlider").objectReferenceValue = slider;
            soImp.FindProperty("_visualRoot").objectReferenceValue = visualObj.transform;
            soImp.FindProperty("_meshRenderer").objectReferenceValue = torsoObj.GetComponent<Renderer>();
            soImp.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(impRoot, ImpPrefabPath);
            Object.DestroyImmediate(impRoot);
            Debug.Log($"[HellfirePhase2Setup] Đã tạo EnemyImp Prefab tại: {ImpPrefabPath}");
            return prefab;
        }

        public static void UpdateNetworkManagerPrefabInternal(GameObject playerPrefab, GameObject impPrefab)
        {
            var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            if (nmPrefab == null) return;

            var nmObj = PrefabUtility.InstantiatePrefab(nmPrefab) as GameObject;
            var nm = nmObj.GetComponent<NetworkManager>();

            if (playerPrefab != null)
            {
                nm.NetworkConfig.PlayerPrefab = playerPrefab;
            }

            if (impPrefab != null)
            {
                if (!nm.NetworkConfig.Prefabs.Contains(impPrefab))
                {
                    nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = impPrefab });
                }
            }

            PrefabUtility.SaveAsPrefabAsset(nmObj, NetworkManagerPrefabPath);
            Object.DestroyImmediate(nmObj);
            Debug.Log("[HellfirePhase2Setup] Đã đăng ký EnemyImp vào NetworkConfig.Prefabs trên NetworkManager.prefab");
        }

        public static void UpdateGameplaySceneInternal(GameObject impPrefab)
        {
            EnsureDirectories();
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            // 1. Cập nhật HUDCanvas với đầy đủ component giao diện chiến đấu
            var canvasObj = GameObject.Find("HUDCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("HUDCanvas");
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            var hudObj = GameObject.Find("GameplayHUD");
            if (hudObj == null)
            {
                hudObj = new GameObject("GameplayHUD");
                hudObj.transform.SetParent(canvasObj.transform, false);
            }
            var hud = hudObj.GetComponent<GameplayHUD>();
            if (hud == null) hud = hudObj.AddComponent<GameplayHUD>();

            // Crosshair & Hitmarker
            var crosshairObj = GameObject.Find("Crosshair");
            if (crosshairObj == null)
            {
                crosshairObj = new GameObject("Crosshair");
                crosshairObj.transform.SetParent(canvasObj.transform, false);
                var img = crosshairObj.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.85f);
                var rect = crosshairObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(6f, 6f);
                rect.anchoredPosition = Vector2.zero;
            }

            var hitmarkerObj = GameObject.Find("Hitmarker");
            if (hitmarkerObj == null)
            {
                hitmarkerObj = new GameObject("Hitmarker");
                hitmarkerObj.transform.SetParent(canvasObj.transform, false);
                var img = hitmarkerObj.AddComponent<Image>();
                img.color = Color.white;
                var rect = hitmarkerObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(16f, 16f);
                rect.anchoredPosition = Vector2.zero;
                hitmarkerObj.SetActive(false);
            }

            // Bottom-Left Health Display
            var healthPanel = CreateHUDPanel("HealthPanel", canvasObj.transform, new Vector2(250f, 70f), new Vector2(160f, 60f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var healthSlider = CreateHUDSlider("HealthSlider", healthPanel.transform, 100f, new Vector2(0f, -10f), new Vector2(220f, 20f));
            var healthText = CreateHUDText("HealthText", healthPanel.transform, "HP: 100 / 100", 20, FontStyles.Bold, new Vector2(0f, 15f), Color.green);

            // Bottom-Right Weapon & Ammo Display
            var ammoPanel = CreateHUDPanel("AmmoPanel", canvasObj.transform, new Vector2(280f, 80f), new Vector2(-170f, 65f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var weaponNameText = CreateHUDText("WeaponNameText", ammoPanel.transform, "Súng lục (Pistol)", 18, FontStyles.Bold, new Vector2(0f, 20f), Color.white);
            var ammoText = CreateHUDText("AmmoText", ammoPanel.transform, "ĐẠN: 12 / 12", 24, FontStyles.Bold, new Vector2(0f, -10f), Color.yellow);
            var reloadText = CreateHUDText("ReloadPromptText", ammoPanel.transform, "ĐANG NẠP ĐẠN...", 16, FontStyles.Italic, new Vector2(0f, -32f), Color.cyan);
            reloadText.gameObject.SetActive(false);

            // Center Revive Prompt & Progress Bar
            var revivePanel = CreateHUDPanel("RevivePromptPanel", canvasObj.transform, new Vector2(350f, 80f), new Vector2(0f, -120f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var reviveText = CreateHUDText("RevivePromptText", revivePanel.transform, "Giữ [E] để cứu đồng đội", 20, FontStyles.Bold, new Vector2(0f, 15f), Color.cyan);
            var reviveBar = CreateHUDSlider("ReviveProgressBar", revivePanel.transform, 3.0f, new Vector2(0f, -15f), new Vector2(280f, 18f));
            revivePanel.SetActive(false);

            // Downed Screen Overlay Panel
            var downedPanel = CreateHUDPanel("DownedOverlayPanel", canvasObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            downedPanel.GetComponent<Image>().color = new Color(0.6f, 0f, 0f, 0.45f);
            var downedText = CreateHUDText("DownedTimerText", downedPanel.transform, "BẠN ĐANG GỤC! CHỜ CỨU... (60s)", 32, FontStyles.Bold, new Vector2(0f, 150f), Color.yellow);
            downedPanel.SetActive(false);

            // Wire Serialized Object on GameplayHUD
            var soHud = new SerializedObject(hud);
            soHud.FindProperty("_crosshairObject").objectReferenceValue = crosshairObj;
            soHud.FindProperty("_crosshairImage").objectReferenceValue = crosshairObj.GetComponent<Image>();
            soHud.FindProperty("_hitmarkerImage").objectReferenceValue = hitmarkerObj.GetComponent<Image>();
            soHud.FindProperty("_healthSlider").objectReferenceValue = healthSlider;
            soHud.FindProperty("_healthText").objectReferenceValue = healthText;
            soHud.FindProperty("_downedOverlayPanel").objectReferenceValue = downedPanel;
            soHud.FindProperty("_downedTimerText").objectReferenceValue = downedText;
            soHud.FindProperty("_weaponNameText").objectReferenceValue = weaponNameText;
            soHud.FindProperty("_ammoText").objectReferenceValue = ammoText;
            soHud.FindProperty("_reloadPromptText").objectReferenceValue = reloadText;
            soHud.FindProperty("_revivePromptPanel").objectReferenceValue = revivePanel;
            soHud.FindProperty("_revivePromptText").objectReferenceValue = reviveText;
            soHud.FindProperty("_reviveProgressBar").objectReferenceValue = reviveBar;
            soHud.ApplyModifiedProperties();

            // 2. Đặt 4 Quái vật Imp thử nghiệm trong Arena để người chơi test bắn
            var existingImps = GameObject.FindObjectsByType<TestEnemyImp>(FindObjectsSortMode.None);
            foreach (var existing in existingImps)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            if (impPrefab != null)
            {
                Vector3[] spawnPositions = new Vector3[]
                {
                    new Vector3(0f, 0f, 10f),
                    new Vector3(-8f, 0f, 12f),
                    new Vector3(8f, 0f, 12f),
                    new Vector3(0f, 0f, 18f)
                };

                for (int i = 0; i < spawnPositions.Length; i++)
                {
                    var imp = PrefabUtility.InstantiatePrefab(impPrefab) as GameObject;
                    imp.name = $"TestEnemyImp_{i + 1}";
                    imp.transform.position = spawnPositions[i];
                    imp.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            Debug.Log("[HellfirePhase2Setup] Đã cập nhật Scene Gameplay với HUD đầy đủ và 4 Imp thử nghiệm!");
        }

        private static GameObject CreateHUDPanel(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent, false);
            var img = panelObj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.08f, 0.1f, 0.85f);
            var rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return panelObj;
        }

        private static TextMeshProUGUI CreateHUDText(string name, Transform parent, string text, float fontSize, FontStyles style, Vector2 pos, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 40f);
            rect.anchoredPosition = pos;
            return tmp;
        }

        private static Slider CreateHUDSlider(string name, Transform parent, float maxVal, Vector2 pos, Vector2 size)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = size;
            sliderRect.anchoredPosition = pos;

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = maxVal;
            slider.value = maxVal;

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.85f, 0.2f, 1f);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Slider CreateOverheadSlider(string name, Transform parent, float maxVal)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(180f, 15f);
            sliderRect.anchoredPosition = new Vector2(0f, -10f);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = maxVal;
            slider.value = maxVal;

            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            var fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }
    }
}
#endif
