// Script: AutomaticWeapon.cs
// Mục đích: Lớp con đại diện cho súng tự động xả đạn liên tục (Automatic: Rifle, SMG).
//           - Giữ chuột trái để xả đạn liên thanh với tốc độ ra đòn (APS) dồn dập.
//           - Độ tản đạn tích lũy (Progressive Bloom & Recoil) tăng dần khi xả liên tục và hồi phục khi ngừng bắn.
//           - Hoàn toàn Zero-GC trong vòng lặp bắn súng.
// Môi trường thực thi: Client & Server (MonoBehaviour).

using UnityEngine;

namespace Hellfire.Combat
{
    public class AutomaticWeapon : WeaponBase
    {
        [Header("Automatic Specifics (Progressive Bloom)")]
        [SerializeField] private float _bloomPerShot = 0.25f;
        [SerializeField] private float _maxBloomDegrees = 3.5f;
        [SerializeField] private float _bloomRecoverySpeed = 9.0f;

        private float _currentBloom = 0f;

        private void Update()
        {
            if (_currentBloom > 0.001f)
            {
                _currentBloom = Mathf.MoveTowards(_currentBloom, 0f, _bloomRecoverySpeed * Time.deltaTime);
            }
        }

        protected override void ExecuteFire(Vector3 origin, Vector3 direction)
        {
            float baseSpread = _data != null && _data.SpreadDegrees > 0.01f ? _data.SpreadDegrees : 1.8f;
            float totalSpread = baseSpread + _currentBloom;

            if (_controller != null && _controller.IsAdsActive)
            {
                totalSpread *= (_data != null ? _data.AdsSpreadMultiplier : 0.45f);
            }

            // Tăng bloom lũy tiến khi xả đạn liên tục
            _currentBloom = Mathf.Min(_maxBloomDegrees, _currentBloom + _bloomPerShot);

            Vector3 spreadDir = ApplyConeSpread(direction, totalSpread);
            float maxRange = _data != null ? _data.Range : 120.0f;
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
            float tracerSpeed = 240f;
            float tracerLength = _data != null ? _data.TracerLength : 4.0f;
            float tracerJitter = _data != null ? _data.TracerSpeedJitter : 0.08f;

            WeaponFxPool.GetOrCreate().PlayShortTracer(muzzlePos, endPoint, tracerSpeed, tracerLength, tracerJitter);
        }
    }
}
