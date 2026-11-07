#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Hellfire.Editor {
 public static class ExcavationSolidWalls {
  const string Art="Assets/_Project/Art/Excavation/";
  [InitializeOnLoadMethod] static void Queue(){EditorApplication.delayCall+=()=>{
   if(!File.Exists("Temp/ThickenExcavationWalls.request")||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(SceneManager.GetActiveScene().path!=ExcavationMapBuilder.ScenePath)return;
   File.Delete("Temp/ThickenExcavationWalls.request");
   try{Upgrade();}catch(Exception e){File.WriteAllText("Logs/WallUpgradeFailure.txt",e.ToString());Debug.LogException(e);}
  };}
  public static void Attach(BoxCollider collider,Material material){
   var size=collider.size;size.x=.8f;collider.size=size;
   string path=Art+"SolidWall_"+size.z.ToString("0.###",CultureInfo.InvariantCulture)+".asset";
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh==null){
    var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);
    mesh=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);
    mesh.name="Stone wall / 0.8 m thick";var v=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
    for(int i=0;i<v.Length;i++){
     v[i]=Vector3.Scale(v[i],size);
     uv[i]=Mathf.Abs(normals[i].x)>.5f?new Vector2(v[i].z,v[i].y)/4:Mathf.Abs(normals[i].y)>.5f?new Vector2(v[i].x,v[i].z)/4:new Vector2(v[i].x,v[i].y)/4;
    }
    mesh.vertices=v;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,path);
   }
   var go=collider.gameObject;var filter=go.GetComponent<MeshFilter>();if(filter==null)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
   var renderer=go.GetComponent<MeshRenderer>();if(renderer==null)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
  }
  [MenuItem("Hellfire/Maps/Thicken Existing Excavation Walls")]
  public static void Upgrade(){
   if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
   var scene=SceneManager.GetActiveScene();if(scene.path!=ExcavationMapBuilder.ScenePath)throw new InvalidOperationException("Open the Excavation map first");
   var planes=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Wall_Placement").ToArray();
   var mat=planes.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).Select(r=>r.sharedMaterial).FirstOrDefault();
   if(mat==null)mat=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.name=="Stone_Wall_Collider").sharedMaterial;
   int count=0;foreach(var col in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name=="Stone_Wall_Collider")){Attach(col,mat);count++;}
   foreach(var p in planes)Object.DestroyImmediate(p.gameObject);
   foreach(var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)){
    var p=label.transform.position;if(label.text=="SUPPLIES")p.x=-13.5f;
    if(label.text=="PUMP STATION / SECTOR 02")p.x=-19.5f;
    if(label.text=="DESCENT LIFT")p.z=111.5f;label.transform.position=p;
   }
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   File.WriteAllText("Docs/MapConcepts/ExcavationEvidence/WallUpgrade.txt",count+" closed stone wall segments upgraded to 0.8 m, with matching colliders and continuous stone UVs on exposed edges.\n");
   Debug.Log("[Excavation] Thickened "+count+" wall segments");
  }
 }
}
#endif
