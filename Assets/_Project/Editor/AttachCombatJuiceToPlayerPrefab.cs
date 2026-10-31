#if UNITY_EDITOR
using Hellfire.Combat;
using UnityEditor;
using UnityEngine;

namespace Hellfire.Editor
{
    public static class AttachCombatJuiceToPlayerPrefab
    {
        public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        [MenuItem("Hellfire/Sprint 1/Attach Combat Juice to Real Player Prefab", false, 10)]
        public static void ApplyToRealPlayerPrefab()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[AttachCombatJuice] Không tải được {PlayerPrefabPath}");
                return;
            }

            try
            {
                var viewmodelTransform = prefabRoot.transform.Find("CameraPivot/PlayerCamera/WeaponViewmodel");
                if (viewmodelTransform == null)
                {
                    Debug.LogError("[AttachCombatJuice] Không tìm thấy CameraPivot/PlayerCamera/WeaponViewmodel trong Player.prefab!");
                    return;
                }

                bool modified = false;

                var ads = viewmodelTransform.GetComponent<WeaponAimDownSights>();
                if (ads == null)
                {
                    ads = viewmodelTransform.gameObject.AddComponent<WeaponAimDownSights>();
                    modified = true;
                }

                var swayBob = viewmodelTransform.GetComponent<WeaponSwayBobbing>();
                if (swayBob == null)
                {
                    swayBob = viewmodelTransform.gameObject.AddComponent<WeaponSwayBobbing>();
                    modified = true;
                }

                var recoil = viewmodelTransform.GetComponent<WeaponRecoil>();
                if (recoil == null)
                {
                    recoil = viewmodelTransform.gameObject.AddComponent<WeaponRecoil>();
                    modified = true;
                }

                var coordinator = viewmodelTransform.GetComponent<WeaponMotionCoordinator>();
                if (coordinator == null)
                {
                    coordinator = viewmodelTransform.gameObject.AddComponent<WeaponMotionCoordinator>();
                    modified = true;
                }

                // Luôn gọi AutoResolveReferences để các SerializedField trỏ chính xác
                ads.AutoResolveReferences();
                swayBob.AutoResolveReferences();
                recoil.AutoResolveReferences();
                coordinator.AutoResolveReferences();

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                    Debug.Log("<color=#00FF88>[AttachCombatJuice] Đã gắn thành công 4 component cảm giác súng (ADS, Sway/Bobbing, Recoil, MotionCoordinator) vào Player.prefab thật!</color>");
                }
                else
                {
                    Debug.Log("[AttachCombatJuice] Player.prefab đã có sẵn các component cảm giác súng, không cần lưu lại.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif
