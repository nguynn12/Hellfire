using Hellfire.WeaponDevelopment;
using UnityEngine;

namespace Hellfire.Combat
{
    [DisallowMultipleComponent]
    public sealed class WeaponViewmodelPresenter : MonoBehaviour
    {
        private WeaponController _controller;
        private HellsWhisperReloadPresenter _pistolPresenter;
        private Renderer _placeholder;
        private GameObject _model;
        private WeaponData _weapon;

        private void Awake()
        {
            _controller = GetComponentInParent<WeaponController>();
            _pistolPresenter = GetComponent<HellsWhisperReloadPresenter>();
            _placeholder = transform.Find("GunMesh")?.GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (_controller == null || !GameModeHelper.CanLocalControl(_controller)) return;
            var current = _controller.CurrentWeaponData;
            if (current == _weapon) return;
            _weapon = current;
            if (_model != null)
            {
                _model.SetActive(false);
                Destroy(_model);
                _model = null;
            }

            bool pistolPresentation = current != null && current.WeaponId == "pistol" &&
                _pistolPresenter != null && _pistolPresenter.enabled;
            if (!pistolPresentation && current != null && current.ViewmodelPrefab != null)
            {
                _model = Instantiate(current.ViewmodelPrefab, transform, false);
                _model.name = "EquippedViewmodel_" + current.WeaponId;
                foreach (var item in _model.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = gameObject.layer;
            }
            if (_placeholder != null) _placeholder.enabled = _model == null && !pistolPresentation;
        }
    }
}
