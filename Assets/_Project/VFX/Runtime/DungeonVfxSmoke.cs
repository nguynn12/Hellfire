#if HELLFIRE_PHASE1_SMOKE
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Hellfire.Dungeon;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.Vfx
{
    internal static class DungeonVfxSmoke
    {
        public static bool CheckPlacement(DungeonGenerator generator)
        {
            var ambient = generator.EnvironmentVfx;
            if (ambient == null) return false;
            var placements = ambient.Emitters.OrderBy(e => e.Room.x).ThenBy(e => e.Room.y).ToArray();
            if (placements.Length == 0 || placements.Select(e => e.Room).Distinct().Count() != placements.Length) return false;

            foreach (var emitter in placements)
            {
                var room = generator.ActiveGrid.Rooms[emitter.Room];
                var geometry = generator.GeometryRoot.Find($"Room_{room.GridPosition.x}_{room.GridPosition.y}_{room.RoomType}");
                string prefix = emitter.Instance.name == "LavaEmber" ? "Lava" : "Ice";
                if (room.RoomType != DungeonRoomType.Combat || !room.Contains(emitter.Position, 2f) ||
                    !geometry.GetComponentsInChildren<MeshRenderer>().Any(r => r.name == prefix + "_WallTiles") ||
                    emitter.Instance.GetComponentInChildren<Collider>(true) != null ||
                    emitter.Instance.GetComponentInChildren<Unity.Netcode.NetworkObject>(true) != null ||
                    emitter.Systems.Length != 3 || emitter.Systems.Any(system => system.useAutoRandomSeed ||
                       system.GetComponent<ParticleSystemRenderer>().sharedMaterial is not { shader: { isSupported: true } }))
                    return false;
            }

            Debug.Log("[PHASE1] VFX_LAYOUT " + string.Join(";", placements.Select(e => string.Format(CultureInfo.InvariantCulture,
                "{0},{1}:{2}:{3:F3},{4:F3}:{5}", e.Room.x, e.Room.y, e.Instance.name, e.Position.x, e.Position.z, e.Systems[0].randomSeed))));
            return placements.Any(e => e.Instance.name == "FrozenMist") && placements.Any(e => e.Instance.name == "LavaEmber");
        }

        public static IEnumerator CaptureGameplay(DungeonGenerator generator, Camera camera, Func<string, IEnumerator> capture)
        {
            var ambient = generator.EnvironmentVfx;
            var view = camera.transform;
            Vector3 originalPosition = view.localPosition;
            Quaternion originalRotation = view.localRotation;
            try
            {
                foreach (string id in new[] { "LavaEmber", "FrozenMist" })
                {
                    var emitter = ambient.Emitters.First(e => e.Instance.name == id);
                    var room = generator.ActiveGrid.Rooms[emitter.Room];
                    Vector3 inward = room.WorldCenter - emitter.Position;
                    Vector3 across = new Vector3(Mathf.Sign(inward.x), 0f, Mathf.Sign(inward.z)).normalized;
                    Vector3 position = emitter.Position + across * 5f + Vector3.up * 1.65f;
                    view.SetPositionAndRotation(position,
                        Quaternion.LookRotation(emitter.Position + Vector3.up * .92f - position));
                    ambient.EffectsEnabled = false;
                    ambient.Refresh(position);
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return capture("VFX-" + id + "-Off");
                    ambient.EffectsEnabled = true;
                    ambient.Refresh(position);
                    yield return new WaitForSecondsRealtime(.65f);
                    var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    Debug.Log($"[PHASE1] VFX_CAMERA {id} postProcessing={cameraData != null && cameraData.renderPostProcessing} active={Active(ambient)}");
                    yield return capture("VFX-" + id + "-On");
                    for (int frame = 0; frame < 12; frame++)
                    {
                        yield return capture($"VFX-{id}-Motion-{frame:00}");
                        yield return new WaitForSecondsRealtime(.20f);
                    }
                    yield return Measure(ambient, position, false, id);
                    yield return Measure(ambient, position, true, id);
                }

                // Benchmark from the navigable midpoint of two adjacent combat rooms.
                var pair = (from a in ambient.Emitters
                            from b in ambient.Emitters
                            where generator.ActiveGrid.Rooms[a.Room].HasConnection(b.Room)
                            orderby (a.Position - b.Position).sqrMagnitude
                            select (a, b)).First();
                Vector3 midpoint = (generator.ActiveGrid.Rooms[pair.a.Room].WorldCenter +
                    generator.ActiveGrid.Rooms[pair.b.Room].WorldCenter) * .5f;
                if (!NavMesh.SamplePosition(midpoint, out var hit, 3f, NavMesh.AllAreas))
                    throw new InvalidOperationException("No navigable VFX benchmark position between combat rooms.");
                Vector3 between = hit.position + Vector3.up * 1.65f;
                Vector3 forward = generator.ActiveGrid.Rooms[pair.a.Room].WorldCenter - midpoint;
                forward.y = -.3f;
                view.SetPositionAndRotation(between, Quaternion.LookRotation(forward));
                Debug.Log($"[PHASE1] VFX_BENCHMARK_POSITION {between} between {pair.a.Room} and {pair.b.Room}");
                yield return Measure(ambient, between, false, "Corridor");
                yield return Measure(ambient, between, true, "Corridor");
            }
            finally
            {
                view.SetLocalPositionAndRotation(originalPosition, originalRotation);
                ambient.EffectsEnabled = true;
                ambient.Refresh(view.position);
            }
        }

        private static int Active(DungeonEnvironmentVfx ambient)
        {
            int active = 0;
            foreach (var emitter in ambient.Emitters)
                if (emitter.Instance.activeSelf) active++;
            return active;
        }

        private static IEnumerator Measure(DungeonEnvironmentVfx ambient, Vector3 observer, bool enabled, string viewpoint)
        {
            const int frames = 160;
            ambient.EffectsEnabled = enabled;
            ambient.Refresh(observer);
            yield return new WaitForSecondsRealtime(.8f);
            var wait = new WaitForEndOfFrame();
            var cpu = new double[frames];
            var gpu = new double[frames];
            var gc = new double[frames];
            var draws = new double[frames];
            var timing = new FrameTiming[1];
            using var gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            int maxActive = 0, particles = 0;
            for (int i = 0; i < frames; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return wait;
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    cpu[i] = timing[0].cpuMainThreadFrameTime;
                    gpu[i] = timing[0].gpuFrameTime;
                }
                gc[i] = gcRecorder.Valid ? gcRecorder.LastValue : -1;
                draws[i] = drawRecorder.Valid ? drawRecorder.LastValue : -1;
                maxActive = Math.Max(maxActive, Active(ambient));
                foreach (var emitter in ambient.Emitters)
                    if (emitter.Instance.activeSelf)
                        foreach (var system in emitter.Systems) particles += system.particleCount;
            }

            Debug.Log($"[PHASE1] VFX_PERF {Screen.width}x{Screen.height} {SystemInfo.graphicsDeviceName}/{SystemInfo.graphicsDeviceType} " +
                $"view={viewpoint} enabled={enabled} frames={frames} active_max={maxActive} cpu_main_ms_p50={Median(cpu, cpu.Any(x => x > 0))} " +
                $"gpu_ms_p50={Median(gpu, gpu.Any(x => x > 0))} gc_bytes_p50={Median(gc, gcRecorder.Valid)} " +
                $"draws_p50={Median(draws, drawRecorder.Valid)} particles_mean={((double)particles / frames).ToString("F1", CultureInfo.InvariantCulture)}");
        }

        private static string Median(double[] values, bool valid)
        {
            if (!valid) return "unavailable";
            Array.Sort(values);
            return values[values.Length / 2].ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
#endif
