using System;
using System.Linq;
using UnityEngine;

namespace Hellfire.WeaponDevelopment
{
    // Normalized times; offsets in the presentation pivot's parent-local space.
    // Euler angles are unwrapped degrees. No tracked camera or mocap data is implied.
    [Serializable] public sealed class ReloadKeyframeDataTask08
    {
        public enum Interpolation { Smooth, Linear, Hold }
        [Serializable] public sealed class PoseKey
        {
            public float time;
            public Vector3 positionOffset, rotationDegrees;
            public Interpolation interpolation;
        }
        public int schemaVersion = 1;
        public string style = "Prototype", positionUnits = "ParentLocal";
        public string referenceVideo = "", referenceNotes = "", approvalStatus = "PROVISIONAL_NO_REFERENCE";
        public float referenceStartSeconds, referenceEndSeconds;
        public float duration = 2.2f, motionIntensity = 1, prepareEnd = .25f, rechargeEnd = .65f;
        public bool vfxEnabled = true;
        public int particleCount = 40;
        public float particleRadius = .65f, vfxFadeInFraction = .25f, vfxReturnFadeFraction = .65714286f;
        public PoseKey[] keys;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        public string Error()
        {
            if (schemaVersion != 1) return "Unsupported schema version.";
            if (positionUnits != "ParentLocal" && positionUnits != "ModelLengthFraction") return "Unknown position units.";
            if (!Finite(duration) || duration < .3f || duration > 4 || !Finite(motionIntensity) || motionIntensity < 0 || motionIntensity > 2)
                return "Duration must be 0.3–4 seconds; intensity must be 0–2.";
            if (!Finite(prepareEnd) || !Finite(rechargeEnd) || prepareEnd <= 0 || prepareEnd >= rechargeEnd || rechargeEnd >= 1)
                return "Phase markers must satisfy 0 < Prepare end < Recharge end < 1.";
            if (keys == null || keys.Length < 2 || keys.Length > 64 || keys.Any(k => k == null)) return "Use 2–64 pose keys.";
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (!Finite(k.time) || k.time < 0 || k.time > 1 || !Finite(k.positionOffset) || !Finite(k.rotationDegrees) ||
                    !Enum.IsDefined(typeof(Interpolation), k.interpolation)) return "Key values must be finite and within normalized time 0–1.";
                if (i > 0 && k.time - keys[i-1].time < .0001f) return "Key times must increase, without duplicates.";
            }
            if (keys[0].time != 0 || keys[keys.Length-1].time != 1 ||
                keys[0].positionOffset != Vector3.zero || keys[0].rotationDegrees != Vector3.zero ||
                keys[keys.Length-1].positionOffset != Vector3.zero || keys[keys.Length-1].rotationDegrees != Vector3.zero)
                return "First/last keys must be rest poses at time 0 and 1.";
            if (particleCount < 1 || particleCount > 64 || !Finite(particleRadius) || particleRadius <= 0 ||
                !Finite(vfxFadeInFraction) || !Finite(vfxReturnFadeFraction) || vfxFadeInFraction <= 0 || vfxFadeInFraction > 1 || vfxReturnFadeFraction <= 0 || vfxReturnFadeFraction > 1)
                return "Invalid particle or VFX fade values.";
            if (!Finite(referenceStartSeconds) || !Finite(referenceEndSeconds) || referenceStartSeconds < 0 || referenceEndSeconds < referenceStartSeconds)
                return "Reference interval must be finite, non-negative and ordered.";
            return null;
        }
        public void Evaluate(float t, out Vector3 position, out Vector3 rotation)
        {
            t = Mathf.Clamp01(t);
            if (t >= 1) { position = keys[keys.Length-1].positionOffset; rotation = keys[keys.Length-1].rotationDegrees; return; }
            int i = 0;
            while (i < keys.Length-2 && t >= keys[i+1].time) i++;
            var a = keys[i]; var b = keys[i+1];
            float u = Mathf.InverseLerp(a.time, b.time, t);
            if (a.interpolation == Interpolation.Smooth) u = u*u*(3-2*u);
            else if (a.interpolation == Interpolation.Hold) u = 0;
            position = Vector3.LerpUnclamped(a.positionOffset, b.positionOffset, u);
            rotation = Vector3.LerpUnclamped(a.rotationDegrees, b.rotationDegrees, u);
        }
        public void RetimePhases(float prepare, float recharge)
        {
            if (!(prepare > 0 && prepare < recharge && recharge < 1)) throw new ArgumentException("Invalid phase markers");
            foreach (var k in keys)
            {
                float t = k.time;
                k.time = t <= prepareEnd ? t / prepareEnd * prepare : t <= rechargeEnd ?
                    Mathf.Lerp(prepare, recharge, (t-prepareEnd)/(rechargeEnd-prepareEnd)) :
                    Mathf.Lerp(recharge, 1, (t-rechargeEnd)/(1-rechargeEnd));
            }
            keys[0].time = 0; keys[keys.Length-1].time = 1;
            prepareEnd = prepare; rechargeEnd = recharge;
        }
        public string Phase(float t)
        {
            if(style=="DemonicHeartbeatBloodRitual")
            { float s=t*2.2f; return s<.4f ? "Awakening" : s<.85f ? "First Heartbeat" : s<1.4f ? "Second Heartbeat" : s<1.75f ? "Blood Ritual" : "Return to Ready"; }
            return t <= prepareEnd ? "Prepare" : t <= rechargeEnd ? "Recharge" : "Return";
        }
    }
}
