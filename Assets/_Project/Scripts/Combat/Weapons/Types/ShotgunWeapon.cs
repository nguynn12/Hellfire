// Script: ShotgunWeapon.cs
// Mục đích: Lớp con đại diện cho súng săn bắn đạn chùm (Shotgun).
//           - Bắn 8 mảnh đạn (pellets) tỏa theo góc nón ±5.0°.
//           - Lực giật lùi người chơi mạnh (Pushback).
//           - Cấp phát Zero-GC qua ZeroGcHitscanBuffer tĩnh.
// Môi trường thực thi: Client & Server (MonoBehaviour).

using UnityEngine;

namespace Hellfire.Combat
{
    public class ShotgunWeapon : WeaponBase
    {
        [Header("Shotgun Specifics")]
        [SerializeField] private int _pelletsCount = 8;
        [SerializeField] private float _spreadDegrees = 5.0f;

        protected override void ExecuteFire(Vector3 origin, Vector3 direction)
        {
            int pellets = _data != null && _data.PelletCount > 1 ? _data.PelletCount : _pelletsCount;
            float spreadDeg = _data != null && _data.SpreadDegrees > 0.01f ? _data.SpreadDegrees : _spreadDegrees;

            if (_controller != null && _controller.IsAdsActive)
            {
                spreadDeg *= (_data != null ? _data.AdsSpreadMultiplier : 0.6f);
            }

            float maxRange = _data != null ? _data.Range : 45.0f;
            LayerMask mask = _controller != null ? _controller.RaycastMask : ~0;
            Vector3 muzzlePos = _muzzlePoint != null ? _muzzlePoint.position : origin;

            float tracerLength = _data != null ? _data.TracerLength : 2.8f;
            float tracerJitter = _data != null ? _data.TracerSpeedJitter : 0.15f;

            var fxPool = WeaponFxPool.GetOrCreate();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 spreadDir = ApplyConeSpread(direction, spreadDeg);
                int hitCount = Physics.RaycastNonAlloc(origin, spreadDir, ZeroGcHitscanBuffer.HitBuffer, maxRange, mask, QueryTriggerInteraction.Ignore);
                Vector3 endPoint = origin + spreadDir * maxRange;

                if (hitCount > 0)
                {
                    RaycastHit hit = ZeroGcHitscanBuffer.HitBuffer[0];
                    endPoint = hit.point;

                    HandleHitImpact(hit);

                    if (_controller != null)
                    {
                        _controller.OnWeaponHitTarget(hit, spreadDir, _data);
                    }
                }

                fxPool.PlayShortTracer(muzzlePos, endPoint, 150f, tracerLength, tracerJitter);
            }
        }
    }
}
