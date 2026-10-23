// DevShortcuts : raccourcis clavier F1 à F11 pour tester vite (annexe B du tutoriel).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Actifs UNIQUEMENT dans l'éditeur et dans les builds « Development » : dans la version finale, ce
// composant se désactive tout seul (aucun raccourci de triche pour le public).
//   F1 commandes (Maj+F1 : légende) · F2 casque ↔ clavier/souris (Maj+F2 : rig VR simulé) · F3 / Maj+F3 heure ±1 h · F4 vitesse du jour × 1/10/60
//   F5 tout débloquer · F6 finir l'arène avec 3 étoiles · F7 arène suivante · F8 monstre devant soi
//   F9 FPS et statistiques · F10 vent et trajectoires · F11 changer de difficulté

using ArcheryChallenge.Archery;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcheryChallenge.Core
{
    /// <summary>Raccourcis de développement.</summary>
    [DisallowMultipleComponent]
    public class DevShortcuts : MonoBehaviour
    {
        private bool showHelp = true;
        private bool showStats;
        private bool showWind;
        private float fpsSmoothed = 60f;
        private int arenaIndex;
        private string lastMessage = "";
        private float messageUntil;
        private GUIStyle style;

        private static readonly string[] ArenaSpawnIds = { "hub", "training", "arena_01", "arena_02", "arena_03", "arena_04", "arena_05" };

        /// <summary>Dernier cadre dessiné par la légende de développement (pour les tests : doit tenir dans l'écran).</summary>
        public static Rect LastBox { get; private set; }

        /// <summary>Vrai si les raccourcis sont autorisés dans cette version.</summary>
        public static bool Allowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (!Allowed)
                enabled = false;
            Services.Register(this);
        }

        private void OnDestroy() => Services.Unregister(this);

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
                fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / dt, 0.05f);

            // F1 seul ouvre l'écran des commandes (UiHub) ; Maj + F1 masque ou affiche la légende de développement.
            if (kb.f1Key.wasPressedThisFrame && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) showHelp = !showHelp;
            if (kb.f2Key.wasPressedThisFrame) Do(F2);
            if (kb.f3Key.wasPressedThisFrame) Do(() => F3(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? -1 : 1));
            if (kb.f4Key.wasPressedThisFrame) Do(F4);
            if (kb.f5Key.wasPressedThisFrame) Do(F5);
            if (kb.f6Key.wasPressedThisFrame) Do(F6);
            if (kb.f7Key.wasPressedThisFrame) Do(F7);
            if (kb.f8Key.wasPressedThisFrame) Do(F8);
            if (kb.f9Key.wasPressedThisFrame) showStats = !showStats;
            if (kb.f10Key.wasPressedThisFrame) showWind = !showWind;
            if (kb.f11Key.wasPressedThisFrame) Do(F11);
        }

        private void Do(System.Action action)
        {
            try { action(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        private void Say(string message)
        {
            lastMessage = message;
            messageUntil = Time.unscaledTime + 3f;
            Debug.Log("[DevShortcuts] " + message);
        }

        // ---------------------------------------------------------------- actions

        private void F2()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode == null) return;
            Keyboard kb = Keyboard.current;
            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shift)
                mode.ToggleSimulator();     // Maj + F2 : clavier/souris ↔ rig VR sans casque
            else
                mode.TogglePlayMode();      // F2 : casque ↔ clavier/souris
            Say("Mode : " + mode.Mode);
        }

        private void F3(int direction)
        {
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            if (time == null) { Say("TimeOfDayService absent"); return; }
            time.AddHours(direction);
            Say($"Heure : {time.Hour:0.0} h");
        }

        private void F4()
        {
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            if (time == null) { Say("TimeOfDayService absent"); return; }
            time.CycleSpeed();
            Say($"Vitesse du jour : × {time.SpeedMultiplier:0}");
        }

        private void F5()
        {
            UnlockService unlocks = Services.Get<UnlockService>();
            if (unlocks == null) return;
            unlocks.DemoUnlockAll = !unlocks.DemoUnlockAll;
            Say("Mode démo (tout débloqué) : " + (unlocks.DemoUnlockAll ? "OUI" : "non"));
        }

        private void F6()
        {
            LevelController level = LevelController.Active;
            if (level == null) { Say("Aucune arène active"); return; }
            level.ForceFinishWithThreeStars();
            Say("Arène terminée (3 étoiles)");
        }

        private void F7()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            FastTravelService travel = Services.Get<FastTravelService>();
            if (mode == null) return;
            arenaIndex = (arenaIndex + 1) % ArenaSpawnIds.Length;
            string id = ArenaSpawnIds[arenaIndex];
            if (travel != null)
            {
                travel.TravelTo(id, true);
                Say("Téléportation : " + id);
            }
            else
            {
                Say(mode.TeleportToSpawn(id) ? "Téléportation : " + id : "Point introuvable : " + id);
            }
        }

        private void F8()
        {
            Monsters.MonsterSpawner.SpawnInFrontOfPlayer();
            Say("Monstre invoqué");
        }

        private void F11()
        {
            SettingsService settings = Services.Get<SettingsService>();
            if (settings == null) return;
            GameDifficulty next = (GameDifficulty)(((int)settings.Difficulty + 1) % 3);
            settings.SetDifficulty(next);
            Say("Difficulté : " + next);
        }

        // ---------------------------------------------------------------- affichage

        private void OnGUI()
        {
            if (!Allowed)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
                style.normal.textColor = Color.white;
            }

            string text = "";
            // La légende reste affichée par défaut : toutes les touches de développement sont visibles à l'écran.
            if (showHelp)
                text += ControlsCatalog.DevLegend() + "\n";
            if (showStats)
            {
                ScoreService score = Services.Get<ScoreService>();
                text += $"<b>{fpsSmoothed:0} FPS</b> ({1000f / Mathf.Max(1f, fpsSmoothed):0.0} ms)";
                if (score != null)
                    text += $"  Score {score.Score}  Combo {score.Combo}  Flèches {score.ArrowsShot}  Précision {score.Accuracy * 100f:0} %";
                text += "\n";
                AimSway sway = Services.Get<AimSway>();
                if (sway != null)
                    text += $"Tremblement : {sway.Amplitude:0.00}° · maintien {sway.HoldTime:0.0} s · décalage {sway.Offset.x:+0.00;-0.00}°, {sway.Offset.y:+0.00;-0.00}°\n";
            }
            if (showWind)
            {
                WindService wind = Services.Get<WindService>();
                text += wind != null ? $"Vent : {wind.Speed:0.0} m/s  {wind.CurrentWind}\n" : "Vent : service absent\n";
            }
            if (Time.unscaledTime < messageUntil)
                text += "<color=#FFD866>" + lastMessage + "</color>\n";

            if (text.Length > 0)
            {
                // Le cadre est MESURÉ d'après le texte (lignes qui passent à la ligne comprises) et la police s'adapte à la
                // fenêtre : il ne déborde donc jamais de l'écran, même dans une petite vue Game ou quand le message de
                // « vitesse du jour », les statistiques et le vent s'ajoutent à la légende.
                // Placement : en bas à droite, juste au-dessus de la barre d'aide (le haut gauche porte le score).
                // La zone utilisable est la PARTIE VISIBLE de l'écran (fenêtre Game zoomée ou tronquée : voir ScreenViewport).
                Rect area = ScreenViewport.VisiblePixelsTopLeft;
                float scale = Mathf.Clamp(area.height / 900f, 0.7f, 1.5f);
                int fontSize = Mathf.RoundToInt(14f * scale);
                float width = Mathf.Min(560f * scale, area.width - 16f);
                float bottomMargin = (Screen.height - area.yMax) + area.height * 0.045f + 10f;     // barre d'aide de DesktopHud
                float maxHeight = area.yMax - area.yMin - (area.height * 0.045f + 10f) - 8f;
                string content = text.TrimEnd('\n');

                style.fontSize = fontSize;
                float height = style.CalcHeight(new GUIContent(content), width - 12f) + 10f;
                while (height > maxHeight && fontSize > 8)
                {
                    fontSize--;
                    style.fontSize = fontSize;
                    height = style.CalcHeight(new GUIContent(content), width - 12f) + 10f;
                }
                height = Mathf.Min(height, maxHeight);

                float x = area.xMax - width - 8f;
                float y = Screen.height - bottomMargin - height;
                LastBox = new Rect(x, y, width, height);
                GUI.Box(new Rect(x, y, width, height), GUIContent.none);
                GUI.Label(new Rect(x + 6f, y + 5f, width - 12f, height - 10f), content, style);
            }
        }
    }
}
