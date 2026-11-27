using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hellfire.EditorTools
{
    // Reprojects intact stone swatches from the existing wall atlases into a repeating running bond.
    internal static class DungeonMasonryTextures
    {
        private const int Size = 512;
        private const int Courses = 4;
        private const int Stones = 4;

        public static (Texture2D color, Texture2D normal, Texture2D mask) Build(string biome, string output)
        {
            string source = $"Assets/_Project/Models/Layout/{biome}Wall/Textures/texture_";
            var color = Read(source + "diffuse.png");
            var normal = Read(source + "normal.png");
            var roughness = Read(source + "roughness.png");
            try
            {
                // Pixel positions in the original wall atlas, normalized here from a 600 px inspection.
                var swatches = biome == "Lava"
                    ? new[] { Swatch(366, 211, 173, 137), Swatch(183, 210, 119, 133), Swatch(406, 53, 130, 100) }
                    : new[] { Swatch(158, 223, 190, 152), Swatch(451, 53, 114, 120), Swatch(92, 435, 104, 90) };
                var colors = new Color[Size * Size];
                var normals = new Color[colors.Length];
                var masks = new Color[colors.Length];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float course = (y + .5f) / Size * Courses;
                    int row = Mathf.FloorToInt(course);
                    float stone = (x + .5f) / Size * Stones + (row % 2) * .5f;
                    int column = Mathf.FloorToInt(stone) % Stones;
                    int pattern = (column + 2 * row) % swatches.Length;
                    var patch = swatches[pattern];
                    float u = Mathf.Repeat(stone, 1f);
                    float v = Mathf.Repeat(course, 1f);
                    float flip = (row + column) % 2 == 0 ? 1f : -1f;
                    float sampleX = Mathf.Lerp(patch.xMin, patch.xMax, flip > 0f ? u : 1f - u);
                    float sampleY = Mathf.Lerp(patch.yMin, patch.yMax, v);
                    float stoneside = Mathf.Min(u, 1f - u);
                    float courseside = Mathf.Min(v, 1f - v);
                    float bevel = Mathf.Min(stoneside, courseside);
                    float raised = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.012f, .052f, bevel));
                    Color texel = color.GetPixelBilinear(sampleX, sampleY);
                    float tone = .88f + ((row * 11 + column * 7) % 7 + (row + column * 3) % 5) * .026f;
                    int index = y * Size + x;
                    colors[index] = Color.Lerp(texel * .24f, texel * tone, raised);
                    colors[index].a = 1f;
                    var bump = normal.GetPixelBilinear(sampleX, sampleY);
                    var vector = new Vector3((bump.r * 2f - 1f) * flip, bump.g * 2f - 1f, bump.b * 2f - 1f);
                    if (bevel > .012f && bevel < .052f)
                    {
                        float slope = Mathf.Sin(Mathf.PI * raised) * .59f;
                        if (stoneside < courseside) vector.x += u < .5f ? -slope : slope;
                        else vector.y += v < .5f ? -slope : slope;
                    }
                    vector = vector.normalized * .5f + Vector3.one * .5f;
                    normals[index] = new Color(vector.x, vector.y, vector.z, 1f);
                    float rough = roughness.GetPixelBilinear(sampleX, sampleY).r;
                    float smooth = Mathf.Lerp(.08f, Mathf.Lerp(.12f, biome == "Lava" ? .37f : .56f, 1f - rough), raised);
                    masks[index] = new Color(0f, Mathf.Lerp(.38f, .99f, raised), 0f, smooth);
                }

                return (Write(output + "/" + biome + "_Masonry_Color.png", colors, true),
                    Write(output + "/" + biome + "_Masonry_Normal.png", normals, false, TextureImporterType.NormalMap),
                    Write(output + "/" + biome + "_Masonry_Mask.png", masks, false));
            }
            finally
            {
                Object.DestroyImmediate(color);
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(roughness);
            }
        }

        private static Rect Swatch(int x, int y, int width, int height) =>
            new Rect(x / 600f, (600f - y - height) / 600f, width / 600f, height / 600f);

        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
            return texture;
        }

        private static Texture2D Write(string path, Color[] pixels, bool srgb, TextureImporterType type = TextureImporterType.Default)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels(pixels);
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
