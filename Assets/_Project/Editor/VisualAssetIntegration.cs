using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hellfire.Combat;
using Hellfire.Dungeon;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hellfire.EditorTools
{
    internal static class VisualAssetIntegration
    {
        private const string Output = "Assets/_Project/Art/GameplayVisuals";

        // The FBX meshes and texture importers are read-only inputs.
        public static void Assemble()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            try
            {
                var lava = MakeBiome("Lava", new Color(.26f, .17f, .15f), new Color(1, .57f, .3f));
                var ice = MakeBiome("Ice", new Color(.21f, .31f, .39f), new Color(.41f, .74f, 1));
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Gameplay.unity");
                var generator = Object.FindAnyObjectByType<DungeonGenerator>();
                Assign(generator, ("_lavaArt", lava), ("_iceArt", ice));
                EditorSceneManager.SaveScene(scene);

                Assign(Weapon("Pistol"), ("_viewmodelPrefab", WeaponLab("Viewmodel")), ("_worldModelPrefab", WeaponLab("WorldModel")));
                ConfigureWeapon("SMG", "IronShredder", .43f, new Vector3(0f, -.13f, .10f), 0f);
                ConfigureWeapon("Shotgun", "BoneCrusher", .51f, new Vector3(0f, -.14f, .11f), 0f);
                ConfigureWeapon("Rifle", "ObsidianFang", .53f, new Vector3(0f, -.10f, .13f), 0f);

                const string playerPath = "Assets/_Project/Prefabs/Player.prefab";
                using (var edit = new PrefabUtility.EditPrefabContentsScope(playerPath))
                {
                    var socket = edit.prefabContentsRoot.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
                    if (socket.GetComponent<WeaponViewmodelPresenter>() == null) socket.gameObject.AddComponent<WeaponViewmodelPresenter>();
                }
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Assign(Object target, params (string path, Object asset)[] assignments)
        {
            if (target == null) throw new InvalidOperationException("Missing serialized reference target.");
            var serialized = new SerializedObject(target);
            foreach (var (path, asset) in assignments)
            {
                if (asset == null) throw new InvalidOperationException("Missing asset for " + path);
                serialized.FindProperty(path).objectReferenceValue = asset;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static WeaponData Weapon(string slot) => AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/_Project/Data/Weapons/" + slot + "_Data.asset");

        private static GameObject WeaponLab(string kind) => AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Art/WeaponDevelopment/HellsWhisper/PF_HellsWhisper_" + kind + ".prefab");

        private static void ConfigureWeapon(string slot, string model, float length, Vector3 position, float yaw)
        {
            string path = "Assets/_Project/Models/Weapons/" + model;
            var original = LoadModel(path);
            var material = MaterialForModel(path, model);
            var root = new GameObject(model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original, root.transform);
            var bounds = GetBounds(instance);
            float scale = length / bounds.size.z;
            var offset = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            var rotate = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localPosition = position - rotate * (offset * scale);
            instance.transform.localRotation = rotate * instance.transform.localRotation;
            instance.transform.localScale *= scale;
            foreach (var item in instance.GetComponentsInChildren<Renderer>())
            {
                item.sharedMaterials = item.sharedMaterials.Select(_ => material).ToArray();
                item.shadowCastingMode = ShadowCastingMode.Off;
                item.receiveShadows = false;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + model + "_Viewmodel.prefab");
            Object.DestroyImmediate(root);
            Assign(Weapon(slot), ("_viewmodelPrefab", prefab));
            Debug.Log($"[VISUAL] {slot} uses {model}, FPS length={length:F2}m");
        }

        private static DungeonArtSet MakeBiome(string biome, Color floorColor, Color light)
        {
            var set = GetOrCreate<DungeonArtSet>(Output + "/" + biome + ".asset");
            (set.Wall, set.WallSize) = MakeModule(biome + "Wall", true);
            (set.Door, set.DoorSize) = MakeModule(biome + "DoorWay");
            (set.Gate, set.GateSize) = MakeModule(biome + "Gate");
            (set.Pillar, set.PillarSize) = MakeModule(biome + "Pillar");
            var baseline = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_LoS_Obstacle_Wall.mat");
            set.Floor = Surface(baseline, biome + "_Floor", floorColor);
            set.Ceiling = Surface(baseline, biome + "_Ceiling", floorColor * .60f);
            set.CorridorLight = light;
            EditorUtility.SetDirty(set);
            return set;
        }

        private static (GameObject prefab, Vector3 size) MakeModule(string id, bool recurringWall = false)
        {
            string path = "Assets/_Project/Models/Layout/" + id;
            var model = Object.Instantiate(LoadModel(path));
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            var filter = renderer.GetComponent<MeshFilter>();
            var bounds = renderer.bounds;
            var bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var mesh = Simplify(filter.sharedMesh, filter.transform.localToWorldMatrix, bottom,
                recurringWall ? 24 : 72, recurringWall ? 8 : 24);
            string meshPath = Output + "/" + id + "_Runtime.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                saved = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
            }

            var root = new GameObject(id);
            root.AddComponent<MeshFilter>().sharedMesh = saved;
            var art = root.AddComponent<MeshRenderer>();
            art.sharedMaterial = MaterialForModel(path, id);
            art.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + id + ".prefab");
            Debug.Log($"[VISUAL] {id} {filter.sharedMesh.vertexCount}->{saved.vertexCount} verts; " +
                      $"{saved.GetIndexCount(0) / 3} triangles; authored size {bounds.size:F3}");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(model);
            return (prefab, bounds.size);
        }

        private static GameObject LoadModel(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(
            Directory.GetFiles(path + "/Model", "*.fbx").Single().Replace('\\', '/'));

        private static Bounds GetBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material MaterialForModel(string path, string id)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(path + "/New Material.mat");
            var material = Surface(original, id, Color.white);
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/Textures/texture_emissive.png");
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", new Color(.58f, .58f, .58f));
                material.EnableKeyword("_EMISSION");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Surface(Material original, string id, Color color)
        {
            string path = Output + "/" + id + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(original) { name = id };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(original);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Mesh Simplify(Mesh original, Matrix4x4 world, Vector3 bottom, int resolution, int uvResolution)
        {
            using var mesh = MeshUtility.AcquireReadOnlyMeshData(original);
            var source = mesh[0];
            if (source.subMeshCount != 1) throw new InvalidOperationException("Expected one material for " + original.name);
            using var positions = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp);
            using var normals = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp);
            using var uv = new NativeArray<Vector2>(source.vertexCount, Allocator.Temp);
            source.GetVertices(positions);
            source.GetNormals(normals);
            source.GetUVs(0, uv);
            float step = resolution / Mathf.Max(.01f, world.MultiplyVector(original.bounds.size).magnitude);
            var groups = new Dictionary<(Vector3Int position, Vector2Int uv, bool negativeZ), int>();
            var positionsOut = new List<Vector3>();
            var normalsOut = new List<Vector3>();
            var uvOut = new List<Vector2>();
            var weights = new List<int>();
            var map = new int[source.vertexCount];
            for (int i = 0; i < source.vertexCount; i++)
            {
                var p = world.MultiplyPoint3x4(positions[i]) - bottom;
                var n = world.MultiplyVector(normals[i]).normalized;
                var t = uv[i];
                var key = (Vector3Int.RoundToInt(p * step), Vector2Int.RoundToInt(t * uvResolution), n.z < 0f);
                if (!groups.TryGetValue(key, out int index))
                {
                    index = groups.Count;
                    groups.Add(key, index);
                    positionsOut.Add(p);
                    normalsOut.Add(n);
                    uvOut.Add(t);
                    weights.Add(1);
                }
                else
                {
                    positionsOut[index] += p;
                    normalsOut[index] += n;
                    uvOut[index] += t;
                    weights[index]++;
                }
                map[i] = index;
            }
            for (int i = 0; i < weights.Count; i++)
            {
                positionsOut[i] /= weights[i];
                normalsOut[i] = normalsOut[i].normalized;
                uvOut[i] /= weights[i];
            }

            var triangles = new List<int>();
            var distinct = new HashSet<(int, int, int)>();
            using var originalIndices = new NativeArray<int>(source.GetSubMesh(0).indexCount, Allocator.Temp);
            source.GetIndices(originalIndices, 0);
            for (int i = 0; i < originalIndices.Length; i += 3)
            {
                int a = map[originalIndices[i]], b = map[originalIndices[i + 1]], c = map[originalIndices[i + 2]];
                if (a == b || b == c || a == c) continue;
                var key = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
                if (!distinct.Add(key)) continue;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            var result = new Mesh { name = original.name + "_Runtime", indexFormat = IndexFormat.UInt32 };
            result.SetVertices(positionsOut);
            result.SetNormals(normalsOut);
            result.SetUVs(0, uvOut);
            result.SetTriangles(triangles, 0);
            result.RecalculateTangents();
            result.RecalculateBounds();
            return result;
        }
    }
}
