#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Hellfire.AI;
using Hellfire.Combat;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace Hellfire.Editor {
 public static class ExcavationMapBuilder {
  public const string ScenePath="Assets/_Project/Scenes/Hellfire_Infected_Excavation_Floor01.unity";
  const string Kit="Assets/RPG Dungeon Pack Sample/Prefabs/";
  const string Cat="Assets/LineKotsi/LN3D_Modular_CatWalk/Free_Part/Prefabs/LN3D_Modular_CatWalk_";
  const string Props="Assets/Medieval props/Prefabs/";
  const string Art="Assets/_Project/Art/Excavation/";
  const string Evidence="Docs/MapConcepts/ExcavationEvidence/";
  static Transform env,ceilings;
  static Material iron,stone,red,green;
  static readonly Dictionary<Material,Material> mats=new Dictionary<Material,Material>();
  [InitializeOnLoadMethod]static void Queue(){EditorApplication.delayCall+=()=>{
   if(!File.Exists("Temp/BuildExcavation.request")||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(!File.Exists("Logs/ExcavationAssetDimensions.txt"))return;
   File.Move("Temp/BuildExcavation.request","Temp/BuildExcavation."+DateTime.UtcNow.Ticks+".started");
   try{Build();}catch(Exception e){File.WriteAllText("Logs/ExcavationBuildFailure.txt",e.ToString());Debug.LogException(e);}
  };}
  public static void OpenBuilt(){EditorSceneManager.OpenScene(ScenePath);}
  [MenuItem("Hellfire/Maps/Build Infected Excavation Floor 01")]
  public static void Build(){
   if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
   if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before building");
   Directory.CreateDirectory(Art);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();mats.Clear();
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   scene.name="Hellfire_Infected_Excavation_Floor01";
   env=new GameObject("01_Imported_Environment").transform;
   ceilings=new GameObject("02_Ceiling").transform;
   iron=Material("Excavation_Iron",new Color(.12f,.15f,.14f),.65f);
   red=Material("Infection_Ember",new Color(.23f,.025f,.012f),.1f,new Color(.65f,.035f,.008f));
   green=Material("Guide_Green",new Color(.04f,.25f,.14f),.2f,new Color(.08f,.9f,.35f));
   stone=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"Individuals/Column.prefab").GetComponentInChildren<Renderer>().sharedMaterial;
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.23f,.21f);
   RenderSettings.fog=true;RenderSettings.fogColor=new Color(.045f,.075f,.07f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;
   var light=new GameObject("Soft_Ceiling_Fill").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(65,-25,0);light.color=new Color(.53f,.7f,.67f);light.intensity=.65f;light.shadows=LightShadows.Soft;
   Floors(-14,14,-12,12);Floors(-22,-14,-6,6);Floors(-3,3,-24,-16);Floors(-2,2,-16,-12);Floors(-10,10,16,32);Floors(-3,3,12,16);Floors(-3,3,32,40);
   // Side walls retain two openings to the looping supply room.
   Wall(-14,-10,4,0);Wall(-14,-5,2,0);Wall(-14,0,4,0);Wall(-14,5,2,0);Wall(-14,10,4,0);
   Wall(-14,-7,2,0);Wall(-14,7,2,0);Wall(-3,14,4,0);Wall(3,14,4,0);Wall(14,0,24,0);Wall(0,-12,28,90,4); // south boundary with the entry opening
   Wall(-8.5f,12,11,90);Wall(8.5f,12,11,90);
   Wall(-22,0,12,0);Wall(-18,-6,8,90);Wall(-18,6,8,90);
   Wall(-3,-20,8,0);Wall(3,-20,8,0);Wall(0,-24,6,90);
   Wall(-2,-14,4,0);Wall(2,-14,4,0);Wall(-2.5f,-16,1,90);Wall(2.5f,-16,1,90);
   Wall(-10,24,16,0);Wall(10,24,16,0);Wall(-6.5f,16,7,90);Wall(6.5f,16,7,90);
   Wall(-6.5f,32,7,90);Wall(6.5f,32,7,90);
   Wall(-3,36,8,0);Wall(3,36,8,0);
   foreach(float x in new[]{-4.5f,4.5f})foreach(float z in new[]{-9f,-3f,3f,9f}){
    Fit(Kit+"Individuals/Column.prefab",new Vector3(x,0,z),new Vector3(1.45f,6.8f,1.45f),0,true);
    Lantern(new Vector3(x+(x<0?.9f:-.9f),3.4f,z),false);
   }
   foreach(float z in new[]{-9f,-3f,3f,9f})Arch(new Vector3(0,2.6f,z),4.5f);
   Roof(-14,14,-12,12,7.2f);Roof(-22,-14,-6,6,5);Roof(-10,10,16,32,7.2f);Roof(-3,3,-24,-16,4.5f);Roof(-3,3,32,40,4.5f);
   Roof(-3,3,12,16,6);Roof(-2,2,-16,-12,4.5f);
   // Authored catwalk has its walking deck at local Y=0, with supports below.
   for(int z=-4;z<=4;z+=4){var cw=Spawn(Cat+"Line2x4.prefab",new Vector3(11.5f,3,z-2),Quaternion.identity);cw.name="Archer_Catwalk_"+z;}
   Stairs(new Vector3(11.5f,0,-12),false);Stairs(new Vector3(11.5f,0,12),true);RampCollider(new Vector3(11.5f,0,-12),false);RampCollider(new Vector3(11.5f,0,12),true);
   for(int z=-4;z<=4;z+=4){var sup=Spawn(Cat+"Support2x2.prefab",new Vector3(11.5f,3,z),Quaternion.identity);sup.name="Catwalk_Support";}
   foreach(var p in new[]{new Vector3(-9,0,-7),new Vector3(8,0,-7),new Vector3(-7,0,3),new Vector3(7,0,8),new Vector3(-10,0,9)}){
    Fit(Props+"BoxV1.prefab",p,new Vector3(1.5f,1.35f,1.5f),p.x*3,true);
    Fit(Kit+"Props/Barrel.prefab",p+new Vector3(1.8f,0,.4f),new Vector3(.9f,1.1f,.9f),0,true);
   }
   Fit(Props+"ShelfV1.prefab",new Vector3(-20.8f,0,0),new Vector3(.8f,2.6f,6),0,true);
   Fit(Props+"TableV1.prefab",new Vector3(-18,0,2),new Vector3(2.6f,1.05f,1.3f),0,true);
   Fit(Kit+"Props/Chest.prefab",new Vector3(-18,1.05f,2),new Vector3(.8f,.65f,1.2f),90,false);
   for(int i=0;i<3;i++)Fit(Props+"SackV1.prefab",new Vector3(-20+i*1.1f,0,-4),new Vector3(.8f,.85f,.75f),i*35,true);
   Lantern(new Vector3(-18,3,0),true);Lantern(new Vector3(0,3.4f,-20),false);Lantern(new Vector3(0,3.4f,37),false);
   foreach(float x in new[]{-8f,8f})foreach(float z in new[]{19f,25f,30f})Lantern(new Vector3(x,3.8f,z),true);
   // Industrial service lines run along the hall sides.
   foreach(float x in new[]{-13.55f,13.55f}){
    Cylinder("Rusted_Service_Pipe",new Vector3(x,4.5f,0),new Vector3(.22f,12,.22f),Quaternion.Euler(90,0,0),iron);
    for(int z=-10;z<=10;z+=4)Box("Pipe_Bracket",new Vector3(x,4.5f,z),new Vector3(.4f,.5f,.15f),iron,false);
   }
   for(int i=0;i<18;i++){
    float a=i*Mathf.PI*2/18;var p=new Vector3(Mathf.Cos(a)*8,.05f,24+Mathf.Sin(a)*6);
    var go=Box("Infection_Vein",p,new Vector3(.12f,.08f,2.1f),red,false);go.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
   }
   var hallGate=Gate("Hall_Clear_Gate",12,5.8f,4.2f);var exitGate=Gate("Brute_Clear_Gate",33,5.8f,4.2f);
   Arch(new Vector3(0,3,12),3);Arch(new Vector3(0,3,33),3);
   Label("01 / INFECTED EXCAVATION",new Vector3(0,3.2f,-23.5f),.13f,180);
   Label("SUPPLIES",new Vector3(-13.5f,2.7f,-3.8f),.12f,-90);
   Label("02 / PUMP STATION",new Vector3(0,3.3f,40),.16f,0);
   Box("Lift_Guide",new Vector3(0,.025f,37),new Vector3(3,.03f,.15f),green,false);
   var finalGate=BuildPumpStation();
   foreach(var r in ceilings.GetComponentsInChildren<Renderer>()){var b=r.bounds;var cap=Box("Ceiling_Back",new Vector3(b.center.x,b.max.y+.1f,b.center.z),new Vector3(b.size.x,.2f,b.size.z),iron,false);cap.transform.SetParent(ceilings);}
   Box("Entry_Stone_Lintel",new Vector3(0,5.8f,-12),new Vector3(4,2.8f,.4f),ConvertMaterial(stone),false);
   foreach(float z in new[]{12f,16f,32f})Box("Gate_Stone_Lintel",new Vector3(0,6.5f,z),new Vector3(6,1.8f,.4f),ConvertMaterial(stone),false);
   foreach(float z in new[]{-3f,3f})Box("Supply_Stone_Lintel",new Vector3(-14,6.1f,z),new Vector3(.4f,2.4f,2),ConvertMaterial(stone),false);
   PlayerAndEnemies(hallGate,exitGate,finalGate);
   EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
   File.WriteAllText(Evidence+"BuildReport.txt","Floor01 created from four imported packs. Original 28 x 24 m pillar hall plus 40 x 32 m pump station, two side loops, second raised catwalk, final arena and relocated exit. Total extent: z -24 to 112 m; maximum width 56 m.\n");
   Debug.Log("[Excavation] Built "+ScenePath);
  }
  static GameObject BuildPumpStation(){
   // A second full encounter follows the original floor, without a loading transition.
   Floors(-3,3,40,48);Roof(-3,3,40,48,5);Wall(-3,44,8,0);Wall(3,44,8,0);
   Floors(-20,20,48,80);Roof(-20,20,48,80,7.2f);
   Wall(0,48,40,90,6);Wall(0,80,40,90,6);
   foreach(float x in new[]{-20f,20f}){
    Wall(x,52,8,0);Wall(x,64,8,0);Wall(x,76,8,0);
    float outer=x<0?-28:28,mid=x<0?-24:24;
    Floors(Mathf.Min(x,outer),Mathf.Max(x,outer),56,72);Roof(Mathf.Min(x,outer),Mathf.Max(x,outer),56,72,5);
    Wall(outer,64,16,0);Wall(mid,56,8,90);Wall(mid,72,8,90);
    foreach(float z in new[]{58f,70f})Box("Loop_Door_Lintel",new Vector3(x,6.1f,z),new Vector3(.4f,2.4f,4),ConvertMaterial(stone),false);
    Lantern(new Vector3(mid,3.2f,64),true);
    Fit(Props+"ShelfV1.prefab",new Vector3(outer+(x<0?1:-1),0,64),new Vector3(.8f,2.6f,7),0,true);
    foreach(float z in new[]{58.5f,69.5f})Fit(Kit+"Props/Barrel.prefab",new Vector3(mid+1,0,z),new Vector3(1.1f,1.4f,1.1f),0,true);
   }
   foreach(float x in new[]{-8f,8f})foreach(float z in new[]{54f,64f,74f}){
    Fit(Kit+"Individuals/Column.prefab",new Vector3(x,0,z),new Vector3(1.6f,6.8f,1.6f),0,true);
    Lantern(new Vector3(x+(x<0?1:-1),3.5f,z),false);
   }
   // Three pump housings split sightlines while leaving broad flanking lanes.
   foreach(float z in new[]{58f,64f,70f}){
    Cylinder("Pump_Housing",new Vector3(0,1.1f,z),new Vector3(3,1.1f,3),Quaternion.identity,iron);
    Proxy("Pump_Housing_Collision",new Vector3(0,1.1f,z),new Vector3(3,2.2f,3));
    Box("Pump_Green_Status",new Vector3(0,1.3f,z-1.52f),new Vector3(1,.25f,.05f),green,false);
    Cylinder("Pump_Outlet",new Vector3(0,2.8f,z),new Vector3(.45f,1.7f,.45f),Quaternion.identity,iron);
   }
   for(int z=56;z<72;z+=4){var c=Spawn(Cat+"Line2x4.prefab",new Vector3(16,3,z),Quaternion.identity);c.name="Pump_Archer_Catwalk";Spawn(Cat+"Support2x2.prefab",new Vector3(16,3,z+2),Quaternion.identity);}
   Stairs(new Vector3(16,0,50),false);RampCollider(new Vector3(16,0,50),false);
   Stairs(new Vector3(16,0,78),true);RampCollider(new Vector3(16,0,78),true);
   foreach(var p in new[]{new Vector3(-13,0,53),new Vector3(11,0,54),new Vector3(-12,0,68),new Vector3(9,0,76),new Vector3(-4,0,77)}){
    Fit(Props+"BoxV1.prefab",p,new Vector3(2,1.6f,1.6f),p.z,true);
    Fit(Kit+"Props/Barrel.prefab",p+Vector3.right*2,new Vector3(1,1.2f,1),0,true);
   }
   foreach(float x in new[]{-19.5f,19.5f})Cylinder("Pump_Service_Main",new Vector3(x,5.5f,64),new Vector3(.35f,16,.35f),Quaternion.Euler(90,0,0),iron);
   Floors(-3,3,80,84);Roof(-3,3,80,84,5);Wall(-3,82,4,0);Wall(3,82,4,0);
   Floors(-12,12,84,100);Roof(-12,12,84,100,7.2f);Wall(-12,92,16,0);Wall(12,92,16,0);Wall(0,84,24,90,6);Wall(0,100,24,90,6);
   foreach(float x in new[]{-9f,9f})foreach(float z in new[]{87f,93f,98f}){Lantern(new Vector3(x,3.6f,z),true);Fit(Kit+"Props/Barrel.prefab",new Vector3(x,0,z),new Vector3(1.2f,1.6f,1.2f),0,true);}
   foreach(float x in new[]{-6f,6f})Fit(Kit+"Individuals/Column.prefab",new Vector3(x,0,92),new Vector3(1.5f,6.8f,1.5f),0,true);
   Floors(-3,3,100,112);Roof(-3,3,100,112,5);Wall(-3,106,12,0);Wall(3,106,12,0);Wall(0,112,6,90);
   foreach(float z in new[]{48f,80f,84f,100f}){Box("Pump_Gate_Lintel",new Vector3(0,6,z),new Vector3(6,2.8f,.4f),ConvertMaterial(stone),false);Arch(new Vector3(0,2.6f,z),3);}
   var waveGate=Gate("Pump_Hall_Clear_Gate",80,5.8f,4.2f);Gate("Pump_Final_Clear_Gate",101,5.8f,4.2f);
   Lantern(new Vector3(0,3.4f,44),false);Lantern(new Vector3(0,3.4f,108),false);
   Label("PUMP STATION / SECTOR 02",new Vector3(-19.5f,3.2f,64),.15f,-90);
   Label("DESCENT LIFT",new Vector3(0,3.3f,111.5f),.16f,0);
   Box("Final_Lift_Guide",new Vector3(0,.025f,108),new Vector3(3,.03f,.15f),green,false);
   return waveGate;
  }
  static void PlayerAndEnemies(GameObject gate,GameObject exit,GameObject finalGate){
   var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Testing/Player_ZombiePlaytest.prefab"));player.name="Player_Test";player.transform.position=new Vector3(0,.12f,-20);
   var cam=player.GetComponentInChildren<Camera>(true);cam.farClipPlane=100;cam.fieldOfView=75;
   var weapon=player.GetComponent<WeaponController>();var field=typeof(WeaponController).GetField("_playerCamera",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(weapon,cam);
   var roles=new[]{"Runner","Archer","Brute"};var prefabs=roles.Select(r=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Zombie_"+r+"_Showcase.prefab")).ToArray();
   var enemies=new List<EnemyStateMachine>();
   foreach(var p in new[]{new Vector3(-7,0,-2),new Vector3(6,0,4),new Vector3(-6,0,8)})enemies.Add(Enemy(prefabs[0],p));
   foreach(var p in new[]{new Vector3(11.5f,3,0),new Vector3(11.5f,3,4.5f)})enemies.Add(Enemy(prefabs[1],p));
   var brute=Enemy(prefabs[2],new Vector3(0,0,26));brute.name="Excavation_Brute";
   var host=new GameObject("Floor01_LocalHost");var nm=host.AddComponent<NetworkManager>();var transport=host.AddComponent<UnityTransport>();nm.NetworkConfig=new NetworkConfig{NetworkTransport=transport,PlayerPrefab=null,EnableSceneManagement=true};
   var nav=host.AddComponent<CodexZombieTestSceneNavigation>();nav.IgnoreFromBuild=new[]{gate.transform,exit.transform,finalGate.transform,GameObject.Find("Pump_Final_Clear_Gate").transform};nav.BuildBounds=new Bounds(new Vector3(0,0,44),new Vector3(160,40,180));
   var session=host.AddComponent<ExcavationRunController>();session.EnemyPrefabs=prefabs;session.PlayerRoot=player.transform;session.PlayerHealth=player.GetComponent<Health>();session.HallGate=gate;session.ExitGate=exit;session.HallEnemies=enemies.ToArray();session.Brute=brute;
   var pump=new List<EnemyStateMachine>();
   foreach(var p in new[]{new Vector3(-12,0,55),new Vector3(8,0,58),new Vector3(-7,0,67),new Vector3(10,0,73),new Vector3(-24,0,64),new Vector3(24,0,64)}){var e=Enemy(prefabs[0],p);e.name="Pump_Runner";pump.Add(e);}
   foreach(var p in new[]{new Vector3(16,3,60),new Vector3(16,3,68),new Vector3(-14,0,73)}){var e=Enemy(prefabs[1],p);e.name="Pump_Archer";pump.Add(e);}
   session.PumpEnemies=pump.ToArray();session.PumpExitGate=finalGate;
   var final=Enemy(prefabs[2],new Vector3(0,0,94));final.name="Pump_Final_Brute";session.FinalBrute=final;
   session.FinalGate=GameObject.Find("Pump_Final_Clear_Gate");
  }
  static EnemyStateMachine Enemy(GameObject prefab,Vector3 p){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,180,0));return go.GetComponent<EnemyStateMachine>();}
  static GameObject Spawn(string path,Vector3 p,Quaternion rot){
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new FileNotFoundException(path);
   var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(env);go.transform.SetPositionAndRotation(p,rot);
   foreach(var t in go.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
   foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(ConvertMaterial).ToArray();return go;
  }
  static Bounds Bounds(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
  static GameObject Fit(string path,Vector3 bottom,Vector3 size,float yaw,bool solid){
   var go=Spawn(path,Vector3.zero,Quaternion.identity);var b=Bounds(go);go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(b.size.x>.01f?size.x/b.size.x:1,b.size.y>.01f&&size.y>.001f?size.y/b.size.y:1,b.size.z>.01f?size.z/b.size.z:1));
   b=Bounds(go);go.transform.position=new Vector3(-b.center.x,-b.min.y,-b.center.z);var pivot=new GameObject(go.name+"_Placement");pivot.transform.SetParent(env);go.transform.SetParent(pivot.transform);pivot.transform.SetPositionAndRotation(bottom,Quaternion.Euler(0,yaw,0));
   foreach(var c in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
   if(solid){var c=pivot.AddComponent<BoxCollider>();c.center=new Vector3(0,size.y/2,0);c.size=new Vector3(Mathf.Max(.05f,size.x),Mathf.Max(.05f,size.y),Mathf.Max(.05f,size.z));}
   return pivot;
  }
  static void Floors(float x0,float x1,float z0,float z1){for(float x=x0;x<x1-.1f;x+=4)for(float z=z0;z<z1-.1f;z+=4){float w=Mathf.Min(4,x1-x),d=Mathf.Min(4,z1-z);Fit(Kit+"Individuals/Floor.prefab",new Vector3(x+w/2,0,z+d/2),new Vector3(w,0,d),0,false);Proxy("Stone_Floor_Collider",new Vector3(x+w/2,-.15f,z+d/2),new Vector3(w,.3f,d));}}
  static void Roof(float x0,float x1,float z0,float z1,float y){for(float x=x0;x<x1;x+=4)for(float z=z0;z<z1;z+=4){var r=Fit(Kit+"Individuals/Roof.prefab",new Vector3(x+Mathf.Min(4,x1-x)/2,y-.2f,z+Mathf.Min(4,z1-z)/2),new Vector3(Mathf.Min(4,x1-x),.22f,Mathf.Min(4,z1-z)),0,false);r.transform.SetParent(ceilings);}}
  static void Wall(float x,float z,float length,float yaw,float opening=0){
   if(opening>0){float side=(length-opening)/2;float offset=opening/2+side/2;Wall(x-offset,z,side,yaw);Wall(x+offset,z,side,yaw);return;}
   var col=Proxy("Stone_Wall_Collider",new Vector3(x,3.6f,z),new Vector3(.8f,7.2f,length));col.transform.rotation=Quaternion.Euler(0,yaw,0);
   var wallMaterial=ConvertMaterial(AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"Individuals/Wall.prefab").GetComponentInChildren<Renderer>().sharedMaterial);
   ExcavationSolidWalls.Attach(col.GetComponent<BoxCollider>(),wallMaterial);
  }
  static void Arch(Vector3 center,float radius){for(int i=0;i<14;i++){float a=(i+.5f)*Mathf.PI/14;var p=center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);var b=Box("Stone_Arch_Voussoir",p,new Vector3(radius*Mathf.PI/14*.94f,.45f,.65f),stone,false);b.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90);}}
  static void Stairs(Vector3 bottom,bool reverse){
   // Twelve low steps form a broad walkable return at each end of the catwalk.
   for(int i=0;i<12;i++){float h=(i+1)*.25f,z=bottom.z+(reverse?-1:1)*(i+.5f)*.5f;Fit(Kit+"Individuals/Floor.prefab",new Vector3(bottom.x,h,z),new Vector3(2.2f,0,.5f),0,false);Box("Iron_Stair_Riser",new Vector3(bottom.x,h/2,z),new Vector3(2.2f,h,.5f),iron,false);}
  }
   static void RampCollider(Vector3 bottom,bool reverse){
    var go=new GameObject("Stair_Walkable_Ramp");go.transform.SetParent(env);go.transform.position=bottom;if(reverse)go.transform.rotation=Quaternion.Euler(0,180,0);
    var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-1.1f,0,0),new Vector3(1.1f,0,0),new Vector3(-1.1f,3,6),new Vector3(1.1f,3,6),new Vector3(-1.1f,0,6),new Vector3(1.1f,0,6)};
    mesh.triangles=new[]{0,2,1,1,2,3,0,4,2,1,3,5,4,5,3,4,3,2,0,1,5,0,5,4};mesh.RecalculateNormals();
    var path=Art+"StairRamp.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
    go.AddComponent<MeshCollider>().sharedMesh=saved;
   }
  static GameObject Gate(string name,float z,float width,float height){var root=new GameObject(name);root.transform.SetParent(env);for(float x=-width/2;x<=width/2;x+=.42f){var bar=Box("Gate_Bar",new Vector3(x,height/2,z),new Vector3(.1f,height,.16f),iron,false);bar.transform.SetParent(root.transform);}foreach(float y in new[]{.4f,height-.4f}){var b=Box("Gate_Rail",new Vector3(0,y,z),new Vector3(width,.16f,.18f),iron,false);b.transform.SetParent(root.transform);}var c=root.AddComponent<BoxCollider>();c.center=new Vector3(0,height/2,z);c.size=new Vector3(width,height,.25f);return root;}
  static void Lantern(Vector3 p,bool warm){var go=Fit("Assets/Laterns and candles/Prefabs/Latern1.prefab",p,new Vector3(.5f,.7f,.5f),0,false);var l=new GameObject("Lantern_Light").AddComponent<Light>();l.transform.SetParent(go.transform,true);l.transform.position=p+Vector3.up*.3f;l.type=LightType.Point;l.range=warm?10:9;l.intensity=warm?4:5;l.color=warm?new Color(1,.5f,.16f):new Color(.2f,1,.58f);l.shadows=LightShadows.None;}
  static GameObject Box(string name,Vector3 center,Vector3 size,Material mat,bool collider){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(env);go.transform.position=center;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
  static GameObject Proxy(string name,Vector3 center,Vector3 size){var go=new GameObject(name);go.transform.SetParent(env);go.transform.position=center;go.AddComponent<BoxCollider>().size=size;return go;}
  static void Cylinder(string name,Vector3 p,Vector3 size,Quaternion rot,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(env);go.transform.SetPositionAndRotation(p,rot);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
  static Material Material(string name,Color color,float metal,Color emission=default){var path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.28f);if(emission.maxColorComponent>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}return m;}
  static Material ConvertMaterial(Material source){if(source==null)return iron;if(mats.TryGetValue(source,out var cached))return cached;
   string path=Art+"M_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(source);var tex=source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):null;var color=source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
    if(source.shader==null||!source.shader.name.StartsWith("Universal Render Pipeline/")){m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",color);}
    m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,path);
   }mats[source]=m;return m;
  }
  static void Label(string text,Vector3 p,float size,float yaw){var go=new GameObject(text);go.transform.SetParent(env);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));var tm=go.AddComponent<TextMesh>();tm.text=text;tm.fontSize=48;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.65f,.85f,.7f);}
 }
}
#endif
