// Script: ProceduralWeaponModelBuilder.cs
// Mục đích: Xây dựng các mẫu hình 3D súng cách điệu (Stylized Procedural Gun Models) có đầy đủ Socket Nòng súng (Muzzle).
//           Được kích hoạt tự động làm fallback hoàn hảo khi WeaponData chưa được gán ViewmodelPrefab/WorldModelPrefab từ file FBX.
// Môi trường thực thi: Client.

using UnityEngine;

namespace Hellfire.Combat
{
    public static class ProceduralWeaponModelBuilder
    {
        private static Material _matMetalDark;
        private static Material _matMetalLight;
        private static Material _matWood;
        private static Material _matGrip;

        private static void InitMaterials()
        {
            if (_matMetalDark != null) return;

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");

            _matMetalDark = CreateMat(s, new Color(0.18f, 0.18f, 0.20f));
            _matMetalLight = CreateMat(s, new Color(0.45f, 0.48f, 0.52f));
            _matWood = CreateMat(s, new Color(0.40f, 0.22f, 0.10f));
            _matGrip = CreateMat(s, new Color(0.10f, 0.10f, 0.10f));
        }

        private static Material CreateMat(Shader s, Color color)
        {
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        public static GameObject BuildProceduralGun(WeaponData data, bool isWorldModel = false)
        {
            InitMaterials();

            string weaponId = data != null ? data.WeaponId.ToLower() : "pistol";
            var root = new GameObject($"[Model]_{weaponId}_{(isWorldModel ? "World" : "VM")}");

            float scaleMult = isWorldModel ? 0.85f : 1.0f;

            switch (weaponId)
            {
                case "shotgun":
                    BuildShotgun(root.transform, scaleMult);
                    break;
                case "rifle":
                    BuildRifle(root.transform, scaleMult);
                    break;
                case "smg":
                    BuildSmg(root.transform, scaleMult);
                    break;
                default:
                    BuildPistol(root.transform, scaleMult);
                    break;
            }

            return root;
        }

        private static void BuildPistol(Transform parent, float scale)
        {
            // 1. Thân trượt (Slide)
            var slide = CreatePart(PrimitiveType.Cube, "Slide", parent,
                new Vector3(0f, 0.05f, 0.08f) * scale,
                new Vector3(0.045f, 0.05f, 0.22f) * scale,
                _matMetalDark);

            // 2. Nòng súng (Barrel)
            CreatePart(PrimitiveType.Cylinder, "Barrel", parent,
                new Vector3(0f, 0.05f, 0.15f) * scale,
                new Vector3(0.03f, 0.08f, 0.03f) * scale,
                _matMetalLight, Quaternion.Euler(90f, 0f, 0f));

            // 3. Tay cầm (Grip)
            CreatePart(PrimitiveType.Cube, "Grip", parent,
                new Vector3(0f, -0.06f, -0.02f) * scale,
                new Vector3(0.04f, 0.14f, 0.06f) * scale,
                _matGrip, Quaternion.Euler(15f, 0f, 0f));

            // 4. Vành cò (Trigger Guard)
            CreatePart(PrimitiveType.Cube, "TriggerGuard", parent,
                new Vector3(0f, -0.015f, 0.04f) * scale,
                new Vector3(0.015f, 0.04f, 0.05f) * scale,
                _matMetalDark);

            // 5. Socket Muzzle
            CreateMuzzleSocket(parent, new Vector3(0f, 0.05f, 0.23f) * scale);
        }

        private static void BuildShotgun(Transform parent, float scale)
        {
            // 1. Thân súng chính (Receiver)
            CreatePart(PrimitiveType.Cube, "Receiver", parent,
                new Vector3(0f, 0.02f, -0.05f) * scale,
                new Vector3(0.06f, 0.08f, 0.25f) * scale,
                _matMetalDark);

            // 2. Hai nòng súng song song (Twin Barrels)
            CreatePart(PrimitiveType.Cylinder, "BarrelLeft", parent,
                new Vector3(-0.018f, 0.035f, 0.25f) * scale,
                new Vector3(0.035f, 0.35f, 0.035f) * scale,
                _matMetalLight, Quaternion.Euler(90f, 0f, 0f));

            CreatePart(PrimitiveType.Cylinder, "BarrelRight", parent,
                new Vector3(0.018f, 0.035f, 0.25f) * scale,
                new Vector3(0.035f, 0.35f, 0.035f) * scale,
                _matMetalLight, Quaternion.Euler(90f, 0f, 0f));

            // 3. Ống nạp đạn bên dưới (Mag Tube)
            CreatePart(PrimitiveType.Cylinder, "MagTube", parent,
                new Vector3(0f, -0.01f, 0.20f) * scale,
                new Vector3(0.032f, 0.28f, 0.032f) * scale,
                _matMetalDark, Quaternion.Euler(90f, 0f, 0f));

            // 4. Cần bơm (Pump Forend)
            CreatePart(PrimitiveType.Cylinder, "Pump", parent,
                new Vector3(0f, -0.01f, 0.16f) * scale,
                new Vector3(0.055f, 0.14f, 0.055f) * scale,
                _matWood, Quaternion.Euler(90f, 0f, 0f));

            // 5. Báng súng gỗ (Stock)
            CreatePart(PrimitiveType.Cube, "Stock", parent,
                new Vector3(0f, -0.05f, -0.28f) * scale,
                new Vector3(0.05f, 0.10f, 0.26f) * scale,
                _matWood, Quaternion.Euler(-12f, 0f, 0f));

            // 6. Socket Muzzle
            CreateMuzzleSocket(parent, new Vector3(0f, 0.035f, 0.62f) * scale);
        }

        private static void BuildRifle(Transform parent, float scale)
        {
            // 1. Thân súng chính (Upper & Lower Receiver)
            CreatePart(PrimitiveType.Cube, "Receiver", parent,
                new Vector3(0f, 0.03f, 0f) * scale,
                new Vector3(0.05f, 0.09f, 0.32f) * scale,
                _matMetalDark);

            // 2. Nòng súng dài có ốp lót tay (Barrel & Handguard)
            CreatePart(PrimitiveType.Cube, "Handguard", parent,
                new Vector3(0f, 0.03f, 0.30f) * scale,
                new Vector3(0.045f, 0.065f, 0.30f) * scale,
                _matMetalDark);

            CreatePart(PrimitiveType.Cylinder, "Barrel", parent,
                new Vector3(0f, 0.035f, 0.52f) * scale,
                new Vector3(0.025f, 0.20f, 0.025f) * scale,
                _matMetalLight, Quaternion.Euler(90f, 0f, 0f));

            // 3. Loa che lửa đầu nòng (Muzzle Brake)
            CreatePart(PrimitiveType.Cylinder, "MuzzleBrake", parent,
                new Vector3(0f, 0.035f, 0.68f) * scale,
                new Vector3(0.035f, 0.05f, 0.035f) * scale,
                _matMetalDark, Quaternion.Euler(90f, 0f, 0f));

            // 4. Băng đạn cong (Curved Magazine)
            CreatePart(PrimitiveType.Cube, "Magazine", parent,
                new Vector3(0f, -0.12f, 0.08f) * scale,
                new Vector3(0.035f, 0.18f, 0.08f) * scale,
                _matMetalDark, Quaternion.Euler(15f, 0f, 0f));

            // 5. Tay cầm súng (Pistol Grip)
            CreatePart(PrimitiveType.Cube, "PistolGrip", parent,
                new Vector3(0f, -0.08f, -0.10f) * scale,
                new Vector3(0.038f, 0.14f, 0.055f) * scale,
                _matGrip, Quaternion.Euler(20f, 0f, 0f));

            // 6. Báng súng chiến thuật (Tactical Stock)
            CreatePart(PrimitiveType.Cube, "Stock", parent,
                new Vector3(0f, 0.01f, -0.30f) * scale,
                new Vector3(0.045f, 0.11f, 0.28f) * scale,
                _matGrip);

            // 7. Ống ngắm / Ray (Rail / Optic Mount)
            CreatePart(PrimitiveType.Cube, "OpticRail", parent,
                new Vector3(0f, 0.09f, 0.02f) * scale,
                new Vector3(0.03f, 0.025f, 0.18f) * scale,
                _matMetalLight);

            // 8. Socket Muzzle
            CreateMuzzleSocket(parent, new Vector3(0f, 0.035f, 0.74f) * scale);
        }

        private static void BuildSmg(Transform parent, float scale)
        {
            // 1. Thân súng nhỏ gọn (Compact Receiver)
            CreatePart(PrimitiveType.Cube, "Receiver", parent,
                new Vector3(0f, 0.04f, 0f) * scale,
                new Vector3(0.05f, 0.08f, 0.22f) * scale,
                _matMetalDark);

            // 2. Nòng súng ngắn kèm giảm thanh/tản nhiệt (Short Barrel)
            CreatePart(PrimitiveType.Cylinder, "Barrel", parent,
                new Vector3(0f, 0.04f, 0.20f) * scale,
                new Vector3(0.032f, 0.18f, 0.032f) * scale,
                _matMetalLight, Quaternion.Euler(90f, 0f, 0f));

            // 3. Tay cầm chính (Main Grip)
            CreatePart(PrimitiveType.Cube, "Grip", parent,
                new Vector3(0f, -0.06f, -0.05f) * scale,
                new Vector3(0.038f, 0.13f, 0.05f) * scale,
                _matGrip, Quaternion.Euler(15f, 0f, 0f));

            // 4. Tay nắm phụ phía trước (Angled Foregrip)
            CreatePart(PrimitiveType.Cube, "Foregrip", parent,
                new Vector3(0f, -0.04f, 0.12f) * scale,
                new Vector3(0.03f, 0.09f, 0.04f) * scale,
                _matGrip, Quaternion.Euler(-15f, 0f, 0f));

            // 5. Băng đạn gậy dài (Stick Magazine)
            CreatePart(PrimitiveType.Cube, "Magazine", parent,
                new Vector3(0f, -0.16f, 0.02f) * scale,
                new Vector3(0.03f, 0.24f, 0.045f) * scale,
                _matMetalDark, Quaternion.Euler(5f, 0f, 0f));

            // 6. Socket Muzzle
            CreateMuzzleSocket(parent, new Vector3(0f, 0.04f, 0.32f) * scale);
        }

        private static GameObject CreatePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.transform.localRotation = (localRot == default) ? Quaternion.identity : localRot;

            var col = part.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = part.GetComponent<Renderer>();
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            int viewmodelLayer = LayerMask.NameToLayer("Viewmodel");
            if (viewmodelLayer >= 0) part.layer = viewmodelLayer;

            return part;
        }

        private static void CreateMuzzleSocket(Transform parent, Vector3 localPos)
        {
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(parent);
            muzzle.transform.localPosition = localPos;
            muzzle.transform.localRotation = Quaternion.identity;
        }
    }
}
