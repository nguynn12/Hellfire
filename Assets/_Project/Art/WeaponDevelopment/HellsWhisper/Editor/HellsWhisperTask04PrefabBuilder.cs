#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hellfire.WeaponDevelopment.EditorOnly
{
    // Task-scoped, one-shot asset builder. Never saves or edits an existing scene.
    // Uses only the two new Task 04 scaffold prefabs, in isolated prefab-content scenes.
    internal static class HellsWhisperTask04PrefabBuilder
    {
        const string Folder = "Assets/_Project/Art/WeaponDevelopment/HellsWhisper/";
        const string Fbx = "Assets/_Project/Models/Weapons/HellsWhisper/Model/base_basic_pbr.fbx";
        const string MaterialPath = Folder + "MAT_HellsWhisper_PBR_Test.mat";
        const string View = Folder + "PF_HellsWhisper_Viewmodel.prefab";
        const string World = Folder + "PF_HellsWhisper_WorldModel.prefab";
        const string ReportPath = Folder + "QA_HellsWhisper_Prefabs_Task04.json";
        const string SessionKey = "Hellfire.HellsWhisper.Task04.Attempted.v1";

        [Serializable] sealed class TransformRow
        {
            public string path;
            public Vector3 localPosition, localEulerAngles, localScale, worldPosition, lossyScale;
        }
        [Serializable] sealed class MeshRow
        {
            public string name, assetPath, assetGuid;
            public long localFileId, vertexCount, triangleCount, indexCount, runtimeMemoryBytes;
            public int submeshCount, vertexBufferCount, vertexStride0;
            public string indexFormat;
            public Vector3 localBoundsCenter, localBoundsSize;
            public bool normals, tangents, uv0, readable;
        }
        [Serializable] sealed class SceneRow
        {
            public string path, name;
            public bool dirty;
            public int rootCount;
            public string handle;
        }
        [Serializable] sealed class PrefabRow
        {
            public string path, assetGuid, nestedModelSource, materialPath, meshPath;
            public bool assetDatabaseFound, rootIdentity, visualPivotIdentity, materialMatches, meshMatches;
            public int missingScripts, monoBehaviours, rigidbodies, colliders, animatorCount;
            public string[] componentTypes;
            public TransformRow[] hierarchy;
            public Vector3 fullHierarchyBoundsCenter, fullHierarchyBoundsSize;
        }
        [Serializable] sealed class Report
        {
            public string status, unityVersion, projectPath, shader, generatedUtc;
            public string orientationCalibration, socketCalibration;
            public bool compilationFinished, sourceImported, materialImported, materialInstancingEnabled;
            public bool existingSceneStateUnchanged, activeSceneUnchanged;
            public string[] shaderErrors;
            public MeshRow[] sourceMeshes;
            public PrefabRow[] prefabs;
            public SceneRow[] scenesBefore, scenesAfter;
            public string longestAxis;
        }

        [MenuItem("Hellfire/Weapon Development/Prepare Task04 Prefabs")]
        static void Schedule()
        {
            if (File.Exists(ReportPath) || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += WaitForReady;
        }

        static void WaitForReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += WaitForReady;
                return;
            }
            if (File.Exists(ReportPath) || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            try { Run(); }
            catch (Exception e) { Debug.LogError("[Hellfire Task04] STOP; no cleanup or scene save attempted. " + e); }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static bool Identity(Transform t)
        {
            return t.localPosition.sqrMagnitude < 1e-12f
                && Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.0001f
                && (t.localScale - Vector3.one).sqrMagnitude < 1e-12f;
        }

        static SceneRow[] SceneState()
        {
            var rows = new List<SceneRow>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (EditorSceneManager.IsPreviewScene(s)) continue;
                rows.Add(new SceneRow { path = s.path, name = s.name, dirty = s.isDirty,
                    rootCount = s.rootCount, handle = s.handle.GetRawData().ToString() });
            }
            return rows.ToArray();
        }

        static string PathOf(Transform t, Transform root)
        {
            return t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;
        }

        static TransformRow[] Hierarchy(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t => new TransformRow {
                path = PathOf(t, root.transform), localPosition = t.localPosition,
                localEulerAngles = t.localEulerAngles, localScale = t.localScale,
                worldPosition = t.position, lossyScale = t.lossyScale }).ToArray();
        }

        static MeshRow MeshInfo(Mesh m)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long id);
            long triangles = 0, indices = 0;
            for (int i = 0; i < m.subMeshCount; i++)
            {
                long count = m.GetIndexCount(i); indices += count;
                Require(m.GetTopology(i) == MeshTopology.Triangles, "Unexpected non-triangle mesh topology");
                triangles += count / 3;
            }
            return new MeshRow { name = m.name, assetPath = AssetDatabase.GetAssetPath(m),
                assetGuid = guid, localFileId = id, vertexCount = m.vertexCount,
                triangleCount = triangles, indexCount = indices, submeshCount = m.subMeshCount,
                runtimeMemoryBytes = Profiler.GetRuntimeMemorySizeLong(m), indexFormat = m.indexFormat.ToString(),
                vertexBufferCount = m.vertexBufferCount, vertexStride0 = m.GetVertexBufferStride(0),
                localBoundsCenter = m.bounds.center, localBoundsSize = m.bounds.size,
                normals = m.HasVertexAttribute(VertexAttribute.Normal),
                tangents = m.HasVertexAttribute(VertexAttribute.Tangent),
                uv0 = m.HasVertexAttribute(VertexAttribute.TexCoord0), readable = m.isReadable };
        }

        static void CheckScaffold(string path)
        {
            Require(File.Exists(path), "Missing new scaffold: " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Require(Identity(root.transform), "Scaffold root is not identity");
                Require(root.transform.childCount == 1, "Unexpected scaffold hierarchy");
                var pivot = root.transform.GetChild(0);
                Require(pivot.name == "VisualPivot" && pivot.childCount == 0 && Identity(pivot),
                    "Scaffold already contains content; refusing to overwrite");
                Require(root.GetComponentsInChildren<Component>(true).All(c => c is Transform),
                    "Unexpected scaffold components");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Populate(string path, GameObject model, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pivot = root.transform.GetChild(0);
                var instance = PrefabUtility.InstantiatePrefab(model, root.scene) as GameObject;
                Require(instance != null, "Unity failed to instantiate original FBX");
                instance.transform.SetParent(pivot, false); // Preserve the imported local transform.
                instance.name = "ImportedFBX";
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Require(renderers.Length == 1, "Unexpected renderer count");
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                Require(success, "Prefab save failed: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static PrefabRow Validate(string path, Material material, Mesh expected)
        {
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Prefab absent from Asset Database");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                var components = root.GetComponentsInChildren<Component>(true);
                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                var pivot = root.transform.Find("VisualPivot");
                Require(pivot != null && pivot.childCount == 1, "Invalid visual hierarchy");
                var imported = pivot.GetChild(0);
                var row = new PrefabRow { path = path, assetGuid = AssetDatabase.AssetPathToGUID(path),
                    assetDatabaseFound = true, rootIdentity = Identity(root.transform), visualPivotIdentity = Identity(pivot),
                    nestedModelSource = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(imported.gameObject),
                    missingScripts = transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                    monoBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true).Length,
                    rigidbodies = root.GetComponentsInChildren<Rigidbody>(true).Length,
                    colliders = root.GetComponentsInChildren<Collider>(true).Length,
                    animatorCount = root.GetComponentsInChildren<Animator>(true).Length,
                    componentTypes = components.Select(c => c == null ? "MISSING" : c.GetType().FullName).Distinct().ToArray(),
                    hierarchy = Hierarchy(root) };
                row.meshMatches = filters.Length == 1 && filters[0].sharedMesh == expected;
                row.materialMatches = renderers.Length == 1 && renderers[0].sharedMaterials.Length == expected.subMeshCount
                    && renderers[0].sharedMaterials.All(m => m == material);
                row.meshPath = filters.Length == 1 ? AssetDatabase.GetAssetPath(filters[0].sharedMesh) : "INVALID";
                row.materialPath = renderers.Length == 1 ? AssetDatabase.GetAssetPath(renderers[0].sharedMaterial) : "INVALID";
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    row.fullHierarchyBoundsCenter = bounds.center;
                    row.fullHierarchyBoundsSize = bounds.size;
                }
                Require(row.rootIdentity && row.visualPivotIdentity && row.meshMatches && row.materialMatches,
                    "Transform/mesh/material verification failed: " + path);
                Require(row.nestedModelSource == Fbx, "Nested original FBX link was not preserved");
                Require(row.missingScripts == 0 && row.monoBehaviours == 0 && row.rigidbodies == 0 && row.colliders == 0,
                    "Unexpected missing script, behaviour or physics component");
                return row;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Run()
        {
            Require(Application.dataPath == "/Users/stormpaw/File/Hellfire/Assets", "Wrong Unity project");
            Require(Application.unityVersion == "6000.5.6f1", "Unexpected Unity version");
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Play Mode is active");
            Require(!File.Exists(ReportPath), "Report already exists; refusing overwrite");
            var before = SceneState();
            var activeHandle = SceneManager.GetActiveScene().handle;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Require(model != null && material != null, "Source FBX or material is not imported");
            Require(material.shader != null && material.shader.name == "Universal Render Pipeline/Lit", "Wrong material shader");
            string[] shaderErrors = ShaderUtil.GetShaderMessages(material.shader)
                .Where(m => m.severity.ToString() == "Error").Select(m => m.message).ToArray();
            Require(shaderErrors.Length == 0, "Shader contains compilation errors");
            var sourceMeshes = model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Distinct().ToArray();
            Require(sourceMeshes.Length == 1 && sourceMeshes[0] != null, "Unexpected original mesh structure");
            Require(sourceMeshes[0].subMeshCount == 1 && sourceMeshes[0].HasVertexAttribute(VertexAttribute.TexCoord0),
                "Source lacks expected submesh/UV data");
            CheckScaffold(View); CheckScaffold(World);
            Populate(View, model, material); Populate(World, model, material);
            var view = Validate(View, material, sourceMeshes[0]);
            var world = Validate(World, material, sourceMeshes[0]);
            var after = SceneState();
            bool scenesSame = JsonUtility.ToJson(new SceneEnvelope { scenes = before })
                == JsonUtility.ToJson(new SceneEnvelope { scenes = after });
            Require(scenesSame && activeHandle == SceneManager.GetActiveScene().handle, "Existing scene state changed");
            var size = view.fullHierarchyBoundsSize;
            var report = new Report { status = "TECHNICAL_VALIDATION_PASSED_ARTISTIC_CALIBRATION_PENDING",
                unityVersion = Application.unityVersion, projectPath = Application.dataPath,
                generatedUtc = DateTime.UtcNow.ToString("o"), shader = material.shader.name,
                compilationFinished = !EditorApplication.isCompiling, sourceImported = true, materialImported = true,
                materialInstancingEnabled = material.enableInstancing, shaderErrors = shaderErrors,
                sourceMeshes = sourceMeshes.Select(MeshInfo).ToArray(), prefabs = new[] { view, world },
                scenesBefore = before, scenesAfter = after, existingSceneStateUnchanged = scenesSame,
                activeSceneUnchanged = activeHandle == SceneManager.GetActiveScene().handle,
                longestAxis = size.x >= size.y && size.x >= size.z ? "X" : size.y >= size.z ? "Y" : "Z",
                orientationCalibration = "Forward, up and grip in weapon semantics NOT VERIFIED. All VisualPivot transforms remain neutral. Imported transforms are preserved.",
                socketCalibration = "MuzzleFX, GripAnchor, SightAnchor NOT VERIFIED; no sockets created." };
            using (var stream = new FileStream(ReportPath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[Hellfire Task04] Created and reloaded both prefabs. Original FBX/mesh links and PBR material verified. Existing scenes were not saved. Report: " + ReportPath);
        }
        [Serializable] sealed class SceneEnvelope { public SceneRow[] scenes; }
    }
}
#endif
