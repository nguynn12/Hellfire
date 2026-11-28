using System;
using System.IO;
using System.Linq;
using Hellfire.Vfx;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hellfire.EditorTools
{
    internal static class EnvironmentVfxChecks
    {
        public static void VerifyAndBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(EnvironmentVfxBuilder.Sandbox);
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline == null || QualitySettings.activeColorSpace != ColorSpace.Linear)
                    throw new InvalidOperationException("Expected a linear URP sandbox.");
                string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly).version;
                Debug.Log($"[VFX QA] Unity {Application.unityVersion} URP {package}; {pipeline.name} depth={pipeline.supportsCameraDepthTexture}");
                foreach (string name in new[] { "LavaEmber", "FrozenMist" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/VFX/Prefabs/" + name + ".prefab");
                    if (prefab == null || prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 3 ||
                        prefab.GetComponentsInChildren<Transform>(true).Any(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0))
                        throw new InvalidOperationException("Missing reference in " + name);
                    foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>())
                    {
                        var material = renderer.sharedMaterial;
                        if (material == null || !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader) ||
                            material.GetTexture("_BaseMap") == null)
                            throw new InvalidOperationException("Missing material, texture, or compiled shader in " + name);
                    }
                }

                string path = Environment.GetEnvironmentVariable("HELLFIRE_VFX_BUILD");
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("Set HELLFIRE_VFX_BUILD to the sandbox player path.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { EnvironmentVfxBuilder.Sandbox },
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development,
                    locationPathName = path
                }).summary;
                Debug.Log($"[VFX QA] BUILD {report.result} errors={report.totalErrors}, warnings={report.totalWarnings}");
                EditorApplication.Exit(report.result == BuildResult.Succeeded ? 0 : 2);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(2);
            }
        }

        [InitializeOnLoadMethod]
        private static void ConnectPlayModeCallback()
        {
            if (!Environment.GetCommandLineArgs().Contains("-hfVfxEditorCheck")) return;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    bool ok = false;
                    try
                    {
                        var sandbox = UnityEngine.Object.FindAnyObjectByType<EnvironmentVfxSandbox>();
                        ok = sandbox != null && sandbox.ValidateSystems();
                        if (ok)
                        {
                            int previousQuality = QualitySettings.GetQualityLevel();
                            int noDepth = Array.FindIndex(QualitySettings.names, name => name == "Mobile");
                            if (noDepth >= 0)
                            {
                                try
                                {
                                    QualitySettings.SetQualityLevel(noDepth);
                                    var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                                    ok = pipeline != null && !pipeline.supportsCameraDepthTexture && sandbox.ValidateSystems();
                                    Debug.Log("[VFX QA] No-depth material selection (Editor) " + (ok ? "PASS" : "FAIL"));
                                }
                                finally
                                {
                                    QualitySettings.SetQualityLevel(previousQuality);
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                    }
                    SessionState.SetBool("Hellfire.VFX.Validation", ok);
                    Debug.Log("[VFX QA] Editor Play Mode " + (ok ? "PASS" : "FAIL"));
                    EditorApplication.isPlaying = false;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.Exit(SessionState.GetBool("Hellfire.VFX.Validation", false) ? 0 : 2);
                }
            };
        }

        public static void VerifyEditorPlayMode()
        {
            EditorSceneManager.OpenScene(EnvironmentVfxBuilder.Sandbox);
            EditorApplication.isPlaying = true;
        }
    }
}
