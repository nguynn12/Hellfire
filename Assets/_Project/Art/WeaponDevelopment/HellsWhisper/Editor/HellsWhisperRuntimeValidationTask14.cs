#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hellfire.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    // Explicit local QA only. No scene/prefab saves, network startup or asset mutation.
    [InitializeOnLoad]
    internal static class HellsWhisperRuntimeValidationTask14
    {
        const string Audit="/private/tmp/hellfire_task14_biu7597t/";
        const string Key="Hellfire.Task14.RuntimeQA";
        static IEnumerator routine;
        static Report report;
        static GameObject actor;
        static string output;
        [Serializable] sealed class Report
        {
            public string status,editorScenesBefore,editorScenesAfter,mode="Local Play Mode, actual Player prefab + WeaponController timer; no network session";
            public bool started,presentationStarted,completed,ammoGameplayOwned,cancelled,switchReset,disableReset,controllerDisableCancels,destroyClean,durationAdapted,task13Material;
            public float duration,observedSeconds,overrideDuration,overrideObserved,positionResetError,rotationResetError,scaleResetError,capturedProgress;
            public int missingScripts,shaderErrors;public string[] errors;
        }
        static HellsWhisperRuntimeValidationTask14()
        {EditorApplication.playModeStateChanged+=PlayState;}
        static void Check(bool value,string message)
        {if(!value)throw new InvalidOperationException(message);}
        static string Scenes()
        {return string.Join("\n",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Select(s=>$"{s.path}|{s.isDirty}|{s.rootCount}"));}
        [MenuItem("Hellfire/Weapon Development/Task14 Validate Runtime Integration (Play Mode)")]
        static void Begin()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop existing Play Mode first");
            SessionState.SetString(Key+".scenes",Scenes());SessionState.SetBool(Key,true);
            SessionState.SetBool(Key+".offline",GameModeHelper.ForceOfflineMode);
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Hellfire/Weapon Development/Task14 Inspect Console (read only)")]
        static void InspectConsole()
        {
            var entries=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            var entryType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntry");
            var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var counts=new object[]{0,0,0};entries.GetMethod("GetCountsByType",flags).Invoke(null,counts);
            var rows=new System.Collections.Generic.List<string>();
            int count=(int)entries.GetMethod("StartGettingEntries",flags).Invoke(null,null);
            try
            {
                for(int i=0;i<count;i++)
                {
                    var entry=Activator.CreateInstance(entryType,true);
                    entries.GetMethod("GetEntryInternal",flags).Invoke(null,new object[]{i,entry});
                    string message=(string)entryType.GetField("message",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(entry);
                    int mode=(int)entryType.GetField("mode",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(entry);
                    // LogEntry mode warning bits: runtime warning (1<<9), script compile warning (1<<8).
                    if((mode&((1<<9)|(1<<8)))!=0)rows.Add(message);
                }
            }
            finally{entries.GetMethod("EndGettingEntries",flags).Invoke(null,null);}
            File.WriteAllText(Audit+"console_task14.txt","errors="+counts[0]+" warnings="+counts[1]+" logs="+counts[2]+"\n"+string.Join("\n---\n",rows));
        }
        static void PlayState(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                output=Path.Combine(Audit,"Runtime_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(output);
                SessionState.SetString(Key+".output",output);
                report=new Report{editorScenesBefore=SessionState.GetString(Key+".scenes","")};
                routine=Run();EditorApplication.update+=Tick;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Tick;routine=null;
                output=SessionState.GetString(Key+".output","");
                if(report==null&&File.Exists(Path.Combine(output,"runtime_validation_task14.json")))
                    report=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(output,"runtime_validation_task14.json")));
                if(report!=null)
                {
                    report.editorScenesAfter=Scenes();
                    if(report.editorScenesBefore!=report.editorScenesAfter)report.status="FAILED_EDITOR_SCENE_STATE_CHANGED";
                    File.WriteAllText(Path.Combine(output,"runtime_validation_task14.json"),JsonUtility.ToJson(report,true));
                    Debug.Log("Task14 runtime QA: "+report.status+" — "+output);
                }
                GameModeHelper.ForceOfflineMode=SessionState.GetBool(Key+".offline",false);
                SessionState.SetBool(Key,false);
            }
        }
        static void Tick()
        {
            try
            {
                if(routine!=null&&!routine.MoveNext())Finish();
            }
            catch(Exception e)
            {
                report.status="FAILED";report.errors=new[]{e.GetBaseException().ToString()};Debug.LogException(e);Finish();
            }
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;routine=null;
            File.WriteAllText(Path.Combine(output,"runtime_validation_task14.json"),JsonUtility.ToJson(report,true));
            EditorApplication.ExitPlaymode();
        }
        static void Invoke(object target,string name)
        {target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
        static void Set(object target,string name,object value)
        {target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        static void ResetCheck(HellsWhisperReloadPresenter presenter)
        {
            var p=presenter.PresentationPivot;
            report.positionResetError=Vector3.Distance(p.localPosition,Vector3.zero);
            report.rotationResetError=Quaternion.Angle(p.localRotation,Quaternion.identity);
            report.scaleResetError=Vector3.Distance(p.localScale,Vector3.one);
            Check(report.positionResetError==0&&report.rotationResetError==0&&report.scaleResetError==0&&presenter.ParticleCount==0&&!presenter.IsPresenting,"Presenter did not reset cleanly");
        }
        static void Capture(Camera camera,string name)
        {
            var method=typeof(HellsWhisperVisualLabTask05).GetMethod("RenderPreview",BindingFlags.NonPublic|BindingFlags.Static);
            var image=(Texture2D)method.Invoke(null,new object[]{camera,1920,1080});
            try{File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(image);}
        }
        static IEnumerator Run()
        {
            // Deactivate only the PLAY MODE copies of loaded scenes. Edit Mode dirty state is restored by Unity on exit.
            for(int i=0;i<SceneManager.sceneCount;i++)
                foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())root.SetActive(false);
            GameModeHelper.ForceOfflineMode=true;
            var scene=SceneManager.CreateScene("Task14_Transient_Runtime_QA");SceneManager.SetActiveScene(scene);
            actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"));
            foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(behaviour is WeaponController)&&!(behaviour is HellsWhisperReloadPresenter)&&!(behaviour is UniversalAdditionalCameraData))behaviour.enabled=false;
            var controller=actor.GetComponent<WeaponController>();var presenter=actor.GetComponentInChildren<HellsWhisperReloadPresenter>(true);
            var camera=actor.GetComponentInChildren<Camera>(true);camera.gameObject.SetActive(true);camera.enabled=true;
            camera.fieldOfView=90;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.025f,.025f);
            camera.cullingMask=1<<presenter.gameObject.layer;
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.25f,.25f);
            var light=new GameObject("QA_Neutral_Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,-35,0);
            yield return null;yield return null;
            Check(controller.CurrentWeaponData.WeaponId=="pistol"&&presenter.PresentationPivot!=null,"Player prefab presenter not installed");
            report.missingScripts=actor.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            Set(controller,"_currentAmmo",5);Invoke(controller,"StartReload");report.started=controller.IsReloading;report.duration=controller.ActiveReloadDuration;
            float began=Time.time;bool captured=false;double deadline=EditorApplication.timeSinceStartup+10;
            while(controller.IsReloading)
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Real gameplay reload did not finish");
                Invoke(presenter,"LateUpdate");
                report.presentationStarted|=presenter.IsPresenting&&presenter.ParticleCount>0;
                if(!captured&&controller.ReloadProgress>=1.62f/2.2f)
                {report.capturedProgress=controller.ReloadProgress;Capture(camera,"Runtime_Task13_Ritual_1920x1080.png");captured=true;}
                yield return null;
            }
            report.observedSeconds=Time.time-began;report.completed=true;report.ammoGameplayOwned=controller.CurrentAmmo==controller.CurrentWeaponData.MagSize;
            ResetCheck(presenter);report.task13Material=presenter.GetComponentsInChildren<ParticleSystemRenderer>(true).All(x=>x.sharedMaterial.shader.name=="Hellfire/WeaponLab/BloodPresenceTask13");
            Set(controller,"_currentAmmo",5);Invoke(controller,"StartReload");
            while(controller.ReloadProgress<.25f)yield return null;
            controller.CancelReload();ResetCheck(presenter);report.cancelled=!controller.IsReloading&&controller.CurrentAmmo==5;
            Invoke(controller,"StartReload");yield return null;controller.SwitchWeapon(1);yield return null;
            ResetCheck(presenter);report.switchReset=!controller.IsReloading&&!presenter.PresentationPivot.parent.parent.gameObject.activeSelf;
            controller.SwitchWeapon(0);yield return null;Invoke(controller,"StartReload");yield return null;
            presenter.enabled=false;ResetCheck(presenter);report.disableReset=true;controller.CancelReload();presenter.enabled=true;yield return null;
            Invoke(controller,"StartReload");yield return null;controller.enabled=false;ResetCheck(presenter);report.controllerDisableCancels=!controller.IsReloading;
            controller.enabled=true;yield return null;
            // In-memory WeaponData clone tests duration adaptation without changing any source ScriptableObject.
            var clone=UnityEngine.Object.Instantiate(controller.CurrentWeaponData);var so=new SerializedObject(clone);so.FindProperty("_reloadTime").floatValue=1.1f;so.ApplyModifiedPropertiesWithoutUndo();
            Set(controller,"_currentWeapon",clone);Set(controller,"_currentAmmo",5);Invoke(controller,"StartReload");report.overrideDuration=controller.ActiveReloadDuration;began=Time.time;
            while(controller.IsReloading)yield return null;
            report.overrideObserved=Time.time-began;ResetCheck(presenter);report.durationAdapted=Mathf.Approximately(report.overrideDuration,1.1f)&&report.overrideObserved<1.5f&&report.overrideObserved>=1.09f;
            var shader=Shader.Find("Hellfire/WeaponLab/BloodPresenceTask13");report.shaderErrors=ShaderUtil.GetShaderMessages(shader).Count(x=>x.severity.ToString()=="Error");
            var runtimeRoot=presenter.PresentationPivot.parent.parent.gameObject;UnityEngine.Object.Destroy(actor);UnityEngine.Object.Destroy(clone);yield return null;yield return null;
            report.destroyClean=runtimeRoot==null;
            Check(report.started&&report.presentationStarted&&report.completed&&report.ammoGameplayOwned&&report.cancelled&&report.switchReset&&report.disableReset&&report.controllerDisableCancels&&report.destroyClean&&report.durationAdapted&&report.task13Material&&report.missingScripts==0&&report.shaderErrors==0,"Runtime integration checks failed");
            report.status="PASS_LOCAL_PLAYMODE_NETWORK_SESSION_NOT_TESTED";
        }
    }
}
#endif
