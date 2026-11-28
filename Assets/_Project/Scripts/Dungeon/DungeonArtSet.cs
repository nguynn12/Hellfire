using UnityEngine;
using UnityEngine.Rendering;

namespace Hellfire.Dungeon
{
    [CreateAssetMenu(menuName = "Hellfire/Dungeon art set")]
    public sealed class DungeonArtSet : ScriptableObject
    {
        public GameObject Wall;
        public GameObject Door;
        public GameObject Gate;
        public GameObject Pillar;
        public GameObject AmbientVfx;
        public Material Floor;
        public Material Ceiling;
        public Color CorridorLight;
        public Vector3 WallSize;
        public Vector3 DoorSize;
        public Vector3 GateSize;
        public Vector3 PillarSize;

        public GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var instance = Instantiate(prefab, parent, false);
            instance.transform.SetLocalPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        public void TileWall(Transform collider, float length, float height, float thickness, Quaternion orientation)
        {
            collider.GetComponent<Renderer>().enabled = false;
            // Keep the original collider dimensions. These tiles are visual only.
            Courses(collider.parent, collider.localPosition - Vector3.up * height * .5f,
                orientation, length, height, thickness);
        }

        public void Courses(Transform parent, Vector3 bottom, Quaternion orientation, float length, float height, float thickness)
        {
            var mount = new GameObject(name + "_WallTiles").transform;
            mount.SetParent(parent, false);
            mount.SetLocalPositionAndRotation(bottom, orientation);
            int columns = Mathf.CeilToInt(length / WallSize.x) + 1;
            int rows = Mathf.Max(1, Mathf.CeilToInt(height / WallSize.y));
            float stride = length / (columns - .45f);
            float rise = Mathf.Max(0f, (height - WallSize.y) / Mathf.Max(rows - 1, 1));
            var source = Wall.GetComponent<MeshFilter>();
            var renderer = mount.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Wall.GetComponent<MeshRenderer>().sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var filter = mount.gameObject.AddComponent<MeshFilter>();
            var tiles = new CombineInstance[columns * rows];
            float start = (stride - length) * .5f;
            float depth = (WallSize.z - thickness) * .5f;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float inset = (row + column) % 2 == 0 ? -.024f : .024f;
                float x = start + column * stride - (row % 2) * stride * .5f;
                x = Mathf.Clamp(x, (WallSize.x - length) * .5f - .08f, (length - WallSize.x) * .5f + .08f);
                Quaternion rotation = (row + column) % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
                float y = row * rise + ((row + column) % 2 == 0 ? 0f : WallSize.y);
                tiles[row * columns + column] = new CombineInstance
                {
                    mesh = source.sharedMesh,
                    transform = Matrix4x4.TRS(new Vector3(x, y, depth + inset), rotation, Vector3.one)
                };
            }
            var combined = new Mesh { name = name + "_WallRun", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(tiles);
            combined.UploadMeshData(true);
            filter.sharedMesh = combined;
            mount.gameObject.AddComponent<GeneratedDungeonMesh>().Mesh = combined;
            DungeonMasonry.WallCore(mount, length, height, depth + WallSize.z * .45f, Ceiling);
        }
    }

    internal sealed class GeneratedDungeonMesh : MonoBehaviour
    {
        public Mesh Mesh;
        private void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);
        }
    }
}
