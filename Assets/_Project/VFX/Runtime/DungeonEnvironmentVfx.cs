using System.Collections.Generic;
using Hellfire.Dungeon;
using Hellfire.Player;
using UnityEngine;

namespace Hellfire.Vfx
{
    // Each peer derives the same placements from dungeon data. Only the local camera drives playback.
    internal sealed class DungeonEnvironmentVfx : MonoBehaviour
    {
        public const int ActiveLimit = 2;
        private const float Range = 29f;
        private const float RefreshInterval = .16f;
        private static readonly Vector2[] Corners = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
        private readonly List<Emitter> _emitters = new();
        private readonly Emitter[] _nearest = new Emitter[ActiveLimit];
        private readonly float[] _distances = new float[ActiveLimit];
        private float _nextRefresh;

        internal sealed class Emitter
        {
            public Vector2Int Room;
            public GameObject Instance;
            public Vector3 Position;
            public ParticleSystem[] Systems;
        }

        public IReadOnlyList<Emitter> Emitters => _emitters;
        public bool EffectsEnabled { get; set; } = true;

        public void AddRoom(DungeonRoomNode room, GameObject prefab, int dungeonSeed)
        {
            if (prefab == null || room.RoomType != DungeonRoomType.Combat) return;

            int seed = DungeonRoomNode.Seed(dungeonSeed, room.GridPosition, 0x64A7);
            var random = new System.Random(seed);
            int start = random.Next(Corners.Length);
            float scale = Mathf.Clamp(Mathf.Min(room.Size.x, room.Size.y) / 17f, .65f, .93f);
            // Keep the authored wind direction toward the corner along its wall.
            for (int index = 0; index < Corners.Length; index++)
            {
                var corner = Corners[(start + index) % Corners.Length];
                var position = room.WorldCenter + new Vector3(corner.x * room.Size.x * .30f, 0f, corner.y * room.Size.y * .26f);
                var yaw = corner.x > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                // Covers the box emitters and the visible part of the wisps without reaching the central lane.
                var footprint = new Bounds(position, new Vector3(6.1f * scale, 2f, 3.7f * scale));
                if (!AwayFromDoors(room, footprint)) continue;
                if (room.ObstacleBounds.Exists(obstacle => obstacle.Contains(new Vector3(position.x, obstacle.center.y, position.z))))
                    continue;

                var instance = Instantiate(prefab, position, yaw, transform);
                instance.name = prefab.name;
                instance.transform.localScale = Vector3.one * scale;
                var systems = instance.GetComponentsInChildren<ParticleSystem>();
                for (int layer = 0; layer < systems.Length; layer++)
                {
                    var system = systems[layer];
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.randomSeed = unchecked((uint)(seed + layer * 47)) | 1u;
                    var main = system.main;
                    main.playOnAwake = false;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    var emission = system.emission;
                    emission.rateOverTimeMultiplier *= .60f;
                }
                instance.SetActive(false);
                _emitters.Add(new Emitter { Room = room.GridPosition, Instance = instance, Position = position, Systems = systems });
                return;
            }
        }

        private static bool AwayFromDoors(DungeonRoomNode room, Bounds footprint)
        {
            foreach (var neighbor in room.ConnectedNeighbors)
            {
                Vector2Int dir = neighbor - room.GridPosition;
                var offset = new Vector3(dir.x * (room.Size.x * .5f - 2.75f), 0f, dir.y * (room.Size.y * .5f - 2.75f));
                var doorway = new Bounds(room.WorldCenter + offset,
                    dir.x == 0 ? new Vector3(5f, 2f, 5.5f) : new Vector3(5.5f, 2f, 5f));
                if (footprint.Intersects(doorway)) return false;
            }
            return true;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            var camera = PlayerMovement.LocalInstance?.PlayerCamera;
            Refresh(camera != null && camera.isActiveAndEnabled ? camera.transform.position : (Vector3?)null);
        }

        internal void Refresh(Vector3? observer)
        {
            for (int i = 0; i < ActiveLimit; i++)
            {
                _nearest[i] = null;
                _distances[i] = Range * Range;
            }

            if (EffectsEnabled && observer.HasValue)
            {
                foreach (var candidate in _emitters)
                {
                    float distance = (observer.Value - candidate.Position).sqrMagnitude;
                    if (candidate.Instance.activeSelf) distance *= .86f; // Hysteresis near the boundary.
                    for (int slot = 0; slot < ActiveLimit; slot++)
                    {
                        if (distance >= _distances[slot]) continue;
                        for (int shift = ActiveLimit - 1; shift > slot; shift--)
                        {
                            _nearest[shift] = _nearest[shift - 1];
                            _distances[shift] = _distances[shift - 1];
                        }
                        _nearest[slot] = candidate;
                        _distances[slot] = distance;
                        break;
                    }
                }
            }

            foreach (var emitter in _emitters)
            {
                bool active = System.Array.IndexOf(_nearest, emitter) >= 0;
                if (active == emitter.Instance.activeSelf) continue;
                if (active)
                {
                    emitter.Instance.SetActive(true);
                    foreach (var system in emitter.Systems)
                    {
                        system.Simulate(2.75f, false, true);
                        system.Play(false);
                    }
                }
                else
                {
                    foreach (var system in emitter.Systems)
                        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    emitter.Instance.SetActive(false);
                }
            }
        }
    }
}
