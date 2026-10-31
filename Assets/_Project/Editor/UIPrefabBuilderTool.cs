#if UNITY_EDITOR

using System.IO;

using Hellfire.UI;

using TMPro;

using UnityEditor;

using UnityEngine;

using UnityEngine.UI;



namespace Hellfire.Editor

{

    [InitializeOnLoad]

    public static class UIPrefabBuilderTool

    {

        private const string UIPrefabsDir = "Assets/_Project/Prefabs/UI";

        private const string ResourcesUIPrefabsDir = "Assets/Resources/UI";



        private const string CardPrefabPath = "Assets/_Project/Prefabs/UI/UpgradeChoiceCard.prefab";

        private const string ModalPrefabPath = "Assets/_Project/Prefabs/UI/ShrineChoiceModal.prefab";

        private const string WeaponWorkbenchModalPrefabPath = "Assets/_Project/Prefabs/UI/WeaponWorkbenchModal.prefab";

        private const string WeaponHudPrefabPath = "Assets/_Project/Prefabs/UI/WeaponHUD.prefab";

        private const string PlayerHudPrefabPath = "Assets/_Project/Prefabs/UI/PlayerHUD.prefab";

        private const string GameUISystemPrefabPath = "Assets/_Project/Prefabs/UI/GameUI_System.prefab";



        private const string ResCardPrefabPath = "Assets/Resources/UI/UpgradeChoiceCard.prefab";

        private const string ResModalPrefabPath = "Assets/Resources/UI/ShrineChoiceModal.prefab";

        private const string ResWeaponWorkbenchModalPrefabPath = "Assets/Resources/UI/WeaponWorkbenchModal.prefab";

        private const string ResWeaponHudPrefabPath = "Assets/Resources/UI/WeaponHUD.prefab";

        private const string ResPlayerHudPrefabPath = "Assets/Resources/UI/PlayerHUD.prefab";

        private const string ResGameUISystemPrefabPath = "Assets/Resources/UI/GameUI_System.prefab";



        private const string SessionKey = "Hellfire_AutoBuildUIPrefabs_v7";



        // Asset paths

        private const string FrameBigPath = "Assets/Resources/GUI_Parts/Gui_parts/Frame_big.png";

        private const string BigBgPath = "Assets/Resources/GUI_Parts/Gui_parts/big_background.png";

        private const string FrameMidPath = "Assets/Resources/GUI_Parts/Gui_parts/Frame_mid.png";

        private const string HpFramePath = "Assets/Resources/GUI_Parts/Gui_parts/Hp_frame.png";

        private const string HpLinePath = "Assets/Resources/GUI_Parts/Gui_parts/Hp_line.png";

        private const string NameBarPath = "Assets/Resources/GUI_Parts/Gui_parts/name_bar.png";

        private const string ButtonPath = "Assets/Resources/GUI_Parts/Gui_parts/button.png";

        private const string ButtonReadyPath = "Assets/Resources/GUI_Parts/Gui_parts/button_ready_on.png";

        private const string CrosshairPath = "Assets/Resources/Crosshairs/Art/Textures/Crosshair_01.png";



        static UIPrefabBuilderTool()

        {

            EditorApplication.delayCall += OnEditorReady;

        }



        private static void OnEditorReady()

        {

            if (SessionState.GetBool(SessionKey, false)) return;

            SessionState.SetBool(SessionKey, true);



            BuildAllUIPrefabs();

        }



        [MenuItem("Hellfire/UI/Build Weapon Workbench Modal Prefab (Green Emerald)", false, 2)]

        public static void BuildWeaponWorkbenchModalMenuItem()

        {

            EnsureDirectory(UIPrefabsDir);

            EnsureDirectory(ResourcesUIPrefabsDir);

            BuildWeaponWorkbenchModalPrefab();

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

        }



        [MenuItem("Hellfire/UI/Build Weapon HUD Prefab (Ammo & Slots)", false, 3)]

        public static void BuildWeaponHudMenuItem()

        {

            EnsureDirectory(UIPrefabsDir);

            EnsureDirectory(ResourcesUIPrefabsDir);

            BuildWeaponHudPrefab();

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

        }



        [MenuItem("Hellfire/UI/Build Complete GameUI_System Prefab (All-in-One)", false, 5)]

        public static void BuildCompleteGameUISystemMenuItem()

        {

            EnsureDirectory(UIPrefabsDir);

            EnsureDirectory(ResourcesUIPrefabsDir);

            BuildGameUISystemPrefab();

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

        }



        [MenuItem("Hellfire/UI/Build All UI Prefabs (Using GUI_Parts & Crosshairs)", false, 10)]

        public static void BuildAllUIPrefabs()

        {

            EnsureDirectory(UIPrefabsDir);

            EnsureDirectory(ResourcesUIPrefabsDir);



            Debug.Log("<color=#00E5FF>================ BẮT ĐẦU TẠO PREFAB UI TỪ TÀI NGUYÊN GUI_PARTS & CROSSHAIRS ================</color>");



            // 1. Tạo Prefab Thẻ Chọn Chỉ Số (UpgradeChoiceCard.prefab)

            BuildUpgradeChoiceCardPrefab();



            // 2. Tạo Prefab Bảng Chọn Chỉ Số Tế Đàn Thân Thể (ShrineChoiceModal.prefab)

            BuildShrineChoiceModalPrefab();



            // 3. Tạo Prefab Bảng Chọn Cường Hóa Bàn Súng (WeaponWorkbenchModal.prefab)

            BuildWeaponWorkbenchModalPrefab();



            // 4. Tạo Prefab HUD Hiển Thị Súng & Đạn Riêng Biệt (WeaponHUD.prefab)

            BuildWeaponHudPrefab();



            // 5. Tạo Prefab HUD Người Chơi Hoàn Chỉnh Máu + Súng + Tâm Ngắm (PlayerHUD.prefab)

            BuildPlayerHudPrefab();



            // 6. Tạo Prefab Hệ Thống UI Hoàn Chỉnh Trọn Gói Cho Mọi Màn Chơi (GameUI_System.prefab)

            BuildGameUISystemPrefab();



            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();



            Debug.Log("<color=#00FF88>================ HOÀN TẤT GÁN TÀI NGUYÊN ĐỒ HỌA VÀO PREFAB UI ================</color>");

        }



        private static void EnsureDirectory(string path)

        {

            if (!Directory.Exists(path))

            {

                Directory.CreateDirectory(path);

                AssetDatabase.Refresh();

            }

        }



        private static Sprite LoadSprite(string assetPath)

        {

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        }



        // =================================================================================

        // 1. PREFAB THẺ BÀI CHỌN CHỈ SỐ (UPGRADE CHOICE CARD)

        // =================================================================================

        private static void BuildUpgradeChoiceCardPrefab()

        {

            var spriteMid = LoadSprite(FrameMidPath);

            var spriteNameBar = LoadSprite(NameBarPath);

            var spriteButton = LoadSprite(ButtonPath);



            GameObject cardObj = new GameObject("UpgradeChoiceCard", typeof(RectTransform), typeof(Image), typeof(Button));

            var cRect = cardObj.GetComponent<RectTransform>();

            cRect.sizeDelta = new Vector2(290f, 340f);



            var cardBg = cardObj.GetComponent<Image>();

            if (spriteMid != null)

            {

                cardBg.sprite = spriteMid;

                cardBg.type = Image.Type.Sliced;

                cardBg.color = Color.white;

            }

            else

            {

                cardBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            }



            var btn = cardObj.GetComponent<Button>();

            var colors = btn.colors;

            colors.highlightedColor = Color.white;

            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

            btn.colors = colors;



            // Khung Badge Bar từ GUI_Parts/name_bar

            var badgeBarObj = new GameObject("BadgeBar", typeof(RectTransform), typeof(Image));

            badgeBarObj.transform.SetParent(cardObj.transform, false);

            var bBarRect = badgeBarObj.GetComponent<RectTransform>();

            bBarRect.anchorMin = new Vector2(0.5f, 0.90f);

            bBarRect.anchorMax = new Vector2(0.5f, 0.90f);

            bBarRect.sizeDelta = new Vector2(210f, 32f);

            var bBarImg = badgeBarObj.GetComponent<Image>();

            if (spriteNameBar != null)

            {

                bBarImg.sprite = spriteNameBar;

                bBarImg.type = Image.Type.Sliced;

            }



            var badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));

            badgeObj.transform.SetParent(badgeBarObj.transform, false);

            var bRect = badgeObj.GetComponent<RectTransform>();

            bRect.anchorMin = Vector2.zero;

            bRect.anchorMax = Vector2.one;

            bRect.sizeDelta = Vector2.zero;

            var bTmp = badgeObj.GetComponent<TextMeshProUGUI>();

            bTmp.text = "[THÂN THỂ]";

            bTmp.fontSize = 13f;

            bTmp.fontStyle = FontStyles.Bold;

            bTmp.alignment = TextAlignmentOptions.Center;

            bTmp.color = new Color(0.35f, 0.85f, 1.0f);



            // Đánh giá sao (Tier Stars)

            var starObj = new GameObject("StarRating", typeof(RectTransform), typeof(TextMeshProUGUI));

            starObj.transform.SetParent(cardObj.transform, false);

            var sRect = starObj.GetComponent<RectTransform>();

            sRect.anchorMin = new Vector2(0.5f, 0.79f);

            sRect.anchorMax = new Vector2(0.5f, 0.79f);

            sRect.sizeDelta = new Vector2(250f, 26f);

            var sTmp = starObj.GetComponent<TextMeshProUGUI>();

            sTmp.text = "★★★";

            sTmp.fontSize = 20f;

            sTmp.fontStyle = FontStyles.Bold;

            sTmp.alignment = TextAlignmentOptions.Center;

            sTmp.color = new Color(1.0f, 0.85f, 0.2f);



            // Tiêu đề thẻ nâng cấp

            var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));

            titleObj.transform.SetParent(cardObj.transform, false);

            var tRect = titleObj.GetComponent<RectTransform>();

            tRect.anchorMin = new Vector2(0.5f, 0.64f);

            tRect.anchorMax = new Vector2(0.5f, 0.64f);

            tRect.sizeDelta = new Vector2(250f, 44f);

            var tTmp = titleObj.GetComponent<TextMeshProUGUI>();

            tTmp.text = "Sinh Lực Bất Diệt";

            tTmp.fontSize = 18f;

            tTmp.fontStyle = FontStyles.Bold;

            tTmp.alignment = TextAlignmentOptions.Center;

            tTmp.color = Color.white;



            // Mô tả chi tiết chỉ số

            var descObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));

            descObj.transform.SetParent(cardObj.transform, false);

            var dRect = descObj.GetComponent<RectTransform>();

            dRect.anchorMin = new Vector2(0.5f, 0.40f);

            dRect.anchorMax = new Vector2(0.5f, 0.40f);

            dRect.sizeDelta = new Vector2(240f, 80f);

            var dTmp = descObj.GetComponent<TextMeshProUGUI>();

            dTmp.text = "+30 Máu Tối Đa (Thần Thoại 200% Thưởng)\nÁp dụng vĩnh viễn";

            dTmp.fontSize = 14f;

            dTmp.alignment = TextAlignmentOptions.Center;

            dTmp.color = new Color(0.85f, 0.92f, 1.0f);



            // Nút bấm đồ họa từ GUI_Parts/button

            var btnVisualObj = new GameObject("ButtonVisual", typeof(RectTransform), typeof(Image));

            btnVisualObj.transform.SetParent(cardObj.transform, false);

            var btnVisualRect = btnVisualObj.GetComponent<RectTransform>();

            btnVisualRect.anchorMin = new Vector2(0.5f, 0.12f);

            btnVisualRect.anchorMax = new Vector2(0.5f, 0.12f);

            btnVisualRect.sizeDelta = new Vector2(190f, 42f);

            var btnVisualImg = btnVisualObj.GetComponent<Image>();

            if (spriteButton != null)

            {

                btnVisualImg.sprite = spriteButton;

                btnVisualImg.type = Image.Type.Sliced;

            }



            var promptObj = new GameObject("KeyPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));

            promptObj.transform.SetParent(btnVisualObj.transform, false);

            var pRect = promptObj.GetComponent<RectTransform>();

            pRect.anchorMin = Vector2.zero;

            pRect.anchorMax = Vector2.one;

            pRect.sizeDelta = Vector2.zero;

            var pTmp = promptObj.GetComponent<TextMeshProUGUI>();

            pTmp.text = "[PHÍM 1] CHỌN";

            pTmp.fontSize = 13f;

            pTmp.fontStyle = FontStyles.Bold;

            pTmp.alignment = TextAlignmentOptions.Center;

            pTmp.color = Color.white;



            PrefabUtility.SaveAsPrefabAsset(cardObj, CardPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(cardObj, ResCardPrefabPath);

            Object.DestroyImmediate(cardObj);

            Debug.Log($"[UIPrefabBuilderTool] Đã tạo thành công Prefab Thẻ: {CardPrefabPath} & {ResCardPrefabPath}");

        }



        // =================================================================================

        // 2. PREFAB MODAL CHỌN CHỈ SỐ TẾ ĐÀN (SHRINE CHOICE MODAL)

        // =================================================================================

        private static void BuildShrineChoiceModalPrefab()

        {

            var spriteBig = LoadSprite(BigBgPath) ?? LoadSprite(FrameBigPath);

            var spriteMid = LoadSprite(FrameMidPath);

            var spriteNameBar = LoadSprite(NameBarPath);

            var spriteButton = LoadSprite(ButtonPath);



            GameObject root = new GameObject("ShrineChoiceModal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ShrineChoiceModalUI));

            var canvas = root.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 100;



            var scaler = root.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1920, 1080);

            scaler.matchWidthOrHeight = 0.5f;



            var modalUI = root.GetComponent<ShrineChoiceModalUI>();



            // Panel Root

            var panelRoot = new GameObject("ShrineChoiceModalPanel", typeof(RectTransform), typeof(Image));

            panelRoot.transform.SetParent(root.transform, false);

            var pRect = panelRoot.GetComponent<RectTransform>();

            pRect.anchorMin = Vector2.zero;

            pRect.anchorMax = Vector2.one;

            pRect.sizeDelta = Vector2.zero;

            var pImg = panelRoot.GetComponent<Image>();

            if (spriteBig != null)

            {

                pImg.sprite = spriteBig;

                pImg.type = Image.Type.Simple;

                pImg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            }

            else

            {

                pImg.color = new Color(0.02f, 0.04f, 0.07f, 0.95f);

            }



            // Thanh Header Bar từ GUI_Parts/name_bar

            var headerBarObj = new GameObject("HeaderBarBg", typeof(RectTransform), typeof(Image));

            headerBarObj.transform.SetParent(panelRoot.transform, false);

            var hbRect = headerBarObj.GetComponent<RectTransform>();

            hbRect.anchorMin = new Vector2(0.5f, 0.86f);

            hbRect.anchorMax = new Vector2(0.5f, 0.86f);

            hbRect.sizeDelta = new Vector2(620f, 65f);

            var hbImg = headerBarObj.GetComponent<Image>();

            if (spriteNameBar != null)

            {

                hbImg.sprite = spriteNameBar;

                hbImg.type = Image.Type.Sliced;

            }



            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));

            headerObj.transform.SetParent(headerBarObj.transform, false);

            var hRect = headerObj.GetComponent<RectTransform>();

            hRect.anchorMin = Vector2.zero;

            hRect.anchorMax = Vector2.one;

            hRect.sizeDelta = Vector2.zero;

            var hTmp = headerObj.GetComponent<TextMeshProUGUI>();

            hTmp.text = "BAN PHƯỚC TẾ ĐÀN";

            hTmp.fontSize = 28f;

            hTmp.fontStyle = FontStyles.Bold;

            hTmp.alignment = TextAlignmentOptions.Center;

            hTmp.color = new Color(0.0f, 0.95f, 1.0f);



            // Phụ đề Subtitle

            var subObj = new GameObject("HeaderSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));

            subObj.transform.SetParent(panelRoot.transform, false);

            var subRect = subObj.GetComponent<RectTransform>();

            subRect.anchorMin = new Vector2(0.5f, 0.79f);

            subRect.anchorMax = new Vector2(0.5f, 0.79f);

            subRect.sizeDelta = new Vector2(900f, 32f);

            var subTmp = subObj.GetComponent<TextMeshProUGUI>();

            subTmp.text = "Chọn 1 trong 3 nâng cấp vĩnh viễn (Bấm [1], [2], [3] hoặc Click)";

            subTmp.fontSize = 17f;

            subTmp.alignment = TextAlignmentOptions.Center;

            subTmp.color = new Color(0.8f, 0.88f, 0.95f);



            // Container chứa 3 thẻ bài

            var cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));

            cardsContainer.transform.SetParent(panelRoot.transform, false);

            var cardsRect = cardsContainer.GetComponent<RectTransform>();

            cardsRect.anchorMin = new Vector2(0.5f, 0.44f);

            cardsRect.anchorMax = new Vector2(0.5f, 0.44f);

            cardsRect.sizeDelta = new Vector2(960f, 360f);



            GameObject[] cardContainers = new GameObject[3];

            Image[] cardBgImages = new Image[3];

            TextMeshProUGUI[] cardBadges = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardStarTexts = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardTitles = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardDescriptions = new TextMeshProUGUI[3];

            Button[] cardButtons = new Button[3];



            float cardW = 290f;

            float spacing = 35f;

            float startX = -((cardW * 3 + spacing * 2) / 2f) + cardW / 2f;



            for (int i = 0; i < 3; i++)

            {

                var card = new GameObject($"ChoiceCard_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));

                card.transform.SetParent(cardsContainer.transform, false);

                var cr = card.GetComponent<RectTransform>();

                cr.sizeDelta = new Vector2(cardW, 340f);

                cr.anchoredPosition = new Vector2(startX + i * (cardW + spacing), 0f);



                var img = card.GetComponent<Image>();

                if (spriteMid != null)

                {

                    img.sprite = spriteMid;

                    img.type = Image.Type.Sliced;

                }

                img.color = Color.white;



                var btnComp = card.GetComponent<Button>();

                var cols = btnComp.colors;

                cols.highlightedColor = Color.white;

                cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

                btnComp.colors = cols;



                // Badge Bar

                var bBar = new GameObject("BadgeBar", typeof(RectTransform), typeof(Image));

                bBar.transform.SetParent(card.transform, false);

                var bBarR = bBar.GetComponent<RectTransform>();

                bBarR.anchorMin = new Vector2(0.5f, 0.90f);

                bBarR.anchorMax = new Vector2(0.5f, 0.90f);

                bBarR.sizeDelta = new Vector2(210f, 32f);

                var bBarI = bBar.GetComponent<Image>();

                if (spriteNameBar != null)

                {

                    bBarI.sprite = spriteNameBar;

                    bBarI.type = Image.Type.Sliced;

                }



                var bObj = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));

                bObj.transform.SetParent(bBar.transform, false);

                var br = bObj.GetComponent<RectTransform>();

                br.anchorMin = Vector2.zero;

                br.anchorMax = Vector2.one;

                br.sizeDelta = Vector2.zero;

                var bT = bObj.GetComponent<TextMeshProUGUI>();

                bT.text = $"[LỰA CHỌN {i + 1}]";

                bT.fontSize = 13f;

                bT.fontStyle = FontStyles.Bold;

                bT.alignment = TextAlignmentOptions.Center;

                bT.color = new Color(0.35f, 0.85f, 1f);



                // Stars

                var sObj = new GameObject("StarRating", typeof(RectTransform), typeof(TextMeshProUGUI));

                sObj.transform.SetParent(card.transform, false);

                var sr = sObj.GetComponent<RectTransform>();

                sr.anchorMin = new Vector2(0.5f, 0.79f);

                sr.anchorMax = new Vector2(0.5f, 0.79f);

                sr.sizeDelta = new Vector2(260f, 26f);

                var sT = sObj.GetComponent<TextMeshProUGUI>();

                sT.text = "★★★";

                sT.fontSize = 18f;

                sT.fontStyle = FontStyles.Bold;

                sT.alignment = TextAlignmentOptions.Center;

                sT.color = new Color(1f, 0.85f, 0.2f);



                // Title

                var tObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));

                tObj.transform.SetParent(card.transform, false);

                var tr = tObj.GetComponent<RectTransform>();

                tr.anchorMin = new Vector2(0.5f, 0.64f);

                tr.anchorMax = new Vector2(0.5f, 0.64f);

                tr.sizeDelta = new Vector2(250f, 44f);

                var tT = tObj.GetComponent<TextMeshProUGUI>();

                tT.text = "Tên Chỉ Số";

                tT.fontSize = 18f;

                tT.fontStyle = FontStyles.Bold;

                tT.alignment = TextAlignmentOptions.Center;

                tT.color = Color.white;



                // Description

                var dObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));

                dObj.transform.SetParent(card.transform, false);

                var dr = dObj.GetComponent<RectTransform>();

                dr.anchorMin = new Vector2(0.5f, 0.40f);

                dr.anchorMax = new Vector2(0.5f, 0.40f);

                dr.sizeDelta = new Vector2(240f, 80f);

                var dT = dObj.GetComponent<TextMeshProUGUI>();

                dT.text = "+ Chi tiết nâng cấp...";

                dT.fontSize = 14f;

                dT.alignment = TextAlignmentOptions.Center;

                dT.color = new Color(0.9f, 0.95f, 1f);



                // Visual Button từ GUI_Parts/button

                var btnV = new GameObject("ButtonVisual", typeof(RectTransform), typeof(Image));

                btnV.transform.SetParent(card.transform, false);

                var bvr = btnV.GetComponent<RectTransform>();

                bvr.anchorMin = new Vector2(0.5f, 0.12f);

                bvr.anchorMax = new Vector2(0.5f, 0.12f);

                bvr.sizeDelta = new Vector2(190f, 42f);

                var bvi = btnV.GetComponent<Image>();

                if (spriteButton != null)

                {

                    bvi.sprite = spriteButton;

                    bvi.type = Image.Type.Sliced;

                }



                // Prompt

                var pObj = new GameObject("KeyPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));

                pObj.transform.SetParent(btnV.transform, false);

                var pr = pObj.GetComponent<RectTransform>();

                pr.anchorMin = Vector2.zero;

                pr.anchorMax = Vector2.one;

                pr.sizeDelta = Vector2.zero;

                var pT = pObj.GetComponent<TextMeshProUGUI>();

                pT.text = $"[PHÍM {i + 1}] CHỌN";

                pT.fontSize = 13f;

                pT.fontStyle = FontStyles.Bold;

                pT.alignment = TextAlignmentOptions.Center;

                pT.color = Color.white;



                cardContainers[i] = card;

                cardBgImages[i] = img;

                cardBadges[i] = bT;

                cardStarTexts[i] = sT;

                cardTitles[i] = tT;

                cardDescriptions[i] = dT;

                cardButtons[i] = btnComp;

            }



            // Gợi ý chân trang

            var footObj = new GameObject("FooterHint", typeof(RectTransform), typeof(TextMeshProUGUI));

            footObj.transform.SetParent(panelRoot.transform, false);

            var footRect = footObj.GetComponent<RectTransform>();

            footRect.anchorMin = new Vector2(0.5f, 0.10f);

            footRect.anchorMax = new Vector2(0.5f, 0.10f);

            footRect.sizeDelta = new Vector2(800f, 28f);

            var footTmp = footObj.GetComponent<TextMeshProUGUI>();

            footTmp.text = "Phím tắt: Bấm [1], [2], [3] để chọn thẻ ngay | [ESC] Đóng";

            footTmp.fontSize = 13f;

            footTmp.alignment = TextAlignmentOptions.Center;

            footTmp.color = new Color(0.6f, 0.75f, 0.85f);



            // Gán Serialized Object

            var so = new SerializedObject(modalUI);

            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;

            so.FindProperty("_headerTitleText").objectReferenceValue = hTmp;

            so.FindProperty("_headerSubtitleText").objectReferenceValue = subTmp;



            var cCont = so.FindProperty("_cardContainers");

            var cBg = so.FindProperty("_cardBgImages");

            var cBadge = so.FindProperty("_cardBadges");

            var cStars = so.FindProperty("_cardStarTexts");

            var cTitle = so.FindProperty("_cardTitles");

            var cDesc = so.FindProperty("_cardDescriptions");

            var cBtn = so.FindProperty("_cardButtons");



            for (int i = 0; i < 3; i++)

            {

                cCont.GetArrayElementAtIndex(i).objectReferenceValue = cardContainers[i];

                cBg.GetArrayElementAtIndex(i).objectReferenceValue = cardBgImages[i];

                cBadge.GetArrayElementAtIndex(i).objectReferenceValue = cardBadges[i];

                cStars.GetArrayElementAtIndex(i).objectReferenceValue = cardStarTexts[i];

                cTitle.GetArrayElementAtIndex(i).objectReferenceValue = cardTitles[i];

                cDesc.GetArrayElementAtIndex(i).objectReferenceValue = cardDescriptions[i];

                cBtn.GetArrayElementAtIndex(i).objectReferenceValue = cardButtons[i];

            }

            so.ApplyModifiedProperties();



            panelRoot.SetActive(false);



            PrefabUtility.SaveAsPrefabAsset(root, ModalPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, ResModalPrefabPath);

            Object.DestroyImmediate(root);

            Debug.Log($"[UIPrefabBuilderTool] Đã tạo thành công Prefab Modal: {ModalPrefabPath} & {ResModalPrefabPath}");

        }



        // =================================================================================

        // 3. PREFAB MODAL CƯỜNG HÓA VŨ KHÍ (WEAPON WORKBENCH MODAL)

        // =================================================================================

        private static void BuildWeaponWorkbenchModalPrefab()

        {

            var spriteBig = LoadSprite(BigBgPath) ?? LoadSprite(FrameBigPath);

            var spriteMid = LoadSprite(FrameMidPath);

            var spriteNameBar = LoadSprite(NameBarPath);

            var spriteButton = LoadSprite(ButtonPath);



            GameObject root = new GameObject("WeaponWorkbenchModal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ShrineChoiceModalUI));

            var canvas = root.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 100;



            var scaler = root.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1920, 1080);

            scaler.matchWidthOrHeight = 0.5f;



            var modalUI = root.GetComponent<ShrineChoiceModalUI>();



            // Panel Root (Tông xanh đậm Cyberpunk)

            var panelRoot = new GameObject("WeaponWorkbenchModalPanel", typeof(RectTransform), typeof(Image));

            panelRoot.transform.SetParent(root.transform, false);

            var pRect = panelRoot.GetComponent<RectTransform>();

            pRect.anchorMin = Vector2.zero;

            pRect.anchorMax = Vector2.one;

            pRect.sizeDelta = Vector2.zero;

            var pImg = panelRoot.GetComponent<Image>();

            if (spriteBig != null)

            {

                pImg.sprite = spriteBig;

                pImg.type = Image.Type.Simple;

                pImg.color = new Color(0.08f, 0.16f, 0.14f, 0.98f);

            }

            else

            {

                pImg.color = new Color(0.02f, 0.07f, 0.05f, 0.96f);

            }



            // Thanh Header Bar từ GUI_Parts/name_bar (Viền xanh lục bảo)

            var headerBarObj = new GameObject("HeaderBarBg", typeof(RectTransform), typeof(Image));

            headerBarObj.transform.SetParent(panelRoot.transform, false);

            var hbRect = headerBarObj.GetComponent<RectTransform>();

            hbRect.anchorMin = new Vector2(0.5f, 0.86f);

            hbRect.anchorMax = new Vector2(0.5f, 0.86f);

            hbRect.sizeDelta = new Vector2(660f, 65f);

            var hbImg = headerBarObj.GetComponent<Image>();

            if (spriteNameBar != null)

            {

                hbImg.sprite = spriteNameBar;

                hbImg.type = Image.Type.Sliced;

                hbImg.color = new Color(0.25f, 1.0f, 0.55f, 0.9f);

            }



            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));

            headerObj.transform.SetParent(headerBarObj.transform, false);

            var hRect = headerObj.GetComponent<RectTransform>();

            hRect.anchorMin = Vector2.zero;

            hRect.anchorMax = Vector2.one;

            hRect.sizeDelta = Vector2.zero;

            var hTmp = headerObj.GetComponent<TextMeshProUGUI>();

            hTmp.text = "BÀN CƯỜNG HÓA VŨ KHÍ";

            hTmp.fontSize = 28f;

            hTmp.fontStyle = FontStyles.Bold;

            hTmp.alignment = TextAlignmentOptions.Center;

            hTmp.color = new Color(0.15f, 1.0f, 0.45f);



            // Phụ đề Subtitle

            var subObj = new GameObject("HeaderSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));

            subObj.transform.SetParent(panelRoot.transform, false);

            var subRect = subObj.GetComponent<RectTransform>();

            subRect.anchorMin = new Vector2(0.5f, 0.79f);

            subRect.anchorMax = new Vector2(0.5f, 0.79f);

            subRect.sizeDelta = new Vector2(900f, 32f);

            var subTmp = subObj.GetComponent<TextMeshProUGUI>();

            subTmp.text = "Mỗi lần nâng cấp tốn 2 Pha Lê Xanh • Làm mới tốn 1 Pha Lê Xanh [R]";

            subTmp.fontSize = 17f;

            subTmp.alignment = TextAlignmentOptions.Center;

            subTmp.color = new Color(0.75f, 0.95f, 0.85f);



            // Container chứa 3 thẻ bài súng

            var cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));

            cardsContainer.transform.SetParent(panelRoot.transform, false);

            var cardsRect = cardsContainer.GetComponent<RectTransform>();

            cardsRect.anchorMin = new Vector2(0.5f, 0.44f);

            cardsRect.anchorMax = new Vector2(0.5f, 0.44f);

            cardsRect.sizeDelta = new Vector2(960f, 360f);



            GameObject[] cardContainers = new GameObject[3];

            Image[] cardBgImages = new Image[3];

            TextMeshProUGUI[] cardBadges = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardStarTexts = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardTitles = new TextMeshProUGUI[3];

            TextMeshProUGUI[] cardDescriptions = new TextMeshProUGUI[3];

            Button[] cardButtons = new Button[3];



            float cardW = 290f;

            float spacing = 35f;

            float startX = -((cardW * 3 + spacing * 2) / 2f) + cardW / 2f;



            string[] defaultBadges = { "[VŨ KHÍ]", "[CHIẾN ĐẤU]", "[ĐẠN DƯỢC]" };

            string[] defaultTitles = { "SÁT THƯƠNG TĂNG CƯỜNG", "TỐC BẮN THẦN TỐC", "BĂNG ĐẠN MỞ RỘNG" };

            string[] defaultDescs = {

                "+25% Sát thương đạn cho mọi loại súng trong trận đấu.",

                "+30% Tốc độ xả đạn, giảm giật và tăng độ ổn định.",

                "+40% Sức chứa băng đạn, nạp đạn nhanh hơn 20%."

            };



            for (int i = 0; i < 3; i++)

            {

                var card = new GameObject($"WeaponCard_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));

                card.transform.SetParent(cardsContainer.transform, false);

                var cr = card.GetComponent<RectTransform>();

                cr.sizeDelta = new Vector2(cardW, 340f);

                cr.anchoredPosition = new Vector2(startX + i * (cardW + spacing), 0f);



                var img = card.GetComponent<Image>();

                if (spriteMid != null)

                {

                    img.sprite = spriteMid;

                    img.type = Image.Type.Sliced;

                }

                img.color = new Color(0.95f, 1f, 0.95f, 1f);



                var btnComp = card.GetComponent<Button>();

                var cols = btnComp.colors;

                cols.highlightedColor = Color.white;

                cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

                btnComp.colors = cols;



                // Badge Bar

                var bBar = new GameObject("BadgeBar", typeof(RectTransform), typeof(Image));

                bBar.transform.SetParent(card.transform, false);

                var bBarRect = bBar.GetComponent<RectTransform>();

                bBarRect.anchorMin = new Vector2(0.5f, 0.90f);

                bBarRect.anchorMax = new Vector2(0.5f, 0.90f);

                bBarRect.sizeDelta = new Vector2(210f, 32f);

                var bBarImg = bBar.GetComponent<Image>();

                if (spriteNameBar != null)

                {

                    bBarImg.sprite = spriteNameBar;

                    bBarImg.type = Image.Type.Sliced;

                    bBarImg.color = new Color(0.2f, 0.9f, 0.5f, 0.85f);

                }



                var badge = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));

                badge.transform.SetParent(bBar.transform, false);

                var br = badge.GetComponent<RectTransform>();

                br.anchorMin = Vector2.zero;

                br.anchorMax = Vector2.one;

                br.sizeDelta = Vector2.zero;

                var bT = badge.GetComponent<TextMeshProUGUI>();

                bT.text = defaultBadges[i];

                bT.fontSize = 13f;

                bT.fontStyle = FontStyles.Bold;

                bT.alignment = TextAlignmentOptions.Center;

                bT.color = new Color(0.15f, 1.0f, 0.5f);



                // Stars

                var stars = new GameObject("Stars", typeof(RectTransform), typeof(TextMeshProUGUI));

                stars.transform.SetParent(card.transform, false);

                var sr = stars.GetComponent<RectTransform>();

                sr.anchorMin = new Vector2(0.5f, 0.77f);

                sr.anchorMax = new Vector2(0.5f, 0.77f);

                sr.sizeDelta = new Vector2(200f, 26f);

                var sT = stars.GetComponent<TextMeshProUGUI>();

                sT.text = "★★★";

                sT.fontSize = 16f;

                sT.alignment = TextAlignmentOptions.Center;

                sT.color = new Color(1.0f, 0.85f, 0.2f);



                // Title

                var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));

                title.transform.SetParent(card.transform, false);

                var tr = title.GetComponent<RectTransform>();

                tr.anchorMin = new Vector2(0.5f, 0.63f);

                tr.anchorMax = new Vector2(0.5f, 0.63f);

                tr.sizeDelta = new Vector2(250f, 44f);

                var tT = title.GetComponent<TextMeshProUGUI>();

                tT.text = defaultTitles[i];

                tT.fontSize = 17f;

                tT.fontStyle = FontStyles.Bold;

                tT.alignment = TextAlignmentOptions.Center;

                tT.color = Color.white;



                // Description

                var desc = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));

                desc.transform.SetParent(card.transform, false);

                var dr = desc.GetComponent<RectTransform>();

                dr.anchorMin = new Vector2(0.5f, 0.38f);

                dr.anchorMax = new Vector2(0.5f, 0.38f);

                dr.sizeDelta = new Vector2(240f, 75f);

                var dT = desc.GetComponent<TextMeshProUGUI>();

                dT.text = defaultDescs[i];

                dT.fontSize = 13.5f;

                dT.alignment = TextAlignmentOptions.Center;

                dT.color = new Color(0.85f, 0.95f, 0.90f);



                // Button visual

                var btnVis = new GameObject("ButtonVisual", typeof(RectTransform), typeof(Image));

                btnVis.transform.SetParent(card.transform, false);

                var bvr = btnVis.GetComponent<RectTransform>();

                bvr.anchorMin = new Vector2(0.5f, 0.12f);

                bvr.anchorMax = new Vector2(0.5f, 0.12f);

                bvr.sizeDelta = new Vector2(190f, 42f);

                var bvImg = btnVis.GetComponent<Image>();

                if (spriteButton != null)

                {

                    bvImg.sprite = spriteButton;

                    bvImg.type = Image.Type.Sliced;

                    bvImg.color = new Color(0.2f, 1.0f, 0.5f, 1f);

                }



                var prompt = new GameObject("KeyPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));

                prompt.transform.SetParent(btnVis.transform, false);

                var pr = prompt.GetComponent<RectTransform>();

                pr.anchorMin = Vector2.zero;

                pr.anchorMax = Vector2.one;

                pr.sizeDelta = Vector2.zero;

                var pT = prompt.GetComponent<TextMeshProUGUI>();

                pT.text = $"[PHÍM {i + 1}] CHỌN";

                pT.fontSize = 13f;

                pT.fontStyle = FontStyles.Bold;

                pT.alignment = TextAlignmentOptions.Center;

                pT.color = Color.white;



                cardContainers[i] = card;

                cardBgImages[i] = img;

                cardBadges[i] = bT;

                cardStarTexts[i] = sT;

                cardTitles[i] = tT;

                cardDescriptions[i] = dT;

                cardButtons[i] = btnComp;

            }



            // Gợi ý chân trang

            var footObj = new GameObject("FooterHint", typeof(RectTransform), typeof(TextMeshProUGUI));

            footObj.transform.SetParent(panelRoot.transform, false);

            var footRect = footObj.GetComponent<RectTransform>();

            footRect.anchorMin = new Vector2(0.5f, 0.10f);

            footRect.anchorMax = new Vector2(0.5f, 0.10f);

            footRect.sizeDelta = new Vector2(800f, 28f);

            var footTmp = footObj.GetComponent<TextMeshProUGUI>();

            footTmp.text = "Phím tắt: Bấm [1], [2], [3] để chọn thẻ | [R] Làm mới | [ESC] Đóng bàn";

            footTmp.fontSize = 13f;

            footTmp.alignment = TextAlignmentOptions.Center;

            footTmp.color = new Color(0.6f, 0.85f, 0.75f);



            // Gán Serialized Object

            var so = new SerializedObject(modalUI);

            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;

            so.FindProperty("_headerTitleText").objectReferenceValue = hTmp;

            so.FindProperty("_headerSubtitleText").objectReferenceValue = subTmp;



            var cCont = so.FindProperty("_cardContainers");

            var cBg = so.FindProperty("_cardBgImages");

            var cBadge = so.FindProperty("_cardBadges");

            var cStars = so.FindProperty("_cardStarTexts");

            var cTitle = so.FindProperty("_cardTitles");

            var cDesc = so.FindProperty("_cardDescriptions");

            var cBtn = so.FindProperty("_cardButtons");



            for (int i = 0; i < 3; i++)

            {

                cCont.GetArrayElementAtIndex(i).objectReferenceValue = cardContainers[i];

                cBg.GetArrayElementAtIndex(i).objectReferenceValue = cardBgImages[i];

                cBadge.GetArrayElementAtIndex(i).objectReferenceValue = cardBadges[i];

                cStars.GetArrayElementAtIndex(i).objectReferenceValue = cardStarTexts[i];

                cTitle.GetArrayElementAtIndex(i).objectReferenceValue = cardTitles[i];

                cDesc.GetArrayElementAtIndex(i).objectReferenceValue = cardDescriptions[i];

                cBtn.GetArrayElementAtIndex(i).objectReferenceValue = cardButtons[i];

            }

            so.ApplyModifiedProperties();



            panelRoot.SetActive(false);



            PrefabUtility.SaveAsPrefabAsset(root, WeaponWorkbenchModalPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, ResWeaponWorkbenchModalPrefabPath);

            Object.DestroyImmediate(root);

            Debug.Log($"[UIPrefabBuilderTool] Đã tạo thành công Prefab Bàn Nâng Cấp Súng: {WeaponWorkbenchModalPrefabPath} & {ResWeaponWorkbenchModalPrefabPath}");

        }



        // =================================================================================

        // HÀM TIỆN ÍCH TẠO CỤM PANEL ĐẠN & SLOT SÚNG (WEAPON AMMO PANEL HIERARCHY)

        // =================================================================================

        private static GameObject CreateWeaponPanelHierarchy(Transform parent, Sprite spriteMid)

        {

            var weaponPanel = new GameObject("WeaponAmmoPanel", typeof(RectTransform), typeof(Image));

            weaponPanel.transform.SetParent(parent, false);

            var wpRect = weaponPanel.GetComponent<RectTransform>();

            wpRect.anchorMin = new Vector2(1f, 0f);

            wpRect.anchorMax = new Vector2(1f, 0f);

            wpRect.pivot = new Vector2(1f, 0f);

            wpRect.anchoredPosition = new Vector2(-25f, 25f);

            wpRect.sizeDelta = new Vector2(280f, 100f);



            var wpImg = weaponPanel.GetComponent<Image>();

            if (spriteMid != null)

            {

                wpImg.sprite = spriteMid;

                wpImg.type = Image.Type.Sliced;

                wpImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            }

            else

            {

                wpImg.color = new Color(0.04f, 0.07f, 0.11f, 0.92f);

            }



            // Thanh Slot Vũ Khí (Slot 1 & Slot 2)

            var slotContainer = new GameObject("SlotBar", typeof(RectTransform));

            slotContainer.transform.SetParent(weaponPanel.transform, false);

            var scRect = slotContainer.GetComponent<RectTransform>();

            scRect.anchorMin = new Vector2(0f, 1f);

            scRect.anchorMax = new Vector2(1f, 1f);

            scRect.pivot = new Vector2(0.5f, 0f);

            scRect.anchoredPosition = new Vector2(0f, 6f);

            scRect.sizeDelta = new Vector2(0f, 28f);



            // Slot 1

            var slot1 = new GameObject("Slot_1", typeof(RectTransform), typeof(Image));

            slot1.transform.SetParent(slotContainer.transform, false);

            var s1Rect = slot1.GetComponent<RectTransform>();

            s1Rect.anchorMin = new Vector2(0f, 0f);

            s1Rect.anchorMax = new Vector2(0.49f, 1f);

            s1Rect.offsetMin = Vector2.zero;

            s1Rect.offsetMax = Vector2.zero;

            var s1Img = slot1.GetComponent<Image>();

            s1Img.color = new Color(0.0f, 0.45f, 0.75f, 0.95f);



            var s1TextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));

            s1TextObj.transform.SetParent(slot1.transform, false);

            var s1TRect = s1TextObj.GetComponent<RectTransform>();

            s1TRect.anchorMin = Vector2.zero;

            s1TRect.anchorMax = Vector2.one;

            s1TRect.sizeDelta = Vector2.zero;

            var s1Tmp = s1TextObj.GetComponent<TextMeshProUGUI>();

            s1Tmp.text = "[1] SÚNG CHÍNH";

            s1Tmp.fontSize = 12f;

            s1Tmp.fontStyle = FontStyles.Bold;

            s1Tmp.alignment = TextAlignmentOptions.Center;

            s1Tmp.color = Color.white;



            // Slot 2

            var slot2 = new GameObject("Slot_2", typeof(RectTransform), typeof(Image));

            slot2.transform.SetParent(slotContainer.transform, false);

            var s2Rect = slot2.GetComponent<RectTransform>();

            s2Rect.anchorMin = new Vector2(0.51f, 0f);

            s2Rect.anchorMax = new Vector2(1f, 1f);

            s2Rect.offsetMin = Vector2.zero;

            s2Rect.offsetMax = Vector2.zero;

            var s2Img = slot2.GetComponent<Image>();

            s2Img.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);



            var s2TextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));

            s2TextObj.transform.SetParent(slot2.transform, false);

            var s2TRect = s2TextObj.GetComponent<RectTransform>();

            s2TRect.anchorMin = Vector2.zero;

            s2TRect.anchorMax = Vector2.one;

            s2TRect.sizeDelta = Vector2.zero;

            var s2Tmp = s2TextObj.GetComponent<TextMeshProUGUI>();

            s2Tmp.text = "[2] SÚNG PHỤ";

            s2Tmp.fontSize = 12f;

            s2Tmp.fontStyle = FontStyles.Bold;

            s2Tmp.alignment = TextAlignmentOptions.Center;

            s2Tmp.color = new Color(0.7f, 0.75f, 0.8f);



            // Tên vũ khí

            var nameObj = new GameObject("WeaponNameText", typeof(RectTransform), typeof(TextMeshProUGUI));

            nameObj.transform.SetParent(weaponPanel.transform, false);

            var nRect = nameObj.GetComponent<RectTransform>();

            nRect.anchorMin = new Vector2(0f, 1f);

            nRect.anchorMax = new Vector2(0.7f, 1f);

            nRect.pivot = new Vector2(0f, 1f);

            nRect.anchoredPosition = new Vector2(14f, -10f);

            nRect.sizeDelta = new Vector2(0f, 26f);

            var nTmp = nameObj.GetComponent<TextMeshProUGUI>();

            nTmp.text = "ASSAULT RIFLE";

            nTmp.fontSize = 17f;

            nTmp.fontStyle = FontStyles.Bold;

            nTmp.alignment = TextAlignmentOptions.Left;

            nTmp.color = Color.white;



            // Chế độ bắn Badge

            var badgeObj = new GameObject("FireModeBadge", typeof(RectTransform), typeof(TextMeshProUGUI));

            badgeObj.transform.SetParent(weaponPanel.transform, false);

            var bdRect = badgeObj.GetComponent<RectTransform>();

            bdRect.anchorMin = new Vector2(0.7f, 1f);

            bdRect.anchorMax = new Vector2(1f, 1f);

            bdRect.pivot = new Vector2(1f, 1f);

            bdRect.anchoredPosition = new Vector2(-14f, -10f);

            bdRect.sizeDelta = new Vector2(0f, 26f);

            var bdTmp = badgeObj.GetComponent<TextMeshProUGUI>();

            bdTmp.text = "AUTO | AIM";

            bdTmp.fontSize = 13f;

            bdTmp.fontStyle = FontStyles.Bold;

            bdTmp.alignment = TextAlignmentOptions.Right;

            bdTmp.color = new Color(0.2f, 0.95f, 1f);



            // Số đạn hiện tại / Tối đa

            var ammoObj = new GameObject("AmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));

            ammoObj.transform.SetParent(weaponPanel.transform, false);

            var aRect = ammoObj.GetComponent<RectTransform>();

            aRect.anchorMin = new Vector2(0f, 0f);

            aRect.anchorMax = new Vector2(0.65f, 0.7f);

            aRect.pivot = new Vector2(0f, 0f);

            aRect.anchoredPosition = new Vector2(14f, 14f);

            aRect.sizeDelta = new Vector2(0f, 44f);

            var aTmp = ammoObj.GetComponent<TextMeshProUGUI>();

            aTmp.text = "30 <size=19><color=#D8E8FF>/ 30 (∞)</color></size>";

            aTmp.fontSize = 38f;

            aTmp.fontStyle = FontStyles.Bold;

            aTmp.alignment = TextAlignmentOptions.Left;

            aTmp.color = Color.white;



            // Thanh nạp đạn

            var reloadBar = new GameObject("ReloadProgressBar", typeof(RectTransform), typeof(Image));

            reloadBar.transform.SetParent(weaponPanel.transform, false);

            var rbRect = reloadBar.GetComponent<RectTransform>();

            rbRect.anchorMin = new Vector2(0f, 0f);

            rbRect.anchorMax = new Vector2(1f, 0f);

            rbRect.pivot = new Vector2(0.5f, 0f);

            rbRect.anchoredPosition = new Vector2(0f, 6f);

            rbRect.sizeDelta = new Vector2(-28f, 6f);

            var rbImg = reloadBar.GetComponent<Image>();

            rbImg.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);



            // Nhắc nạp đạn Prompt

            var promptObj = new GameObject("ReloadPromptText", typeof(RectTransform), typeof(TextMeshProUGUI));

            promptObj.transform.SetParent(weaponPanel.transform, false);

            var prRect = promptObj.GetComponent<RectTransform>();

            prRect.anchorMin = new Vector2(0.65f, 0f);

            prRect.anchorMax = new Vector2(1f, 0.7f);

            prRect.pivot = new Vector2(1f, 0f);

            prRect.anchoredPosition = new Vector2(-14f, 16f);

            prRect.sizeDelta = new Vector2(0f, 30f);

            var prTmp = promptObj.GetComponent<TextMeshProUGUI>();

            prTmp.text = "[R] NẠP";

            prTmp.fontSize = 14f;

            prTmp.fontStyle = FontStyles.Bold;

            prTmp.alignment = TextAlignmentOptions.Right;

            prTmp.color = new Color(1f, 0.85f, 0.2f);



            return weaponPanel;

        }



        // =================================================================================

        // 4. PREFAB HUD VŨ KHÍ & ĐẠN DƯỢC RIÊNG BIỆT (WEAPON HUD)

        // =================================================================================

        private static void BuildWeaponHudPrefab()

        {

            var spriteMid = LoadSprite(FrameMidPath);



            GameObject root = new GameObject("WeaponHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameplayHUD));

            var canvas = root.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 20;



            var scaler = root.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1920, 1080);

            scaler.matchWidthOrHeight = 0.5f;



            var weaponPanel = CreateWeaponPanelHierarchy(root.transform, spriteMid);

            var hudComp = root.GetComponent<GameplayHUD>();
            var so = new SerializedObject(hudComp);
            var wpnNameTmp = weaponPanel.transform.Find("WeaponNameText")?.GetComponent<TextMeshProUGUI>();
            var ammoTmp = weaponPanel.transform.Find("AmmoText")?.GetComponent<TextMeshProUGUI>();
            var reloadTmp = weaponPanel.transform.Find("ReloadPromptText")?.GetComponent<TextMeshProUGUI>();
            if (wpnNameTmp != null) so.FindProperty("_weaponNameText").objectReferenceValue = wpnNameTmp;
            if (ammoTmp != null) so.FindProperty("_ammoText").objectReferenceValue = ammoTmp;
            if (reloadTmp != null) so.FindProperty("_reloadPromptText").objectReferenceValue = reloadTmp;
            so.ApplyModifiedProperties();



            PrefabUtility.SaveAsPrefabAsset(root, WeaponHudPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(root, ResWeaponHudPrefabPath);

            Object.DestroyImmediate(root);

            Debug.Log($"[UIPrefabBuilderTool] Đã tạo thành công Prefab Weapon HUD: {WeaponHudPrefabPath} & {ResWeaponHudPrefabPath}");

        }



        // =================================================================================

        // 5. PREFAB HUD MÁU & BẢNG CHỈ SỐ NHÂN VẬT (PLAYER HUD)

        // =================================================================================

        private static void BuildPlayerHudPrefab()

        {

            var spriteHpFrame = LoadSprite(HpFramePath);

            var spriteHpLine = LoadSprite(HpLinePath);

            var spriteFrameBig = LoadSprite(FrameBigPath);

            var spriteNameBar = LoadSprite(NameBarPath);

            var spriteCrosshair = LoadSprite(CrosshairPath);



            GameObject hudRoot = new GameObject(

                "PlayerHUD",

                typeof(Canvas),

                typeof(CanvasScaler),

                typeof(GraphicRaycaster),

                typeof(GameplayHUD)

            );

            var canvas = hudRoot.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 20;



            var scaler = hudRoot.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1920, 1080);

            scaler.matchWidthOrHeight = 0.5f;



            // ================= 1. Cụm HUD Máu & Giáp (Góc dưới bên trái) =================

            var hpPanel = new GameObject("HealthAndArmorPanel", typeof(RectTransform));

            hpPanel.transform.SetParent(hudRoot.transform, false);

            var hpRect = hpPanel.GetComponent<RectTransform>();

            hpRect.anchorMin = new Vector2(0f, 0f);

            hpRect.anchorMax = new Vector2(0f, 0f);

            hpRect.pivot = new Vector2(0f, 0f);

            hpRect.anchoredPosition = new Vector2(24f, 24f);

            hpRect.sizeDelta = new Vector2(340f, 95f);



            // Số máu HP Text

            var hpTextObj = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI));

            hpTextObj.transform.SetParent(hpPanel.transform, false);

            var hpTRect = hpTextObj.GetComponent<RectTransform>();

            hpTRect.anchorMin = new Vector2(0f, 1f);

            hpTRect.anchorMax = new Vector2(0f, 1f);

            hpTRect.pivot = new Vector2(0f, 1f);

            hpTRect.anchoredPosition = new Vector2(8f, -6f);

            hpTRect.sizeDelta = new Vector2(220f, 32f);

            var hpTmp = hpTextObj.GetComponent<TextMeshProUGUI>();

            hpTmp.text = "100 / 100 HP";

            hpTmp.fontSize = 24f;

            hpTmp.fontStyle = FontStyles.Bold;

            hpTmp.color = new Color(0.0f, 0.95f, 0.65f);



            // Khung thanh máu từ GUI_Parts/Hp_frame & Hp_line

            var hpBarContainer = new GameObject("HealthBarGraphic", typeof(RectTransform));

            hpBarContainer.transform.SetParent(hpPanel.transform, false);

            var hbRect = hpBarContainer.GetComponent<RectTransform>();

            hbRect.anchorMin = new Vector2(0f, 1f);

            hbRect.anchorMax = new Vector2(1f, 1f);

            hbRect.pivot = new Vector2(0f, 1f);

            hbRect.anchoredPosition = new Vector2(0f, -42f);

            hbRect.sizeDelta = new Vector2(0f, 26f);



            // Frame Background (Hp_frame)

            var frameObj = new GameObject("HpFrame", typeof(RectTransform), typeof(Image));

            frameObj.transform.SetParent(hpBarContainer.transform, false);

            var fRect = frameObj.GetComponent<RectTransform>();

            fRect.anchorMin = Vector2.zero;

            fRect.anchorMax = Vector2.one;

            fRect.sizeDelta = Vector2.zero;

            var fImg = frameObj.GetComponent<Image>();

            if (spriteHpFrame != null)

            {

                fImg.sprite = spriteHpFrame;

                fImg.type = Image.Type.Simple;

                fImg.preserveAspect = true;

            }



            // Fill Line (Hp_line)

            var lineObj = new GameObject("HpLineFill", typeof(RectTransform), typeof(Image));

            lineObj.transform.SetParent(hpBarContainer.transform, false);

            var lRect = lineObj.GetComponent<RectTransform>();

            lRect.anchorMin = Vector2.zero;

            lRect.anchorMax = Vector2.one;

            lRect.sizeDelta = Vector2.zero;

            var lImg = lineObj.GetComponent<Image>();

            if (spriteHpLine != null)

            {

                lImg.sprite = spriteHpLine;

                lImg.type = Image.Type.Filled;

                lImg.fillMethod = Image.FillMethod.Horizontal;

                lImg.fillOrigin = (int)Image.OriginHorizontal.Left;

                lImg.fillAmount = 1f;

                lImg.preserveAspect = true;

            }



            // Thông số Giáp Text

            var armorTextObj = new GameObject("ArmorText", typeof(RectTransform), typeof(TextMeshProUGUI));

            armorTextObj.transform.SetParent(hpPanel.transform, false);

            var aRect = armorTextObj.GetComponent<RectTransform>();

            aRect.anchorMin = new Vector2(0f, 0f);

            aRect.anchorMax = new Vector2(1f, 0f);

            aRect.pivot = new Vector2(0f, 0f);

            aRect.anchoredPosition = new Vector2(8f, 6f);

            aRect.sizeDelta = new Vector2(-16f, 20f);

            var aTmp = armorTextObj.GetComponent<TextMeshProUGUI>();

            aTmp.text = "🛡️ GIÁP: 20 / 60 (-19% Sát thương)";

            aTmp.fontSize = 12f;

            aTmp.color = new Color(0.35f, 0.75f, 1.0f);



            // ================= 2. Tâm Ngắm Trung Tâm (Crosshairs/Crosshair_01) =================

            var crosshairObj = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));

            crosshairObj.transform.SetParent(hudRoot.transform, false);

            var chRect = crosshairObj.GetComponent<RectTransform>();

            chRect.anchorMin = new Vector2(0.5f, 0.5f);

            chRect.anchorMax = new Vector2(0.5f, 0.5f);

            chRect.sizeDelta = new Vector2(42f, 42f);

            var chImg = crosshairObj.GetComponent<Image>();

            if (spriteCrosshair != null)

            {

                chImg.sprite = spriteCrosshair;

                chImg.preserveAspect = true;

                chImg.color = new Color(1f, 1f, 1f, 0.85f);

                chImg.raycastTarget = false;

            }



            // ================= 3. Bảng Tab Stats Overlay từ GUI_Parts/Frame_big & name_bar =================

            var tabOverlay = new GameObject("TabStatsOverlayPanel", typeof(RectTransform), typeof(Image));

            tabOverlay.transform.SetParent(hudRoot.transform, false);

            var toRect = tabOverlay.GetComponent<RectTransform>();

            toRect.anchorMin = Vector2.zero;

            toRect.anchorMax = Vector2.one;

            toRect.sizeDelta = Vector2.zero;

            var toImg = tabOverlay.GetComponent<Image>();

            toImg.color = new Color(0f, 0f, 0f, 0.70f);



            var tabBox = new GameObject("StatsWindow", typeof(RectTransform), typeof(Image));

            tabBox.transform.SetParent(tabOverlay.transform, false);

            var tbRect = tabBox.GetComponent<RectTransform>();

            tbRect.anchorMin = new Vector2(0.5f, 0.5f);

            tbRect.anchorMax = new Vector2(0.5f, 0.5f);

            tbRect.sizeDelta = new Vector2(860f, 560f);

            var tbImg = tabBox.GetComponent<Image>();

            if (spriteFrameBig != null)

            {

                tbImg.sprite = spriteFrameBig;

                tbImg.type = Image.Type.Simple;

                tbImg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            }

            else

            {

                tbImg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);

            }



            // Title Bar

            var tabTitleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));

            tabTitleBar.transform.SetParent(tabBox.transform, false);

            var tbrRect = tabTitleBar.GetComponent<RectTransform>();

            tbrRect.anchorMin = new Vector2(0.5f, 0.93f);

            tbrRect.anchorMax = new Vector2(0.5f, 0.93f);

            tbrRect.sizeDelta = new Vector2(600f, 48f);

            var tbrImg = tabTitleBar.GetComponent<Image>();

            if (spriteNameBar != null)

            {

                tbrImg.sprite = spriteNameBar;

                tbrImg.type = Image.Type.Sliced;

            }



            var tabTitle = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));

            tabTitle.transform.SetParent(tabTitleBar.transform, false);

            var ttRect = tabTitle.GetComponent<RectTransform>();

            ttRect.anchorMin = Vector2.zero;

            ttRect.anchorMax = Vector2.one;

            ttRect.sizeDelta = Vector2.zero;

            var ttTmp = tabTitle.GetComponent<TextMeshProUGUI>();

            ttTmp.text = "BẢNG CHỈ SỐ NHÂN VẬT & TRANG BỊ [TAB]";

            ttTmp.fontSize = 20f;

            ttTmp.fontStyle = FontStyles.Bold;

            ttTmp.alignment = TextAlignmentOptions.Center;

            ttTmp.color = new Color(0.0f, 0.92f, 1.0f);



            tabOverlay.SetActive(false);



            // ================= 4. Cụm HUD Đạn Dược & Slot Súng (Góc dưới bên phải) =================

            var weaponPanel = CreateWeaponPanelHierarchy(hudRoot.transform, LoadSprite(FrameMidPath));

            var hudComp = hudRoot.GetComponent<GameplayHUD>();
            var so = new SerializedObject(hudComp);
            so.FindProperty("_crosshairObject").objectReferenceValue = crosshairObj;
            so.FindProperty("_crosshairImage").objectReferenceValue = chImg;
            so.FindProperty("_healthText").objectReferenceValue = hpTmp;
            so.FindProperty("_armorText").objectReferenceValue = aTmp;
            var wpnNameTmp = weaponPanel.transform.Find("WeaponNameText")?.GetComponent<TextMeshProUGUI>();
            var ammoTmp = weaponPanel.transform.Find("AmmoText")?.GetComponent<TextMeshProUGUI>();
            var reloadTmp = weaponPanel.transform.Find("ReloadPromptText")?.GetComponent<TextMeshProUGUI>();
            if (wpnNameTmp != null) so.FindProperty("_weaponNameText").objectReferenceValue = wpnNameTmp;
            if (ammoTmp != null) so.FindProperty("_ammoText").objectReferenceValue = ammoTmp;
            if (reloadTmp != null) so.FindProperty("_reloadPromptText").objectReferenceValue = reloadTmp;
            so.ApplyModifiedProperties();



            PrefabUtility.SaveAsPrefabAsset(hudRoot, PlayerHudPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(hudRoot, ResPlayerHudPrefabPath);

            Object.DestroyImmediate(hudRoot);

            Debug.Log($"[UIPrefabBuilderTool] Đã tạo thành công Prefab Player HUD: {PlayerHudPrefabPath} & {ResPlayerHudPrefabPath}");

        }



        // =================================================================================

        // 6. PREFAB HỆ THỐNG UI TRỌN GÓI CHO MỌI MÀN CHƠI (GAME UI SYSTEM)

        // =================================================================================

        private static void BuildGameUISystemPrefab()
        {
            GameObject systemRoot = new GameObject(
                "GameUI_System",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            var canvas = systemRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            var scaler = systemRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 1. Thêm EventSystem con để chuột tương tác được ở mọi màn chơi
            var eventSystemObj = new GameObject("UI_EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule), typeof(SafeEventSystem));
            eventSystemObj.transform.SetParent(systemRoot.transform, false);

            // 2. Tích hợp PlayerHUD vào GameUI_System
            var playerHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHudPrefabPath);
            if (playerHudPrefab != null)
            {
                var hudChild = (GameObject)PrefabUtility.InstantiatePrefab(playerHudPrefab, systemRoot.transform);
                hudChild.name = "PlayerHUD";
                var c = hudChild.GetComponent<Canvas>();
                if (c != null) Object.DestroyImmediate(c);
                var cs = hudChild.GetComponent<CanvasScaler>();
                if (cs != null) Object.DestroyImmediate(cs);
                var gr = hudChild.GetComponent<GraphicRaycaster>();
                if (gr != null) Object.DestroyImmediate(gr);
            }

            // 3. Tích hợp ShrineChoiceModal vào GameUI_System
            var modalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModalPrefabPath);
            if (modalPrefab != null)
            {
                var modalChild = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, systemRoot.transform);
                modalChild.name = "ShrineChoiceModal";
                var c = modalChild.GetComponent<Canvas>();
                if (c != null) Object.DestroyImmediate(c);
                var cs = modalChild.GetComponent<CanvasScaler>();
                if (cs != null) Object.DestroyImmediate(cs);
                var gr = modalChild.GetComponent<GraphicRaycaster>();
                if (gr != null) Object.DestroyImmediate(gr);
            }

            PrefabUtility.SaveAsPrefabAsset(systemRoot, GameUISystemPrefabPath);

            PrefabUtility.SaveAsPrefabAsset(systemRoot, ResGameUISystemPrefabPath);

            Object.DestroyImmediate(systemRoot);

            Debug.Log($"<color=#00FF88>[UIPrefabBuilderTool] ĐÃ TẠO THÀNH CÔNG PREFAB TRỌN BỘ UI CHO MÀN CHƠI: {GameUISystemPrefabPath} & {ResGameUISystemPrefabPath}</color>");

        }

    }

}

#endif

