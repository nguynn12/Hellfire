using UnityEngine;
using UnityEngine.Rendering;

namespace Hellfire.Dungeon
{
    internal static class DungeonMasonry
    {
        private const float SpringOffset = .82f;

        public static void Floor(Transform parent, Transform walkableFloor, Material material)
        {
            walkableFloor.GetComponent<Renderer>().enabled = false;
            Vector3 size = walkableFloor.localScale;
            Vector3 centre = walkableFloor.localPosition + Vector3.up * (size.y * .5f + .002f);
            var half = new Vector3(size.x, 0f, size.z) * .5f;
            var vertices = new[] { centre - half, centre + new Vector3(half.x, 0f, -half.z),
                centre + half, centre + new Vector3(-half.x, 0f, half.z) };
            var uv = new Vector2[4];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = parent.TransformPoint(vertices[i]);
                uv[i] = new Vector2(world.x / 5.6f, world.z / 4.2f);
            }
            Surface(parent, "MasonryFloor", material, vertices, uv, new[] { 0, 3, 1, 1, 3, 2 }, ShadowCastingMode.Off);
        }

        public static float VaultHeight(float wallHeight, Vector2 size, float x, float z, bool corridor)
        {
            float xCurve = Curve(x, size.x);
            float zCurve = Curve(z, size.y);
            bool alongX = size.x >= size.y;
            if (corridor)
            {
                float span = alongX ? x : z;
                float length = alongX ? size.x : size.y;
                float endBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((length * .5f - Mathf.Abs(span) - .15f) / 1.4f));
                return wallHeight - SpringOffset + endBlend * (alongX ? zCurve : xCurve) * .9f;
            }
            return wallHeight - SpringOffset + Mathf.Min(xCurve, zCurve) * 1.9f;
        }

        public static void Vault(Transform parent, Vector3 centre, Vector2 size, float wallHeight, Material material, bool corridor)
        {
            bool alongX = size.x >= size.y;
            int columns = Mathf.CeilToInt(size.x / .24f);
            int rows = Mathf.CeilToInt(size.y / .24f);
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[columns * rows * 6];
            int index = 0;
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                float x = ((float)column / columns - .5f) * size.x;
                float z = ((float)row / rows - .5f) * size.y;
                float longitudinal = alongX ? x : z;
                float ribDistance = Mathf.Abs(Mathf.Repeat(longitudinal + 2.2f, 4.4f) - 2.2f);
                float rib = .20f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.11f, .31f, ribDistance)));
                float y = VaultHeight(wallHeight, size, x, z, corridor) - rib;
                int i = row * (columns + 1) + column;
                vertices[i] = centre + new Vector3(x, y, z);
                uv[i] = new Vector2(x / 4.9f, -z / 3.5f);
                if (row == rows || column == columns) continue;
                int next = i + columns + 1;
                indices[index++] = i;
                indices[index++] = i + 1;
                indices[index++] = next;
                indices[index++] = i + 1;
                indices[index++] = next + 1;
                indices[index++] = next;
            }
            Surface(parent, "MasonryVault", material, vertices, uv, indices, ShadowCastingMode.Off);
        }

        public static void WallCore(Transform parent, float length, float height, float depth, Material material)
        {
            float edge = length * .5f + .045f;
            var vertices = new[] { new Vector3(-edge, -.02f, depth), new Vector3(-edge, height + .02f, depth),
                new Vector3(edge, height + .02f, depth), new Vector3(edge, -.02f, depth) };
            var uv = new[] { Vector2.zero, new Vector2(0f, height / 3.5f),
                new Vector2(length / 4.9f, height / 3.5f), new Vector2(length / 4.9f, 0f) };
            Surface(parent, "MasonryBehindCourses", material, vertices, uv, new[] { 0, 1, 2, 0, 2, 3 }, ShadowCastingMode.Off);
        }

        private static float Curve(float position, float size)
        {
            float t = Mathf.Clamp(position / (size * .5f), -1f, 1f);
            return 1f - t * t;
        }

        private static void Surface(Transform parent, string name, Material material,
            Vector3[] vertices, Vector2[] uv, int[] indices, ShadowCastingMode shadows)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(true);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            obj.AddComponent<GeneratedDungeonMesh>().Mesh = mesh;
        }
    }
}
