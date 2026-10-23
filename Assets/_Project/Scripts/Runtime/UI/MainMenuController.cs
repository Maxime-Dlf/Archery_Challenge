// MainMenuController : menu principal (jouer, difficulté, équipement, volume, confort, jour/nuit, profil).
//
// OÙ LE POSER : sur le panneau du prefab « MainMenuPanel » (tableau du hub, et instance globale dans UI_Global).
// PAGES (objets enfants, un seul visible à la fois) : Page_Main, Page_Difficulty, Page_Equipment, Page_Volume,
// Page_Comfort, Page_Time, Page_Profile ; chaque page a un BackButton.
// ÉLÉMENTS ATTENDUS (noms d'objets) :
//   Page_Main      BtnPlay, BtnTraining, BtnDifficulty, BtnEquipment, BtnVolume, BtnComfort, BtnTime, BtnProfile,
//                  BtnLeaderboard, BtnControls (commandes), BtnFastTravel, BtnQuit
//   Page_Difficulty BtnEasy, BtnNormal, BtnHard, DifficultyDescription
//   Page_Equipment BowName, BowStats, BowPrev, BowNext, ArrowName, ArrowStats, ArrowPrev, ArrowNext
//   Page_Volume    SliderMaster, SliderMusic, SliderSfx, SliderAmbience, SliderUi (+ MasterValue … textes)
//   Page_Comfort   ToggleVignette, ToggleSnapTurn, ToggleAutoNock, ToggleBowLeftHand, SliderMoveSpeed, SliderSeatedHeight
//   Page_Time      ToggleCycle, ToggleFixedHour, BtnHourMinus, BtnHourPlus, HourLabel
//   Page_Profile   ProfileLabel, BtnChangeProfile, BtnSaveNow
//
// Chaque réglage agit immédiatement et est enregistré dans settings.json.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using ArcheryChallenge.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Menu principal et réglages.</summary>
    public class MainMenuController : UiPanel
    {
        private static readonly string[] PageNames =
            { "Page_Main", "Page_Difficulty", "Page_Equipment", "Page_Volume", "Page_Comfort", "Page_Time", "Page_Profile" };

        private readonly System.Collections.Generic.Dictionary<string, GameObject> pages = new System.Collections.Generic.Dictionary<string, GameObject>();
        private bool bound;
        private bool syncing;

        // Éléments
        private TMP_Text difficultyDescription, bowName, bowStats, arrowName, arrowStats, hourLabel, profileLabel;
        private Slider sMaster, sMusic, sSfx, sAmbience, sUi, sMoveSpeed, sSeated;
        private Toggle tVignette, tSnap, tAutoNock, tLeft, tCycle, tFixed;
        private Button bEasy, bNormal, bHard;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        private SettingsService Settings => Services.Get<SettingsService>();

        private void Bind()
        {
            if (bound) return;
            bound = true;

            foreach (string name in PageNames)
            {
                Transform t = UiBind.Find(transform, name);
                if (t != null)
                    pages[name] = t.gameObject;
            }
            foreach (var kv in pages)
            {
                Button back = UiBind.Button(kv.Value.transform, "BackButton", false);
                if (back != null && kv.Key != "Page_Main")
                    back.onClick.AddListener(() => ShowPage("Page_Main"));
            }

            // Croix « X » en haut à droite : ferme le menu (et revient à la pause s'il en venait).
            UiBind.Button(transform, "BtnClose", false)?.onClick.AddListener(CloseFromButton);

            // --- Page principale ---
            Click("BtnPlay", OnPlay);
            Click("BtnTraining", () => Travel("training"));
            Click("BtnDifficulty", () => ShowPage("Page_Difficulty"));
            Click("BtnEquipment", () => ShowPage("Page_Equipment"));
            Click("BtnVolume", () => ShowPage("Page_Volume"));
            Click("BtnComfort", () => ShowPage("Page_Comfort"));
            Click("BtnTime", () => ShowPage("Page_Time"));
            Click("BtnProfile", () => ShowPage("Page_Profile"));
            Click("BtnLeaderboard", () => Services.Get<UiHub>()?.OpenLeaderboard());
            Click("BtnControls", () => Services.Get<UiHub>()?.OpenControls());
            Click("BtnFastTravel", () => Services.Get<UiHub>()?.OpenFastTravel());
            Click("BtnQuit", OnQuit);

            // --- Difficulté ---
            bEasy = UiBind.Button(transform, "BtnEasy", false);
            bNormal = UiBind.Button(transform, "BtnNormal", false);
            bHard = UiBind.Button(transform, "BtnHard", false);
            bEasy?.onClick.AddListener(() => SetDifficulty(GameDifficulty.Easy));
            bNormal?.onClick.AddListener(() => SetDifficulty(GameDifficulty.Normal));
            bHard?.onClick.AddListener(() => SetDifficulty(GameDifficulty.Hard));
            difficultyDescription = UiBind.Text(transform, "DifficultyDescription", false);

            // --- Équipement ---
            bowName = UiBind.Text(transform, "BowName", false);
            bowStats = UiBind.Text(transform, "BowStats", false);
            arrowName = UiBind.Text(transform, "ArrowName", false);
            arrowStats = UiBind.Text(transform, "ArrowStats", false);
            Click("BowPrev", () => Services.Get<EquipmentService>()?.CycleBow(-1));
            Click("BowNext", () => Services.Get<EquipmentService>()?.CycleBow(1));
            Click("ArrowPrev", () => Services.Get<EquipmentService>()?.CycleArrow(-1));
            Click("ArrowNext", () => Services.Get<EquipmentService>()?.CycleArrow(1));

            // --- Volume ---
            sMaster = BindSlider("SliderMaster", v => Edit(d => d.masterVolume = v));
            sMusic = BindSlider("SliderMusic", v => Edit(d => d.musicVolume = v));
            sSfx = BindSlider("SliderSfx", v => Edit(d => d.sfxVolume = v));
            sAmbience = BindSlider("SliderAmbience", v => Edit(d => d.ambienceVolume = v));
            sUi = BindSlider("SliderUi", v => Edit(d => d.uiVolume = v));

            // --- Confort ---
            tVignette = BindToggle("ToggleVignette", v => Edit(d => d.vignetteEnabled = v));
            tSnap = BindToggle("ToggleSnapTurn", v => Edit(d => d.snapTurn = v));
            tAutoNock = BindToggle("ToggleAutoNock", v => Edit(d => d.autoNock = v));
            tLeft = BindToggle("ToggleBowLeftHand", v => Edit(d => d.bowInLeftHand = v));
            sMoveSpeed = BindSlider("SliderMoveSpeed", v => Edit(d => d.moveSpeed = Mathf.Lerp(0.5f, 6f, v)));
            sSeated = BindSlider("SliderSeatedHeight", v => Edit(d => d.seatedHeightOffset = Mathf.Lerp(-0.5f, 0.5f, v)));

            // --- Jour / nuit ---
            tCycle = BindToggle("ToggleCycle", v => Edit(d => d.dayNightCycleActive = v));
            tFixed = BindToggle("ToggleFixedHour", OnFixedHourToggled);
            hourLabel = UiBind.Text(transform, "HourLabel", false);
            Click("BtnHourMinus", () => AdjustHour(-1f));
            Click("BtnHourPlus", () => AdjustHour(1f));

            // --- Profil ---
            profileLabel = UiBind.Text(transform, "ProfileLabel", false);
            Click("BtnChangeProfile", () => Services.Get<UiHub>()?.OpenProfileMenu());
            Click("BtnSaveNow", () =>
            {
                Services.Get<SaveSlotService>()?.Save();
                Settings?.SaveNow();
                GameEvents.RaiseToast("Partie sauvegardée.");
            });
        }

        private void Click(string name, UnityEngine.Events.UnityAction action)
        {
            Button b = UiBind.Button(transform, name, false);
            if (b != null)
                b.onClick.AddListener(action);
        }

        private Slider BindSlider(string name, UnityEngine.Events.UnityAction<float> onChange)
        {
            Slider s = UiBind.Slider(transform, name, false);
            if (s != null)
                s.onValueChanged.AddListener(v => { if (!syncing) onChange(v); UpdateValueLabel(s); });
            return s;
        }

        private Toggle BindToggle(string name, UnityEngine.Events.UnityAction<bool> onChange)
        {
            Toggle t = UiBind.Toggle(transform, name, false);
            if (t != null)
                t.onValueChanged.AddListener(v => { if (!syncing) onChange(v); });
            return t;
        }

        // Modifie un réglage puis prévient le jeu (effet immédiat + sauvegarde différée).
        private void Edit(System.Action<SettingsData> change)
        {
            SettingsService settings = Settings;
            if (settings == null) return;
            change(settings.Data);
            settings.MarkChanged();
        }

        private void OnEnable()
        {
            Bind();
            GameEvents.EquipmentChanged += Refresh;
            GameEvents.SettingsChanged += Refresh;
            GameEvents.ProfileChanged += Refresh;
            Refresh();
            if (!hiddenUntilOpened || IsOpen)
                ShowPage("Page_Main");
        }

        private void OnDisable()
        {
            GameEvents.EquipmentChanged -= Refresh;
            GameEvents.SettingsChanged -= Refresh;
            GameEvents.ProfileChanged -= Refresh;
        }

        protected override void OnOpened()
        {
            Refresh();
            ShowPage("Page_Main");
        }

        /// <summary>Affiche une page (les autres sont masquées).</summary>
        public void ShowPage(string pageName)
        {
            Bind();
            foreach (var kv in pages)
                kv.Value.SetActive(kv.Key == pageName);
            Refresh();
        }

        // ---------------------------------------------------------------- synchronisation

        /// <summary>Remet à jour tous les contrôles d'après les réglages et l'équipement.</summary>
        public void Refresh()
        {
            Bind();
            SettingsService settings = Settings;
            if (settings == null) return;
            SettingsData d = settings.Data;

            syncing = true;
            SetSlider(sMaster, d.masterVolume);
            SetSlider(sMusic, d.musicVolume);
            SetSlider(sSfx, d.sfxVolume);
            SetSlider(sAmbience, d.ambienceVolume);
            SetSlider(sUi, d.uiVolume);
            SetSlider(sMoveSpeed, Mathf.InverseLerp(0.5f, 6f, d.moveSpeed));
            SetSlider(sSeated, Mathf.InverseLerp(-0.5f, 0.5f, d.seatedHeightOffset));
            if (tVignette != null) tVignette.SetIsOnWithoutNotify(d.vignetteEnabled);
            if (tSnap != null) tSnap.SetIsOnWithoutNotify(d.snapTurn);
            if (tAutoNock != null) tAutoNock.SetIsOnWithoutNotify(d.autoNock);
            if (tLeft != null) tLeft.SetIsOnWithoutNotify(d.bowInLeftHand);
            if (tCycle != null) tCycle.SetIsOnWithoutNotify(d.dayNightCycleActive);
            if (tFixed != null) tFixed.SetIsOnWithoutNotify(d.fixedHour >= 0f);
            syncing = false;

            RefreshDifficulty(settings.Difficulty);
            RefreshEquipment();
            RefreshTime();
            RefreshProfile();
        }

        private void SetSlider(Slider s, float value)
        {
            if (s == null) return;
            s.SetValueWithoutNotify(value);
            UpdateValueLabel(s);
        }

        // Chaque curseur a un texte « <nom> Value » à côté (ex. SliderMasterValue) qui montre le pourcentage.
        private void UpdateValueLabel(Slider s)
        {
            Transform label = UiBind.Find(s.transform.parent != null ? s.transform.parent : transform, s.name + "Value");
            TMP_Text text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text != null)
                text.text = $"{Mathf.RoundToInt(s.value * 100f)} %";
        }

        private void RefreshDifficulty(GameDifficulty current)
        {
            SetButtonSelected(bEasy, current == GameDifficulty.Easy);
            SetButtonSelected(bNormal, current == GameDifficulty.Normal);
            SetButtonSelected(bHard, current == GameDifficulty.Hard);
            if (difficultyDescription != null)
            {
                switch (current)
                {
                    case GameDifficulty.Easy:
                        difficultyDescription.text = "Facile : grandes cibles, déplacement lent, plus de temps et de flèches. Trajectoire affichée, aimantation légère, pas de vent.";
                        break;
                    case GameDifficulty.Hard:
                        difficultyDescription.text = "Difficile : petites cibles, déplacement rapide, moins de temps. Vent fort et traînée réaliste, aucune aide.";
                        break;
                    default:
                        difficultyDescription.text = "Normal : cibles et temps intermédiaires, brise légère, aucune aide.";
                        break;
                }
            }
        }

        private static void SetButtonSelected(Button b, bool selected)
        {
            if (b == null) return;
            Image img = b.targetGraphic as Image;
            if (img != null)
                img.color = selected ? new Color(0.88f, 0.69f, 0.29f) : new Color(0.45f, 0.33f, 0.2f);
        }

        private void RefreshEquipment()
        {
            EquipmentService equipment = Services.Get<EquipmentService>();
            if (equipment == null) return;
            BowDefinition bow = equipment.SelectedBow;
            ArrowDefinition arrow = equipment.SelectedArrow;
            if (bow != null)
            {
                UiBind.SetText(bowName, bow.displayName);
                UiBind.SetText(bowStats, $"Vitesse max {bow.maxSpeed:0} m/s · Allonge {bow.drawLength:0.00} m\n{bow.description}");
            }
            if (arrow != null)
            {
                UiBind.SetText(arrowName, arrow.displayName);
                UiBind.SetText(arrowStats, $"Vitesse × {arrow.speedMultiplier:0.00} · Sensibilité au vent × {arrow.windSensitivity:0.0}\n{arrow.description}");
            }
        }

        private void RefreshTime()
        {
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            if (time != null)
                UiBind.SetText(hourLabel, $"{Mathf.FloorToInt(time.Hour):00} h {Mathf.FloorToInt((time.Hour % 1f) * 60f):00}");
        }

        private void RefreshProfile()
        {
            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            string text = profiles != null && profiles.Current != null
                ? $"Profil : {profiles.Current.name}\nEmplacement : {(slots != null ? slots.Current.slotIndex : 1)}"
                : "Aucun profil sélectionné (partie invité : non sauvegardée).";
            UiBind.SetText(profileLabel, text);
        }

        // ---------------------------------------------------------------- actions

        private void SetDifficulty(GameDifficulty difficulty)
        {
            Settings?.SetDifficulty(difficulty);
            Refresh();
        }

        private void OnFixedHourToggled(bool fixedHour)
        {
            SettingsService settings = Settings;
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            if (settings == null) return;
            settings.Data.fixedHour = fixedHour ? (time != null ? time.Hour : 12f) : -1f;
            settings.MarkChanged();
        }

        private void AdjustHour(float delta)
        {
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            if (time == null) return;
            time.AddHours(delta);
            RefreshTime();
        }

        private void OnPlay()
        {
            // Direction l'arène en cours de campagne : la première débloquée mais pas encore réussie.
            GameCatalog catalog = Services.Get<GameCatalog>();
            UnlockService unlocks = Services.Get<UnlockService>();
            if (catalog == null) return;
            LevelDefinition target = null;
            foreach (LevelDefinition level in catalog.GetCampaignLevels())
            {
                if (unlocks != null && unlocks.IsLevelUnlocked(level) && !unlocks.IsLevelPassed(level.levelId))
                {
                    target = level;
                    break;
                }
            }
            if (target == null)
            {
                var campaign = catalog.GetCampaignLevels();
                target = campaign.Count > 0 ? campaign[campaign.Count - 1] : null;
            }
            if (target != null)
                Travel(target.spawnId);
        }

        private void Travel(string destinationId)
        {
            Services.Get<UiHub>()?.CloseAll();
            Services.Get<FastTravelService>()?.TravelTo(destinationId);
        }

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
