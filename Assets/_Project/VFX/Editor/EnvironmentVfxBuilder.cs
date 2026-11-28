using System;
using System.IO;
using System.Linq;
using Hellfire.Dungeon;
using Hellfire.Vfx;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Hellfire.EditorTools
{
    internal static class EnvironmentVfxBuilder
    {
        private const string Root = "Assets/_Project/VFX";
        internal const string Sandbox = Root + "/Scenes/EnvironmentVfxSandbox.unity";
        private enum Mask { Ember, Haze, Frost }

        public static void BindDungeonPrefabs()
        {
            BindBiome("Lava", "LavaEmber");
            BindBiome("Ice", "FrozenMist");
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }

        private static void BindBiome(string biome, string effect)
        {
            var art = AssetDatabase.LoadAssetAtPath<DungeonArtSet>("Assets/_Project/Art/GameplayVisuals/" + biome + ".asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + effect + ".prefab");
            if (art == null || prefab == null) throw new InvalidOperationException("Missing biome or VFX prefab for " + biome);
            art.AmbientVfx = prefab;
            EditorUtility.SetDirty(art);
        }

        [MenuItem("Hellfire/VFX/Rebuild Environment Sandbox")]
        public static void Assemble()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { "Materials", "Textures", "Prefabs", "Scenes" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();

            var ember = Texture(Mask.Ember);
            var haze = Texture(Mask.Haze);
            var frost = Texture(Mask.Frost);
            var lavaGlow = ParticlesMaterial("Lava_Ember", ember, new Color(3.4f, 1.28f, .40f), true);
            var lavaAsh = ParticlesMaterial("Lava_Ash", ember, new Color(.68f, .48f, .38f), false);
            var lavaHaze = ParticlesMaterial("Lava_Haze", haze, new Color(.45f, .24f, .15f), false, true);
            var iceFrost = ParticlesMaterial("Ice_Dust", frost, new Color(1.8f, 2.2f, 2.7f), true);
            var iceBlue = ParticlesMaterial("Ice_Mist", haze, new Color(.49f, .68f, .80f), false, true);
            var icePale = ParticlesMaterial("Ice_Wisps", haze, new Color(.65f, .77f, .89f), false, true);

            var lava = new GameObject("Lava Ember");
            Layer(lava.transform, "01 · Rising Embers", lavaGlow, 1107, new Vector3(0, .17f, .25f),
                new Vector2(3.8f, 1.5f), 15f, new Vector2(2.4f, 4.1f), new Vector2(.070f, .16f),
                new Vector2(.42f, .78f), new Color(1f, .41f, .16f, .83f), .085f);
            Layer(lava.transform, "02 · Ash in the Updraft", lavaAsh, 1108, new Vector3(0, .38f, .35f),
                new Vector2(4.2f, 2.3f), 13f, new Vector2(3.6f, 5.3f), new Vector2(.033f, .086f),
                new Vector2(.19f, .42f), new Color(.72f, .54f, .46f, .54f), .13f);
            Layer(lava.transform, "03 · Heat Haze", lavaHaze, 1109, new Vector3(0, .34f, .1f),
                new Vector2(4.0f, 1.8f), 3.5f, new Vector2(4.3f, 6.6f), new Vector2(1.7f, 2.5f),
                new Vector2(.07f, .22f), new Color(1f, .82f, .70f, .21f), .085f, .40f);
            string lavaPrefab = Save(lava, "LavaEmber");

            var ice = new GameObject("Frozen Mist");
            Layer(ice.transform, "01 · Low Blue Mist", iceBlue, 2207, new Vector3(0, .24f, .0f),
                new Vector2(5.2f, 3.3f), 5.4f, new Vector2(5.2f, 8f), new Vector2(1.9f, 3.2f),
                new Vector2(.025f, .13f), new Color(.77f, .91f, 1f, .36f), .05f, .32f, .28f);
            Layer(ice.transform, "02 · Pale Surface Wisps", icePale, 2208, new Vector3(0, .29f, .35f),
                new Vector2(4.9f, 2.80f), 3.3f, new Vector2(4.1f, 6.2f), new Vector2(1.4f, 2.4f),
                new Vector2(-.025f, .06f), new Color(.85f, .95f, 1f, .26f), .040f, .30f, .46f);
            Layer(ice.transform, "03 · Floating Ice Dust", iceFrost, 2209, new Vector3(0, .63f, .38f),
                new Vector2(4.8f, 2.9f), 12.5f, new Vector2(3f, 5.4f), new Vector2(.041f, .10f),
                new Vector2(-.13f, .01f), new Color(.67f, .84f, 1f, .63f), .075f, .85f, .29f);

            string icePrefab = Save(ice, "FrozenMist");
            BuildScene(lavaPrefab, icePrefab);
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static string Save(GameObject root, string id)
        {
            string path = Root + "/Prefabs/" + id + ".prefab";
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Cannot save " + path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static void Layer(Transform parent, string name, Material material, uint seed, Vector3 origin,
            Vector2 footprint, float rate, Vector2 lifetime, Vector2 size, Vector2 vertical, Color tint,
            float turbulence, float aspect = 1f, float wind = .07f)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = origin;
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = seed;
            var main = particles.main;
            main.duration = 6f;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSizeY = new ParticleSystem.MinMaxCurve(size.x * aspect, size.y * aspect);
            main.startSizeZ = 1f;
            main.startRotation = aspect <= .4f ? new ParticleSystem.MinMaxCurve(-.13f, .13f) :
                new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = tint;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * lifetime.y) + 5, 12, 128);
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(footprint.x, name.Contains("Dust", StringComparison.Ordinal) ? 1.1f : .18f, footprint.y);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(wind * .4f, wind);
            velocity.y = new ParticleSystem.MinMaxCurve(vertical.x, vertical.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-.055f, .07f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.frequency = .30f;
            noise.strength = turbulence;
            noise.scrollSpeed = .28f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0, 0f), new GradientAlphaKey(.94f, .18f),
                    new GradientAlphaKey(.85f, .54f), new GradientAlphaKey(0, 1f) });
            fade.color = gradient;
            var grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.1f, AnimationCurve.EaseInOut(0f, aspect < .5f ? .65f : 1f,
                1f, aspect < .5f ? 1.12f : .39f));
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material.IsKeywordEnabled("_SOFTPARTICLES_ON"))
            {
                var fallback = ParticlesMaterial(material.name + "_NoDepth", material.GetTexture("_BaseMap"),
                    material.GetColor("_BaseColor"), false);
                var settings = new SerializedObject(child.AddComponent<EnvironmentParticleDepth>());
                settings.FindProperty("_soft").objectReferenceValue = material;
                settings.FindProperty("_fallback").objectReferenceValue = fallback;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Material ParticlesMaterial(string id, Texture texture, Color tint, bool additive, bool soft = false)
        {
            var material = MaterialAsset(id, "Universal Render Pipeline/Particles/Unlit");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)(additive ? BaseShaderGUI.BlendMode.Additive : BaseShaderGUI.BlendMode.Alpha));
            material.SetFloat("_SoftParticlesEnabled", soft ? 1 : 0);
            material.SetFloat("_SoftParticlesNearFadeDistance", .025f);
            material.SetFloat("_SoftParticlesFarFadeDistance", .50f);
            material.SetFloat("_CameraFadingEnabled", 1);
            material.SetFloat("_CameraNearFadeDistance", .32f);
            material.SetFloat("_CameraFarFadeDistance", 1.25f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MaterialAsset(string id, string shaderName)
        {
            string path = Root + "/Materials/" + id + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing " + shaderName);
            material = new Material(shader) { name = id };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D Texture(Mask kind)
        {
            const int size = 128;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(((x + .5f) / size - .5f) * 2f, ((y + .5f) / size - .5f) * 2f);
                float alpha = kind switch
                {
                    Mask.Ember => Mathf.Exp(-13f * (p.x * p.x + p.y * p.y * .54f))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, p.magnitude))),
                    Mask.Frost => Mathf.Exp(-13f * (Mathf.Abs(p.x) + Mathf.Abs(p.y))) * .5f
                        + Mathf.Exp(-98f * Mathf.Min(p.x * p.x, p.y * p.y)) *
                        (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.16f, .83f, p.magnitude))) * .52f,
                    _ => Cloud(p)
                };
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }
            image.SetPixels32(pixels);
            string path = Root + "/Textures/" + kind + "Mask.png";
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Cloud(Vector2 p)
        {
            float noise = Mathf.PerlinNoise(7 + p.x * 2.3f, 17 + p.y * 2.8f) * .65f +
                Mathf.PerlinNoise(22 + p.x * 5f, 2 + p.y * 5f) * .35f;
            float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.16f, .76f, noise));
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.24f, .97f, p.magnitude));
            return cloud * edge * edge;
        }

        private static void BuildScene(string lavaPrefab, string icePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Sandbox FPS Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .019f, .027f);
            camera.allowHDR = true;
            camera.nearClipPlane = .07f;
            camera.farClipPlane = 27f;
            camera.fieldOfView = 49f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.GetUniversalAdditionalCameraData().requiresDepthOption = CameraOverrideOption.UsePipelineSettings;

            var volume = new GameObject("Sandbox Bloom Toggle").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            string profilePath = Root + "/Materials/SandboxBloom.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var effect = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(effect, profile);
            }
            profile.TryGet<Bloom>(out var bloom);
            bloom.intensity.Override(.62f);
            bloom.threshold.Override(1.03f);
            bloom.scatter.Override(.63f);
            EditorUtility.SetDirty(bloom);
            volume.sharedProfile = profile;
            volume.enabled = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.28f, .28f, .32f);
            RenderSettings.ambientEquatorColor = new Color(.22f, .22f, .25f);
            RenderSettings.ambientGroundColor = new Color(.12f, .12f, .15f);
            RenderSettings.reflectionIntensity = .15f;

            var (lavaView, lavaEffect) = Stage("Lava", -20f, lavaPrefab, new Color(1f, .49f, .25f));
            var (iceView, iceEffect) = Stage("Ice", 20f, icePrefab, new Color(.41f, .74f, 1f));
            camera.transform.SetPositionAndRotation(lavaView.position, lavaView.rotation);
            var settings = new SerializedObject(camera.gameObject.AddComponent<EnvironmentVfxSandbox>());
            settings.FindProperty("_camera").objectReferenceValue = camera;
            settings.FindProperty("_bloom").objectReferenceValue = volume;
            AssignArray(settings.FindProperty("_views"), lavaView, iceView);
            AssignArray(settings.FindProperty("_effects"), lavaEffect, iceEffect);
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, Sandbox)) throw new InvalidOperationException("Couldn't save VFX sandbox");
        }

        private static void AssignArray(SerializedProperty property, params Object[] objects)
        {
            property.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        private static (Transform view, GameObject effect) Stage(string id, float x, string prefabPath, Color light)
        {
            var stage = new GameObject(id + " Preview Stage").transform;
            stage.position = Vector3.right * x;
            var view = new GameObject("Matched FPS View").transform;
            view.SetParent(stage, false);
            view.localPosition = new Vector3(-.25f, 1.62f, -5.75f);
            view.LookAt(stage.TransformPoint(new Vector3(.14f, 1.22f, 2f)));
            var effect = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), stage);
            effect.transform.localPosition = new Vector3(0f, 0f, .45f);

            var stone = MaterialAsset(id + "_StageStone", "Universal Render Pipeline/Lit");
            stone.SetColor("_BaseColor", id == "Lava" ? new Color(.16f, .125f, .116f) : new Color(.125f, .149f, .175f));
            stone.SetFloat("_Smoothness", .23f);
            EditorUtility.SetDirty(stone);
            Block(stage, "Stone floor", new Vector3(0, -.21f, 2f), new Vector3(10f, .42f, 18f), stone);
            Block(stage, "Dark backing", new Vector3(0, 2f, 8.6f), new Vector3(10f, 4f, .55f), stone);
            Block(stage, "Left masonry", new Vector3(-4.8f, 2f, 2f), new Vector3(.55f, 4f, 17f), stone);
            Block(stage, "Right masonry", new Vector3(4.8f, 2f, 2f), new Vector3(.55f, 4f, 17f), stone);
            Block(stage, "Ceiling", new Vector3(0, 3.8f, 3f), new Vector3(10f, .3f, 16f), stone);

            PlaceExisting(stage, id + "DoorWay", new Vector3(0f, 0f, 3.35f), 3.3f, 0f);
            PlaceExisting(stage, id + "Wall", new Vector3(-3.26f, 0f, 3.38f), 3.35f, 0f);
            PlaceExisting(stage, id + "Wall", new Vector3(3.26f, 0f, 3.38f), 3.35f, 180f);
            PlaceExisting(stage, id + "Pillar", new Vector3(-2.85f, 0f, -.60f), 1.45f, 30f);
            PlaceExisting(stage, id + "Pillar", new Vector3(2.85f, 0f, -.58f), 1.45f, -30f);
            StageLight(stage, "Arch backlight", new Vector3(0f, 1.32f, 4f), light, 14f, 5.6f);
            StageLight(stage, "Raking side light", new Vector3(-2.8f, 2.2f, .35f), light, 8.5f, 8f);
            StageLight(stage, "Soft camera fill", new Vector3(.8f, 2.7f, -3f), new Color(.88f, .90f, 1f), 5.9f, 8f);
            return (view, effect);
        }

        private static void Block(Transform stage, string name, Vector3 position, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(stage, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void StageLight(Transform stage, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        private static void PlaceExisting(Transform stage, string id, Vector3 position, float height, float yaw)
        {
            string source = "Assets/_Project/Models/Layout/" + id;
            string filename = Directory.GetFiles(source + "/Model", "*.fbx").Single();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(filename.Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(source + "/New Material.mat");
            if (model == null || mat == null) throw new InvalidOperationException("Missing preview asset " + source);
            var parent = new GameObject(id + " Placement").transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = mat;
            var bounds = renderer.bounds;
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            parent.localScale = Vector3.one * (height / bounds.size.y);
            parent.SetParent(stage, false);
            parent.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
