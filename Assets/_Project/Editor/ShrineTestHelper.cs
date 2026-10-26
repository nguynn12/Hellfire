#if UNITY_EDITOR
using Hellfire.Items;
using Hellfire.Player;
using Hellfire.UI;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hellfire.Editor
{
    public static class ShrineTestHelper
    {
        [MenuItem("Hellfire/Create Test Shrine in Scene", false, 50)]
        public static void CreateShrineInScene()
        {
            // Tìm vị trí của Player để đặt Tế Đàn ngay trước mặt
            Vector3 spawnPos = new Vector3(21.5f, 0f, 25f);
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            if (player != null)
            {
                spawnPos = player.transform.position + player.transform.forward * 3.0f;
                spawnPos.y = player.transform.position.y;
            }

            // 1. Tạo Root GameObject
            GameObject shrineObj = new GameObject("TestShrine_Object");
            shrineObj.transform.position = spawnPos;

            // 2. Thêm NetworkObject
            var netObj = shrineObj.AddComponent<NetworkObject>();

            // 3. Thêm Bệ đá (Cylinder)
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(shrineObj.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pedestal.transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);

            // Bỏ collider của bệ con để dùng trigger collider ở root
            var pedCol = pedestal.GetComponent<Collider>();
            if (pedCol != null) Object.DestroyImmediate(pedCol);

            // Gán màu xám đá cho bệ
            var renderer = pedestal.GetComponent<Renderer>();
            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.35f, 0.35f, 0.4f);
            renderer.sharedMaterial = stoneMat;

            // 4. Thêm Ngọn lửa / Viên ngọc trên đỉnh
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "FlameVisual";
            flame.transform.SetParent(shrineObj.transform, false);
            flame.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            flame.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var flameCol = flame.GetComponent<Collider>();
            if (flameCol != null) Object.DestroyImmediate(flameCol);

            var flameMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            flameMat.color = new Color(0.2f, 0.8f, 1.0f);
            if (flameMat.HasProperty("_EmissionColor"))
            {
                flameMat.EnableKeyword("_EMISSION");
                flameMat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1.0f) * 2f);
            }
            flame.GetComponent<Renderer>().sharedMaterial = flameMat;

            // 5. Thêm Point Light
            GameObject lightObj = new GameObject("ShrineLight");
            lightObj.transform.SetParent(shrineObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.2f, 0.8f, 1.0f);
            light.range = 6.0f;
            light.intensity = 2.0f;

            // 6. Thêm Trigger Collider ở Root
            var triggerCol = shrineObj.AddComponent<SphereCollider>();
            triggerCol.isTrigger = true;
            triggerCol.radius = 3.0f;
            triggerCol.center = new Vector3(0f, 1.0f, 0f);

            // 7. Thêm 3D Text Prompt
            GameObject promptObj = new GameObject("PromptText");
            promptObj.transform.SetParent(shrineObj.transform, false);
            promptObj.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var tmp = promptObj.AddComponent<TextMeshPro>();
            tmp.text = "[E] Cầu Nguyện Tế Đàn";
            tmp.fontSize = 5;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            promptObj.SetActive(false);

            // 8. Gắn InteractiveShrine
            var shrine = shrineObj.AddComponent<InteractiveShrine>();

            // Dùng SerializedObject để gán references
            var so = new SerializedObject(shrine);
            so.FindProperty("_shrineName").stringValue = "Tế Đàn Ban Phước (Test)";
            so.FindProperty("_promptText").objectReferenceValue = tmp;
            so.FindProperty("_flameVisual").objectReferenceValue = flame;
            so.FindProperty("_shrineLight").objectReferenceValue = light;
            so.ApplyModifiedProperties();

            shrine.ApplyGlowVisuals();

            // 9. Đảm bảo Canvas UI cho Shrine Modal tồn tại
            if (Object.FindAnyObjectByType<ShrineChoiceModalUI>() == null)
            {
                GameObject uiObj = new GameObject("ShrineChoiceModalUI");
                uiObj.AddComponent<ShrineChoiceModalUI>();
            }

            // Đánh dấu scene đã sửa đổi để lưu
            EditorSceneManager.MarkSceneDirty(shrineObj.scene);
            Selection.activeGameObject = shrineObj;

            Debug.Log($"<color=#00FF88>[ShrineTestHelper] Đã tạo thành công Tế Đàn tại vị trí: {spawnPos}!</color>");
        }
    }
}
#endif
