using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Hellfire.Vfx
{
    public sealed class EnvironmentVfxSandbox : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Volume _bloom;
        [SerializeField] private Transform[] _views;
        [SerializeField] private GameObject[] _effects;

        private int _selected;
        private bool _effectsVisible = true;
        private bool _capturing;
        private readonly WaitForEndOfFrame _endOfFrame = new();

        private void Start()
        {
            Select(0, true, false);
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-hfVfxCapture");
            if (index < 0 || index + 1 >= args.Length) return;
            _capturing = true;
            StartCoroutine(CaptureSuite(args[index + 1]));
        }

        private void Update()
        {
            if (_capturing || Keyboard.current is not { } keys) return;
            if (keys.digit1Key.wasPressedThisFrame) Select(0, _effectsVisible, _bloom.enabled);
            if (keys.digit2Key.wasPressedThisFrame) Select(1, _effectsVisible, _bloom.enabled);
            if (keys.bKey.wasPressedThisFrame) _bloom.enabled = !_bloom.enabled;
            if (keys.vKey.wasPressedThisFrame) Select(_selected, !_effectsVisible, _bloom.enabled);
            if (keys.rKey.wasPressedThisFrame) Select(_selected, _effectsVisible, _bloom.enabled);
        }

        private void Select(int selected, bool effects, bool bloom)
        {
            _selected = selected;
            _effectsVisible = effects;
            _camera.transform.SetPositionAndRotation(_views[selected].position, _views[selected].rotation);
            _bloom.enabled = bloom;
            for (int i = 0; i < _effects.Length; i++)
            {
                var effect = _effects[i];
                foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.SetActive(effects && i == selected);
                if (effects && i == selected)
                {
                    foreach (var system in effect.GetComponentsInChildren<ParticleSystem>())
                    {
                        system.Simulate(7f, false, true, true);
                        system.Play(false);
                    }
                }
            }
        }

        private IEnumerator CaptureSuite(string directory)
        {
            Directory.CreateDirectory(directory);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Debug.Log($"[VFX QA] {Application.unityVersion} {SystemInfo.deviceModel} / {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType} " +
                $"{Screen.width}x{Screen.height}, editor={Application.isEditor}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}, depth={pipeline?.supportsCameraDepthTexture}");
            if (!ValidateSystems())
            {
                Debug.LogError("[VFX QA] Failed prefab, stop/reset, or deterministic simulation checks.");
                Application.Quit(2);
                yield break;
            }

            for (int i = 0; i < _effects.Length; i++)
            {
                string name = i == 0 ? "Lava-Ember" : "Frozen-Mist";
                Select(i, false, false);
                yield return new WaitForSecondsRealtime(.8f);
                yield return Measure(name + " baseline");
                yield return Capture(directory, name + "-Baseline");

                Select(i, true, false);
                yield return new WaitForSecondsRealtime(.8f);
                yield return Measure(name + " active");
                var systems = _effects[i].GetComponentsInChildren<ParticleSystem>();
                foreach (var system in systems) system.Simulate(8f, false, true, true);
                yield return Capture(directory, name + "-Bloom-Off");
                foreach (var system in systems) system.Play(false);
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return Capture(directory, $"{name}-Motion-{frame:00}");
                    yield return new WaitForSecondsRealtime(.28f);
                }

                _bloom.enabled = true;
                foreach (var system in systems) system.Simulate(8f, false, true, true);
                yield return null;
                yield return Capture(directory, name + "-Bloom-On");
            }

            Debug.Log("[VFX QA] COMPLETE");
            Application.Quit();
        }

        public bool ValidateSystems()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            foreach (var root in _effects)
            {
                root.SetActive(false);
                root.SetActive(true);
                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length != 3 || systems.Any(system => system.useAutoRandomSeed || system.main.maxParticles > 128))
                    return false;
                foreach (var system in systems)
                {
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.shader.isSupported)
                        return false;
                    bool soft = renderer.sharedMaterial.IsKeywordEnabled("_SOFTPARTICLES_ON");
                    bool layerUsesDepth = system.GetComponent<EnvironmentParticleDepth>() != null;
                    if (soft != (layerUsesDepth && pipeline != null && pipeline.supportsCameraDepthTexture))
                        return false;
                }

                foreach (var system in systems) system.Simulate(8f, false, true, true);
                int first = Signature(systems);
                if (systems.Any(system => system.particleCount == 0)) return false;
                foreach (var system in systems) system.Simulate(8f, false, true, true);
                if (first != Signature(systems)) return false;
                foreach (var system in systems) system.Simulate(31f, false, true, true);
                if (systems.Any(system => system.particleCount == 0) || first == Signature(systems)) return false;
                foreach (var system in systems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (systems.Any(system => system.particleCount != 0)) return false;
                root.SetActive(false);
                Debug.Log("[VFX QA] Verified running, 31s sustained, deterministic reset, stop/clear: " + root.name);
            }
            Select(0, true, false);
            return true;
        }

        private static int Signature(IEnumerable<ParticleSystem> systems)
        {
            int hash = 17;
            foreach (var system in systems)
            {
                var particles = new ParticleSystem.Particle[system.main.maxParticles];
                int count = system.GetParticles(particles);
                for (int i = 0; i < count; i++)
                    hash = unchecked(hash * 31 + particles[i].position.GetHashCode());
            }
            return hash;
        }

        private IEnumerator Capture(string directory, string filename)
        {
            yield return _endOfFrame;
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, filename + ".png"), image.EncodeToPNG());
            Destroy(image);
        }

        private IEnumerator Measure(string name)
        {
            const int frames = 180;
            var elapsed = new double[frames];
            var draws = new double[frames];
            var alloc = new double[frames];
            var gpu = new double[frames];
            var timing = new FrameTiming[1];
            var systems = _effects[_selected].GetComponentsInChildren<ParticleSystem>(true);
            using var drawCounter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var allocCounter = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            float count = 0f;
            long previous = Stopwatch.GetTimestamp();
            for (int i = 0; i < frames; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return _endOfFrame;
                long current = Stopwatch.GetTimestamp();
                elapsed[i] = (current - previous) * 1000.0 / Stopwatch.Frequency;
                previous = current;
                draws[i] = drawCounter.Valid ? drawCounter.LastValue : -1;
                alloc[i] = allocCounter.Valid ? allocCounter.LastValue : -1;
                if (FrameTimingManager.GetLatestTimings(1, timing) != 0)
                    gpu[i] = timing[0].gpuFrameTime;
                foreach (var system in systems)
                    count += system.gameObject.activeInHierarchy ? system.particleCount : 0;
            }

            string gpuMedian = gpu.Count(value => value > 0) > frames / 2 ? Percentile(gpu, .50) : "unavailable";
            Debug.Log($"[VFX QA] {name} ({frames} frames) wall_ms_p50={Percentile(elapsed, .50)} " +
                $"wall_ms_p95={Percentile(elapsed, .95)} gpu_ms_p50={gpuMedian} " +
                $"draws_p50={Percentile(draws, .50)} gc_bytes_p50={Percentile(alloc, .50)} " +
                $"particles_mean={(count / frames).ToString("F1", CultureInfo.InvariantCulture)}");
        }

        private static string Percentile(double[] samples, double percentile)
        {
            Array.Sort(samples);
            return samples[(int)((samples.Length - 1) * percentile)].ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
