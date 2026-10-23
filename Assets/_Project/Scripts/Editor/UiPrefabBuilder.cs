// UiPrefabBuilder : assemble les prefabs d'interface « fonctionnels mais bruts » (§P12.2 du tutoriel).
//
// Menu : Tools → Archery Challenge → Build UI.
// Chaque prefab respecte les noms d'objets attendus par les scripts (voir l'en-tête de chaque script UI).

using ArcheryChallenge.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des prefabs d'interface.</summary>
    public static class UiPrefabBuilder
    {
        private const string Folder = BuildUtils.PrefabRoot + "/UI";
        private static readonly Color Ink = UiFactory.Ink;
        private static readonly Color Gold = UiFactory.Gold;

        [MenuItem("Tools/Archery Challenge/Build UI")]
        public static void BuildAll()
        {
            SpriteFactory.BuildAll();
            FontBuilder.Build();
            UiFactory.Font = FontBuilder.MainFont;

            BuildUtils.EnsureFolder(Folder);
            BuildHud("HudPanel");
            BuildWristHud();
            BuildDesktopHud();
            BuildBriefing();
            BuildResult();
            BuildLeaderboard();
            BuildFastTravel();
            BuildMainMenu();
            BuildPause();
            BuildControls();
            BuildCampfire();
            BuildTraining();
            BuildTutorialBoard();
            BuildToast();
            BuildGlobal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiPrefabBuilder] Prefabs d'interface générés.");
        }

        private static GameObject Save(GameObject root, string name)
        {
            return BuildUtils.SaveAsPrefab(root, $"{Folder}/{name}.prefab");
        }

        private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");

        // ---------------------------------------------------------------- HUD

        private static RectTransform BuildHudContent(Transform parent, float w, float h, bool withStatePanel)
        {
            // Contenu commun : la partie « manche » est dans StatePanel (si demandé), les PV restent hors du panneau d'état.
            RectTransform state = withStatePanel ? UiFactory.Empty(parent, "StatePanel", 0, 0, w, h) : null;
            Transform p = withStatePanel ? state : parent;
            float scale = w / 800f;

            UiFactory.Text(p, "LevelName", "Arène", 30 * scale, Gold, 0, 8 * scale, w, 40 * scale, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(p, "ScoreLabel", "SCORE", 20 * scale, Color.white, 20 * scale, 54 * scale, 240 * scale, 28 * scale);
            UiFactory.Text(p, "ScoreValue", "0", 72 * scale, Color.white, 20 * scale, 78 * scale, 250 * scale, 90 * scale, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(p, "TimeLabel", "TEMPS", 20 * scale, Color.white, 290 * scale, 54 * scale, 220 * scale, 28 * scale, TextAlignmentOptions.Top);
            UiFactory.Text(p, "TimeValue", "0:00", 72 * scale, Color.white, 290 * scale, 78 * scale, 220 * scale, 90 * scale, TextAlignmentOptions.Top, FontStyles.Bold);
            UiFactory.Text(p, "ArrowsLabel", "FLÈCHES", 20 * scale, Color.white, 540 * scale, 54 * scale, 240 * scale, 28 * scale, TextAlignmentOptions.TopRight);
            UiFactory.Text(p, "ArrowsValue", "0", 72 * scale, Color.white, 540 * scale, 78 * scale, 240 * scale, 90 * scale, TextAlignmentOptions.TopRight, FontStyles.Bold);
            UiFactory.Text(p, "ComboValue", "", 34 * scale, Gold, 20 * scale, 176 * scale, 300 * scale, 44 * scale, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Stars(p, "StarsRow", 330 * scale, 176 * scale, 52 * scale);
            UiFactory.Image(p, "WindArrow", SpriteFactory.Arrow, new Color(0.6f, 0.85f, 1f), 560 * scale, 172 * scale, 52 * scale, 52 * scale);
            ((RectTransform)UiBind.Find(p, "WindArrow")).pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Text(p, "WindText", "Pas de vent", 28 * scale, Color.white, 620 * scale, 182 * scale, 180 * scale, 40 * scale);
            UiFactory.Text(p, "ObjectiveText", "", 26 * scale, Color.white, 20 * scale, 240 * scale, w - 40 * scale, 70 * scale);
            UiFactory.Hearts(parent, "HeartsRow", 20 * scale, h - 70 * scale, 40 * scale);
            UiFactory.Text(parent, "CountdownText", "3", 160 * scale, Gold, 0, h * 0.2f, w, h * 0.6f, TextAlignmentOptions.Center, FontStyles.Bold).gameObject.SetActive(false);
            return state;
        }

        private static void BuildHud(string name)
        {
            GameObject canvas = UiFactory.WorldCanvas(name, 800, 400);
            canvas.AddComponent<CanvasGroup>();
            UiFactory.Background(canvas.transform, new Color(0.1f, 0.07f, 0.04f, 0.85f));
            BuildHudContent(canvas.transform, 800, 400, false);
            canvas.AddComponent<HudController>();
            canvas.AddComponent<WorldHudBoard>();   // HUD toujours dans le champ (masqué au PC, qui a l'incrustation ; suit le regard en casque)
            Save(canvas, name);
        }

        private static void BuildWristHud()
        {
            GameObject canvas = UiFactory.WorldCanvas("WristHud", 800, 400);
            canvas.AddComponent<CanvasGroup>();
            UiFactory.Background(canvas.transform, new Color(0.1f, 0.07f, 0.04f, 0.85f));
            BuildHudContent(canvas.transform, 800, 400, true);
            canvas.AddComponent<WristHud>();
            Save(canvas, "WristHud");
        }

        private static void BuildDesktopHud()
        {
            GameObject canvas = UiFactory.OverlayCanvas("DesktopHud", 10);
            canvas.AddComponent<CanvasGroup>();
            Transform root = canvas.transform;

            // Partie « manche » : score, temps, flèches… en haut à gauche, sur un cartouche sombre.
            RectTransform state = UiFactory.Empty(root, "StatePanel", 24, 24, 640, 330);
            Image bg = UiFactory.Image(state, "Background", SpriteFactory.Panel, new Color(0.1f, 0.07f, 0.04f, 0.78f), 0, 0, 640, 330);
            BuildStateContent(state);
            UiFactory.PlaceBottomLeft(UiFactory.Hearts(root, "HeartsRow", 0, 0, 44), 24, 54, 44 * 1.1f * 10f, 44);
            UiFactory.Text(root, "CountdownText", "3", 220, Gold, 560, 300, 800, 300, TextAlignmentOptions.Center, FontStyles.Bold).gameObject.SetActive(false);

            // Réticule et barre de tension au centre de l'écran.
            UiFactory.Image(root, "Crosshair", SpriteFactory.Reticle, new Color(1f, 1f, 1f, 0.9f), 944, 524, 32, 32);
            Image bar = UiFactory.Image(root, "TensionBar", SpriteFactory.Bar, Gold, 760, 940, 400, 18);
            UiFactory.PlaceBottomCenter(bar.rectTransform, 175, 400, 18);
            bar.type = UnityEngine.UI.Image.Type.Filled;
            bar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            bar.fillOrigin = 0;
            bar.fillAmount = 0f;

            // Aides de touches (toutes ancrées en bas de l'écran) :
            //  - « KeyBar » : bande permanente avec les touches principales ;
            //  - « ContextHint » : la touche utile ici et maintenant (« E : équiper l'arc Chasse »...) ;
            //  - « Prompt » : astuce passagère.
            Image strip = UiFactory.Image(root, "KeyBarBackground", SpriteFactory.Bar, new Color(0.05f, 0.04f, 0.03f, 0.55f), 0, 0, 10, 10);
            UiFactory.PlaceBottomStrip(strip.rectTransform, 40);
            TextMeshProUGUI keyBar = UiFactory.Text(root, "KeyBar", ControlsCatalog.DesktopBar, 22, new Color(1f, 1f, 1f, 0.92f), 0, 0, 10, 10, TextAlignmentOptions.Center);
            UiFactory.PlaceBottomStrip(keyBar.rectTransform, 40);
            keyBar.richText = true;
            keyBar.textWrappingMode = TextWrappingModes.NoWrap;
            keyBar.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI context = UiFactory.Text(root, "ContextHint", "", 40, Color.white, 0, 0, 10, 10, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceBottomCenter(context.rectTransform, 215, 1500, 64);
            context.gameObject.SetActive(false);
            TextMeshProUGUI prompt = UiFactory.Text(root, "Prompt", "", 30, Gold, 0, 0, 10, 10, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.PlaceBottomCenter(prompt.rectTransform, 112, 1500, 50);
            prompt.textWrappingMode = TextWrappingModes.NoWrap;
            prompt.gameObject.SetActive(false);

            canvas.AddComponent<DesktopHud>();
            Save(canvas, "DesktopHud");
        }

        // Disposition de la partie « manche » du HUD (même noms que le HUD d'arène).
        private static void BuildStateContent(Transform p)
        {
            UiFactory.Text(p, "LevelName", "Arène", 36, Gold, 20, 8, 600, 44, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(p, "ScoreLabel", "SCORE", 22, Color.white, 20, 56, 200, 28);
            UiFactory.Text(p, "ScoreValue", "0", 78, Color.white, 20, 80, 200, 90, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(p, "TimeLabel", "TEMPS", 22, Color.white, 230, 56, 180, 28, TextAlignmentOptions.Top);
            UiFactory.Text(p, "TimeValue", "0:00", 78, Color.white, 220, 80, 200, 90, TextAlignmentOptions.Top, FontStyles.Bold);
            UiFactory.Text(p, "ArrowsLabel", "FLÈCHES", 22, Color.white, 430, 56, 190, 28, TextAlignmentOptions.TopRight);
            UiFactory.Text(p, "ArrowsValue", "0", 78, Color.white, 430, 80, 190, 90, TextAlignmentOptions.TopRight, FontStyles.Bold);
            UiFactory.Text(p, "ComboValue", "", 36, Gold, 20, 176, 260, 46, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Stars(p, "StarsRow", 290, 176, 48);
            UiFactory.Image(p, "WindArrow", SpriteFactory.Arrow, new Color(0.6f, 0.85f, 1f), 470, 172, 46, 46);
            ((RectTransform)UiBind.Find(p, "WindArrow")).pivot = new Vector2(0.5f, 0.5f);
            UiFactory.Text(p, "WindText", "Pas de vent", 28, Color.white, 530, 180, 100, 40);
            UiFactory.Text(p, "ObjectiveText", "", 26, Color.white, 20, 238, 600, 80);
        }

        // ---------------------------------------------------------------- briefing et résultat

        private static void BuildBriefing()
        {
            GameObject canvas = UiFactory.WorldCanvas("BriefingPanel", 1000, 600);
            RectTransform root = UiFactory.Empty(canvas.transform, "Root", 0, 0, 1000, 600);
            UiFactory.Stretch(root);
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Arène", 64, Ink, 40, 24, 920, 80, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "Objective", "", 34, Ink, 50, 120, 900, 150);
            UiFactory.Text(root, "Details", "", 32, Ink, 50, 280, 440, 170);
            UiFactory.Text(root, "BestScores", "", 30, Ink, 520, 280, 430, 170);
            UiFactory.Text(root, "LockMessage", "Verrouillée", 44, new Color(0.6f, 0.15f, 0.1f), 60, 150, 880, 250, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "Hint", "Tirez sur la cible DÉPART pour commencer.", 34, UiFactory.Forest, 50, 500, 900, 70, TextAlignmentOptions.Center, FontStyles.Bold);
            canvas.AddComponent<BriefingPanel>();
            Save(canvas, "BriefingPanel");
        }

        private static void BuildResult()
        {
            GameObject canvas = UiFactory.WorldCanvas("LevelResultPanel", 1000, 600);
            RectTransform root = UiFactory.Empty(canvas.transform, "Root", 0, 0, 1000, 600);
            UiFactory.Stretch(root);
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Arène réussie !", 60, Ink, 40, 16, 920, 76, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "ScoreValue", "0", 110, UiFactory.Forest, 40, 90, 400, 130, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Stars(root, "StarsRow", 90, 215, 80);
            UiFactory.Text(root, "AccuracyValue", "", 30, Ink, 40, 310, 440, 40, TextAlignmentOptions.Center);
            UiFactory.Text(root, "DetailText", "", 30, Ink, 500, 95, 470, 170);
            UiFactory.Text(root, "RewardsText", "", 32, new Color(0.55f, 0.35f, 0.05f), 500, 265, 470, 110, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "NameLabel", "Votre nom (1 à 20 caractères)", 28, Ink, 40, 385, 500, 36);
            UiFactory.InputField(root, "NameInput", "Votre nom", 40, 420, 460, 66);
            UiFactory.Button(root, "SaveButton", "Enregistrer", 520, 420, 220, 66, 30);
            UiFactory.Text(root, "SaveMessage", "", 26, UiFactory.Forest, 760, 410, 220, 90);
            UiFactory.Button(root, "RetryButton", "Rejouer", 40, 510, 300, 70, 32);
            UiFactory.Button(root, "ContinueButton", "Continuer", 660, 510, 300, 70, 32);
            canvas.AddComponent<LevelResultPanel>();
            Save(canvas, "LevelResultPanel");
        }

        // ---------------------------------------------------------------- classement et voyage

        private static void BuildLeaderboard()
        {
            GameObject canvas = UiFactory.WorldCanvas("LeaderboardPanel", 1000, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Classement", 52, Ink, 40, 10, 920, 64, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BtnPrevLevel", "<", 40, 76, 70, 56, 36);
            UiFactory.Text(root, "LevelLabel", "Arène", 36, Ink, 120, 76, 360, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BtnNextLevel", ">", 490, 76, 70, 56, 36);
            UiFactory.Button(root, "BtnDifficulty", "Difficulté", 590, 76, 150, 56, 22);
            UiFactory.Text(root, "DifficultyLabel", "Toutes difficultés", 28, Ink, 750, 82, 230, 48, TextAlignmentOptions.MidlineLeft);
            // En-têtes
            UiFactory.Text(root, "HeadRank", "#", 24, new Color(Ink.r, Ink.g, Ink.b, 0.7f), 50, 138, 60, 30);
            UiFactory.Text(root, "HeadName", "Joueur", 24, new Color(Ink.r, Ink.g, Ink.b, 0.7f), 120, 138, 400, 30);
            UiFactory.Text(root, "HeadScore", "Score", 24, new Color(Ink.r, Ink.g, Ink.b, 0.7f), 540, 138, 180, 30, TextAlignmentOptions.TopRight);
            UiFactory.Text(root, "HeadDiff", "Difficulté", 24, new Color(Ink.r, Ink.g, Ink.b, 0.7f), 760, 138, 200, 30);
            for (int i = 0; i < 10; i++)
            {
                RectTransform row = UiFactory.Empty(root, "Row" + i, 40, 170 + i * 35, 920, 34);
                UiFactory.Text(row, "Rank", (i + 1).ToString(), 26, Ink, 10, 0, 60, 34, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UiFactory.Text(row, "Name", "-", 26, Ink, 80, 0, 420, 34, TextAlignmentOptions.MidlineLeft);
                UiFactory.Text(row, "Score", "0", 26, Ink, 500, 0, 180, 34, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UiFactory.Text(row, "Difficulty", "-", 24, Ink, 720, 0, 190, 34, TextAlignmentOptions.MidlineLeft);
            }
            UiFactory.Text(root, "EmptyText", "", 34, Ink, 100, 250, 800, 160, TextAlignmentOptions.Center);
            UiFactory.Button(root, "BtnClear", "Effacer le classement", 40, 540, 420, 50, 24);
            UiFactory.Button(root, "BtnClose", "X", 920, 14, 56, 56, 28);
            canvas.AddComponent<LeaderboardPanel>();
            Save(canvas, "LeaderboardPanel");
        }

        /// <summary>Hauteur du panneau de voyage rapide : 7 destinations de 60 + 6 intervalles de 8, sous le titre, sans bouton « Fermer ».</summary>
        public const float FastTravelHeight = 620f;

        private static void BuildFastTravel()
        {
            GameObject canvas = UiFactory.WorldCanvas("FastTravelPanel", 700, FastTravelHeight);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Téléportation rapide", 50, Ink, 30, 14, 640, 64, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "Message", "", 28, Ink, 40, 84, 620, 40, TextAlignmentOptions.Center);
            RectTransform content = UiFactory.VerticalList(root, "Content", 100, 130, 500, 470, 8);
            Button template = UiFactory.Button(content, "ButtonTemplate", "Destination", 0, 0, 500, 60, 30);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            template.gameObject.SetActive(false);
            UiFactory.Button(root, "BtnClose", "X", 630, 12, 54, 50, 26);
            canvas.AddComponent<FastTravelPanel>();
            Save(canvas, "FastTravelPanel");
        }

        // ---------------------------------------------------------------- menu principal

        private static void BuildMainMenu()
        {
            GameObject canvas = UiFactory.WorldCanvas("MainMenuPanel", 1000, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Archery Challenge", 56, Ink, 40, 10, 920, 70, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BtnClose", "X", 920, 14, 56, 56, 28);

            // --- Page principale : 6 × 2 boutons ---
            RectTransform main = UiFactory.Empty(root, "Page_Main", 0, 90, 1000, 500);
            string[,] mainButtons =
            {
                { "BtnPlay", "Jouer la campagne" }, { "BtnTraining", "Entraînement" },
                { "BtnDifficulty", "Difficulté" }, { "BtnEquipment", "Arc et flèches" },
                { "BtnVolume", "Volume" }, { "BtnComfort", "Confort" },
                { "BtnTime", "Jour et nuit" }, { "BtnProfile", "Profil et sauvegarde" },
                { "BtnLeaderboard", "Classement" }, { "BtnFastTravel", "Voyage rapide" },
                { "BtnControls", "Commandes" }, { "BtnQuit", "Quitter" }
            };
            for (int i = 0; i < 12; i++)
            {
                int col = i % 2, row = i / 2;
                UiFactory.Button(main, mainButtons[i, 0], mainButtons[i, 1], 60 + col * 450, 4 + row * 80, 430, 70, 32);
            }

            // --- Difficulté ---
            RectTransform diff = UiFactory.Empty(root, "Page_Difficulty", 0, 90, 1000, 500);
            PageTitle(diff, "Difficulté");
            UiFactory.Button(diff, "BtnEasy", "Facile", 60, 80, 270, 90, 38);
            UiFactory.Button(diff, "BtnNormal", "Normal", 365, 80, 270, 90, 38);
            UiFactory.Button(diff, "BtnHard", "Difficile", 670, 80, 270, 90, 38);
            UiFactory.Text(diff, "DifficultyDescription", "", 32, Ink, 70, 200, 860, 190);
            Back(diff);

            // --- Équipement ---
            RectTransform eq = UiFactory.Empty(root, "Page_Equipment", 0, 90, 1000, 500);
            PageTitle(eq, "Arc et flèches");
            UiFactory.Button(eq, "BowPrev", "<", 40, 90, 70, 70, 40);
            UiFactory.Text(eq, "BowName", "Arc", 42, Ink, 120, 80, 760, 50, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(eq, "BowStats", "", 26, Ink, 120, 130, 760, 90, TextAlignmentOptions.Center);
            UiFactory.Button(eq, "BowNext", ">", 890, 90, 70, 70, 40);
            UiFactory.Button(eq, "ArrowPrev", "<", 40, 250, 70, 70, 40);
            UiFactory.Text(eq, "ArrowName", "Flèche", 42, Ink, 120, 240, 760, 50, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(eq, "ArrowStats", "", 26, Ink, 120, 290, 760, 90, TextAlignmentOptions.Center);
            UiFactory.Button(eq, "ArrowNext", ">", 890, 250, 70, 70, 40);
            Back(eq);

            // --- Volume ---
            RectTransform vol = UiFactory.Empty(root, "Page_Volume", 0, 90, 1000, 500);
            PageTitle(vol, "Volume");
            string[,] sliders =
            {
                { "SliderMaster", "Général" }, { "SliderMusic", "Musique" }, { "SliderSfx", "Effets" },
                { "SliderAmbience", "Ambiance" }, { "SliderUi", "Interface" }
            };
            for (int i = 0; i < 5; i++)
            {
                float y = 70 + i * 64;
                UiFactory.Text(vol, sliders[i, 0] + "Label", sliders[i, 1], 32, Ink, 80, y, 230, 50, TextAlignmentOptions.MidlineLeft);
                UiFactory.Slider(vol, sliders[i, 0], 330, y + 6, 450, 40);
                UiFactory.Text(vol, sliders[i, 0] + "Value", "100 %", 30, Ink, 800, y, 140, 50, TextAlignmentOptions.MidlineLeft);
            }
            Back(vol);

            // --- Confort ---
            RectTransform comfort = UiFactory.Empty(root, "Page_Comfort", 0, 90, 1000, 500);
            PageTitle(comfort, "Confort");
            UiFactory.Toggle(comfort, "ToggleVignette", "Vignette anti-nausée", 60, 70, 440, 56);
            UiFactory.Toggle(comfort, "ToggleSnapTurn", "Rotation par pas de 45°", 520, 70, 440, 56);
            UiFactory.Toggle(comfort, "ToggleAutoNock", "Encochage automatique", 60, 140, 440, 56);
            UiFactory.Toggle(comfort, "ToggleBowLeftHand", "Arc dans la main gauche", 520, 140, 440, 56);
            UiFactory.Text(comfort, "MoveSpeedLabel", "Vitesse de marche", 30, Ink, 60, 220, 330, 50, TextAlignmentOptions.MidlineLeft);
            UiFactory.Slider(comfort, "SliderMoveSpeed", 400, 226, 500, 40);
            UiFactory.Text(comfort, "SeatedLabel", "Hauteur (assis)", 30, Ink, 60, 290, 330, 50, TextAlignmentOptions.MidlineLeft);
            UiFactory.Slider(comfort, "SliderSeatedHeight", 400, 296, 500, 40);
            Back(comfort);

            // --- Jour et nuit ---
            RectTransform time = UiFactory.Empty(root, "Page_Time", 0, 90, 1000, 500);
            PageTitle(time, "Jour et nuit");
            UiFactory.Toggle(time, "ToggleCycle", "Le temps s'écoule (journée de 20 min)", 60, 80, 880, 56);
            UiFactory.Toggle(time, "ToggleFixedHour", "Figer l'heure", 60, 160, 500, 56);
            UiFactory.Button(time, "BtnHourMinus", "- 1 h", 140, 250, 200, 80, 34);
            UiFactory.Text(time, "HourLabel", "09 h 00", 54, Ink, 370, 250, 260, 80, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(time, "BtnHourPlus", "+ 1 h", 660, 250, 200, 80, 34);
            Back(time);

            // --- Profil ---
            RectTransform profile = UiFactory.Empty(root, "Page_Profile", 0, 90, 1000, 500);
            PageTitle(profile, "Profil et sauvegarde");
            UiFactory.Text(profile, "ProfileLabel", "", 36, Ink, 80, 90, 840, 130, TextAlignmentOptions.Center);
            UiFactory.Button(profile, "BtnChangeProfile", "Changer de profil ou d'emplacement", 150, 240, 700, 80, 32);
            UiFactory.Button(profile, "BtnSaveNow", "Sauvegarder maintenant", 150, 340, 700, 80, 32);
            Back(profile);

            // Seule la page principale est visible au départ.
            foreach (string page in new[] { "Page_Difficulty", "Page_Equipment", "Page_Volume", "Page_Comfort", "Page_Time", "Page_Profile" })
                UiBind.Find(root, page).gameObject.SetActive(false);

            canvas.AddComponent<MainMenuController>();
            Save(canvas, "MainMenuPanel");
        }

        private static void PageTitle(Transform page, string text)
        {
            UiFactory.Text(page, "PageTitle", text, 44, UiFactory.Forest, 40, 0, 920, 60, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private static void Back(Transform page)
        {
            UiFactory.Button(page, "BackButton", "Retour", 380, 420, 240, 64, 30);
        }

        // ---------------------------------------------------------------- pause

        private static void BuildPause()
        {
            GameObject canvas = UiFactory.WorldCanvas("PauseMenu", 700, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Pause", 52, Ink, 30, 10, 640, 64, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "Status", "", 26, Ink, 40, 76, 620, 60, TextAlignmentOptions.Center);
            string[,] buttons =
            {
                { "BtnResume", "Reprendre" }, { "BtnRestart", "Recommencer l'arène" }, { "BtnAbandon", "Abandonner la manche" },
                { "BtnFastTravel", "Téléportation rapide" }, { "BtnSettings", "Réglages" }, { "BtnControls", "Commandes" },
                { "BtnHub", "Retour au village" }
            };
            for (int i = 0; i < 7; i++)
                UiFactory.Button(root, buttons[i, 0], buttons[i, 1], 120, 140 + i * 63, 460, 54, 28);
            UiFactory.Button(root, "BtnClose", "X", 630, 12, 54, 50, 26);
            canvas.AddComponent<PauseMenuController>();
            Save(canvas, "PauseMenu");
        }

        // ---------------------------------------------------------------- commandes

        private static void BuildControls()
        {
            GameObject canvas = UiFactory.WorldCanvas("ControlsPanel", 1000, 680);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Commandes", 46, Ink, 40, 8, 920, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BtnClose", "X", 930, 12, 54, 50, 26);

            UiFactory.Button(root, "BtnPageGame", "Jeu", 40, 66, 150, 44, 24);
            UiFactory.Button(root, "BtnPageVr", "Gameplay VR", 200, 66, 220, 44, 24);
            UiFactory.Button(root, "BtnPageSimulator", "Simulateur XR", 430, 66, 220, 44, 24);
            UiFactory.Button(root, "BtnPageDev", "Développement", 660, 66, 250, 44, 24);

            Color head = new Color(Ink.r, Ink.g, Ink.b, 0.75f);
            UiFactory.Text(root, "HeadAction", "Action", 20, head, 50, 118, 300, 28, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "HeadDesktop", "Clavier / souris", 20, head, 350, 118, 300, 28, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "HeadVr", "Manettes VR (Quest)", 20, head, 650, 118, 330, 28, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            for (int i = 0; i < ControlsPanel.RowCount; i++)
            {
                RectTransform row = UiFactory.Empty(root, "Row" + i, 40, 150 + i * 32, 940, 32);
                if (i % 2 == 1)
                    UiFactory.Image(row, "Stripe", SpriteFactory.Bar, new Color(0.45f, 0.31f, 0.17f, 0.14f), 0, 0, 940, 32);
                UiFactory.Text(row, "Action", "-", 18, Ink, 10, 0, 300, 32, TextAlignmentOptions.MidlineLeft);
                UiFactory.Text(row, "Desktop", "-", 18, UiFactory.Forest, 310, 0, 300, 32, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UiFactory.Text(row, "Vr", "-", 17, Ink, 610, 0, 330, 32, TextAlignmentOptions.MidlineLeft);
            }
            canvas.AddComponent<ControlsPanel>();
            Save(canvas, "ControlsPanel");
        }

        // ---------------------------------------------------------------- feu de camp

        private static void BuildCampfire()
        {
            GameObject canvas = UiFactory.WorldCanvas("CampfireProfileMenu", 1000, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Au coin du feu", 56, Ink, 40, 10, 920, 70, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BtnClose", "X", 920, 14, 56, 56, 28);

            // Page des profils.
            RectTransform profiles = UiFactory.Empty(root, "PageProfiles", 0, 90, 1000, 510);
            UiFactory.Text(profiles, "Message", "", 30, Ink, 40, 0, 920, 44, TextAlignmentOptions.Center);
            RectTransform list = UiFactory.VerticalList(profiles, "Content", 40, 56, 500, 440, 8);
            Button template = UiFactory.Button(list, "ProfileButtonTemplate", "Profil", 0, 0, 500, 62, 30);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            template.gameObject.SetActive(false);
            UiFactory.Text(profiles, "NewLabel", "Nouveau profil", 34, UiFactory.Forest, 580, 60, 380, 44, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.InputField(profiles, "NameInput", "Votre nom", 580, 112, 380, 66);
            UiFactory.Button(profiles, "BtnCreate", "Créer le profil", 580, 196, 380, 70, 30);

            // Page des emplacements.
            RectTransform slots = UiFactory.Empty(root, "PageSlots", 0, 90, 1000, 510);
            UiFactory.Text(slots, "SlotsTitle", "Profil", 40, UiFactory.Forest, 40, 0, 920, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            for (int i = 0; i < 3; i++)
            {
                RectTransform slot = UiFactory.Empty(slots, "Slot" + (i + 1), 40, 70 + i * 100, 920, 88);
                UiFactory.Image(slot, "Bg", SpriteFactory.Panel, new Color(1f, 1f, 1f, 0.7f), 0, 0, 920, 88);
                UiFactory.Text(slot, "Label", "Emplacement", 28, Ink, 20, 0, 500, 88, TextAlignmentOptions.MidlineLeft);
                UiFactory.Button(slot, "BtnContinue", "Continuer", 530, 12, 180, 64, 26);
                UiFactory.Button(slot, "BtnNew", "Nouvelle partie", 725, 12, 180, 64, 22);
            }
            UiFactory.Button(slots, "BtnBackProfiles", "Retour", 40, 400, 240, 64, 28);
            UiFactory.Button(slots, "BtnDeleteProfile", "Supprimer le profil", 640, 400, 320, 64, 26);

            // Confirmation.
            RectTransform confirm = UiFactory.Empty(root, "PageConfirm", 0, 90, 1000, 510);
            UiFactory.Text(confirm, "ConfirmText", "Confirmer ?", 42, Ink, 80, 100, 840, 180, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(confirm, "BtnConfirmYes", "Oui", 180, 320, 260, 80, 36);
            UiFactory.Button(confirm, "BtnConfirmNo", "Non", 560, 320, 260, 80, 36);

            // Fin de sélection (tableau du camp).
            RectTransform done = UiFactory.Empty(root, "PageDone", 0, 90, 1000, 510);
            UiFactory.Text(done, "DoneText", "", 42, Ink, 80, 90, 840, 250, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(done, "BtnChangeProfile", "Changer de profil", 300, 360, 400, 80, 32);

            slots.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
            done.gameObject.SetActive(false);

            canvas.AddComponent<CampfireProfileMenu>();
            Save(canvas, "CampfireProfileMenu");
        }

        // ---------------------------------------------------------------- entraînement

        private static void BuildTraining()
        {
            GameObject canvas = UiFactory.WorldCanvas("TrainingPanel", 1000, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Entraînement", 52, Ink, 40, 10, 920, 64, TextAlignmentOptions.Center, FontStyles.Bold);

            UiFactory.Text(root, "PointsLabel", "Dernier tir", 26, Ink, 40, 84, 300, 34);
            UiFactory.Text(root, "LastPoints", "-", 100, UiFactory.Forest, 40, 112, 300, 120, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "LastDistance", "-", 32, Ink, 40, 240, 300, 40);
            UiFactory.Text(root, "LastSpeed", "-", 32, Ink, 40, 282, 300, 40);
            UiFactory.Text(root, "LastTension", "-", 32, Ink, 40, 324, 300, 40);
            UiFactory.Text(root, "ShotCount", "0 tirs", 28, Ink, 40, 372, 300, 40);

            // Mini-blason des impacts.
            Image map = UiFactory.Image(root, "ImpactMiniMap", SpriteFactory.Target, Color.white, 360, 90, 280, 280);
            map.gameObject.AddComponent<ImpactMiniMap>();
            Image dotSprite = null; // le sprite de point est assigné ci-dessous
            BuildUtils.Set(map.GetComponent<ImpactMiniMap>(), "dotSprite", SpriteFactory.Round);

            // Équipement et vent.
            UiFactory.Text(root, "BowLabel", "Arc", 26, Ink, 670, 84, 300, 34);
            UiFactory.Button(root, "BowPrev", "<", 670, 116, 56, 56, 30);
            UiFactory.Text(root, "BowName", "-", 26, Ink, 730, 116, 180, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "BowNext", ">", 914, 116, 56, 56, 30);
            UiFactory.Text(root, "ArrowLabel", "Flèches", 26, Ink, 670, 190, 300, 34);
            UiFactory.Button(root, "ArrowPrev", "<", 670, 222, 56, 56, 30);
            UiFactory.Text(root, "ArrowName", "-", 26, Ink, 730, 222, 180, 56, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Button(root, "ArrowNext", ">", 914, 222, 56, 56, 30);
            UiFactory.Text(root, "WindLabel", "Vent : aucun", 28, Ink, 670, 296, 300, 36);
            UiFactory.Button(root, "BtnWind", "Changer le vent", 670, 336, 300, 56, 26);
            UiFactory.Button(root, "BtnReset", "Réinitialiser", 670, 410, 300, 56, 26);
            UiFactory.Text(root, "Hint", "Tirez librement : flèches et temps illimités.\nTout l'équipement est utilisable ici.", 26, UiFactory.Forest, 40, 500, 920, 80, TextAlignmentOptions.Center, FontStyles.Bold);

            canvas.AddComponent<TrainingStatsPanel>();
            Save(canvas, "TrainingPanel");
        }

        private static void BuildTutorialBoard()
        {
            GameObject canvas = UiFactory.WorldCanvas("TutorialBoard", 1000, 600);
            Transform root = canvas.transform;
            UiFactory.Background(root);
            UiFactory.Text(root, "Title", "Comment tirer ?", 54, Ink, 40, 8, 920, 66, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Text(root, "VrTitle", "En réalité virtuelle", 36, UiFactory.Forest, 40, 80, 440, 44, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "VrText",
                "1. Main droite près de l'épaule + Grip : l'arc arrive dans la main.\n" +
                "2. Autre main dans le dos + Grip : une flèche apparaît.\n" +
                "3. Approchez la flèche de la corde : elle s'encoche.\n" +
                "4. Reculez la main en gardant le Grip : la corde se tend.\n" +
                "5. Relâchez le Grip : la flèche part !\n" +
                "Stick gauche : marcher · stick droit : tourner / se téléporter.",
                25, Ink, 40, 128, 460, 440);
            UiFactory.Text(root, "PcTitle", "Au clavier et à la souris", 36, UiFactory.Forest, 520, 80, 440, 44, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiFactory.Text(root, "PcText",
                "Z Q S D (ou W A S D) : marcher · Maj : courir · Espace : sauter\n" +
                "R : prendre et encocher une flèche\n" +
                "Clic gauche maintenu : tendre la corde\n" +
                "Relâcher : tirer\n" +
                "Clic droit : viser (zoom)\n" +
                "E : interagir · T : voyage rapide · Échap : menu\n" +
                "Astuce : visez un peu au-dessus de la cible, la flèche tombe !",
                25, Ink, 520, 128, 460, 440);
            Save(canvas, "TutorialBoard");
        }

        // ---------------------------------------------------------------- bandeau et global

        private static void BuildToast()
        {
            GameObject canvas = UiFactory.WorldCanvas("ToastBanner", 1000, 120, false);
            canvas.GetComponent<WorldCanvasCameraBinder>().enabled = false;
            canvas.AddComponent<CanvasGroup>().blocksRaycasts = false;
            // « Bar » est le bandeau lui-même : en superposition (PC) il ne couvre qu'une barre en haut de l'écran,
            // pas tout l'écran (ToastBannerView règle ses ancres selon le mode).
            RectTransform bar = UiFactory.Empty(canvas.transform, "Bar", 0, 0, 1000, 120);
            UiFactory.Stretch(bar);
            UiFactory.Image(bar, "Background", SpriteFactory.Panel, new Color(0.1f, 0.07f, 0.04f, 0.88f), 0, 0, 1000, 120);
            UiFactory.Stretch((RectTransform)UiBind.Find(bar, "Background"));
            RectTransform text = UiFactory.Text(bar, "ToastText", "", 40, Gold, 20, 0, 960, 120, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform;
            UiFactory.Stretch(text).offsetMin = new Vector2(20f, 0f);
            text.offsetMax = new Vector2(-20f, 0f);
            // Superposition à l'échelle de l'écran (ignorée tant que le Canvas est dans le monde).
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.AddComponent<ToastBannerView>();
            Save(canvas, "ToastBanner");
        }

        private static void BuildGlobal()
        {
            GameObject root = new GameObject("UI_Global");
            UiHub hub = root.AddComponent<UiHub>();

            GameObject pause = Instance("PauseMenu", root.transform, true);
            GameObject main = Instance("MainMenuPanel", root.transform, true);
            GameObject leaderboard = Instance("LeaderboardPanel", root.transform, true);
            GameObject travel = Instance("FastTravelPanel", root.transform, true);
            GameObject campfire = Instance("CampfireProfileMenu", root.transform, true);
            GameObject controls = Instance("ControlsPanel", root.transform, true);
            foreach (GameObject panel in new[] { pause, main, leaderboard, travel, campfire, controls })
            {
                panel.AddComponent<LazyFollow>();
                UiPanel ui = panel.GetComponent<UiPanel>();
                BuildUtils.Set(ui, "hiddenUntilOpened", true);
                panel.SetActive(false);
            }

            Instance("DesktopHud", root.transform, false);
            GameObject wrist = Instance("WristHud", root.transform, false);
            Instance("ToastBanner", root.transform, false);
            wrist.name = "WristHud";

            BuildUtils.Set(hub, "profileMenu", campfire.GetComponent<CampfireProfileMenu>());
            BuildUtils.Set(hub, "mainMenu", main.GetComponent<MainMenuController>());
            BuildUtils.Set(hub, "leaderboard", leaderboard.GetComponent<LeaderboardPanel>());
            BuildUtils.Set(hub, "fastTravel", travel.GetComponent<FastTravelPanel>());
            BuildUtils.Set(hub, "pauseMenu", pause.GetComponent<PauseMenuController>());
            BuildUtils.Set(hub, "controls", controls.GetComponent<ControlsPanel>());
            Save(root, "UI_Global");
        }

        private static GameObject Instance(string prefabName, Transform parent, bool global)
        {
            GameObject prefab = Load(prefabName);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return instance;
        }
    }
}
