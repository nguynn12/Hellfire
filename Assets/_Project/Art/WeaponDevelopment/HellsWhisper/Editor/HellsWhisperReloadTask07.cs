#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    // Presentation-only Edit Mode preview. No gameplay component or prefab edits.
    [InitializeOnLoad]
    internal static partial class HellsWhisperReloadTask07
    {
        const string Folder = HellsWhisperVisualLabTask05.Folder;
        const string Audit = "/private/tmp/hellfire_task11_kxk6oa26/";
        const string PivotPath = "FPSCameraRig/PreviewCamera/ViewmodelAnchor/CalibrationPivot_PROVISIONAL/ReloadPresentationPivot";
        [Serializable] sealed class SceneState
        { public string path, handle; public bool dirty; public int roots; }
        [Serializable] sealed class SceneStates { public SceneState[] scenes; }
        [Serializable] sealed class Check
        {
            public string unityVersion, status, scenesBefore, scenesAfter, dirtyMaterialsBefore;
            public bool prepareSampleMatches, rechargeSampleMatches, returnSampleMatches, stopRestores, replayRestores, endRestores,
                vfxActive, vfxOffWorks, vfxCleared, nestedPrefabIntact, calibrationPreserved, scenesPreserved, materialValid;
            public bool referenceProfileValid, phaseRetimeWorks, invalidDataRejected, jsonRoundTrip, baselineEquivalent;
            public int missingScripts, shaderErrors;
            public float duration, positionResetError, rotationResetError;
        }
        static ReloadKeyframeDataTask08 motion;
        static Transform pivot;
        static ParticleSystem particles;
        static Vector3 restPosition, restScale, effectCenter;
        static Quaternion restRotation;
        static bool running, vfx = true, hasRest;
        static float duration = 2.2f, intensity = 1, progress;
        static bool durationOverride, timingCheck;
        static float overrideSeconds = 2.2f, activeDuration = 2.2f;
        static double previousElapsed, maxUpdateGap;
        static float EffectiveDuration => durationOverride ? overrideSeconds : duration;
        static double startTime, lastPaint;
        static string message = "Edit Mode only. PROVISIONAL motion; no production sockets.";
        static bool frozen;
        internal static event Action Changed;
        internal static bool Running => running;

        static HellsWhisperReloadTask07()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Reset;
            EditorApplication.quitting += Reset;
            EditorApplication.playModeStateChanged += _ => Reset();
            EditorSceneManager.sceneSaving += (s, path) => { if (path == HellsWhisperVisualLabTask05.LabPath) Reset(); };
            EditorSceneManager.sceneClosing += (s, removing) => { if (s.path == HellsWhisperVisualLabTask05.LabPath) Reset(); };
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyRitualPreview;
            EditorApplication.quitting += DestroyRitualPreview;
            EditorSceneManager.sceneClosing += (s, removing) => { if(s.path==HellsWhisperVisualLabTask05.LabPath) DestroyRitualPreview(); };
            SceneView.duringSceneGui += DrawGizmos11;
            EditorApplication.update += GuardFraming11;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreFraming11;
            EditorApplication.quitting += RestoreFraming11;
            EditorSceneManager.sceneSaving += (s,path) => { if(path==HellsWhisperVisualLabTask05.LabPath) RestoreFraming11(); };
            EditorSceneManager.sceneClosing += (s,remove) => { if(s.path==HellsWhisperVisualLabTask05.LabPath) RestoreFraming11(); };
            // Installation is complete. Opening Unity never rebuilds or saves the lab.
        }
        static void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason); }
        static Scene Lab => SceneManager.GetSceneByPath(HellsWhisperVisualLabTask05.LabPath);
        static Transform Root => Lab.IsValid() && Lab.isLoaded ? Lab.GetRootGameObjects().Single(g => g.name == "VisualLab").transform : null;
        static string OtherScenes()
        {
            return JsonUtility.ToJson(new SceneStates { scenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Where(s => s.path != HellsWhisperVisualLabTask05.LabPath)
                .Select(s => new SceneState { path = s.path, handle = s.handle.GetRawData().ToString(), dirty = s.isDirty, roots = s.rootCount }).ToArray() });
        }
        static void LoadMotion()
        {
            LoadReferenceProfile();
        }
        static void Bind()
        {
            if (motion == null) LoadMotion();
            if (Root == null) return;
            var found = Root.Find(PivotPath);
            if (found == null || (pivot == found && hasRest)) return;
            pivot = found; restPosition = pivot.localPosition; restRotation = pivot.localRotation; restScale = pivot.localScale;
            particles = pivot.Find("FX_Recharge_BoundsCenter_NOT_SOCKET")?.GetComponent<ParticleSystem>();
            var renderer = pivot.GetComponentsInChildren<MeshRenderer>().Single();
            effectCenter = pivot.InverseTransformPoint(renderer.bounds.center);
            hasRest = true;
            MeasurePresentationLength(renderer);
        }
        static string DirtyMaterials()
        {
            return string.Join("\n", Resources.FindObjectsOfTypeAll<Material>()
                .Where(m => EditorUtility.IsPersistent(m) && EditorUtility.IsDirty(m))
                .Select(AssetDatabase.GetAssetPath).Distinct().OrderBy(p => p));
        }
        static string TransformState(Transform t) => JsonUtility.ToJson(new TransformSnapshot { p=t.localPosition, r=t.localRotation, s=t.localScale });
        [Serializable] sealed class TransformSnapshot { public Vector3 p, s; public Quaternion r; }
        internal static void Play()
        {
            if (frozen || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Reset(); Bind(); if (!hasRest || pivot == null || motion == null) return;
            string error = motion.Error();
            if (error != null) { message = error; return; }
            if (!ValidDuration(EffectiveDuration)) { message = "Duration must be finite and greater than zero."; return; }
            activeDuration = EffectiveDuration; previousElapsed = 0; maxUpdateGap = 0;
            startTime = EditorApplication.timeSinceStartup; running = true; progress = 0;
        }
        internal static void Reset()
        {
            running = false; timingCheck = false; progress = 0; scrubTime = 0;
            if (pivot != null && hasRest)
            { pivot.localPosition = restPosition; pivot.localRotation = restRotation; pivot.localScale = restScale; }
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ClearRitualPreview();
            Changed?.Invoke(); SceneView.RepaintAll();
        }
        static float Ease(float t) { t=Mathf.Clamp01(t); return t*t*(3-2*t); }
        static bool ValidDuration(float seconds) => !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0;
        static float NormalizedTime(double elapsed, float seconds) => Mathf.Clamp01((float)(elapsed/seconds));
        [Serializable] sealed class TimingCheck
        {
            public float requested;
            public double observed, maxEditorUpdateGap;
            public bool completedAfterDeadline, restRestored, vfxCleared;
        }
        static void ValidateDurationMapping()
        {
            Require(NormalizedTime(1.1,2.2f)>.4999f && NormalizedTime(1.1,2.2f)<.5001f, "Duration mapping failed");
            Require(Mathf.Approximately(NormalizedTime(.55,1.1f),.5f) && NormalizedTime(3,2.2f)==1, "Override mapping failed");
            Require(!ValidDuration(0) && !ValidDuration(float.NaN) && !ValidDuration(float.PositiveInfinity), "Invalid duration accepted");
        }
        // Editor prototype only. A future runtime adapter must consume gameplay-owned
        // progress/remaining time and cancellation, rather than start a second clock.
        static void DrawDurationOverride()
        {
            EditorGUI.BeginChangeCheck();
            durationOverride=EditorGUILayout.Toggle("Duration override (preview only)",durationOverride);
            if(durationOverride) overrideSeconds=EditorGUILayout.FloatField("Effective seconds (simulation)",overrideSeconds);
            if(EditorGUI.EndChangeCheck()) Reset();
            EditorGUILayout.LabelField($"Playback: {EffectiveDuration:F3}s — {(durationOverride ? "preview override" : "profile duration")}");
            var pistol=AssetDatabase.LoadAssetAtPath<Hellfire.Combat.WeaponData>("Assets/_Project/Data/Weapons/Pistol_Data.asset");
            if(pistol!=null)
            {
                bool dirty=EditorUtility.IsDirty(pistol);
                EditorGUILayout.LabelField($"Pistol data default: {pistol.ReloadTime:F3}s | Asset dirty: {dirty}");
                if(GUILayout.Button("Use Pistol default as preview override"))
                { Reset(); overrideSeconds=pistol.ReloadTime; durationOverride=true; }
            }
            if(!ValidDuration(EffectiveDuration)) EditorGUILayout.HelpBox("Enter a finite duration greater than zero.",MessageType.Error);
        }
        static void Sample(float normalized)
        {
            Bind(); if (pivot == null || !hasRest || motion == null || motion.Error() != null) return;
            float t = Mathf.Clamp01(normalized);
            motion.Evaluate(t, out var position, out var rotation);
            pivot.localPosition = restPosition + PresentationOffset(position) * intensity;
            pivot.localRotation = restRotation * Quaternion.Euler(rotation * intensity);
            pivot.localScale = restScale;
            if(ritualSelected) { if(particles!=null) particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); SampleRitual(t); return; }
            ClearRitualPreview();
            if (particles == null) return;
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            float envelope = Ease((t-motion.prepareEnd)/((motion.rechargeEnd-motion.prepareEnd)*motion.vfxFadeInFraction)) *
                (1-Ease((t-motion.rechargeEnd)/((1-motion.rechargeEnd)*motion.vfxReturnFadeFraction)));
            if (!vfx || envelope <= .001f)
            { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            particles.Pause(true);
            var points = new ParticleSystem.Particle[Mathf.Clamp(motion.particleCount, 1, 64)];
            float cycle = (t-motion.prepareEnd)/((motion.rechargeEnd-motion.prepareEnd)*1.125f);
            for (int i=0;i<points.Length;i++)
            {
                float seed = i*.61803399f, age = Mathf.Repeat(cycle + seed, 1);
                float angle = seed*Mathf.PI*2 + age*.6f;
                float z = Mathf.Sin(i*2.39996f)*.55f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), z).normalized;
                float fade = Mathf.Sin(age*Mathf.PI) * envelope;
                points[i] = new ParticleSystem.Particle {
                    position = effectCenter + direction * motion.particleRadius * (1-Ease(age)),
                    startSize = Mathf.Lerp(.026f,.045f,(i%5)/4f), startLifetime = 10, remainingLifetime = 10,
                    startColor = new Color(Mathf.Lerp(.42f,1,(i%5)/4f), Mathf.Lerp(.015f,.24f,(i%5)/4f), .008f, fade*.75f),
                    randomSeed = (uint)(i+1) };
            }
            particles.SetParticles(points, points.Length);
        }
        static void Update()
        {
            if (!running || frozen) return;
            if (pivot == null || !Lab.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
            { Reset(); return; }
            double elapsed = EditorApplication.timeSinceStartup-startTime;
            maxUpdateGap = Math.Max(maxUpdateGap, elapsed-previousElapsed); previousElapsed = elapsed;
            progress = NormalizedTime(elapsed,activeDuration);
            if (progress >= 1)
            {
                bool record=timingCheck; float requested=activeDuration;
                Reset();
                if(record)
                {
                    Directory.CreateDirectory(Audit);
                    File.WriteAllText(Audit+"duration_timing_task11.json",JsonUtility.ToJson(new TimingCheck {
                        requested=requested, observed=elapsed, maxEditorUpdateGap=maxUpdateGap,
                        completedAfterDeadline=elapsed>=requested, restRestored=AtRest(), vfxCleared=PreviewParticleCount()==0 },true));
                }
                return;
            }
            Sample(progress);
            if (EditorApplication.timeSinceStartup-lastPaint > .08)
            { lastPaint=EditorApplication.timeSinceStartup; Changed?.Invoke(); SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
        }
        internal static void DrawGUI()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Task 07 — Fantasy Energy Reload", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(message, frozen ? MessageType.Error : MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || frozen || Root == null))
            {
                if (motion == null) LoadMotion();
                if (motion == null) { EditorGUILayout.HelpBox(message, MessageType.Error); return; }
                DrawRitualControls();
                EditorGUI.BeginChangeCheck();
                duration = EditorGUILayout.Slider("Duration (seconds)", duration, .3f, 4);
                intensity = EditorGUILayout.Slider("Motion Intensity", intensity, 0, 2);
                vfx = EditorGUILayout.Toggle("VFX On / Off", vfx);
                if (EditorGUI.EndChangeCheck()) { Reset(); profileDirty = true; }
                DrawDurationOverride();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play Reload")) Play();
                if (GUILayout.Button("Stop Reload")) Reset();
                if (GUILayout.Button("Replay")) Play();
                if (GUILayout.Button("Reset")) Reset();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(running ? "Playing " + (progress*100).ToString("F0") + "%" : scrubTime>0 ? "Scrubbed — " + (scrubTime*EffectiveDuration).ToString("F3") + "s" : "Stopped — rest pose");
                if (GUILayout.Button("Validate Reload Preview")) ValidateInstalled();
                DrawKeyframeEditor();
            }
        }
        static void ValidateInstalled()
        {
            if (motion == null || motion.Error() != null) { message = motion == null ? "Missing profile" : motion.Error(); return; }
            var enabledBefore=(bool[])layerEnabled.Clone();
            try
            {
                for(int i=0;i<4;i++)layerEnabled[i]=true;
                Reset(); Bind();
                Require(pivot != null, "Open the lab first");
                Directory.CreateDirectory(Audit);
                string before=OtherScenes();
                var main=particles.main; main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                Require(motion != null && motion.Error() == null, motion == null ? "Missing profile" : motion.Error());
                ValidateDurationMapping();
                var check=Validate();
                if(ritualSelected) ValidateRitualSpecification();
                Array.Copy(enabledBefore,layerEnabled,4);
                check.scenesBefore=before; check.scenesAfter=OtherScenes();
                check.dirtyMaterialsBefore=DirtyMaterials();
                check.scenesPreserved=before==check.scenesAfter;
                Require(check.scenesPreserved, "Other scene state changed");
                File.WriteAllText(Audit+"unity_validation_task11.json",JsonUtility.ToJson(check,true));
                message="Reload / keyframe validation passed. Rest pose restored; no scene or profile saved.";
                Debug.Log("[Hellfire Task11] Preview checks passed. Starting timed playback using its existing single clock.");
                Play(); timingCheck=running;
            }
            catch(Exception e) { Array.Copy(enabledBefore,layerEnabled,4); Reset(); frozen=true; Debug.LogError("[Hellfire Task11] STOP: "+e); }
        }
        static bool AtRest() => pivot.localPosition == restPosition && Quaternion.Angle(pivot.localRotation,restRotation)<.001f && pivot.localScale==restScale;
        static Check Validate()
        {
            Bind(); var check = new Check { unityVersion=Application.unityVersion, duration=EffectiveDuration };
            var calibration=pivot.parent; var anchor=calibration.parent;
            string calibrationBefore=TransformState(anchor)+TransformState(calibration);
            ValidateKeyframeData(check);
            bool previousVfx = vfx; vfx = true;
            Play(); Sample(motion.prepareEnd*.5f); check.prepareSampleMatches = SampleMatches(motion.prepareEnd*.5f);
            float rechargeTime=(motion.prepareEnd+motion.rechargeEnd)*.5f;
            Sample(rechargeTime); check.rechargeSampleMatches = SampleMatches(rechargeTime);
            check.vfxActive=PreviewParticleCount()>0;
            Capture("IMG_Reload_Recharge_Task10.png");
            vfx=false; Sample(rechargeTime); check.vfxOffWorks=PreviewParticleCount()==0; vfx=true;
            float returnTime=(motion.rechargeEnd+1)*.5f;
            Sample(returnTime); check.returnSampleMatches=SampleMatches(returnTime);
            Capture("IMG_Reload_Return_Task10.png");
            Reset(); check.stopRestores=AtRest() && PreviewParticleCount()==0;
            Play(); Sample(rechargeTime); Play(); check.replayRestores=AtRest();
            Sample(1); check.endRestores=AtRest(); Reset(); check.vfxCleared=PreviewParticleCount()==0;
            check.positionResetError=(pivot.localPosition-restPosition).magnitude;
            check.rotationResetError=Quaternion.Angle(pivot.localRotation,restRotation);
            check.nestedPrefabIntact=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pivot.GetChild(0).gameObject)==Folder+"PF_HellsWhisper_Viewmodel.prefab";
            check.missingScripts=Lab.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"MAT_HellsWhisper_Recharge_Task07.mat");
            check.materialValid=mat.shader.isSupported;
            check.shaderErrors=ShaderUtil.GetShaderMessages(mat.shader).Count(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
            Capture("IMG_Reload_Rest_Task10.png");
            vfx=previousVfx;
            check.calibrationPreserved=calibrationBefore==TransformState(anchor)+TransformState(calibration);
            Require(check.prepareSampleMatches && check.rechargeSampleMatches && check.returnSampleMatches && check.stopRestores && check.replayRestores && check.endRestores && check.vfxActive && check.vfxOffWorks && check.vfxCleared && check.nestedPrefabIntact && check.calibrationPreserved && check.referenceProfileValid && check.phaseRetimeWorks && check.invalidDataRejected && check.jsonRoundTrip && check.missingScripts==0 && check.materialValid && check.shaderErrors==0, "Reload validation failed");
            check.status="TECHNICAL_CHECKS_PASSED_ARTISTIC_APPROVAL_PENDING";
            return check;
        }
        static void Capture(string name)
        {
            var cam=Root.Find("FPSCameraRig/PreviewCamera").GetComponent<Camera>();
            var method=typeof(HellsWhisperVisualLabTask05).GetMethod("RenderPreview",BindingFlags.NonPublic|BindingFlags.Static);
            var image=(Texture2D)method.Invoke(null,new object[]{cam,1280,720});
            try { File.WriteAllBytes(Audit+name,image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
#endif
