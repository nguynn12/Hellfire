#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    internal static partial class HellsWhisperReloadTask07
    {
        const string Config11=Folder+"VFX_HellsWhisper_Refinement_Task11.json";
        static bool refinedSelected=true,qaToolsOpen,frameExperiment,qaGizmos=true;
        static Material refinedInstance;
        static Vector3 savedAnchorPosition,savedCalibrationScale;
        [Serializable] sealed class FramingPreset11
        {public float uniformScaleMultiplier=1.25f;public Vector3 cameraLocalAnchorOffset=new Vector3(-.04f,0,0);}
        static Vector2 probeUV=new Vector2(.70f,.30f);
        static ConfigData11 config11;
        [Serializable] sealed class Anchor11
        { public string name,confidence,meshGuid;public long meshFileId;public int triangle;public Vector3 position,normal;public float recessMetres,clearanceMetres; }
        [Serializable] sealed class ConfigData11
        {
            public int schemaVersion=1;public Anchor11[] anchors=Array.Empty<Anchor11>();
            public string symbolAnchorsStatus="NOT VERIFIED",cavityAnchorsStatus="NOT VERIFIED";
        }
        static ConfigData11 Config11Data => config11 ?? (config11=JsonUtility.FromJson<ConfigData11>(File.ReadAllText(Config11)));
        static Camera Fps11 => Root.Find("FPSCameraRig/PreviewCamera").GetComponent<Camera>();
        static MeshRenderer Model11 => pivot.GetComponentInChildren<MeshRenderer>();
        static void SetRitualMaterials(bool refined)
        {
            Material mat;
            if(refined)
            {
                if(refinedInstance==null)
                {
                    var source=AssetDatabase.LoadAssetAtPath<Material>(Folder+"MAT_HellsWhisper_BloodMist_Task11.mat");
                    Require(source!=null,"Task11 material not imported");
                    refinedInstance=new Material(source){hideFlags=HideFlags.HideAndDontSave};
                }
                mat=refinedInstance;
            }
            else mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"MAT_HellsWhisper_Recharge_Task07.mat");
            foreach(var ps in layers)if(ps!=null)ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;
        }
        static void CleanupRefined11()
        {CleanupVisibility13();if(refinedInstance!=null)UnityEngine.Object.DestroyImmediate(refinedInstance);refinedInstance=null;}
        static void EnsureAnchorNodes11()
        {
            EnsureRitualPreview();
            foreach(var a in Config11Data.anchors??Array.Empty<Anchor11>())
            {
                string name="Task11_"+a.name;
                var found=ritualRoot.transform.Find(name);
                if(found==null)
                {var go=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(ritualRoot.transform,false);found=go.transform;}
                found.localPosition=a.position;found.localRotation=Quaternion.FromToRotation(Vector3.forward,a.normal);
            }
        }
        static Vector3 PresentationPlane11(Camera cam)
        {
            var r=Model11;var b=r.localBounds;float nearest=float.PositiveInfinity;
            for(int i=0;i<8;i++)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                nearest=Mathf.Min(nearest,cam.transform.InverseTransformPoint(r.transform.TransformPoint(corner)).z);
            }
            var center=cam.transform.InverseTransformPoint(pivot.TransformPoint(effectCenter));
            center*=(nearest-modelLengthWorld*.012f)/center.z;
            return pivot.InverseTransformPoint(cam.transform.TransformPoint(center));
        }
        static void SoftArc11(List<ParticleSystem.Particle> dots,Vector3 center,Vector3 right,Vector3 up,float radius,float alpha,float phase)
        {
            for(int i=0;i<170;i++)
            {
                float angle=i*2.399963f;
                float arc=.5f+.5f*Mathf.Sin(angle*2+.7f);
                float gap=Ease((arc-.20f)/.45f);
                float distortion=1+.13f*Mathf.Sin(angle*3+.6f)+.055f*Mathf.Sin(angle*7-phase*2);
                float size=radius*(.20f+.055f*Mathf.Sin(angle*5));
                Vector3 point=center+(right*Mathf.Cos(angle)*1.03f+up*Mathf.Sin(angle)*.87f)*radius*distortion;
                Dot(dots,point,size,alpha*gap*(.7f+.3f*Mathf.Sin(angle*4+1)));
            }
        }
        static void SampleRefined11(float normalized)
        {
            EnsureAnchorNodes11();
            SetRitualMaterials(true);refinedInstance.SetFloat("_Phase",normalized);
            float s=normalized*2.2f,L=unitsPerLength.x;
            var cam=Fps11;Vector3 center=PresentationPlane11(cam);
            var right=pivot.InverseTransformDirection(cam.transform.right).normalized;
            var up=pivot.InverseTransformDirection(cam.transform.up).normalized;
            float beat=Pulse(s,.45f,.54f,.72f)*.55f+Pulse(s,.96f,1.07f,1.28f);
            var a=new List<ParticleSystem.Particle>();
            SoftArc11(a,center,right,up,L*(.12f+.025f*beat),beat*.22f,normalized);Put(0,a);
            var b=new List<ParticleSystem.Particle>();
            foreach(var anchor in Config11Data.anchors??Array.Empty<Anchor11>())
            {
                float glow=Pulse(s,.4f,1.07f,1.8f)*(.2f+beat*.55f);
                Dot(b,anchor.position,L*.016f,glow);
                Dot(a,anchor.position,L*.022f,beat*.45f);
            }
            Put(0,a);Put(1,b);
            // No invented glyphs: source-symbol anchors have not been identified.
            Put(2,new List<ParticleSystem.Particle>());
            var d=new List<ParticleSystem.Particle>();float ritual=Pulse(s,1.4f,1.62f,1.78f);
            SoftArc11(d,center,right,up,L*.14f,ritual*.20f,normalized);
            if(ritual>0)
                for(int i=0;i<40;i++)
                {
                    float age=Mathf.Repeat((s-1.4f)/.38f+i*.618034f,1),angle=i*2.399963f+age*.8f;
                    Vector3 point=center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle)*.8f)*L*.19f*(1-Ease(age));
                    Dot(d,point,L*.023f,ritual*Mathf.Sin(age*Mathf.PI)*.21f);
                }
            Put(3,d);
        }
        static void ApplyFraming11(bool enable)
        {
            Reset();Bind();if(enable==frameExperiment)return;
            var calibration=pivot.parent;var anchor=calibration.parent;
            if(enable)
            {
                savedAnchorPosition=anchor.localPosition;savedCalibrationScale=calibration.localScale;
                var preset=JsonUtility.FromJson<FramingPreset11>(File.ReadAllText(Folder+"CAL_HellsWhisper_Experimental_Task11.json"));
                Require(preset.uniformScaleMultiplier>0 && !float.IsNaN(preset.uniformScaleMultiplier) && !float.IsInfinity(preset.uniformScaleMultiplier),"Invalid experimental framing scale");
                anchor.localPosition+=preset.cameraLocalAnchorOffset;calibration.localScale*=preset.uniformScaleMultiplier;
            }
            else {anchor.localPosition=savedAnchorPosition;calibration.localScale=savedCalibrationScale;}
            frameExperiment=enable;hasRest=false;Bind();Changed?.Invoke();
        }
        static void RestoreFraming11()
        {if(frameExperiment && pivot!=null)ApplyFraming11(false);}
        static void GuardFraming11()
        {if(frameExperiment && (!EditorWindow.HasOpenInstances<HellsWhisperVisualLabTask05>() || EditorApplication.isPlayingOrWillChangePlaymode))RestoreFraming11();}
        [Serializable] sealed class Frame11
        {
            public string variant,preset;public float seconds,fov,nearClip,uniformScale,modelLength;
            public Vector3 anchorPosition,modelViewportMin,modelViewportMax;
            public bool boundsWithinFrame,clipClear;
        }
        [Serializable] sealed class Frames11 { public Frame11[] frames;public string scenesBefore,scenesAfter; }
        static Frame11 FrameMetrics11(float seconds)
        {
            var cam=Fps11;var renderer=Model11;var b=renderer.localBounds;
            var min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
            float oldAspect=cam.aspect;cam.aspect=16f/9;
            try
            {
                for(int i=0;i<8;i++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var screen=cam.WorldToViewportPoint(renderer.transform.TransformPoint(corner));min=Vector3.Min(min,screen);max=Vector3.Max(max,screen);
                }
            }
            finally {cam.aspect=oldAspect;}
            return new Frame11 {variant=refinedSelected?"Task11_Refined":"Task10_Original",preset=frameExperiment?"Experimental125":"Baseline",
                seconds=seconds,fov=cam.fieldOfView,nearClip=cam.nearClipPlane,uniformScale=pivot.parent.localScale.x,
                anchorPosition=pivot.parent.parent.localPosition,modelLength=modelLengthWorld,modelViewportMin=min,modelViewportMax=max,
                boundsWithinFrame=min.x>=0&&min.y>=0&&max.x<=1&&max.y<=1,clipClear=min.z>cam.nearClipPlane};
        }
        static void CaptureClean11(string path)
        {
            var method=typeof(HellsWhisperVisualLabTask05).GetMethod("RenderPreview",BindingFlags.NonPublic|BindingFlags.Static);
            var texture=(Texture2D)method.Invoke(null,new object[]{Fps11,1920,1080});
            try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void ExportQA11()
        {
            Reset();Bind();Directory.CreateDirectory(Audit);
            string before=OtherScenes();bool oldRefined=refinedSelected,oldFrame=frameExperiment,old13=visibilitySelected13;var records=new List<Frame11>();
            try
            {
                visibilitySelected13=false;
                foreach(bool experimental in new[]{false,true})
                {
                    ApplyFraming11(experimental);
                    foreach(bool refined in new[]{false,true})
                    {
                        refinedSelected=refined;
                        foreach(float seconds in new[]{0,.54f,1.07f,1.62f,2.2f})
                        {
                            Sample(seconds/2.2f);var row=FrameMetrics11(seconds);records.Add(row);
                            CaptureClean11(Audit+$"{row.preset}_{row.variant}_{Mathf.RoundToInt(seconds*100):000}_CLEAN.png");
                        }
                    }
                }
                Require(before==OtherScenes(),"Other scene state changed");
                File.WriteAllText(Audit+"framing_qa_task11.json",JsonUtility.ToJson(new Frames11{frames=records.ToArray(),scenesBefore=before,scenesAfter=OtherScenes()},true));
                message="Exported 20 clean camera frames. No video generated; no scene or motion profile saved.";
            }
            finally{Reset();ApplyFraming11(oldFrame);refinedSelected=oldRefined;visibilitySelected13=old13;Changed?.Invoke();}
        }
        [Serializable] sealed class Probe11
        {
            public string name,status,method,meshGuid;public long meshFileId;public int triangle;
            public Vector2 viewport;public Vector3 pointLocal,normalLocal;
            public float depth,meanNeighbourDepth,recess,frontFacing,rayConsistency,clearance;
            public float[] neighbourDepths;public bool surfaceHit,visible,temporaryAnchorCreated;
        }
        static bool RayMesh11(Vector2 uv,out RaycastHit hit)
        {
            var r=Model11;var mesh=r.GetComponent<MeshFilter>().sharedMesh;
            var method=typeof(HandleUtility).GetMethod("IntersectRayMesh",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,
                new[]{typeof(Ray),typeof(Mesh),typeof(Matrix4x4),typeof(RaycastHit).MakeByRefType()},null);
            if(method==null)throw new NotSupportedException("Editor ray/mesh API unavailable; no guessed anchor created");
            var ray=Fps11.ViewportPointToRay(uv);object[] args={ray,mesh,r.localToWorldMatrix,new RaycastHit()};
            bool ok=(bool)method.Invoke(null,args);hit=(RaycastHit)args[3];return ok;
        }
        static void ProbeAnchor11(Vector2 uv,string name)
        {
            Reset();Bind();ApplyFraming11(false);Directory.CreateDirectory(Audit);
            var cam=Fps11;float oldAspect=cam.aspect;cam.aspect=16f/9;
            var q=new Probe11{name=name,viewport=uv,status="NOT VERIFIED",method="HandleUtility.IntersectRayMesh + neighbour depth + visibility"};
            try
            {
                var mesh=Model11.GetComponent<MeshFilter>().sharedMesh;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out q.meshGuid,out q.meshFileId);
                q.surfaceHit=RayMesh11(uv,out var hit);
                if(q.surfaceHit)
                {
                    var ray=cam.ViewportPointToRay(uv);q.rayConsistency=Vector3.Distance(hit.point,ray.GetPoint(hit.distance));
                    q.triangle=hit.triangleIndex;q.depth=cam.WorldToViewportPoint(hit.point).z;
                    var normal=hit.normal.normalized;
                    q.frontFacing=Vector3.Dot(normal,(cam.transform.position-hit.point).normalized);
                    var depths=new List<float>();
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI/4;var sample=uv+new Vector2(Mathf.Cos(a)*.007f,Mathf.Sin(a)*.0124f);
                        if(RayMesh11(sample,out var h))depths.Add(cam.WorldToViewportPoint(h.point).z);
                    }
                    q.neighbourDepths=depths.ToArray();q.meanNeighbourDepth=depths.Count>0?depths.Average():q.depth;q.recess=q.depth-q.meanNeighbourDepth;
                    var point=hit.point+normal*.0005f;var screen=cam.WorldToViewportPoint(point);
                    q.visible=screen.z>cam.nearClipPlane&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1;
                    if(RayMesh11(new Vector2(screen.x,screen.y),out var obstacle))
                    {q.clearance=Vector3.Distance(cam.transform.position,obstacle.point)-Vector3.Distance(cam.transform.position,point);q.visible&=q.clearance>=-.00005f;}
                    q.pointLocal=pivot.InverseTransformPoint(point);q.normalLocal=pivot.InverseTransformDirection(normal);
                    if(depths.Count>=6 && q.recess>.00075f && q.frontFacing>.1f && q.visible && q.rayConsistency<.0001f)
                    {
                        q.status="GEOMETRY_RECESS_VISIBLE__PENDING_ARTISTIC_CONFIRMATION";
                        var existing=(Config11Data.anchors??Array.Empty<Anchor11>()).Where(a=>a.name!=name).ToList();
                        if(existing.Count<3)
                        {
                            existing.Add(new Anchor11{name=name,position=q.pointLocal,normal=q.normalLocal,confidence="MEDIUM: ray-mesh recess and visibility measured; semantic cavity needs visual confirmation",meshGuid=q.meshGuid,meshFileId=q.meshFileId,triangle=q.triangle,recessMetres=q.recess,clearanceMetres=q.clearance});
                            Config11Data.anchors=existing.ToArray();Config11Data.cavityAnchorsStatus="Geometry measured; visual review pending";
                            File.WriteAllText(Config11,JsonUtility.ToJson(Config11Data,true));EnsureAnchorNodes11();q.temporaryAnchorCreated=true;
                        }
                    }
                }
            }
            catch(Exception e){q.status="NOT VERIFIED: "+e.GetBaseException().Message;}
            finally {cam.aspect=oldAspect;}
            File.WriteAllText(Audit+name+"_geometry_probe.json",JsonUtility.ToJson(q,true));
            PreviewAt(1.07f/2.2f);CaptureClean11(Audit+name+"_anchor_preview_CLEAN.png");message=q.status;
        }
        static void DrawGizmos11(SceneView view)
        {
            if(!qaGizmos || !ritualSelected || pivot==null)return;
            var old=Handles.color;Handles.color=Color.yellow;
            var r=Model11;Handles.DrawWireCube(r.bounds.center,r.bounds.size);
            Handles.Label(r.bounds.center+Vector3.up*.15f,"TASK11 QA GIZMOS — not VFX");
            foreach(var a in Config11Data.anchors??Array.Empty<Anchor11>())
            {Vector3 point=pivot.TransformPoint(a.position);Handles.DrawWireDisc(point,Fps11.transform.forward,.006f);Handles.Label(point,a.name);}
            Handles.color=old;
        }
        static void DrawRefinementControls11()
        {
            if(!ritualSelected)return;
            qaToolsOpen=EditorGUILayout.Foldout(qaToolsOpen,"Task 11 — Visual QA / framing / geometry",true);if(!qaToolsOpen)return;
            EditorGUILayout.HelpBox("Motion profiles are protected. Refined removes arrow glyphs and regular dotted rings. Source-symbol anchors remain NOT VERIFIED. Experimental framing is session-only and restored before scene save/close.",MessageType.Info);
            if(GUILayout.Button(frameExperiment?"Restore baseline calibration":"Try experimental framing x1.25 (not approved)"))ApplyFraming11(!frameExperiment);
            if(GUILayout.Button("Export Original / Refined QA frames"))ExportQA11();
            probeUV=EditorGUILayout.Vector2Field("Cavity probe UV (clean camera)",probeUV);
            if(GUILayout.Button("Probe geometry — candidate 1"))ProbeAnchor11(probeUV,"CavityCandidate1");
            if(GUILayout.Button("Probe geometry — candidate 2"))ProbeAnchor11(probeUV,"CavityCandidate2");
            if(GUILayout.Button("Probe geometry — candidate 3"))ProbeAnchor11(probeUV,"CavityCandidate3");
            EditorGUILayout.LabelField("Geometry-supported candidates: "+(Config11Data.anchors?.Length??0));
            foreach(var a in Config11Data.anchors??Array.Empty<Anchor11>())EditorGUILayout.LabelField(a.name+": "+a.position.ToString("F5"),EditorStyles.wordWrappedLabel);
            qaGizmos=EditorGUILayout.Toggle("Scene View QA Gizmos",qaGizmos);
            if(GUILayout.Button("Show Scene View with QA Gizmos"))
            {
                var sceneView=EditorWindow.GetWindow<SceneView>();
                sceneView.LookAt(Model11.bounds.center,Fps11.transform.rotation,modelLengthWorld*.8f,false,true);
                sceneView.camera.cullingMask=-1;sceneView.drawGizmos=true;sceneView.Show();sceneView.Repaint();
            }
        }
    }
}
#endif
