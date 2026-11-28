using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hellfire.EditorTools
{
    public static class PhaseOneIntegrationChecks
    {
        public static void VerifyAndBuild()
        {
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray();
                if (scenes.Length != 3 || !scenes[0].EndsWith("/MainMenu.unity"))
                    throw new InvalidOperationException("Unexpected playable scene list.");

                int missing = 0;
                foreach (string path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                        missing += ScanHierarchy(root, path);
                }
                string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] {
                    "Assets/_Project/Prefabs", "Assets/_Project/Art/WeaponDevelopment", "Assets/_Project/Art/GameplayVisuals",
                    "Assets/_Project/VFX/Prefabs"
                });
                foreach (string guid in prefabs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) missing += ScanHierarchy(prefab, path);
                }

                int badMaterials = 0;
                string[] materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" });
                foreach (string guid in materials)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader" ||
                        ShaderUtil.ShaderHasError(material.shader))
                    {
                        Debug.LogWarning("[PHASE1] Material needs review: " + path);
                        badMaterials++;
                    }
                }
                Debug.Log($"[PHASE1] ASSET_SCAN scenes={scenes.Length} prefabs={prefabs.Length} missingScripts={missing} materials={materials.Length} materialWarnings={badMaterials}");

                string output = Environment.GetEnvironmentVariable("HELLFIRE_QA_BUILD_PATH");
                if (string.IsNullOrEmpty(output)) output = "/private/tmp/hf_stabilization/HellfireStabilizationQA.app";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

                if (missing != 0 || badMaterials != 0)
                    throw new InvalidOperationException("Scene or prefab validation failed.");
                ValidateInfernalSprites();
                var lava = AssetDatabase.LoadAssetAtPath<Hellfire.Dungeon.DungeonArtSet>("Assets/_Project/Art/GameplayVisuals/Lava.asset");
                var ice = AssetDatabase.LoadAssetAtPath<Hellfire.Dungeon.DungeonArtSet>("Assets/_Project/Art/GameplayVisuals/Ice.asset");
                if (lava == null || ice == null || lava.AmbientVfx == null || ice.AmbientVfx == null ||
                    lava.AmbientVfx.name != "LavaEmber" || ice.AmbientVfx.name != "FrozenMist")
                    throw new InvalidOperationException("Missing or mismatched biome VFX prefab.");
                if (UnityEngine.Resources.Load<Font>("Infernal/Fonts/Cinzel") == null)
                    throw new InvalidOperationException("Missing Gothic display font.");

                var build = new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.StandaloneOSX,
                    locationPathName = output,
                    extraScriptingDefines = new[] { "HELLFIRE_PHASE1_SMOKE" },
                    options = BuildOptions.Development
                };
                var result = BuildPipeline.BuildPlayer(build).summary;
                Debug.Log($"[PHASE1] BUILD {result.result}, errors={result.totalErrors}, warnings={result.totalWarnings}");
                EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(2);
            }
        }

        private static void ValidateInfernalSprites()
        {
            foreach (string name in new[] { "Menu", "Loading", "Victory", "Defeat", "Panel", "Button",
                         "Meter", "Boss", "Sigil", "Ammo", "Health" })
            {
                var sprite = UnityEngine.Resources.Load<Sprite>("Infernal/" + name);
                if (sprite == null) throw new InvalidOperationException("Missing UI sprite " + name);
                if ((name == "Panel" || name == "Button" || name == "Meter" || name == "Boss") &&
                    sprite.border.sqrMagnitude == 0f)
                    throw new InvalidOperationException("Missing nine-slice border for " + name);
            }
            Debug.Log("[PHASE1] INFERNAL_SPRITES=11 NINE_SLICE=4");
        }

        private static int ScanHierarchy(GameObject root, string path)
        {
            int count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing == 0) continue;
                Debug.LogWarning($"[PHASE1] Missing script {path} / {transform.name}: {missing}");
                count += missing;
            }
            return count;
        }
    }
}
