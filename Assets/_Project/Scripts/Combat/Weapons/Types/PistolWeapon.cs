// Script: PistolWeapon.cs
// Mục đích: Lớp con đại diện cho vũ khí bắn phát một (Semi-Automatic: Pistol).
//           - Đòi hỏi nhả cò rồi bấm lại mới bắn tiếp (Single shot).
//           - Độ chính xác cao, độ tản thấp, hồi tâm nhanh.
//           - Zero-GC Raycast qua ZeroGcHitscanBuffer.
// Môi trường thực thi: Client & Server (MonoBehaviour).

using UnityEngine;

namespace Hellfire.Combat
{
    public class PistolWeapon : WeaponBase
    {
        [Header("Pistol Specifics")]
        [SerializeField] private float _singleShotSpreadDegrees = 1.2f;

        protected override void ExecuteFire(Vector3 origin, Vector3 direction)
        {
            float spreadDeg = _data != null && _data.SpreadDegrees > 0.01f ? _data.SpreadDegrees : _singleShotSpreadDegrees;
            if (_controller != null && _controller.IsAdsActive)
            {
                spreadDeg *= (_data != null ? _data.AdsSpreadMultiplier : 0.4f);
            }

            Vector3 spreadDir = ApplyConeSpread(direction, spreadDeg);
            float maxRange = _data != null ? _data.Range : 100.0f;
            LayerMask mask = _controller != null ? _controller.RaycastMask : ~0;

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

            Vector3 muzzlePos = _muzzlePoint != null ? _muzzlePoint.position : origin;
            float tracerSpeed = 220f;
            float tracerLength = _data != null ? _data.TracerLength : 3.5f;
            float tracerJitter = _data != null ? _data.TracerSpeedJitter : 0.10f;

            WeaponFxPool.GetOrCreate().PlayShortTracer(muzzlePos, endPoint, tracerSpeed, tracerLength, tracerJitter);
        }
    }
}
