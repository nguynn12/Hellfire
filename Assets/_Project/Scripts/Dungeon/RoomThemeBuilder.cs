// Script: RoomThemeBuilder.cs
// Purpose: Deterministic themed static obstacles before NavMesh bake (section 20.3).
// Execution: Server and clients, using seed + grid coordinate; no network geometry.
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Hellfire.Dungeon
{
    public static class RoomThemeBuilder
    {
        private struct Placement
        {
            public Vector3 Position, Size, Rotation;
            public Bounds Bounds;
        }
        public static void Build(DungeonRoomNode room, Transform parent, int seed, Material fallback)
        {
            if (room.RoomType != DungeonRoomType.Combat || room.Theme == null) return;
            var rng = new System.Random(DungeonRoomNode.Seed(seed, room.GridPosition, 0x203B));
            int requested = Mathf.Clamp(Mathf.RoundToInt(room.Size.x * room.Size.y / 80f), 2, 5);
            int count = 0, clusters = 0, lights = 0;
            float area = 0f;
            var clusterBounds = new List<Bounds>();
            room.ObstacleBounds.Clear();
            for (int cluster = 0; cluster < requested && count < 6; cluster++)
            {
                var def = Choose(room.Theme.Obstacles, rng);
                if (def == null) break;
                int amount = Mathf.Min(6 - count, rng.Next(Mathf.Max(1, def.ClusterCount.x), Mathf.Max(def.ClusterCount.x, def.ClusterCount.y) + 1));
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    Vector3 center = room.WorldCenter + new Vector3(Range(rng, -room.Size.x * 0.5f + 2.5f, room.Size.x * 0.5f - 2.5f), 0,
                        Range(rng, -room.Size.y * 0.5f + 2.5f, room.Size.y * 0.5f - 2.5f));
                    var placements = new List<Placement>();
                    float proposedArea = 0f;
                    Bounds group = new Bounds(center, Vector3.zero);
                    bool valid = true;
                    for (int i = 0; i < amount; i++)
                    {
                        Vector3 size = new Vector3(Range(rng, def.Size.x, def.MaxSize.x), Range(rng, def.Size.y, def.MaxSize.y), Range(rng, def.Size.z, def.MaxSize.z));
                        Vector3 rot = new Vector3(Range(rng, -def.RotationRange.x, def.RotationRange.x), Range(rng, -def.RotationRange.y, def.RotationRange.y), Range(rng, -def.RotationRange.z, def.RotationRange.z));
                        float angle = i * Mathf.PI * 2f / amount;
                        float radius = amount > 1 ? def.ClusterRadius : 0;
                        bool stacked = def.MaxStack > 1 && i == 1 && rng.Next(2) == 0;
                        Vector3 pos = stacked ? placements[0].Position + Vector3.up * placements[0].Size.y : center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                        Bounds bounds = RotatedBounds(pos, size, Quaternion.Euler(rot));
                        if (!Allowed(room, bounds)) { valid = false; break; }
                        if (!stacked)
                            foreach (var prior in placements)
                                if (OverlapXZ(bounds, prior.Bounds)) { valid = false; break; }
                        if (!valid) break;
                        if (i == 0) group = bounds; else group.Encapsulate(bounds);
                        placements.Add(new Placement { Position = pos, Size = size, Rotation = rot, Bounds = bounds });
                        proposedArea += bounds.size.x * bounds.size.z;
                    }
                    if (!valid || area + proposedArea > room.Size.x * room.Size.y * 0.08f) continue;
                    foreach (var previous in clusterBounds)
                        if (DistanceXZ(group, previous) < 3f) { valid = false; break; }
                    if (!valid) continue;
                    var clusterRoot = new GameObject($"ObstacleCluster_{clusters}_{def.Id}");
                    clusterRoot.transform.SetParent(parent, false);
                    foreach (var placement in placements)
                    {
                        Create(def, placement, clusterRoot.transform, fallback, ref lights);
                        room.ObstacleBounds.Add(placement.Bounds);
                    }
                    clusterBounds.Add(group);
                    count += placements.Count; area += proposedArea; clusters++;
                    break;
                }
            }
            Debug.Log($"[RoomTheme] {room.GridPosition} theme={room.Theme.ThemeName}, clusters={clusters}/{requested}, objects={count}/6, area={area:F2}/{room.Size.x * room.Size.y * .08f:F2}m2, lights={lights}/2");
        }
        private static float Range(System.Random rng, float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());
        private static ObstacleDef Choose(ObstacleDef[] defs, System.Random rng)
        {
            int total = 0; foreach (var def in defs) if (def != null) total += Mathf.Max(0, def.Weight);
            if (total == 0) return null;
            int roll = rng.Next(total);
            foreach (var def in defs) if (def != null) { roll -= Mathf.Max(0, def.Weight); if (roll < 0) return def; }
            return null;
        }
        public static float DistanceXZ(Bounds a, Bounds b)
        {
            float x = Mathf.Max(0, Mathf.Abs(a.center.x - b.center.x) - a.extents.x - b.extents.x);
            float z = Mathf.Max(0, Mathf.Abs(a.center.z - b.center.z) - a.extents.z - b.extents.z);
            return Mathf.Sqrt(x * x + z * z);
        }
        private static bool OverlapXZ(Bounds a, Bounds b) => DistanceXZ(a, b) < .01f;
        public static bool Allowed(DungeonRoomNode room, Bounds b)
        {
            if (!room.Contains(b.min, 2.5f) || !room.Contains(b.max, 2.5f)) return false;
            if (DistanceXZ(b, new Bounds(room.WorldCenter, Vector3.zero)) < 3f) return false;
            foreach (var dir in DungeonGrid.CardinalDirections)
            {
                if (!room.HasConnection(room.GridPosition + dir)) continue;
                Vector3 center = room.WorldCenter + new Vector3(dir.x * (room.Size.x * .5f - 3f), 0, dir.y * (room.Size.y * .5f - 3f));
                var entry = new Bounds(center, dir.x != 0 ? new Vector3(6, 10, 5) : new Vector3(5, 10, 6));
                if (OverlapXZ(b, entry)) return false;
            }
            return true;
        }
        private static Bounds RotatedBounds(Vector3 bottom, Vector3 size, Quaternion rotation)
        {
            var b = new Bounds(bottom + rotation * (Vector3.up * size.y * .5f), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        b.Encapsulate(bottom + rotation * (new Vector3(x * size.x, y * size.y, z * size.z) * .5f + Vector3.up * size.y * .5f));
            return b;
        }
        private static void Create(ObstacleDef def, Placement p, Transform parent, Material fallback, ref int lights)
        {
            var obj = new GameObject(def.Id);
            obj.transform.SetParent(parent, false);
            obj.transform.position = p.Position;
            obj.transform.rotation = Quaternion.Euler(p.Rotation);
            bool usePrefab = def.Prefab != null && ValidPrefab(def.Prefab);
            if (usePrefab)
            {
                var visual = UnityEngine.Object.Instantiate(def.Prefab, obj.transform);
                // A prefab's static collider geometry must fit the declared local size; reject oversized replacements.
                var bounds = new Bounds(p.Position, Vector3.zero);
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
                if (!p.Bounds.Contains(bounds.min) || !p.Bounds.Contains(bounds.max))
                { UnityEngine.Object.DestroyImmediate(visual); usePrefab = false; Debug.LogWarning($"[RoomTheme] Prefab {def.Id} exceeds declared size; using primitives."); }
            }
            if (!usePrefab)
            {
                if (def.Parts.Length == 0)
                    Primitive(obj.transform, def.Shape, p.Size, Vector3.up * p.Size.y * .5f, Vector3.zero, def, fallback, false);
                else
                    foreach (var part in def.Parts)
                        Primitive(obj.transform, part.Shape, part.Size, part.Position, part.Rotation, def, fallback, part.Emissive);
                var box = obj.AddComponent<BoxCollider>();
                box.size = p.Size; box.center = Vector3.up * p.Size.y * .5f;
            }
            foreach (var light in obj.GetComponentsInChildren<Light>())
            { light.enabled = lights < 2; if (light.enabled) lights++; }
            if (def.LightRange > 0 && lights < 2)
            {
                var lightObj = new GameObject("ThemeLight"); lightObj.transform.SetParent(obj.transform, false);
                lightObj.transform.localPosition = Vector3.up * p.Size.y;
                var light = lightObj.AddComponent<Light>(); light.type = LightType.Point;
                light.color = def.LightColor; light.range = def.LightRange; light.intensity = def.LightIntensity;
                lightObj.AddComponent<CorridorLightFlicker>(); lights++;
            }
        }
        private static bool ValidPrefab(GameObject prefab)
        {
            if (prefab.GetComponentInChildren<NetworkObject>(true) != null || prefab.GetComponentInChildren<Rigidbody>(true) != null) return false;
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) return false;
            foreach (var collider in colliders) if (collider.isTrigger || !collider.enabled) return false;
            return true;
        }
        private static void Primitive(Transform parent, ObstacleShape shape, Vector3 size, Vector3 position, Vector3 rotation,
            ObstacleDef def, Material fallback, bool emissive)
        {
            var type = shape == ObstacleShape.Box ? PrimitiveType.Cube : shape == ObstacleShape.Cylinder ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
            var obj = GameObject.CreatePrimitive(type);
            obj.name = emissive ? "EmissiveCore" : shape.ToString(); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(rotation);
            obj.transform.localScale = new Vector3(size.x, shape == ObstacleShape.Box ? size.y : size.y * .5f, size.z);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>();
            if (def.Material != null || fallback != null) renderer.sharedMaterial = def.Material != null ? def.Material : fallback;
            Tint(renderer, def.Color);
            if (emissive)
            {
                var mat = new Material(renderer.sharedMaterial); mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", def.LightColor * 3f); renderer.sharedMaterial = mat;
            }
        }
        public static void Tint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }
    }
}
