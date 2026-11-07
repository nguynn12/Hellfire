#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Hellfire.AI;
using UnityEngine.SceneManagement;
namespace Hellfire.Combat {
 public sealed class ExcavationMapPlaytest:MonoBehaviour {
  const string Dir="Docs/MapConcepts/ExcavationEvidence/";
  [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged+=state=>{
   if(state!=PlayModeStateChange.EnteredPlayMode||!File.Exists("Temp/TestExcavation.request")||SceneManager.GetActiveScene().name!="Hellfire_Infected_Excavation_Floor01")return;
   File.Move("Temp/TestExcavation.request","Temp/TestExcavation."+DateTime.UtcNow.Ticks+".started");new GameObject("Transient_Map_QA").AddComponent<ExcavationMapPlaytest>();
  };}
  void Record(string s){File.AppendAllText(Dir+"Playtest.txt",s+"\n");Debug.Log("[ExcavationQA] "+s);}
  IEnumerator Start(){
   yield return new WaitForSeconds(1);Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"Playtest.txt","");
   var run=FindFirstObjectByType<ExcavationRunController>();run.PlayerHealth.IsInvulnerable=true;
   foreach(var ai in run.HallEnemies)ai.enabled=false;run.Brute.enabled=false;foreach(var ai in run.PumpEnemies)ai.enabled=false;run.FinalBrute.enabled=false;
   var walls=FindObjectsByType<BoxCollider>().Where(c=>c.name=="Stone_Wall_Collider").ToArray();
   Record("Solid wall colliders and meshes match="+(walls.Length>0&&walls.All(c=>Mathf.Approximately(c.size.x,.8f)&&c.GetComponent<MeshFilter>()!=null&&Vector3.Distance(c.GetComponent<MeshFilter>().sharedMesh.bounds.size,c.size)<.001f)));
   foreach(var a in FindObjectsByType<NavMeshAgent>())Record(a.name+" on NavMesh="+a.isOnNavMesh);
   foreach(var pair in new[]{new[]{new Vector3(0,0,-20),new Vector3(0,0,0)},new[]{new Vector3(0,0,0),new Vector3(-18,0,0)},new[]{new Vector3(0,0,0),new Vector3(11.5f,3,0)},new[]{new Vector3(0,0,0),new Vector3(0,0,37)},new[]{new Vector3(0,0,37),new Vector3(0,0,52)},new[]{new Vector3(0,0,52),new Vector3(-24,0,64)},new[]{new Vector3(0,0,52),new Vector3(24,0,64)},new[]{new Vector3(0,0,52),new Vector3(16,3,64)},new[]{new Vector3(0,0,52),new Vector3(0,0,109)}}){
    var path=new NavMeshPath();NavMesh.SamplePosition(pair[0],out var start,1,NavMesh.AllAreas);NavMesh.SamplePosition(pair[1],out var end,1,NavMesh.AllAreas);Record("Sampled destination="+end.position);bool valid=NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);Record("Route "+pair[0]+" -> "+pair[1]+" = "+(valid&&path.status==NavMeshPathStatus.PathComplete));
   }
   var cam=run.PlayerRoot.GetComponentInChildren<Camera>();var oldPos=cam.transform.position;var oldRot=cam.transform.rotation;
   var ceiling=GameObject.Find("02_Ceiling");
   foreach(var shot in new[]{(new Vector3(0,1.8f,-10),new Vector3(0,2.8f,8),"01_PillarHall.png"),(new Vector3(8.5f,4.5f,-5),new Vector3(-2,2.5f,5),"02_Catwalk.png"),(new Vector3(-6,2,18),new Vector3(0,1.8f,28),"03_BruteChamber.png"),(new Vector3(0,2,50),new Vector3(0,3,74),"06_PumpStation.png"),(new Vector3(16,4.6f,59),new Vector3(-6,2,67),"07_PumpCatwalk.png"),(new Vector3(-7,2,86),new Vector3(0,2,96),"08_FinalArena.png"),(new Vector3(21.5f,1.7f,58.7f),new Vector3(19.8f,1.7f,60.8f),"09_WallThickness.png")}){cam.transform.position=shot.Item1;cam.transform.LookAt(shot.Item2);yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Dir+shot.Item3);yield return new WaitForSeconds(.2f);}
   ceiling.SetActive(false);cam.orthographic=true;cam.orthographicSize=72;cam.farClipPlane=200;cam.transform.position=new Vector3(0,110,44);cam.transform.rotation=Quaternion.Euler(90,0,0);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Dir+"04_Layout.png");yield return new WaitForSeconds(.3f);cam.orthographic=false;ceiling.SetActive(true);
   cam.transform.SetPositionAndRotation(new Vector3(0,1.8f,-10),Quaternion.LookRotation(new Vector3(0,.7f,18)));
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Dir+"05_GameView.png");yield return new WaitForSeconds(.3f);cam.transform.SetPositionAndRotation(oldPos,oldRot);
   Move(run.PlayerRoot,new Vector3(0,.1f,-10));yield return null;Record("Hall combat triggered="+run.CombatStarted);
   foreach(var ai in run.HallEnemies)ai.GetComponent<Health>().TakeDamage(10000,HitboxType.Torso,0);
   yield return null;Record("Hall clear opens gate="+(run.HallCleared&&!run.HallGate.activeSelf));
   Move(run.PlayerRoot,new Vector3(0,.1f,18));yield return null;Record("Brute triggered="+run.BruteAwakened);
   run.Brute.GetComponent<Health>().TakeDamage(10000,HitboxType.Torso,0);yield return null;Record("Brute death opens exit="+!run.ExitGate.activeSelf);
   Move(run.PlayerRoot,new Vector3(0,.1f,37));yield return null;Record("Original exit no longer finishes run="+!run.Completed);
   Record("Second encounter locked until entry="+(!run.PumpStarted&&run.PumpExitGate.activeSelf&&run.FinalGate.activeSelf));
   run.PlayerHealth.CurrentHealth.Value=30;
   Move(run.PlayerRoot,new Vector3(0,.1f,50));yield return null;Record("Pump encounter triggered="+run.PumpStarted);Record("Sector transition heals player="+(run.PlayerHealth.CurrentHealth.Value==run.PlayerHealth.MaxHealth));
   foreach(var ai in run.PumpEnemies)ai.GetComponent<Health>().TakeDamage(10000,HitboxType.Torso,0);
   yield return null;Record("Pump clear opens final arena="+(run.PumpCleared&&!run.PumpExitGate.activeSelf));
   Move(run.PlayerRoot,new Vector3(0,.1f,86));yield return null;Record("Final Brute triggered="+run.FinalBruteAwakened);Record("Exit closed while final Brute alive="+run.FinalGate.activeSelf);
   run.FinalBrute.GetComponent<Health>().TakeDamage(10000,HitboxType.Torso,0);yield return null;Record("Final Brute opens descent="+!run.FinalGate.activeSelf);
   yield return new WaitForSeconds(3.5f);Record("Progress survives dead enemy despawn="+(run.BruteDefeated&&run.FinalBruteDefeated));
   Move(run.PlayerRoot,new Vector3(0,.1f,109));yield return null;Record("Two-sector objective complete="+run.Completed);Record("COMPLETE");EditorApplication.isPlaying=false;
  }
  static void Move(Transform player,Vector3 p){var cc=player.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;player.position=p;if(cc!=null)cc.enabled=true;}
 }
}
#endif
