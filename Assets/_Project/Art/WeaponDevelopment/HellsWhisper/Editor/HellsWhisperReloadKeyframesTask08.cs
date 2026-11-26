#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    internal static partial class HellsWhisperReloadTask07
    {
        const string PrototypeProfilePath = Folder + "RELOAD_HellsWhisper_Keyframes_Task08.json";
        const string RitualProfilePath = Folder + "RELOAD_HellsWhisper_DemonicHeartbeat_BloodRitual_Task10.json";
        static bool ritualSelected = true;
        static string ProfilePath => ritualSelected ? RitualProfilePath : PrototypeProfilePath;
        static bool keyEditorOpen, profileDirty;
        static int selectedKey;
        static float scrubTime;
        static string loadedProfileHash;
        static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }

        static void LoadReferenceProfile()
        {
            try
            {
                var loaded = JsonUtility.FromJson<ReloadKeyframeDataTask08>(File.ReadAllText(ProfilePath));
                if(loaded!=null)
                {
                    if(string.IsNullOrEmpty(loaded.positionUnits))loaded.positionUnits="ParentLocal";
                    if(string.IsNullOrEmpty(loaded.style))loaded.style="Prototype";
                }
                Require(loaded != null && loaded.Error() == null, loaded == null ? "Empty profile" : loaded.Error());
                motion = loaded; duration = motion.duration; intensity = motion.motionIntensity;
                vfx = motion.vfxEnabled;
                loadedProfileHash = Hash(ProfilePath); profileDirty = false; selectedKey = 0; scrubTime = 0;
            }
            catch (Exception e) { message = "Profile not loaded: " + e.Message; }
        }
        static void PreviewAt(float t)
        {
            Reset(); scrubTime = t;
            if (motion.Error() != null) { message = motion.Error(); return; }
            Sample(t); Changed?.Invoke(); SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
        }
        static void SaveReferenceProfile()
        {
            Reset();
            message="Task 07/08/10 motion profiles are protected during Task 11. No write performed.";
        }
        static bool SampleMatches(float t)
        {
            motion.Evaluate(t,out var pos,out var rot);
            return Vector3.Distance(pivot.localPosition,restPosition+PresentationOffset(pos)*intensity)<.00001f &&
                Quaternion.Angle(pivot.localRotation,restRotation*Quaternion.Euler(rot*intensity))<.01f;
        }
        [Serializable] sealed class LegacyMotion
        { public float prepareEnd = 0, rechargeEnd = 0; public Vector3 lowerOffset = Vector3.zero, tiltDegrees = Vector3.zero; }
        static ReloadKeyframeDataTask08 CloneProfile() => JsonUtility.FromJson<ReloadKeyframeDataTask08>(JsonUtility.ToJson(motion));
        static void ValidateKeyframeData(Check check)
        {
            check.referenceProfileValid=motion.Error()==null;
            var clone=CloneProfile();
            check.jsonRoundTrip=JsonUtility.ToJson(clone)==JsonUtility.ToJson(motion);
            var bad=CloneProfile(); bad.keys[1].time=bad.keys[0].time;
            check.invalidDataRejected=bad.Error()!=null;
            bad=CloneProfile(); bad.keys[0].positionOffset=new Vector3(float.NaN,0,0);
            check.invalidDataRejected &= bad.Error()!=null;
            float oldPrepare=clone.prepareEnd, oldRecharge=clone.rechargeEnd;
            clone.RetimePhases(.3f,.7f);
            check.phaseRetimeWorks=clone.Error()==null;
            for(int i=0;i<motion.keys.Length;i++)
            {
                var a=motion.keys[i]; var b=clone.keys[i];
                float expected=a.time<=oldPrepare ? a.time/oldPrepare*.3f : a.time<=oldRecharge ?
                    Mathf.Lerp(.3f,.7f,(a.time-oldPrepare)/(oldRecharge-oldPrepare)) : Mathf.Lerp(.7f,1,(a.time-oldRecharge)/(1-oldRecharge));
                check.phaseRetimeWorks &= Mathf.Abs(b.time-expected)<.00001f && b.positionOffset==a.positionOffset && b.rotationDegrees==a.rotationDegrees;
            }
            var legacy=JsonUtility.FromJson<LegacyMotion>(File.ReadAllText(Folder+"RELOAD_HellsWhisper_Prototype_Task07.json"));
            check.baselineEquivalent=true;
            for(int i=0;i<=1000;i++)
            {
                float t=i/1000f;
                float weight=t<legacy.prepareEnd ? Ease(t/legacy.prepareEnd) : t<=legacy.rechargeEnd ? 1 : 1-Ease((t-legacy.rechargeEnd)/(1-legacy.rechargeEnd));
                motion.Evaluate(t,out var pos,out var rot);
                check.baselineEquivalent &= Vector3.Distance(pos,legacy.lowerOffset*weight)<.00001f && Vector3.Distance(rot,legacy.tiltDegrees*weight)<.0001f;
            }
            // baselineEquivalent is informational: future authored reference keys may intentionally differ.
        }
        static void DrawKeyframeEditor()
        {
            keyEditorOpen = EditorGUILayout.Foldout(keyEditorOpen, "Task 08 — Reference keyframes", true);
            if (!keyEditorOpen) return;
            string referenceStatus = string.IsNullOrWhiteSpace(motion.referenceVideo) ? "No reference video has been supplied." : "Reference ID recorded; poses must be interpreted manually and reviewed.";
            EditorGUILayout.HelpBox("Provisional motion. " + referenceStatus + " Time is 0–1; position uses the selected profile units; rotation is unwrapped Euler degrees.", MessageType.Info);
            string error = motion.Error();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.LabelField(profileDirty ? "Draft changed — not saved" : "Profile loaded", EditorStyles.miniLabel);
            if(ritualSelected)
                EditorGUILayout.LabelField("Awakening / First Heartbeat / Second Heartbeat / Blood Ritual / Return",EditorStyles.wordWrappedLabel);
            else
            {
            EditorGUI.BeginChangeCheck();
            float prepare = EditorGUILayout.Slider("Prepare end (normalized)", motion.prepareEnd, .01f, motion.rechargeEnd-.01f);
            float recharge = EditorGUILayout.Slider("Recharge end (normalized)", motion.rechargeEnd, prepare+.01f, .99f);
            if (EditorGUI.EndChangeCheck())
            { float t=scrubTime; Reset(); motion.RetimePhases(prepare, recharge); profileDirty = true; PreviewAt(t); }
            EditorGUILayout.LabelField($"Prepare 0–{motion.prepareEnd*EffectiveDuration:F3}s | Recharge –{motion.rechargeEnd*EffectiveDuration:F3}s | Return –{duration:F3}s");
            }
            EditorGUI.BeginChangeCheck();
            float scrub = EditorGUILayout.Slider("Scrub normalized time", scrubTime, 0, 1);
            if (EditorGUI.EndChangeCheck()) PreviewAt(scrub);
            float shownTime=running ? progress : scrubTime;
            EditorGUILayout.LabelField($"Preview {shownTime*EffectiveDuration:F3}s — {motion.Phase(shownTime)}");
            selectedKey = Mathf.Clamp(selectedKey, 0, motion.keys.Length-1);
            int selection = EditorGUILayout.Popup("Selected key", selectedKey, motion.keys.Select((k,i) => $"{i}: {k.time:F3} / {k.time*EffectiveDuration:F3}s — {motion.Phase(k.time)}").ToArray());
            if (selection != selectedKey) { selectedKey = selection; PreviewAt(motion.keys[selectedKey].time); }
            var key = motion.keys[selectedKey]; bool endpoint = selectedKey == 0 || selectedKey == motion.keys.Length-1;
            using (new EditorGUI.DisabledScope(endpoint))
            {
                EditorGUI.BeginChangeCheck();
                float time = EditorGUILayout.FloatField("Key normalized time", key.time);
                var position = EditorGUILayout.Vector3Field(ritualSelected ? "Position fraction of L" : "Local position offset", key.positionOffset);
                var rotation = EditorGUILayout.Vector3Field("Euler rotation (degrees)", key.rotationDegrees);
                if (EditorGUI.EndChangeCheck())
                {
                    Reset();
                    if (!endpoint)
                    {
                        key.time = Mathf.Clamp(time, motion.keys[selectedKey-1].time+.0002f, motion.keys[selectedKey+1].time-.0002f);
                        key.positionOffset = position; key.rotationDegrees = rotation; profileDirty = true;
                        PreviewAt(key.time);
                    }
                }
            }
            EditorGUI.BeginChangeCheck();
            var interpolation = (ReloadKeyframeDataTask08.Interpolation)EditorGUILayout.EnumPopup("Outgoing interpolation", key.interpolation);
            if (EditorGUI.EndChangeCheck()) { float t=scrubTime; Reset(); key.interpolation=interpolation; profileDirty=true; PreviewAt(t); }
            EditorGUILayout.BeginHorizontal();
            bool canAdd = error == null && motion.keys.Length < 64 && scrubTime > .001f && scrubTime < .999f && motion.keys.All(k=>Mathf.Abs(k.time-scrubTime)>.001f);
            using (new EditorGUI.DisabledScope(!canAdd))
                if (GUILayout.Button("Add key at scrub time"))
                {
                    motion.Evaluate(scrubTime,out var pos,out var rot);
                    var added = new ReloadKeyframeDataTask08.PoseKey { time=scrubTime, positionOffset=pos, rotationDegrees=rot };
                    Reset(); motion.keys=motion.keys.Concat(new[]{added}).OrderBy(k=>k.time).ToArray();
                    selectedKey=Array.IndexOf(motion.keys,added); profileDirty=true; PreviewAt(added.time);
                }
            using (new EditorGUI.DisabledScope(endpoint))
                if (GUILayout.Button("Delete selected key"))
                { Reset(); motion.keys=motion.keys.Where((k,i)=>i!=selectedKey).ToArray(); selectedKey=Mathf.Min(selectedKey,motion.keys.Length-1); profileDirty=true; PreviewAt(motion.keys[selectedKey].time); }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            motion.referenceVideo = EditorGUILayout.TextField("Reference video / ID",motion.referenceVideo);
            motion.referenceStartSeconds = EditorGUILayout.FloatField("Reference start (seconds)",motion.referenceStartSeconds);
            motion.referenceEndSeconds = EditorGUILayout.FloatField("Reference end (seconds)",motion.referenceEndSeconds);
            motion.referenceNotes = EditorGUILayout.TextField("Reference notes",motion.referenceNotes);
            if (EditorGUI.EndChangeCheck()) profileDirty=true;
            EditorGUILayout.LabelField("Approval: "+motion.approvalStatus);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save ONLY keyframe profile")) SaveReferenceProfile();
            if (GUILayout.Button("Discard draft / Reload profile")) { Reset(); LoadReferenceProfile(); }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
