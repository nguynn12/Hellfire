using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Hellfire.UI.InfernalTheme;

namespace Hellfire.UI
{
    internal static class InfernalLayouts
    {
        private static readonly Vector2 Centre = new Vector2(.5f, .5f);

        public static void MainMenu(Transform root)
        {
            Controls(root);
            Backdrop(root, "Menu");
            var menu = Find(root, "MainPanel");
            Dim(menu, 0f);
            Place(menu, "TitleText", -550, 312, 640, 128);
            var logo = Find(menu, "TitleText")?.GetComponent<TMP_Text>();
            if (logo != null)
            {
                logo.text = "HELLFIRE";
                logo.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Anton SDF") ?? logo.font;
                logo.fontSize = 112f;
                logo.color = Bone;
                logo.characterSpacing = 9f;
                logo.alignment = TextAlignmentOptions.Center;
            }
            if (Find(menu, "SubTitleText") is { } subtitle) subtitle.gameObject.SetActive(false);
            Ornament(menu, "Sigil", Centre, -550, 422, 72, 72);
            Label(menu, "PlayerNameLabel", "Tên người chơi", -550, 219, 420, 32, 22);
            Place(menu, "PlayerNameInput", -550, 173, 420, 48);
            Place(menu, "HostButton", -550, 96, 420, 60);
            Place(menu, "JoinIpButton", -550, 24, 420, 60);
            Place(menu, "ScanLanButton", -550, -48, 420, 60);
            Place(menu, "SettingsButton", -550, -120, 420, 60);
            Place(menu, "QuitButton", -550, -192, 420, 60);

            Form(root, "HostPanel", "HostTitle");
            Form(root, "JoinIpPanel", "JoinTitle");
            Form(root, "LanScanPanel", "ScanTitle");
            Place(root, "ScanTitle", 0, 256, 680, 60);
            Place(root, "ScanStatus", 0, 198, 600, 32);
            Place(root, "StatusText", 0, -319, 1050, 56);
            Type(root, "StatusText", 24, Bone);
            if (Find(root, "StatusText") is { } status) status.SetAsLastSibling();

            var settings = Find(root, "SettingsPanel");
            Form(root, "SettingsPanel", "SettingsTitle");
            Place(settings, "SettingsTitle", 0, 243, 650, 67);
            if (Find(settings, "SensitivityLabel")?.GetComponent<TMP_Text>() is { } sensitivity)
                sensitivity.text = "Độ nhạy chuột";
            SettingRow(settings, "Sensitivity", 137);
            SettingRow(settings, "Fov", 60);
            SettingRow(settings, "Volume", -17);
            Place(settings, "ScreenShakeToggle", 0, -101, 510, 44);
            Place(settings, "SettingsBackBtn", 0, -200, 324, 60);
            Dim(Find(root, "DisconnectModal"), .89f);
            Paint(Find(root, "ModalBox"), "Panel", Color.white);
            Type(root, "DisconnectTitle", 38, Gold, true);
        }

        private static void Form(Transform root, string name, string title)
        {
            var panel = Find(root, name);
            if (panel == null) return;
            Dim(panel, .49f);
            Panel(panel, 842, 620);
            Type(panel, title, 48, Gold, true);
            Ornament(panel, "Sigil", Centre, 0, 176, 29, 29);
        }

        private static void SettingRow(Transform root, string id, float y)
        {
            Place(root, id + "Label", -223, y, 270, 48);
            Place(root, id + "Slider", 47, y, 282, 36);
            Place(root, id + "Value", 252, y, 95, 48);
        }

        public static void Lobby(Transform root)
        {
            Controls(root);
            var panel = Find(root, "LobbyPanel");
            Backdrop(panel, "Menu");
            Panel(panel, 916, 742);
            Type(panel, "LobbyTitle", 50, Gold, true);
            Place(panel, "LobbyTitle", 0, 255, 700, 70);
            Place(panel, "ConnectionInfo", 0, 186, 660, 40);
            Place(panel, "PlayerCount", 0, 126, 550, 38);
            for (int i = 1; i <= 4; i++)
            {
                string name = "PlayerSlot_" + i;
                var slot = Find(panel, name);
                if (slot == null) continue;
                float y = 129 - 61 * i;
                Place(slot, Centre, 0, y, 688, 55);
                var row = Ornament(panel, "Panel", Centre, 0, y, 688, 55);
                row.preserveAspect = false;
                Paint(row.transform, "Panel", new Color(.67f, .63f, .59f));
                row.pixelsPerUnitMultiplier = 2.5f;
                row.transform.SetSiblingIndex(slot.GetSiblingIndex());
                Type(slot.GetComponentInChildren<TMP_Text>(true), 24, Bone);
            }
            Place(panel, "CopyIpButton", 278, 186, 202, 46);
            Type(panel, "ConnectionInfo", 21, Bone);
            Type(panel, "PlayerCount", 23, Gold, true);
            Place(panel, "StartGameButton", -174, -246, 325, 62);
            Place(panel, "ReadyButton", -174, -246, 325, 62);
            Place(panel, "LeaveButton", 174, -246, 325, 62);
            Place(panel, "WaitingHostText", 0, -300, 650, 35);
        }

        public static void Gameplay(Transform root)
        {
            Controls(root);
            var left = Find(root, "HealthPanel");
            var right = Find(root, "AmmoPanel");
            Place(left, Vector2.zero, 227, 120, 374, 150);
            Place(right, Vector2.right, -227, 120, 374, 150);
            Paint(left, "Panel", Color.white);
            Paint(right, "Panel", Color.white);
            Ornament(left, "Health", Centre, -139, 24, 42, 42);
            Ornament(right, "Ammo", Centre, 137, -17, 42, 42);
            Place(left, "HealthText", 21, 38, 270, 43);
            Type(left, "HealthText", 31, Bone, true);
            Place(left, "HealthSlider", 0, 0, 285, 24);
            Meter(Find(left, "HealthSlider")?.GetComponent<Slider>(), Blood);
            Place(right, "WeaponNameText", 0, 38, 325, 39);
            Type(right, "WeaponNameText", 23, Gold, true);
            Place(right, "AmmoText", -12, -10, 260, 57);
            Type(right, "AmmoText", 49, Bone, true);
            Place(right, "ReloadPromptText", -5, -52, 280, 28);
            Type(right, "ReloadPromptText", 19, Gold, true);
            Place(Find(root, "SoulsText"), Vector2.one, -148, -53, 235, 39);
            Place(Find(root, "FloorText"), Vector2.one, -148, -92, 235, 35);
            Type(root, "SoulsText", 26, Gold, true);
            Type(root, "FloorText", 21, Bone);
            Ornament(root, "Sigil", Vector2.one, -245, -53, 28, 28);
            Place(Find(root, "PlayerInfoText"), Vector2.up, 188, -48, 310, 32);

            Dim(Find(root, "PauseMenuPanel"), .83f);
            Paint(Find(root, "PauseBox"), "Panel", Color.white);
            var pauseBox = Find(root, "PauseBox");
            Place(root, "PauseBox", 0, 0, 564, 376);
            Place(pauseBox, "PauseTitle", 0, 109, 460, 61);
            if (Find(pauseBox, "PauseTitle")?.GetComponent<TMP_Text>() is { } pauseTitle)
            {
                pauseTitle.text = "TẠM DỪNG";
                Type(pauseTitle, 47, Gold, true);
            }
            Ornament(pauseBox, "Sigil", Centre, 0, 53, 30, 30);
            Place(pauseBox, "ResumeButton", 0, -15, 335, 66);
            Place(pauseBox, "LeaveGameButton", 0, -99, 335, 62);
            Paint(Find(root, "RevivePromptPanel"), "Panel", Color.white);
            Type(root, "DownedTimerText", 27, Bone, true);
            Type(root, "BuffToastText", 26, Gold, true);
            var hit = Find(root, "Hitmarker");
            Paint(hit, "Sigil", Color.white, false);
            Place(hit, Centre, 0, 0, 34, 34);
            Outcome(root, "Victory", "Victory");
            Outcome(root, "GameOver", "Defeat");
        }

        private static void Outcome(Transform root, string prefix, string artwork)
        {
            var panel = Find(root, prefix + "OverlayPanel");
            if (panel == null) return;
            Backdrop(panel, artwork);
            Panel(panel, 1080, 536, 0, -10);
            Type(panel, prefix + "Title", 60, Gold, true);
            Place(panel, prefix + "Title", 0, 165, 900, 84);
            Ornament(panel, "Sigil", Centre, 0, 230, 42, 42);
            Place(panel, prefix + "StatsText", 0, 86, 820, 32);
            Type(panel, prefix + "StatsText", 21, Bone);
            Place(panel, prefix + "ReturnButton", 0, -193, 338, 62);
            Place(panel, prefix + "WaitingText", 0, -193, 620, 62);
        }
    }
}
