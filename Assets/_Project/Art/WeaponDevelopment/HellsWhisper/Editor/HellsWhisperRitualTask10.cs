#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    internal static partial class HellsWhisperReloadTask07
    {
        static float modelLengthWorld;
        static Vector3 unitsPerLength;
        static GameObject ritualRoot;
        static readonly ParticleSystem[] layers = new ParticleSystem[4];
        static readonly bool[] layerEnabled = {true,true,true,true};
        static bool cavityAnchorConfirmed;
        static Vector3 cavityAnchor;
        static bool ritualControlsOpen = true;
        static Vector3 PresentationOffset(Vector3 p) => motion.positionUnits=="ModelLengthFraction" ? Vector3.Scale(p,unitsPerLength) : p;
        static void MeasurePresentationLength(MeshRenderer renderer)
        {
            var b=renderer.localBounds;
            modelLengthWorld=Mathf.Max(renderer.transform.TransformVector(Vector3.right*b.size.x).magnitude,
                renderer.transform.TransformVector(Vector3.up*b.size.y).magnitude,renderer.transform.TransformVector(Vector3.forward*b.size.z).magnitude);
            var parent=pivot.parent;
            unitsPerLength=new Vector3(modelLengthWorld/parent.TransformVector(Vector3.right).magnitude,
                modelLengthWorld/parent.TransformVector(Vector3.up).magnitude,modelLengthWorld/parent.TransformVector(Vector3.forward).magnitude);
        }
        static float Pulse(float seconds,float start,float peak,float end)
        { return seconds<start || seconds>end ? 0 : seconds<peak ? Ease((seconds-start)/(peak-start)) : 1-Ease((seconds-peak)/(end-peak)); }
        static void EnsureRitualPreview()
        {
            if(ritualRoot!=null && ritualRoot.transform.parent==pivot) return;
            DestroyRitualPreview();
            ritualRoot=new GameObject("Task10_Transient_Ritual_Preview_NOT_SOCKETS") {hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(ritualRoot,Lab); ritualRoot.transform.SetParent(pivot,false);
            string[] names={"A_Heartbeat","B_Cavities_MANUAL_ANCHOR_REQUIRED","C_Runes_PRESENTATION_SPACE","D_RitualEnergy"};
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"MAT_HellsWhisper_Recharge_Task07.mat");
            for(int i=0;i<4;i++)
            {
                var go=new GameObject(names[i]) {hideFlags=HideFlags.HideAndDontSave}; go.transform.SetParent(ritualRoot.transform,false); go.layer=pivot.gameObject.layer;
                var ps=go.AddComponent<ParticleSystem>(); layers[i]=ps;
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.startSpeed=0;main.maxParticles=512;
                main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var r=go.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.renderMode=ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        static void DestroyRitualPreview()
        {
            CleanupRefined11();
            if(ritualRoot!=null) UnityEngine.Object.DestroyImmediate(ritualRoot);
            ritualRoot=null;for(int i=0;i<4;i++)layers[i]=null;
        }
        static void ClearRitualPreview()
        { foreach(var ps in layers) if(ps!=null)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); }
        static int PreviewParticleCount() => (particles==null ? 0 : particles.particleCount)+layers.Where(p=>p!=null).Sum(p=>p.particleCount);
        static void Put(int layer,List<ParticleSystem.Particle> points)
        {
            var ps=layers[layer];
            if(!vfx || !layerEnabled[layer] || points.Count==0) {ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            ps.Pause(true);ps.SetParticles(points.ToArray(),points.Count);
        }
        static void Dot(List<ParticleSystem.Particle> list,Vector3 position,float size,float alpha)
        {
            if(alpha<=.001f || list.Count>=512)return;
            list.Add(new ParticleSystem.Particle {position=position,startSize=size,startLifetime=10,remainingLifetime=10,
                startColor=new Color(1,.025f,.008f,Mathf.Clamp01(alpha)),randomSeed=(uint)(list.Count+1)});
        }
        static void Ring(List<ParticleSystem.Particle> list,Vector3 center,Vector3 right,Vector3 up,float radius,float size,float alpha,int count=80)
        {for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;Dot(list,center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius,size,alpha);}}
        static void Stroke(List<ParticleSystem.Particle> list,Vector3 a,Vector3 b,float size,float alpha)
        {for(int i=0;i<=10;i++)Dot(list,Vector3.Lerp(a,b,i/10f),size,alpha);}
        static void SampleRitual(float normalized)
        {
            EnsureRitualPreview();
            if(visibilitySelected13) { SampleVisibility13(normalized); return; }
            if(refinedSelected) { SampleRefined11(normalized); return; }
            SetRitualMaterials(false);
            float s=normalized*2.2f,L=unitsPerLength.x;
            var camera=Root.Find("FPSCameraRig/PreviewCamera").GetComponent<Camera>();
            var right=pivot.InverseTransformDirection(camera.transform.right).normalized;
            var up=pivot.InverseTransformDirection(camera.transform.up).normalized;
            // A camera-facing presentation plane just in front of the model bounds.
            // This is not a bone/surface anchor and never moves the camera or mesh.
            var model=pivot.GetComponentInChildren<MeshRenderer>();var bounds=model.localBounds;
            float nearest=float.PositiveInfinity;
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                nearest=Mathf.Min(nearest,camera.transform.InverseTransformPoint(model.transform.TransformPoint(corner)).z);
            }
            var centerCamera=camera.transform.InverseTransformPoint(pivot.TransformPoint(effectCenter));
            centerCamera *= (nearest-modelLengthWorld*.02f)/centerCamera.z;
            Vector3 center=pivot.InverseTransformPoint(camera.transform.TransformPoint(centerCamera));
            var a=new List<ParticleSystem.Particle>();
            float first=Pulse(s,.45f,.54f,.72f),second=Pulse(s,.96f,1.07f,1.28f);
            float beat=first*.55f+second;
            Ring(a,center,right,up,L*(.13f+.065f*beat),L*.018f,beat*.85f);
            Ring(a,center,right,up,L*(.09f+.04f*beat),L*.013f,beat*.65f,56);Put(0,a);
            var b=new List<ParticleSystem.Particle>();
            if(cavityAnchorConfirmed)
            {
                float glow=Pulse(s,.4f,1.07f,1.8f)*(.25f+beat*.5f);
                Dot(b,PresentationOffset(cavityAnchor),L*.025f,glow);Put(1,b);
            }
            else Put(1,b);
            var c=new List<ParticleSystem.Particle>();float rune=Pulse(s,.15f,1.58f,1.95f);
            for(int i=0;i<3;i++)
            {
                float angle=i*2*Mathf.PI/3+.35f;
                Vector3 origin=center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*(L*.18f);
                float h=L*.035f;
                Stroke(c,origin-up*h,origin+up*h,L*.011f,rune*.9f);
                Stroke(c,origin+up*h,origin+right*h,L*.011f,rune*.9f);
                Stroke(c,origin+right*h,origin-up*h*.3f,L*.011f,rune*.9f);
                Stroke(c,origin-right*h*.6f,origin+right*h*.6f,L*.011f,rune*.9f);
            }
            Put(2,c);
            var d=new List<ParticleSystem.Particle>();float ritual=Pulse(s,1.4f,1.62f,1.78f);
            if(ritual>0)
            {
                float phase=Mathf.Clamp01((s-1.4f)/.38f);
                for(int i=0;i<64;i++)
                {
                    float angle=i*2.39996f,age=Mathf.Repeat(phase+i*.618034f,1);
                    Dot(d,center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*L*.22f*(1-Ease(age)),L*.016f,ritual*Mathf.Sin(age*Mathf.PI)*.8f);
                }
                Ring(d,center,right,up,L*.16f,L*.015f,Pulse(s,1.54f,1.62f,1.75f)*.85f);
            }
            Put(3,d);
        }
        static void DrawRitualControls()
        {
            EditorGUILayout.LabelField("Compare VFX / presentation",EditorStyles.boldLabel);
            int current=visibilitySelected13 ? 0 : refinedSelected ? 1 : ritualSelected ? 2 : 3;
            int selected=GUILayout.Toolbar(current,new[]{"Task13","Task11","Task10","Task08"});
            if(selected!=current)
            {Reset();DestroyRitualPreview();visibilitySelected13=selected==0;refinedSelected=selected<=1;ritualSelected=selected!=3;durationOverride=false;motion=null;hasRest=false;LoadReferenceProfile();Bind();}
            if(!ritualSelected)return;
            Bind();EditorGUILayout.LabelField($"Measured calibrated model length L: {modelLengthWorld:F6} m");
            DrawVisibilityControls13();
            DrawRefinementControls11();
            ritualControlsOpen=EditorGUILayout.Foldout(ritualControlsOpen,"Task 10 — VFX layers / anchor review",true);
            if(!ritualControlsOpen)return;
            string[] names=visibilitySelected13 ? new[]{"A Blood aura / heartbeat glow","B Bone cavities — NOT VERIFIED","C Blood motes","D Swirling ritual mist"} : new[]{"A Heartbeat Pulses","B Bone Cavities (anchor pending)","C Blood Runes (presentation space)","D Ritual Energy"};
            EditorGUI.BeginChangeCheck();
            for(int i=0;i<4;i++)layerEnabled[i]=EditorGUILayout.Toggle(names[i],layerEnabled[i]);
            using(new EditorGUI.DisabledScope(refinedSelected || visibilitySelected13))
            cavityAnchorConfirmed=EditorGUILayout.Toggle("Enable manually placed cavity anchor",cavityAnchorConfirmed);
            if(cavityAnchorConfirmed)cavityAnchor=EditorGUILayout.Vector3Field("Cavity anchor fraction of L (unverified)",cavityAnchor);
            if(EditorGUI.EndChangeCheck())Reset();
            EditorGUILayout.HelpBox(visibilitySelected13 ? "Task13 glows use visible ray/mesh surface samples, not verified bone cavities or source-symbol anchors. B remains empty. Preview only; no source materials or prefabs are changed." : "Bone cavity anchor is NOT calibrated. B emits nothing until manually placed and enabled. C/D are free-floating ritual marks, not surface-matched symbols. Anchors/layer toggles are session-only.",MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("First peak .54s"))PreviewAt(.54f/2.2f);
            if(GUILayout.Button("Second peak 1.07s"))PreviewAt(1.07f/2.2f);
            if(GUILayout.Button("Ritual peak 1.62s"))PreviewAt(1.62f/2.2f);
            EditorGUILayout.EndHorizontal();
        }
        [Serializable] sealed class RitualQA
        {
            public float modelLengthMetres, firstMotionExcursionL, secondMotionExcursionL, nearestDepth;
            public bool thirteenKeys, cameraUnchanged, noOvershoot, secondBeatStronger, layersClear, cavityAnchorPending;
            public int[] particlesAtFirstPeak,particlesAtSecondPeak,particlesAtRitualPeak;
        }
        static void ValidateRitualSpecification()
        {
            Reset();Bind();var cam=Root.Find("FPSCameraRig/PreviewCamera");string before=TransformState(cam);
            var q=new RitualQA {modelLengthMetres=modelLengthWorld,thirteenKeys=motion.keys.Length==13,noOvershoot=true,cavityAnchorPending=!cavityAnchorConfirmed,nearestDepth=float.PositiveInfinity};
            for(int i=0;i<motion.keys.Length-1;i++)
            {
                var a=motion.keys[i];var b=motion.keys[i+1];
                for(int j=0;j<=10;j++)
                {
                    float t=Mathf.Lerp(a.time,b.time,j/10f);motion.Evaluate(t,out var pos,out var rot);
                    for(int axis=0;axis<3;axis++)q.noOvershoot &= pos[axis]>=Mathf.Min(a.positionOffset[axis],b.positionOffset[axis])-.00001f && pos[axis]<=Mathf.Max(a.positionOffset[axis],b.positionOffset[axis])+.00001f && rot[axis]>=Mathf.Min(a.rotationDegrees[axis],b.rotationDegrees[axis])-.00001f && rot[axis]<=Mathf.Max(a.rotationDegrees[axis],b.rotationDegrees[axis])+.00001f;
                }
            }
            foreach(var key in motion.keys)
            {
                Sample(key.time);var renderer=pivot.GetComponentInChildren<MeshRenderer>();var bounds=renderer.localBounds;
                for(int i=0;i<8;i++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    q.nearestDepth=Mathf.Min(q.nearestDepth,cam.InverseTransformPoint(renderer.transform.TransformPoint(corner)).z);
                }
            }
            motion.Evaluate(.52f/2.2f,out var firstDown,out _);motion.Evaluate(.62f/2.2f,out var firstUp,out _);
            motion.Evaluate(1.05f/2.2f,out var secondDown,out _);motion.Evaluate(1.15f/2.2f,out var secondUp,out _);
            q.firstMotionExcursionL=Mathf.Abs(firstDown.y-firstUp.y);q.secondMotionExcursionL=Mathf.Abs(secondDown.y-secondUp.y);q.secondBeatStronger=q.secondMotionExcursionL>q.firstMotionExcursionL;
            Sample(.54f/2.2f);q.particlesAtFirstPeak=layers.Select(p=>p.particleCount).ToArray();Capture("IMG_Task10_FirstPeak.png");
            Sample(1.07f/2.2f);q.particlesAtSecondPeak=layers.Select(p=>p.particleCount).ToArray();Capture("IMG_Task10_SecondPeak.png");
            Sample(1.62f/2.2f);q.particlesAtRitualPeak=layers.Select(p=>p.particleCount).ToArray();Capture("IMG_Task10_RitualPeak.png");
            Reset();q.layersClear=PreviewParticleCount()==0;q.cameraUnchanged=before==TransformState(cam);
            Require(q.thirteenKeys && q.noOvershoot && q.secondBeatStronger && q.layersClear && q.cameraUnchanged && q.nearestDepth>cam.GetComponent<Camera>().nearClipPlane,"Ritual technical QA failed");
            File.WriteAllText(Audit+"ritual_qa_task10.json",JsonUtility.ToJson(q,true));
        }
    }
}
#endif
