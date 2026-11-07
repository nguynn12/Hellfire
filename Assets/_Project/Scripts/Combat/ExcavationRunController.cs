using System.Linq;
using Hellfire.AI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Hellfire.Combat {
 public sealed class ExcavationRunController:MonoBehaviour {
  public GameObject[] EnemyPrefabs;
  public Transform PlayerRoot;
  public GameObject HallGate;
  public GameObject ExitGate;
  public EnemyStateMachine[] HallEnemies;
  public EnemyStateMachine Brute;
  public Health PlayerHealth;
  public bool CombatStarted,HallCleared,BruteAwakened,Completed;
  public EnemyStateMachine[] PumpEnemies = System.Array.Empty<EnemyStateMachine>();
  public EnemyStateMachine FinalBrute;
  public GameObject PumpExitGate,FinalGate;
  public bool BruteDefeated,PumpStarted,PumpCleared,FinalBruteAwakened,FinalBruteDefeated;
  bool ready;
  void Awake(){foreach(var e in HallEnemies)if(e!=null)e.enabled=false;if(Brute!=null)Brute.enabled=false;foreach(var e in PumpEnemies)if(e!=null)e.enabled=false;if(FinalBrute!=null)FinalBrute.enabled=false;}
  void Start(){
   var manager=GetComponent<NetworkManager>();
   foreach(var p in EnemyPrefabs)manager.AddNetworkPrefab(p);
   ushort port;using(var probe=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0)))port=(ushort)((System.Net.IPEndPoint)probe.Client.LocalEndPoint).Port;
   manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",port,"127.0.0.1");
   if(!manager.StartHost()){Debug.LogError("[Excavation] Local host failed");return;}
   var net=PlayerRoot.GetComponent<NetworkObject>();if(net.IsSpawned)net.Despawn(false);net.SpawnAsPlayerObject(manager.LocalClientId);
   ready=true;Debug.Log("[Excavation] Floor 01 ready");
  }
  void Update(){
   if(!ready||PlayerRoot==null||PlayerHealth.IsDead.Value)return;
   if(!CombatStarted&&PlayerRoot.position.z>-12){CombatStarted=true;foreach(var e in HallEnemies)if(e!=null)e.enabled=true;}
   if(CombatStarted&&!HallCleared&&HallEnemies.All(e=>e==null||e.GetComponent<Health>().IsDead.Value)){HallCleared=true;HallGate.SetActive(false);Debug.Log("[Excavation] Hall cleared, Brute gate open");}
   if(HallCleared&&!BruteAwakened&&PlayerRoot.position.z>15){BruteAwakened=true;Brute.enabled=true;}
   if(BruteAwakened&&!BruteDefeated&&Dead(Brute)){BruteDefeated=true;ExitGate.SetActive(false);}
   if(BruteDefeated&&!PumpStarted&&PlayerRoot.position.z>48){
    PumpStarted=true;foreach(var e in PumpEnemies)if(e!=null)e.enabled=true;
    // A one-time recovery at the sector boundary supports the longer run.
    PlayerHealth.IncreaseMaxHealthAndHeal(0);
   }
   if(PumpStarted&&!PumpCleared&&PumpEnemies.All(Dead)){PumpCleared=true;PumpExitGate.SetActive(false);}
   if(PumpCleared&&!FinalBruteAwakened&&PlayerRoot.position.z>84){FinalBruteAwakened=true;if(FinalBrute!=null)FinalBrute.enabled=true;}
   if(FinalBruteAwakened&&!FinalBruteDefeated&&Dead(FinalBrute)){FinalBruteDefeated=true;FinalGate.SetActive(false);}
   if(FinalBruteDefeated&&PlayerRoot.position.z>107)Completed=true;
  }
  static bool Dead(EnemyStateMachine e)=>e==null||e.GetComponent<Health>().IsDead.Value;
  void OnGUI(){
   string objective=Completed?"BOTH SECTORS CLEARED / DESCENT READY":!CombatStarted?"LEAVE THE LIFT / ENTER THE HALL":!HallCleared?"CLEAR THE PILLAR HALL":!BruteAwakened?"GATE OPEN / ENTER THE INFECTED CHAMBER":!BruteDefeated?"ELIMINATE THE BRUTE":!PumpStarted?"CONTINUE TO SECTOR 02 / PUMP STATION":!PumpCleared?"CLEAR THE PUMP STATION":!FinalBruteAwakened?"GATE OPEN / ENTER THE FINAL CHAMBER":!FinalBruteDefeated?"DEFEAT THE FINAL BRUTE":"REACH THE DESCENT LIFT";
   string sector=PumpStarted?"SECTOR 02 / PUMP STATION":"SECTOR 01 / INFECTED EXCAVATION";
   GUI.Box(new Rect(16,16,Mathf.Min(600,Screen.width-32),64),"HELLFIRE / "+sector+"\n"+objective+"\nWASD move · Shift sprint · Mouse fire · R reload");
   GUI.Label(new Rect(Screen.width/2-4,Screen.height/2-10,20,20),"+");
  }
 }
}
