using UnityEngine;

namespace Hellfire.Combat
{
    // Cosmetic adapter only: gameplay ammunition, damage and reload timing stay in WeaponController.
    public sealed class FPSArmsSaigaAnimator : MonoBehaviour
    {
        public WeaponController Weapon;
        public CharacterController Character;
        public Animator Animator;
        public float FireClipLength;
        public float FastReloadLength;
        public float FullReloadLength;
        public string CurrentState { get; private set; }
        public int FireEvents { get; private set; }
        public int ReloadEvents { get; private set; }
        private bool reloading;
        private float fireUntil;
        private void OnEnable()
        {
            if (Weapon == null) Weapon = GetComponentInParent<WeaponController>();
            if (Weapon != null) { Weapon.OnWeaponFired += Fire; Weapon.OnReloadStateChanged += Reload; }
        }
        private void OnDisable()
        {
            if (Weapon != null) { Weapon.OnWeaponFired -= Fire; Weapon.OnReloadStateChanged -= Reload; }
            reloading = false;
        }
        private void Update()
        {
            if (Animator == null || Weapon == null || !Weapon.IsLocallyControlled || reloading || Time.time < fireUntil) return;
            float speed = Character == null ? 0 : new Vector2(Character.velocity.x, Character.velocity.z).magnitude;
            string state = speed > 5.5f ? "Saiga_Run" : speed > 1.8f ? "Saiga_Walk" : speed > .1f ? "Arms_FPS_Anim_Walk" : "Saiga_Idle";
            if (CurrentState != state) { CurrentState = state; Animator.CrossFadeInFixedTime(state, .1f); }
        }
        private void Fire()
        {
            if (Animator == null || Weapon.CurrentWeaponData == null) return;
            FireEvents++;
            float duration = Mathf.Max(.06f, Weapon.CurrentWeaponData.FireRate);
            Animator.SetFloat("ActionSpeed", FireClipLength / duration);
            CurrentState = "Saiga_Fire";
            Animator.Play(CurrentState, 0, 0);
            fireUntil = Time.time + duration;
        }
        private void Reload(bool active)
        {
            reloading = active;
            if (!active) { CurrentState = null; fireUntil = 0; return; }
            if (Animator == null || Weapon.CurrentWeaponData == null) return;
            ReloadEvents++;
            bool empty = Weapon.CurrentAmmo == 0;
            CurrentState = empty ? "Saiga_Reload_Full" : "Saiga_Reload_Fast";
            float length = empty ? FullReloadLength : FastReloadLength;
            Animator.SetFloat("ActionSpeed", length / Mathf.Max(.1f, Weapon.CurrentWeaponData.ReloadTime));
            Animator.Play(CurrentState, 0, 0);
        }
    }
}
