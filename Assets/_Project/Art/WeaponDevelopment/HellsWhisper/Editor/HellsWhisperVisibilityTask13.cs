#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    internal static partial class HellsWhisperReloadTask07
    {
        const string Audit13="/private/tmp/hellfire_task13_90aljs_7/";
        static bool visibilitySelected13=true;
        static Material bloodInstance13;
        static readonly List<SurfaceSite13> sites13=new List<SurfaceSite13>();
        [Serializable] sealed class SurfaceSite13
        {
            public string region,status="Visible mesh surface sample; semantic location pending visual review. NOT a cavity anchor.";
            public Vector3 localPosition,localNormal;public Vector2 viewport;public int triangle;
        }
        static void CleanupVisibility13()
        {if(bloodInstance13!=null)UnityEngine.Object.DestroyImmediate(bloodInstance13);bloodInstance13=null;}
        static void SetBloodMaterial13(float t)
        {
            if(bloodInstance13==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(Folder+"MAT_HellsWhisper_BloodPresence_Task13.mat");
                Require(source!=null,"Task13 material not imported");
                bloodInstance13=new Material(source){hideFlags=HideFlags.HideAndDontSave};
            }
            bloodInstance13.SetFloat("_Phase",t);
            foreach(var ps in layers)ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=bloodInstance13;
        }
        // Rays sample the current mesh. The UV seeds are presentation guides, not inferred 3D sockets.
        // Store points in ReloadPresentationPivot space, so they follow the existing motion exactly.
        static void EnsureSites13()
        {
            if(sites13.Count>0)return;
            var seeds=frameExperiment ? new[]{new Vector2(.681f,.455f),new Vector2(.65f,.55f),new Vector2(.696f,.305f),new Vector2(.637f,.602f)}
                : new[]{new Vector2(.705f,.412f),new Vector2(.68f,.505f),new Vector2(.711f,.293f),new Vector2(.657f,.558f)};
            string[] names={"Organic grip region","Body / grip junction","Grip tail / skull region","Upper body silhouette region"};
            float aspect=Fps11.aspect;Fps11.aspect=16f/9;
            try
            {
                for(int i=0;i<seeds.Length;i++)
                {
                    bool found=false;RaycastHit hit=default;Vector2 actual=seeds[i];
                    for(int ring=0;ring<=4&&!found;ring++)
                        for(int n=0;n<(ring==0?1:12)&&!found;n++)
                        {
                            float angle=n*Mathf.PI/6;
                            var uv=seeds[i]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(ring*.003f);
                            if(RayMesh11(uv,out hit)){actual=uv;found=true;}
                        }
                    if(found)
                    {
                        var normal=hit.normal.normalized;
                        sites13.Add(new SurfaceSite13{region=names[i],viewport=actual,triangle=hit.triangleIndex,
                            localPosition=pivot.InverseTransformPoint(hit.point+(Fps11.transform.position-hit.point).normalized*.003f),
                            localNormal=pivot.InverseTransformDirection(normal)});
                    }
                }
            }
            finally{Fps11.aspect=aspect;}
            Require(sites13.Count>=3,"Task13 needs at least three actual mesh surface hits; no guessed sites used");
        }
        static readonly HellsWhisperBloodPresence approvedBlood13=new HellsWhisperBloodPresence();
        static void SampleVisibility13(float t)
        {
            EnsureRitualPreview();EnsureSites13();SetBloodMaterial13(t);
            approvedBlood13.Sample(t,unitsPerLength.x,pivot,Fps11,Model11,sites13.Select(x=>x.localPosition).ToArray(),Put);
        }
        [Serializable] sealed class Review13
        {
            public string status,scenesBefore,scenesAfter,profileHashBefore,profileHashAfter;
            public float duration,positionResetError,rotationResetError,scaleResetError;
            public bool keysUnchanged,transformRestored,vfxCleared,framingRestored;
            public int keyCount,missingScripts,shaderErrors;public SurfaceSite13[] surfaceSites;
            public Frame11[] frames;
        }
        [MenuItem("Hellfire/Weapon Development/Task13 Export and Validate Blood VFX")]
        static void ExportVisibility13()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit Mode only");
            Reset();Bind();Require(Root!=null,"Open the existing Hell's Whisper VisualLab first");
            var oldMotion=motion;float oldDuration=duration,oldIntensity=intensity;bool oldOverride=durationOverride,oldVfx=vfx;
            bool old13=visibilitySelected13,oldRefined=refinedSelected,oldRitual=ritualSelected,oldFrame=frameExperiment;
            var oldLayers=(bool[])layerEnabled.Clone();var anchor=pivot.parent.parent;var calibration=pivot.parent;
            string oldAnchor=TransformState(anchor),oldCalib=TransformState(calibration);
            var report=new Review13{scenesBefore=OtherScenes(),profileHashBefore=Hash(RitualProfilePath)};
            string output=Path.Combine(Audit13,"Review_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(output);
            var frames=new List<Frame11>();
            try
            {
                ritualSelected=true;LoadReferenceProfile();durationOverride=false;intensity=1;vfx=true;
                for(int i=0;i<4;i++)layerEnabled[i]=true;
                ApplyFraming11(true);sites13.Clear();EnsureSites13();
                foreach(bool next in new[]{false,true})
                {
                    visibilitySelected13=next;refinedSelected=true;
                    foreach(float seconds in new[]{0,.54f,1.07f,1.62f,2.2f})
                    {
                        Sample(seconds/2.2f);var row=FrameMetrics11(seconds);row.variant=next?"Task13_BloodPresence":"Task11_Refined";frames.Add(row);
                        CaptureClean11(Path.Combine(output,$"{row.variant}_scale0.25_t{seconds:0.00}s_CLEAN.png"));
                        Require(row.boundsWithinFrame&&row.clipClear,"Model clipping at "+seconds);
                        foreach(var ps in layers)
                        {
                            var points=new ParticleSystem.Particle[ps.particleCount];int n=ps.GetParticles(points);
                            for(int i=0;i<n;i++)
                            {
                                var world=ps.transform.TransformPoint(points[i].position);float depth=Fps11.transform.InverseTransformPoint(world).z;
                                Require(depth-points[i].GetCurrentSize(ps)*ps.transform.lossyScale.x*.5f>Fps11.nearClipPlane,"VFX near clipping");
                            }
                        }
                    }
                }
                report.duration=EffectiveDuration;report.keyCount=motion.keys.Length;
                report.keysUnchanged=report.profileHashBefore==Hash(RitualProfilePath)&&motion.keys.Length==13;
                visibilitySelected13=true;Sample(1.62f/2.2f);Reset();
                report.positionResetError=Vector3.Distance(pivot.localPosition,restPosition);
                report.rotationResetError=Quaternion.Angle(pivot.localRotation,restRotation);
                report.scaleResetError=Vector3.Distance(pivot.localScale,restScale);
                report.transformRestored=report.positionResetError==0&&report.rotationResetError==0&&report.scaleResetError==0;
                report.vfxCleared=PreviewParticleCount()==0;
                report.missingScripts=Root.GetComponentsInChildren<Transform>(true).Sum(x=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
                var shader=Shader.Find("Hellfire/WeaponLab/BloodPresenceTask13");Require(shader!=null,"Task13 shader missing");
                report.shaderErrors=ShaderUtil.GetShaderMessages(shader).Count(x=>x.severity.ToString()=="Error");
                Require(report.keysUnchanged&&Mathf.Approximately(report.duration,2.2f)&&report.transformRestored&&report.vfxCleared&&report.missingScripts==0&&report.shaderErrors==0,"Task13 validation failed");
                report.surfaceSites=sites13.ToArray();report.frames=frames.ToArray();
            }
            finally
            {
                Reset();ApplyFraming11(oldFrame);motion=oldMotion;duration=oldDuration;intensity=oldIntensity;durationOverride=oldOverride;vfx=oldVfx;
                visibilitySelected13=old13;refinedSelected=oldRefined;ritualSelected=oldRitual;
                for(int i=0;i<4;i++)layerEnabled[i]=oldLayers[i];
                report.framingRestored=TransformState(anchor)==oldAnchor&&TransformState(calibration)==oldCalib;
                report.scenesAfter=OtherScenes();report.profileHashAfter=Hash(RitualProfilePath);
                report.status=report.framingRestored&&report.scenesBefore==report.scenesAfter&&report.transformRestored&&report.keysUnchanged ? "TECHNICAL_PASS_ARTISTIC_REVIEW_PENDING" : "FAILED";
                File.WriteAllText(Path.Combine(output,"validation_task13.json"),JsonUtility.ToJson(report,true));
                message="Task13 QA: "+report.status+". 10 clean frames in "+output+". Preview reset; framing restored; nothing saved to scene.";
                Debug.Log(message);Changed?.Invoke();
            }
        }
        static void DrawVisibilityControls13()
        {
            if(!visibilitySelected13)return;
            EditorGUILayout.HelpBox("Task13: blood aura / surface-following glow / mist / motes. Same 13 motion keys, 2.2 seconds. Cavity anchors remain NOT VERIFIED.",MessageType.Info);
            if(GUILayout.Button(frameExperiment?"Task13 review scale 0.25 — restore baseline":"Task13 review — try scale 0.25"))ApplyFraming11(!frameExperiment);
            if(GUILayout.Button("Task13 — Export + Validate"))ExportVisibility13();
        }
    }
}
#endif
