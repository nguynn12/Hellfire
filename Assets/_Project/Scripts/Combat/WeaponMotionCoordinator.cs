// Script: WeaponMotionCoordinator.cs
// Mục đích: Đợt 1 - Combat Foundation: Kết hợp mượt mà các chuyển động của
//           Weapon Sway & Bobbing, Weapon Recoil, và Aim Down Sights (ADS)
//           trong LateUpdate() để viewmodel luôn ổn định và không bị giật lag theo camera.
// Môi trường thực thi: Client-side Cosmetic Viewmodel.

using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public class WeaponMotionCoordinator : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private WeaponAimDownSights _adsComponent;
        [SerializeField] private WeaponSwayBobbing _swayBobbingComponent;
        [SerializeField] private WeaponRecoil _recoilComponent;

        [Header("Viewmodel Transform Override")]
        [Tooltip("Transform của viewmodel (mặc định lấy chính transform này)")]
        [SerializeField] private Transform _viewmodelTransform;

        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void AutoResolveReferences()
        {
            if (_viewmodelTransform == null) _viewmodelTransform = transform;

            _initialLocalPos = _viewmodelTransform.localPosition;
            _initialLocalRot = _viewmodelTransform.localRotation;

            if (_adsComponent == null) _adsComponent = GetComponent<WeaponAimDownSights>();
            if (_swayBobbingComponent == null) _swayBobbingComponent = GetComponent<WeaponSwayBobbing>();
            if (_recoilComponent == null) _recoilComponent = GetComponent<WeaponRecoil>();
        }

        private void LateUpdate()
        {
            if (_viewmodelTransform == null) return;

            Vector3 finalPos = _initialLocalPos;
            Quaternion finalRot = _initialLocalRot;

            // 1. Aim Down Sights (ADS) base position/rotation
            if (_adsComponent != null && _adsComponent.enabled)
            {
                finalPos = _adsComponent.CurrentAimPosition;
                finalRot = _adsComponent.CurrentAimRotation;
            }

            // 2. Weapon Sway & Bobbing offsets
            if (_swayBobbingComponent != null && _swayBobbingComponent.enabled)
            {
                finalPos += _swayBobbingComponent.CombinedPositionOffset;
                finalRot = finalRot * _swayBobbingComponent.CombinedRotationOffset;
            }

            // 3. Weapon Recoil procedural kick & recovery
            if (_recoilComponent != null && _recoilComponent.enabled)
            {
                finalPos += _recoilComponent.RecoilPositionOffset;
                finalRot = finalRot * _recoilComponent.RecoilRotationOffset;
            }

            _viewmodelTransform.localPosition = finalPos;
            _viewmodelTransform.localRotation = finalRot;
        }
    }
}
