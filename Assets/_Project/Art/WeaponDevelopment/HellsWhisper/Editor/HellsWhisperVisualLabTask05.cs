#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    // Editor-only lab builder and calibration UI. No component is added to a gameplay object.
    public sealed class HellsWhisperVisualLabTask05 : EditorWindow
    {
        internal const string Folder = "Assets/_Project/Art/WeaponDevelopment/HellsWhisper/";
        internal const string LabPath = Folder + "SCN_HellsWhisper_VisualLab.unity";
        const string ViewPath = Folder + "PF_HellsWhisper_Viewmodel.prefab";
        const string WorldPath = Folder + "PF_HellsWhisper_WorldModel.prefab";
        const string MaterialPath = Folder + "MAT_HellsWhisper_PBR_Test.mat";
        const string NeutralPath = Folder + "MAT_HellsWhisper_VisualLab_Neutral_Task05.mat";
        const string ReportPath = Folder + "QA_HellsWhisper_VisualLab_Task05.json";
        const string FpsImage = Folder + "IMG_HellsWhisper_VisualLab_FPS_Task05.png";
        const string WorldImage = Folder + "IMG_HellsWhisper_VisualLab_World_Task05.png";
        const string SessionKey = "Hellfire.VisualLab.Task05.Attempted.v2";
        const int ViewLayer = 31, WorldLayer = 30;
        static readonly Vector3 InitialPosition = new Vector3(0.25f, -0.2f, 0.5f);
        const float InitialScale = 0.2f;
        [SerializeField] bool showGuides = true;
        Texture2D fpsPreview, worldPreview;
        Vector2 scroll;
        string status = "Initial values are PROVISIONAL, not an approved grip calibration.";

        [Serializable] sealed class SceneRow
        {
            public string path, handle;
            public bool dirty;
            public int rootCount;
        }
        [Serializable] sealed class SceneEnvelope { public SceneRow[] scenes; }
        [Serializable] sealed class InstanceRow
        {
            public string prefabSource, meshSource, materialSource;
            public long vertices, triangles;
            public bool nestedLinkIntact, materialCorrect, modelInFrontOfCamera;
            public float nearestBoundsDepth;
        }
        [Serializable] sealed class LabReport
        {
            public string status, unityVersion, scene, calibrationStatus, statsScope;
            public bool originalSceneStatePreserved, cameraCapturesWritten;
            public SceneRow[] scenesBefore, scenesAfter;
            public InstanceRow viewmodel, worldModel;
            public Vector3 initialCameraLocalPosition;
            public float initialUniformScale, fpsFov, nearClip, farClip;
            public string[] editorStats;
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += RestoreLoadedLabCameras;
        }

        [MenuItem("Hellfire/Weapon Development/Build Missing Visual Lab")]
        static void WaitAndBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += WaitAndBuild; return; }
            if (File.Exists(LabPath) || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            try { Build(); }
            catch (Exception e)
            {
                const string errorPath = Folder + "QA_HellsWhisper_VisualLab_Error_Task05.txt";
                if (!File.Exists(errorPath)) WriteNew(errorPath, System.Text.Encoding.UTF8.GetBytes(e.ToString()));
                Debug.LogError("[Hellfire Task05] STOP. No existing scene was saved. " + e);
            }
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        static Scene LabScene() { return SceneManager.GetSceneByPath(LabPath); }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        { if (!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == LabPath) IsolateCameras(scene); }

        static void RestoreLoadedLabCameras()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = LabScene();
            if (scene.IsValid() && scene.isLoaded) IsolateCameras(scene);
        }

        static void IsolateCameras(Scene scene)
        {
            // Camera.scene is an Editor scene filter, not a project-wide layer setting.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.scene = scene;
        }

        static SceneRow[] ExistingScenes()
        {
            var rows = new List<SceneRow>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == LabPath || EditorSceneManager.IsPreviewScene(s)) continue;
                rows.Add(new SceneRow { path = s.path, handle = s.handle.GetRawData().ToString(),
                    dirty = s.isDirty, rootCount = s.rootCount });
            }
            return rows.ToArray();
        }

        static GameObject Node(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
        }

        static Camera MakeCamera(string name, Transform parent, int layer, Rect viewport, float fov, float depth)
        {
            var camera = Node(name, parent).AddComponent<Camera>();
            camera.orthographic = false; camera.fieldOfView = fov;
            camera.nearClipPlane = 0.05f; camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            camera.cullingMask = 1 << layer; camera.rect = viewport; camera.depth = depth;
            camera.useOcclusionCulling = false;
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            camera.scene = camera.gameObject.scene;
            return camera;
        }

        static Light MakeLight(string name, Transform parent, Vector3 rotation, float intensity, bool shadows)
        {
            var light = Node(name, parent).AddComponent<Light>();
            light.type = LightType.Directional; light.color = Color.white; light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation);
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.cullingMask = (1 << ViewLayer) | (1 << WorldLayer);
            return light;
        }

        static GameObject Instance(string path, Transform parent, int layer, Scene scene)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(source != null, "Prefab not imported: " + path);
            var go = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
            Require(go != null, "Could not instantiate prefab: " + path);
            go.transform.SetParent(parent, false);
            SetLayer(go, layer);
            return go;
        }

        static InstanceRow Inspect(GameObject go, string prefab, Camera camera)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            var row = new InstanceRow { prefabSource = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go),
                nestedLinkIntact = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == prefab,
                materialCorrect = renderers.All(r => r.sharedMaterials.All(m => AssetDatabase.GetAssetPath(m) == MaterialPath)) };
            Require(filters.Length == 1 && renderers.Length == 1, "Unexpected model structure");
            row.meshSource = AssetDatabase.GetAssetPath(filters[0].sharedMesh);
            row.materialSource = AssetDatabase.GetAssetPath(renderers[0].sharedMaterial);
            row.vertices = filters[0].sharedMesh.vertexCount;
            for (int i = 0; i < filters[0].sharedMesh.subMeshCount; i++) row.triangles += filters[0].sharedMesh.GetIndexCount(i) / 3;
            var b = renderers[0].bounds; float nearest = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                nearest = Mathf.Min(nearest, camera.transform.InverseTransformPoint(corner).z);
            }
            row.nearestBoundsDepth = nearest; row.modelInFrontOfCamera = nearest > camera.nearClipPlane;
            Require(row.nestedLinkIntact && row.materialCorrect && row.modelInFrontOfCamera, "Prefab/material/near-clip verification failed");
            return row;
        }

        static Texture2D RenderPreview(Camera camera, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldRect = camera.rect; var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)width / height;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP single-camera render request unsupported");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                return image;
            }
            finally
            {
                camera.rect = oldRect; camera.ResetAspect();
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            }
        }

        static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes, 0, bytes.Length);
            AssetDatabase.ImportAsset(path);
        }

        static string[] Stats()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.UnityStats")).FirstOrDefault(t => t != null);
            return new[] { "triangles", "vertices", "batches", "drawCalls", "setPassCalls" }.Select(name =>
            {
                var p = type?.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var f = type?.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                try { return name + "=" + (p != null ? p.GetValue(null) : f != null ? f.GetValue(null) : "NOT AVAILABLE"); }
                catch { return name + "=NOT AVAILABLE"; }
            }).ToArray();
        }

        static void Build()
        {
            Require(Application.dataPath == "/Users/stormpaw/File/Hellfire/Assets", "Wrong project");
            Require(Application.unityVersion == "6000.5.6f1", "Wrong Unity version");
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Play Mode is active");
            foreach (var p in new[] { LabPath, NeutralPath, ReportPath, FpsImage, WorldImage })
                Require(!File.Exists(p), "Destination exists; refusing overwrite: " + p);
            var before = ExistingScenes();
            const string scaffold = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &100000\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 400000}\n  m_Layer: 0\n  m_Name: VisualLab\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &400000\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 100000}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: 400000}\n";
            WriteNew(LabPath, System.Text.Encoding.UTF8.GetBytes(scaffold));
            var lab = EditorSceneManager.OpenScene(LabPath, OpenSceneMode.Additive);
            Require(SceneManager.SetActiveScene(lab), "Could not activate new additive lab scene");
            var root = lab.GetRootGameObjects().Single(g => g.name == "VisualLab");
            var rig = Node("FPSCameraRig", root.transform); rig.transform.position = new Vector3(0, 1.6f, 0);
            var fps = MakeCamera("PreviewCamera", rig.transform, ViewLayer, new Rect(0, 0, 0.65f, 1), 90, 100);
            var anchor = Node("ViewmodelAnchor", fps.transform); anchor.transform.localPosition = InitialPosition;
            var pivot = Node("CalibrationPivot_PROVISIONAL", anchor.transform); pivot.transform.localScale = Vector3.one * InitialScale;
            var view = Instance(ViewPath, pivot.transform, ViewLayer, lab);
            var station = Node("WorldModelStation", root.transform); station.transform.position = new Vector3(4, 0, 3);
            var world = Instance(WorldPath, station.transform, WorldLayer, lab);
            var inspectionRig = Node("InspectionCameraRig", root.transform);
            var inspect = MakeCamera("WorldInspectionCamera", inspectionRig.transform, WorldLayer, new Rect(0.65f, 0, 0.35f, 1), 50, 101);
            inspect.transform.position = station.transform.position + new Vector3(4.5f, 1.8f, 0);
            inspect.transform.LookAt(station.transform.position + new Vector3(0, 0.7324f, 0));
            var lights = Node("LightingRig", root.transform);
            MakeLight("Key_Soft_Neutral", lights.transform, new Vector3(35, -35, 0), 1.3f, true);
            MakeLight("Fill_Neutral", lights.transform, new Vector3(25, 145, 0), 0.35f, false);
            MakeLight("Rim_Subtle_Neutral", lights.transform, new Vector3(145, 20, 0), 0.45f, false);
            var environment = Node("ReferenceEnvironment", root.transform);
            var neutral = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MAT_HellsWhisper_VisualLab_Neutral_Task05" };
            neutral.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.35f, 1)); neutral.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(neutral, NeutralPath);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Temporary_WorldReference_Platform_3m";
            floor.transform.SetParent(environment.transform, false); floor.transform.position = station.transform.position + new Vector3(0, -0.035f, 0);
            floor.transform.localScale = new Vector3(3, 0.05f, 3); floor.layer = WorldLayer;
            DestroyImmediate(floor.GetComponent<Collider>()); floor.GetComponent<MeshRenderer>().sharedMaterial = neutral;
            Node("CalibrationTools_TEMPORARY_NoProductionSockets", root.transform);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.25f); RenderSettings.fog = false;
            Require(EditorSceneManager.SaveScene(lab, LabPath), "Could not save new lab scene");
            IsolateCameras(lab);
            // Reload only this newly created scene from disk to validate saved nested links.
            EditorSceneManager.CloseScene(lab, true);
            lab = EditorSceneManager.OpenScene(LabPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(lab); IsolateCameras(lab);
            fps = Find<Camera>("FPSCameraRig/PreviewCamera");
            inspect = Find<Camera>("InspectionCameraRig/WorldInspectionCamera");
            view = FindTransform("FPSCameraRig/PreviewCamera/ViewmodelAnchor/CalibrationPivot_PROVISIONAL").GetChild(0).gameObject;
            world = FindTransform("WorldModelStation").GetChild(0).gameObject;
            var viewResult = Inspect(view, ViewPath, fps); var worldResult = Inspect(world, WorldPath, inspect);
            var fpsTexture = RenderPreview(fps, 1280, 720); var worldTexture = RenderPreview(inspect, 1024, 1024);
            try { WriteNew(FpsImage, fpsTexture.EncodeToPNG()); WriteNew(WorldImage, worldTexture.EncodeToPNG()); }
            finally { DestroyImmediate(fpsTexture); DestroyImmediate(worldTexture); }
            var after = ExistingScenes();
            bool preserved = JsonUtility.ToJson(new SceneEnvelope { scenes = before }) == JsonUtility.ToJson(new SceneEnvelope { scenes = after });
            Require(preserved, "Existing scene state changed");
            var report = new LabReport { status = "CREATED_PENDING_USER_VISUAL_APPROVAL", unityVersion = Application.unityVersion,
                scene = LabPath, calibrationStatus = "PROVISIONAL test values only; no approved grip or socket coordinates",
                scenesBefore = before, scenesAfter = after, originalSceneStatePreserved = preserved,
                cameraCapturesWritten = true, viewmodel = viewResult, worldModel = worldResult,
                initialCameraLocalPosition = InitialPosition, initialUniformScale = InitialScale,
                fpsFov = 90, nearClip = 0.05f, farClip = 30,
                statsScope = "Editor aggregate snapshot after two offscreen camera renders; not isolated Game View performance or FPS/GPU timing",
                editorStats = Stats() };
            WriteNew(ReportPath, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            ShowWindow();
            Debug.Log("[Hellfire Task05] VisualLab created and reopened additively. Both camera captures saved. Gameplay was not saved. Calibration values remain provisional.");
        }

        static Transform FindTransform(string relative)
        {
            var scene = LabScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VisualLab");
            return root == null ? null : root.transform.Find(relative);
        }
        static T Find<T>(string relative) where T : Component { return FindTransform(relative)?.GetComponent<T>(); }

        [MenuItem("Hellfire/Weapon Development/Hell's Whisper Visual Lab")]
        public static void ShowWindow()
        {
            var window = GetWindow<HellsWhisperVisualLabTask05>("Hell's Whisper Lab");
            window.minSize = new Vector2(430, 620); window.Show();
        }

        void OnEnable() { SceneView.duringSceneGui += Guides; HellsWhisperReloadTask07.Changed += ReloadPreviewChanged; }
        void ReloadPreviewChanged()
        {
            var fps = Find<Camera>("FPSCameraRig/PreviewCamera");
            if (fps != null)
            {
                if (fpsPreview != null) DestroyImmediate(fpsPreview);
                fpsPreview = RenderPreview(fps, 960, 540);
            }
            Repaint();
        }
        void OnDisable()
        {
            HellsWhisperReloadTask07.Changed -= ReloadPreviewChanged;
            HellsWhisperReloadTask07.Reset();
            SceneView.duringSceneGui -= Guides;
            if (fpsPreview != null) DestroyImmediate(fpsPreview);
            if (worldPreview != null) DestroyImmediate(worldPreview);
        }
        void Guides(SceneView view)
        {
            if (!showGuides || EditorApplication.isPlaying) return;
            var station = FindTransform("WorldModelStation"); if (station == null) return;
            var old = Handles.color; var p = station.position + new Vector3(-0.3f, 0, -1.2f);
            Handles.color = Color.white; Handles.DrawLine(p, p + Vector3.up);
            for (int i = 0; i <= 10; i++) Handles.DrawLine(p + Vector3.up * (i * 0.1f), p + Vector3.up * (i * 0.1f) + Vector3.forward * 0.06f);
            Handles.Label(p + Vector3.up * 1.05f, "TEMP reference: 1 metre (10 cm ticks)");
            Handles.color = Color.red; Handles.DrawLine(p, p + Vector3.right * 0.3f);
            Handles.color = Color.green; Handles.DrawLine(p, p + Vector3.up * 0.3f);
            Handles.color = Color.blue; Handles.DrawLine(p, p + Vector3.forward * 0.3f);
            Handles.color = old;
        }

        void RefreshPreviews()
        {
            var fps = Find<Camera>("FPSCameraRig/PreviewCamera"); var world = Find<Camera>("InspectionCameraRig/WorldInspectionCamera");
            if (fps == null || world == null) return;
            if (fpsPreview != null) DestroyImmediate(fpsPreview);
            if (worldPreview != null) DestroyImmediate(worldPreview);
            fpsPreview = RenderPreview(fps, 960, 540); worldPreview = RenderPreview(world, 640, 640);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Hell's Whisper — Visual Calibration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Open lab ADDITIVELY (preserve Gameplay)"))
                {
                    var s = LabScene(); if (!s.IsValid() || !s.isLoaded) s = EditorSceneManager.OpenScene(LabPath, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(s); IsolateCameras(s);
                }
                var anchor = FindTransform("FPSCameraRig/PreviewCamera/ViewmodelAnchor");
                var pivot = FindTransform("FPSCameraRig/PreviewCamera/ViewmodelAnchor/CalibrationPivot_PROVISIONAL");
                if (anchor != null && pivot != null)
                {
                    EditorGUI.BeginChangeCheck();
                    var pos = EditorGUILayout.Vector3Field("Position (camera local metres)", anchor.localPosition);
                    var rot = EditorGUILayout.Vector3Field("Rotation (degrees)", anchor.localEulerAngles);
                    float scale = EditorGUILayout.FloatField("Uniform scale (preview only)", pivot.localScale.x);
                    if (EditorGUI.EndChangeCheck() && scale > 0 && !float.IsNaN(scale) && !float.IsInfinity(scale))
                    {
                        Undo.RecordObjects(new UnityEngine.Object[] { anchor, pivot }, "Calibrate Hell's Whisper preview");
                        anchor.localPosition = pos; anchor.localEulerAngles = rot; pivot.localScale = Vector3.one * scale;
                        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene); SceneView.RepaintAll(); Repaint();
                    }
                    if (GUILayout.Button("Reset PROVISIONAL defaults"))
                    {
                        Undo.RecordObjects(new UnityEngine.Object[] { anchor, pivot }, "Reset Hell's Whisper preview");
                        anchor.localPosition = InitialPosition; anchor.localRotation = Quaternion.identity;
                        pivot.localPosition = Vector3.zero; pivot.localRotation = Quaternion.identity; pivot.localScale = Vector3.one * InitialScale;
                        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene); SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Select viewmodel calibration Transform")) Selection.activeTransform = anchor;
                    if (GUILayout.Button("Frame world model in Scene View"))
                    {
                        var station = FindTransform("WorldModelStation"); var renderer = station.GetComponentInChildren<MeshRenderer>();
                        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(renderer.bounds, false);
                        Selection.activeTransform = station;
                    }
                    showGuides = EditorGUILayout.Toggle("Temporary metre / axis guides", showGuides);
                    if (GUILayout.Button("Refresh both camera previews")) RefreshPreviews();
                    if (GUILayout.Button("Save ONLY this lab scene")) EditorSceneManager.SaveScene(anchor.gameObject.scene, LabPath);
                }
                else EditorGUILayout.HelpBox("Open the dedicated lab scene to enable calibration controls.", MessageType.Warning);
            }
            HellsWhisperReloadTask07.DrawGUI();
            EditorGUILayout.LabelField("FPS camera — 90 degrees, 16:9", EditorStyles.boldLabel);
            if (fpsPreview != null) { var rect = GUILayoutUtility.GetAspectRect(16f / 9f); GUI.DrawTexture(rect, fpsPreview, ScaleMode.ScaleToFit); }
            else GUILayout.Label("Use Refresh both camera previews.");
            EditorGUILayout.LabelField("World model — separate inspection camera", EditorStyles.boldLabel);
            if (worldPreview != null) { var rect = GUILayoutUtility.GetAspectRect(1); GUI.DrawTexture(rect, worldPreview, ScaleMode.ScaleToFit); }
            EditorGUILayout.HelpBox("Calibration and reload are PROVISIONAL presentation previews. No production prefab, weapon logic or socket calibration is modified.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
