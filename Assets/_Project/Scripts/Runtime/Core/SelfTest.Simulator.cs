// SelfTest (partie « rig VR simulé ») : vérifie la touche F2 (clavier/souris ↔ rig VR sans casque) :
// hauteur de la tête, arc en main, déplacement, tir complet par les mains virtuelles, bande de touches affichée,
// écrans flottants visibles et cliquables dans les deux sens.
//
// Lancé par SelfTest.RunSimulatedRigScenario() en mode Play (menu de test).

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Input;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcheryChallenge.Core
{
    public partial class SelfTest
    {
        /// <summary>Lance le scénario du rig VR simulé (F2).</summary>
        public static SelfTest RunSimulatedRigScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.SimulatedRigScenario());
            return test;
        }

        private IEnumerator SimulatedRigScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            Services.Get<SettingsService>().SetDifficulty(GameDifficulty.Easy);
            ArrowAllowance.SetUnlimited();
            Services.Get<FastTravelService>()?.TravelTo("training", true);
            yield return new WaitForSeconds(3.5f);

            // --- 1. F2 : tête à hauteur normale, arc en main ---
            mode.ToggleSimulator();
            yield return new WaitForSeconds(2f);
            SimulatedRigDriver driver = mode.XrRig != null ? mode.XrRig.GetComponent<SimulatedRigDriver>() : null;
            XRArcherInput xr = Services.Get<XRArcherInput>();
            Check(mode.Mode == PlayerMode.Simulator && driver != null && driver.IsActive, "F2 : le rig VR simulé est piloté par SimulatedRigDriver");
            if (driver == null || xr == null)
            {
                Finish("Rig VR simulé");
                yield break;
            }
            float eye = mode.ActiveHead.position.y - mode.ActiveRoot.position.y;
            Check(eye > 1.5f && eye < 1.9f, $"La tête est à hauteur normale ({eye:0.00} m au-dessus du sol)");
            Check(xr.LeftHand.gameObject.activeInHierarchy && xr.RightHand.gameObject.activeInHierarchy, "Les deux manettes virtuelles sont actives");
            Bow bow = xr.BowHolster.CurrentBow;
            Check(bow != null && bow.IsHeld, "L'arc est tenu en main (et non resté à l'épaule)");
            if (bow == null)
            {
                Finish("Rig VR simulé");
                yield break;
            }
            Check(Vector3.Distance(bow.transform.position, mode.ActiveHead.position) < 1.2f && bow.transform.position.y > mode.ActiveRoot.position.y + 0.8f,
                "L'arc est devant le joueur, à hauteur de poitrine");

            // La bande de touches est affichée en permanence pour le rig simulé.
            DesktopHud hud = FindAnyObjectByType<DesktopHud>(FindObjectsInactive.Include);
            Canvas hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            TMPro.TMP_Text keyBar = hud != null ? UiBind.Text(hud.transform, "KeyBar", false) : null;
            Check(hudCanvas != null && hudCanvas.enabled, "L'incrustation de touches est affichée avec le rig simulé");
            Check(keyBar != null && keyBar.text.Contains("VR simulé") && keyBar.gameObject.activeInHierarchy, "La bande de touches du rig simulé est visible");
            if (keyBar != null)
            {
                Canvas.ForceUpdateCanvases();
                Vector3[] corners = new Vector3[4];
                keyBar.rectTransform.GetWorldCorners(corners);
                Check(corners[0].x >= -1f && corners[2].x <= Screen.width + 1f && corners[0].y >= -1f && corners[2].y <= Screen.height + 1f,
                    $"La bande de touches tient dans l'écran (x {corners[0].x:0}–{corners[2].x:0} sur {Screen.width})");
                Check(keyBar.textBounds.size.x <= keyBar.rectTransform.rect.width + 2f, "Le texte de la bande ne déborde pas de sa zone");
            }

            // --- 2. Déplacement ---
            Vector3 start = mode.ActiveRoot.position;
            driver.InjectMove(new Vector2(0f, 1f));
            yield return new WaitForSeconds(1f);
            driver.InjectMove(Vector2.zero);
            Vector3 moved = mode.ActiveRoot.position - start;
            moved.y = 0f;
            Check(moved.magnitude > 1.5f, $"Z/W : le joueur avance ({moved.magnitude:0.0} m en 1 s)");
            yield return new WaitForSeconds(0.2f);
            driver.InjectMove(null);

            // --- 3. Tir complet par les mains virtuelles ---
            Target target = null;
            float best = float.MaxValue;
            foreach (Target t in Target.All)
            {
                if (t is PenaltyTarget || t is StartTarget || t.Face == null) continue;
                float d = Vector3.Distance(mode.ActiveHead.position, t.Face.Center);
                if (d < best) { best = d; target = t; }
            }
            Check(target != null, "Une cible existe devant le joueur");
            ScoreService score = Services.Get<ScoreService>();
            score.ResetRun();
            score.AcceptShots = true;
            if (target != null)
            {
                AimSimulatedRig(driver, mode, bow, target.Face.Center);
                yield return null;
                yield return null;

                driver.InjectTake();
                yield return new WaitForSeconds(1.2f);
                Check(bow.HasArrow, $"R : la main libre prend une flèche au carquois et l'encoche (étape {driver.PhaseName})");

                int shotsBefore = score.ArrowsShot;
                bool impact = false;
                ArrowImpactInfo info = default;
                void OnImpact(ArrowImpactInfo i) { impact = true; info = i; }
                GameEvents.ArrowImpacted += OnImpact;

                driver.InjectFire(true);
                yield return new WaitForSeconds(bow.Definition.desktopDrawTime + 0.4f);
                Check(bow.Tension > 0.9f, $"Clic gauche maintenu : la corde est tendue (tension {bow.Tension:P0})");
                AimSimulatedRig(driver, mode, bow, target.Face.Center);
                yield return null;
                driver.InjectFire(false);
                yield return new WaitForSeconds(0.4f);
                Check(Arrow.FlyingCount > 0 || impact, "Relâcher : la flèche part");
                Check(score.ArrowsShot == shotsBefore + 1, "Le tir est compté");
                float flight = 0f;
                while (!impact && flight < 6f) { flight += Time.deltaTime; yield return null; }
                GameEvents.ArrowImpacted -= OnImpact;
                Check(impact && info.HitTarget, "La flèche atteint la cible");
                yield return new WaitForSeconds(0.5f);
                Check(driver.PhaseName == "Idle", $"La main libre est revenue au repos (étape {driver.PhaseName})");
            }

            // --- 4. Menu ouvert en rig simulé : visible et cliquable, puis F2 vers le clavier/souris ---
            Camera simCam = mode.ActiveHead.GetComponent<Camera>();
            hub.OpenPause();                   // la pause gèle le temps du jeu : on attend en temps réel
            yield return new WaitForSecondsRealtime(0.4f);
            yield return null;
            string fitSim;
            Check(PanelFitsCamera(hub.PauseMenu, simCam, out fitSim), "Pause (rig simulé) : tient entièrement à l'écran " + fitSim);
            Check(CursorController.IsFreeRequested, "Pause (rig simulé) : curseur libéré pour cliquer à la souris");
            Check(PanelClickable(hub.PauseMenu, simCam), "Pause (rig simulé) : un bouton est atteignable par le pointeur de la souris");

            mode.ToggleSimulator();            // F2 avec le menu encore ouvert
            yield return new WaitForSecondsRealtime(0.8f);
            Camera deskCam = mode.ActiveHead.GetComponent<Camera>();
            Check(mode.Mode == PlayerMode.Desktop, "F2 : retour au clavier/souris");
            Check(hub.PauseMenu.IsOpen && CursorController.IsFreeRequested, "Le menu reste ouvert et le curseur reste libre");
            string fitDesk;
            Check(PanelFitsCamera(hub.PauseMenu, deskCam, out fitDesk), "Pause (retour clavier) : reposée devant le joueur, entièrement visible " + fitDesk);
            Check(PanelClickable(hub.PauseMenu, deskCam), "Pause (retour clavier) : un bouton est atteignable par le pointeur de la souris");
            Check(Cursor.lockState == CursorLockMode.None, "Le curseur n'est plus verrouillé : on peut cliquer sur le menu");
            hub.CloseAll();
            yield return null;
            Check(!CursorController.IsFreeRequested, "Menu fermé : le curseur est rendu au jeu");

            // Ouverture d'un menu APRÈS le retour au clavier/souris : il doit être cliquable (cas signalé après F2).
            hub.OpenMainMenu();
            yield return new WaitForSecondsRealtime(0.4f);
            string fitMenu;
            Check(PanelFitsCamera(hub.MainMenu, deskCam, out fitMenu), "Menu principal ouvert après F2 : entièrement visible " + fitMenu);
            Check(PanelClickable(hub.MainMenu, deskCam), "Menu principal ouvert après F2 : un bouton est atteignable par la souris");
            Check(Cursor.lockState == CursorLockMode.None, "Menu principal ouvert après F2 : curseur libre");
            hub.CloseAll();
            yield return new WaitForSecondsRealtime(0.2f);

            // Deuxième passage en rig simulé : la tête est toujours à hauteur normale et l'arc de nouveau en main.
            mode.ToggleSimulator();
            yield return new WaitForSeconds(2f);
            Bow again = xr.BowHolster.CurrentBow;
            float eye2 = mode.ActiveHead.position.y - mode.ActiveRoot.position.y;
            Check(mode.Mode == PlayerMode.Simulator && eye2 > 1.5f && eye2 < 1.9f, $"2e passage en rig simulé : tête à hauteur normale ({eye2:0.00} m)");
            Check(again != null && again.IsHeld, "2e passage en rig simulé : l'arc est de nouveau en main");
            mode.ToggleSimulator();
            yield return new WaitForSeconds(0.5f);
            Check(mode.Mode == PlayerMode.Desktop && mode.Desktop != null && mode.Desktop.CameraTransform != null, "Retour final au clavier/souris");

            // F2 (sans Maj) : casque ↔ clavier/souris. Sans casque relié, le rig simulé le remplace ; avec un casque, c'est la VR.
            mode.TogglePlayMode();
            yield return new WaitForSeconds(1f);
            PlayerMode expected = PlayerModeService.IsXrRuntimeActive ? PlayerMode.VR : PlayerMode.Simulator;
            Check(mode.Mode == expected, $"F2 depuis le clavier/souris : {(PlayerModeService.IsXrRuntimeActive ? "casque (VR)" : "rig VR simulé, faute de casque")}");
            mode.TogglePlayMode();
            yield return new WaitForSeconds(0.5f);
            Check(mode.Mode == PlayerMode.Desktop && !PlayerModeService.IsXrRuntimeActive, "F2 depuis la VR/le rig simulé : retour au clavier/souris, runtime XR arrêté");

            Finish("Rig VR simulé");
        }

        /// <summary>Lance le scénario des panneaux d'arène : briefing, résultat et HUD visibles en entier depuis la plateforme.</summary>
        public static SelfTest RunArenaBoardsScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.ArenaBoardsScenario());
            return test;
        }

        private IEnumerator ArenaBoardsScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            Services.Get<UnlockService>().DemoUnlockAll = true;

            foreach (string id in new[] { "arena_01", "arena_02", "arena_03", "arena_04", "arena_05" })
            {
                Services.Get<FastTravelService>().TravelTo(id, true);
                yield return new WaitForSeconds(3.5f);
                LevelController level = null;
                foreach (LevelController l in LevelController.All)
                    if (!l.IsTraining && l.Definition != null && l.Definition.levelId == id)
                        level = l;
                Check(level != null, $"[{id}] l'arène est chargée");
                if (level == null)
                    continue;

                mode.TeleportToSpawn(id);
                yield return new WaitForSeconds(0.8f);
                // Le tireur regarde droit devant lui, vers les cibles.
                foreach (PlayerMode view in new[] { PlayerMode.Desktop, PlayerMode.Simulator })
                {
                    if (view == PlayerMode.Simulator)
                    {
                        mode.ToggleSimulator();
                        mode.TeleportToSpawn(id);
                        yield return new WaitForSeconds(1.2f);
                        SimulatedRigDriver driver = mode.XrRig.GetComponent<SimulatedRigDriver>();
                        driver.SetLookAngles(level.transform.eulerAngles.y, 0f);
                    }
                    yield return new WaitForSeconds(0.3f);
                    Camera cam = mode.ActiveHead.GetComponent<Camera>();
                    foreach (Canvas canvas in level.GetComponentsInChildren<Canvas>(true))
                    {
                        string label = canvas.name;
                        if (canvas.name != "HudPanel" && canvas.name != "BriefingPanel" && canvas.name != "LevelResultPanel")
                            continue;
                        string info;
                        Check(CanvasFitsCamera(canvas, cam, out info), $"[{id} · {view}] {label} entièrement visible depuis la plateforme {info}");
                    }
                }
                mode.ToggleSimulator();      // retour clavier/souris
                yield return new WaitForSeconds(0.5f);

                // Au clavier/souris (et en rig simulé), l'incrustation DesktopHud remplace le HUD posé de côté : celui-ci est masqué.
                WorldHudBoard board = level.GetComponentInChildren<WorldHudBoard>(true);
                Check(board != null && !board.IsVisible, $"[{id}] le HUD de côté est masqué au clavier/souris (l'incrustation d'écran le remplace)");
                if (board != null)
                {
                    Canvas boardCanvas = board.GetComponent<Canvas>();
                    Check(!boardCanvas.enabled, $"[{id}] le HUD de côté n'affiche rien (aucune moitié de tableau à l'écran)");
                }
            }

            // --- HUD d'arène en vrai casque : il suit le regard pendant la manche et reste entièrement dans le champ ---
            {
                Services.Get<FastTravelService>().TravelTo("arena_01", true);
                yield return new WaitForSeconds(3.5f);
                LevelController level = null;
                foreach (LevelController l in LevelController.All)
                    if (!l.IsTraining && l.Definition != null && l.Definition.levelId == "arena_01")
                        level = l;
                WorldHudBoard board = level != null ? level.GetComponentInChildren<WorldHudBoard>(true) : null;
                Check(board != null, "[casque] le HUD d'arène porte WorldHudBoard");
                if (board != null)
                {
                    mode.SetMode(PlayerMode.VR);     // simule un vrai casque (sans pilote clavier/souris)
                    mode.TeleportToSpawn("arena_01");
                    yield return new WaitForSeconds(0.8f);
                    board.ForceFollow = true;
                    yield return new WaitForSeconds(1.2f);
                    Camera head = mode.ActiveHead.GetComponent<Camera>();
                    string info;
                    Canvas boardCanvas = board.GetComponent<Canvas>();
                    Check(board.IsVisible && boardCanvas.enabled && board.IsFollowing, "[casque] le HUD suit le regard pendant la manche");
                    Check(CanvasFitsCamera(boardCanvas, head, out info), "[casque] le HUD (vent, PV…) est entièrement dans le champ " + info);
                    // Le joueur tourne la tête : le HUD le suit toujours.
                    mode.XrRig.transform.rotation = Quaternion.Euler(0f, mode.XrRig.transform.eulerAngles.y + 70f, 0f);
                    yield return new WaitForSeconds(1.5f);
                    Check(CanvasFitsCamera(boardCanvas, head, out info), "[casque] après un quart de tour, le HUD est toujours dans le champ " + info);
                    board.ForceFollow = false;
                    yield return new WaitForSeconds(0.3f);
                    Check(!board.IsFollowing, "[casque] hors manche, le HUD retourne sur son tableau");
                    mode.SetMode(PlayerMode.Desktop);
                    mode.TeleportToSpawn("arena_01");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            Finish("Panneaux d'arène");
        }

        /// <summary>Lance le scénario « fenêtre Game zoomée » : l'interface reste dans la partie visible de l'écran.</summary>
        public static SelfTest RunViewportScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.ViewportScenario());
            return test;
        }

        private IEnumerator ViewportScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            mode.TeleportToSpawn("hub");
            yield return new WaitForSeconds(1f);

            DesktopHud hud = FindAnyObjectByType<DesktopHud>(FindObjectsInactive.Include);
            Check(hud != null && hud.SafeArea != null, "L'incrustation d'écran possède son conteneur « zone visible »");
            if (hud == null || hud.SafeArea == null)
            {
                Finish("Fenêtre Game zoomée");
                yield break;
            }

            // Rectangles visibles testés (parts de l'image) : vue entière, zoom × 1,5 (ce que montrait la capture signalée), zoom × 2 décentré.
            (string name, Rect rect)[] views =
            {
                ("vue entière", new Rect(0f, 0f, 1f, 1f)),
                ("zoom × 1,5", new Rect(1f / 6f, 1f / 6f, 2f / 3f, 2f / 3f)),
                ("zoom × 2 décentré", new Rect(0.3f, 0.1f, 0.5f, 0.5f))
            };
            foreach (var view in views)
            {
                ScreenViewport.Override = view.rect;
                yield return new WaitForSecondsRealtime(0.4f);
                Canvas.ForceUpdateCanvases();
                Rect visible = ScreenViewport.Visible;
                foreach (string element in new[] { "StatePanel", "HeartsRow", "KeyBar", "KeyBarBackground", "WindText", "WindArrow", "StarsRow", "ObjectiveText" })
                {
                    Transform t = UiBind.Find(hud.transform, element);
                    if (t == null) { Check(false, $"[{view.name}] {element} introuvable"); continue; }
                    Vector3[] corners = new Vector3[4];
                    ((RectTransform)t).GetWorldCorners(corners);
                    float minX = corners[0].x / Screen.width, minY = corners[0].y / Screen.height;
                    float maxX = corners[2].x / Screen.width, maxY = corners[2].y / Screen.height;
                    bool inside = minX >= visible.xMin - 0.004f && maxX <= visible.xMax + 0.004f && minY >= visible.yMin - 0.004f && maxY <= visible.yMax + 0.004f;
                    Check(inside, $"[{view.name}] {element} dans la partie visible (x {minX:0.00}–{maxX:0.00}, y {minY:0.00}–{maxY:0.00} ; visible x {visible.xMin:0.00}–{visible.xMax:0.00}, y {visible.yMin:0.00}–{visible.yMax:0.00})");
                }

                // ATH discret : informations en haut à gauche (≤ 22 % de la largeur), vie en bas à gauche (≤ 16 %), tous deux dans leur coin.
                if (view.name == "vue entière")
                {
                    Vector3[] sc = new Vector3[4], hc = new Vector3[4];
                    ((RectTransform)UiBind.Find(hud.transform, "StatePanel")).GetWorldCorners(sc);
                    ((RectTransform)UiBind.Find(hud.transform, "HeartsRow")).GetWorldCorners(hc);
                    float stateWidth = (sc[2].x - sc[0].x) / Screen.width, stateHeight = (sc[2].y - sc[0].y) / Screen.height;
                    float heartsWidth = (hc[2].x - hc[0].x) / Screen.width;
                    Check(stateWidth <= 0.22f && stateHeight <= 0.34f, $"[{view.name}] les informations de manche sont petites ({stateWidth * 100f:0} % de la largeur, {stateHeight * 100f:0} % de la hauteur)");
                    Check(heartsWidth <= 0.16f, $"[{view.name}] la barre de vie est petite ({heartsWidth * 100f:0} % de la largeur)");
                    Check(sc[0].x / Screen.width < 0.1f && sc[2].y / Screen.height > 0.9f, $"[{view.name}] les informations sont en haut à gauche");
                    Check(hc[0].x / Screen.width < 0.1f && hc[0].y / Screen.height < 0.15f, $"[{view.name}] la vie est en bas à gauche");
                }

                // Légende de développement (IMGUI) : elle aussi dans la partie visible.
                Rect box = DevShortcuts.LastBox;
                Rect area = ScreenViewport.VisiblePixelsTopLeft;
                bool boxInside = box.xMin >= area.xMin - 1f && box.xMax <= area.xMax + 1f && box.yMin >= area.yMin - 1f && box.yMax <= area.yMax + 1f;
                Check(boxInside, $"[{view.name}] la légende des touches de développement est dans la partie visible");

                // Écran flottant ouvert (pause) : entièrement dans la partie visible.
                hub.OpenPause();
                yield return new WaitForSecondsRealtime(0.4f);
                Camera cam = mode.ActiveHead.GetComponent<Camera>();
                Canvas panelCanvas = hub.PauseMenu.GetComponentInParent<Canvas>();
                Vector3[] pc = new Vector3[4];
                ((RectTransform)panelCanvas.transform).GetWorldCorners(pc);
                bool fits = true;
                foreach (Vector3 c in pc)
                {
                    Vector3 v = cam.WorldToViewportPoint(c);
                    fits &= v.z > 0.3f && v.x >= visible.xMin - 0.01f && v.x <= visible.xMax + 0.01f && v.y >= visible.yMin - 0.01f && v.y <= visible.yMax + 0.01f;
                }
                Check(fits, $"[{view.name}] l'écran de pause tient dans la partie visible");
                hub.CloseAll();
                yield return new WaitForSecondsRealtime(0.2f);
            }
            ScreenViewport.Override = null;
            Finish("Fenêtre Game zoomée");
        }

        /// <summary>Lance le scénario des menus : croix de fermeture, retour à la pause, voyage rapide, apparition de l'arène 5.</summary>
        public static SelfTest RunMenusScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.MenusScenario());
            return test;
        }

        private static void ClickClose(UiPanel panel)
        {
            Button close = UiBind.Button(panel.transform, "BtnClose", false);
            if (close != null && close.gameObject.activeInHierarchy)
                close.onClick.Invoke();
        }

        private IEnumerator MenusScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            Services.Get<UnlockService>().DemoUnlockAll = true;
            mode.TeleportToSpawn("hub");
            yield return new WaitForSecondsRealtime(0.6f);

            // --- 1. Chaque menu a une croix qui le ferme ---
            (string name, UiPanel panel, System.Action open)[] panels =
            {
                ("Pause", hub.PauseMenu, () => hub.OpenPause()),
                ("Réglages (menu principal)", hub.MainMenu, () => hub.OpenMainMenu()),
                ("Classement", hub.Leaderboard, () => hub.OpenLeaderboard()),
                ("Voyage rapide", hub.FastTravel, () => hub.OpenFastTravel()),
                ("Commandes", hub.Controls, () => hub.OpenControls()),
                ("Profil", hub.ProfileMenu, () => hub.OpenProfileMenu())
            };
            foreach (var entry in panels)
            {
                entry.open();
                yield return new WaitForSecondsRealtime(0.25f);
                Button close = UiBind.Button(entry.panel.transform, "BtnClose", false);
                Check(close != null && close.gameObject.activeInHierarchy, entry.name + " : une croix de fermeture est affichée");
                TMPro.TMP_Text label = close != null ? close.GetComponentInChildren<TMPro.TMP_Text>() : null;
                Check(label != null && label.text == "X", entry.name + " : c'est une croix « X »");
                ClickClose(entry.panel);
                yield return new WaitForSecondsRealtime(0.25f);
                Check(!entry.panel.IsOpen && !CursorController.IsFreeRequested && Time.timeScale > 0f, entry.name + " : la croix ferme le menu et rend le jeu et le curseur");
            }

            // --- 2. Voyage rapide : plus de bouton « Fermer », la liste tient dans le panneau ---
            hub.OpenFastTravel();
            yield return new WaitForSecondsRealtime(0.4f);
            bool hasFermer = false;
            foreach (TMPro.TMP_Text t in hub.FastTravel.GetComponentsInChildren<TMPro.TMP_Text>(false))
                hasFermer |= t.text == "Fermer";
            Check(!hasFermer, "Voyage rapide : plus de bouton « Fermer »");
            Canvas travelCanvas = hub.FastTravel.GetComponentInParent<Canvas>();
            Vector3[] pc = new Vector3[4];
            ((RectTransform)travelCanvas.transform).GetWorldCorners(pc);
            Vector3 panelMin = pc[0], panelMax = pc[2];
            int buttons = 0;
            bool inside = true;
            foreach (Button b in hub.FastTravel.GetComponentsInChildren<Button>(false))
            {
                if (b.name == "BtnClose") continue;
                Vector3[] bc = new Vector3[4];
                ((RectTransform)b.transform).GetWorldCorners(bc);
                buttons++;
                inside &= bc[0].y >= panelMin.y - 0.001f && bc[2].y <= panelMax.y + 0.001f;
            }
            Check(buttons >= 7 && inside, $"Voyage rapide : les {buttons} destinations tiennent dans le panneau");
            hub.CloseAll();
            yield return new WaitForSecondsRealtime(0.2f);

            // --- 3. Sous-écrans ouverts depuis la pause : le jeu reste en pause et la croix ramène à la pause ---
            hub.OpenPause();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(Time.timeScale == 0f && hub.PauseMenu.IsOpen, "La pause arrête le jeu");
            hub.OpenMainMenu();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(hub.MainMenu.IsOpen && !hub.PauseMenu.IsOpen && Time.timeScale == 0f && hub.HoldingPause, "Réglages ouverts depuis la pause : le jeu reste en pause");
            ClickClose(hub.MainMenu);
            yield return new WaitForSecondsRealtime(0.3f);
            Check(!hub.MainMenu.IsOpen && hub.PauseMenu.IsOpen && Time.timeScale == 0f && !hub.HoldingPause, "La croix des réglages ramène à la pause");
            hub.OpenControls();
            yield return new WaitForSecondsRealtime(0.3f);
            hub.CloseAll();     // Échap : tout se ferme et le jeu repart
            yield return new WaitForSecondsRealtime(0.3f);
            Check(!hub.AnyOpen && Time.timeScale == 1f && !hub.HoldingPause, "Échap depuis un sous-écran : tout se ferme et le jeu reprend");
            hub.OpenPause();
            yield return new WaitForSecondsRealtime(0.3f);
            ClickClose(hub.PauseMenu);
            yield return new WaitForSecondsRealtime(0.3f);
            Check(!hub.PauseMenu.IsOpen && Time.timeScale == 1f, "La croix de la pause reprend le jeu");

            // --- 4. L'arène 5 apparaît de loin (point de détection au milieu de l'arène, grand rayon) ---
            SceneService scenes = Services.Get<SceneService>();
            ArcheryChallenge.World.FastTravelService travel = Services.Get<ArcheryChallenge.World.FastTravelService>();
            travel.TravelTo("arena_04", true);
            yield return new WaitForSeconds(4f);
            Check(scenes.IsLoaded("Arena_05"), "Depuis le stand du Ravin (≈ 56 m du milieu de l'arène 5), l'arène 5 est déjà apparue");
            travel.TravelTo("hub", true);
            yield return new WaitForSeconds(4f);
            Check(!scenes.IsLoaded("Arena_05"), "Depuis le village, l'arène 5 n'est pas chargée");
            Finish("Menus et arène 5");
        }

        // Oriente la vue du rig simulé vers un point en compensant la chute de la flèche (comme AimAt pour le clavier/souris).
        private static void AimSimulatedRig(SimulatedRigDriver driver, PlayerModeService mode, Bow bow, Vector3 point)
        {
            Vector3 origin = mode.ActiveHead.position;
            float speed = bow != null && bow.Definition != null ? bow.Definition.maxSpeed : 25f;
            float time = Vector3.Distance(origin, point) / Mathf.Max(5f, speed);
            Vector3 aimPoint = point + Vector3.up * (0.5f * Mathf.Abs(Physics.gravity.y) * time * time);
            Vector3 dir = (aimPoint - origin).normalized;
            driver.SetLookAngles(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -Mathf.Asin(dir.y) * Mathf.Rad2Deg);
        }

        // Le panneau est-il entièrement dans le champ de la caméra (marge de 2 %) ?
        private static bool PanelFitsCamera(UiPanel panel, Camera cam, out string info) =>
            CanvasFitsCamera(panel.GetComponentInParent<Canvas>(), cam, out info);

        private static bool CanvasFitsCamera(Canvas canvas, Camera cam, out string info)
        {
            RectTransform rect = (RectTransform)canvas.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            bool inFront = true;
            foreach (Vector3 corner in corners)
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                inFront &= v.z > 0.3f;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            info = $"(x {minX:0.00}–{maxX:0.00}, y {minY:0.00}–{maxY:0.00})";
            return inFront && minX >= 0.02f && maxX <= 0.98f && minY >= 0.02f && maxY <= 0.98f;
        }

        // Un bouton du panneau reçoit-il le rayon du pointeur placé sur lui (caméra liée, curseur libre) ?
        private static bool PanelClickable(UiPanel panel, Camera cam)
        {
            Button button = panel.GetComponentInChildren<Button>();
            EventSystem system = EventSystem.current;
            if (button == null || system == null || cam == null)
                return false;
            RectTransform rect = (RectTransform)button.transform;
            PointerEventData data = new PointerEventData(system) { position = cam.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)) };
            List<RaycastResult> hits = new List<RaycastResult>();
            system.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
                if (hit.gameObject != null && hit.gameObject.transform.IsChildOf(button.transform))
                    return true;
            return false;
        }
    }
}
#endif
