// SelfTest (partie « interface et décors ») : vérifie les écrans flottants, les aides de touches, les panneaux,
// le râtelier, les drapeaux, le pont et l'absence d'objets démesurés dans les scènes chargées.
//
// Lancé par SelfTest.RunInterfaceScenario() en mode Play (menu de test ou RunCommand).

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    public partial class SelfTest
    {
        /// <summary>Lance le scénario d'interface : écrans flottants, aides de touches, orientation des panneaux, décors.</summary>
        public static SelfTest RunInterfaceScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.InterfaceScenario());
            return test;
        }

        private IEnumerator InterfaceScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            Services.Get<UnlockService>().ResetProgress();
            Services.Get<TimeOfDayService>().SetHour(12f);
            Services.Get<TimeOfDayService>().Paused = true;
            mode.TeleportToSpawn("hub");
            yield return new WaitForSeconds(1f);
            Camera cam = mode.ActiveHead.GetComponent<Camera>();

            // --- 1. Chaque écran global s'ouvre comme un écran flottant, entièrement visible et de taille raisonnable ---
            (string name, UiPanel panel, System.Action open)[] panels =
            {
                ("Menu principal", hub.MainMenu, () => hub.OpenMainMenu()),
                ("Pause", hub.PauseMenu, () => hub.OpenPause()),
                ("Classement", hub.Leaderboard, () => hub.OpenLeaderboard()),
                ("Voyage rapide", hub.FastTravel, () => hub.OpenFastTravel()),
                ("Profil du feu de camp", hub.ProfileMenu, () => hub.OpenProfileMenu()),
                ("Commandes", hub.Controls, () => hub.OpenControls())
            };
            foreach (var entry in panels)
            {
                if (entry.panel == null)
                {
                    Check(false, entry.name + " : panneau absent de UI_Global");
                    continue;
                }
                entry.open();
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas canvas = entry.panel.GetComponentInParent<Canvas>();
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
                float distance = Vector3.Distance(cam.transform.position, canvas.transform.position);
                bool fits = inFront && minX >= 0.02f && maxX <= 0.98f && minY >= 0.02f && maxY <= 0.98f;
                Check(entry.panel.IsOpen && canvas.renderMode == RenderMode.WorldSpace, entry.name + " : ouvert en écran flottant (espace monde)");
                Check(fits, $"{entry.name} : tient entièrement à l'écran (x {minX:0.00}–{maxX:0.00}, y {minY:0.00}–{maxY:0.00})");
                Check(maxY - minY >= 0.25f && maxX - minX >= 0.25f, $"{entry.name} : assez grand pour être lu ({(maxX - minX) * 100f:0} % de la largeur)");
                Check(distance > 0.5f && distance < 4.5f, $"{entry.name} : à {distance:0.0} m devant le joueur");
                Check(CursorController.IsFreeRequested, entry.name + " : curseur libéré pour cliquer");
                hub.CloseAll();
                yield return null;
                Check(!entry.panel.IsOpen && !CursorController.IsFreeRequested, entry.name + " : se referme et rend le curseur");
            }

            // La première ouverture d'un panneau global doit être vue comme « ouverte » (Échap peut alors le refermer).
            hub.TogglePause();
            yield return null;
            Check(hub.PauseMenu.IsOpen, "Échap : la pause s'ouvre");
            hub.TogglePause();
            yield return null;
            Check(!hub.PauseMenu.IsOpen && Time.timeScale > 0f, "Échap une 2e fois : la pause se referme et le jeu reprend");

            // --- 2. Écran des commandes : toutes les pages, toutes les touches ---
            hub.OpenControls();
            yield return null;
            ControlsPanel controls = hub.Controls;
            controls.ShowPage(2);
            yield return null;
            Check(controls.CurrentPage == 2, "La page « Développement » est disponible en éditeur");
            string legend = ControlsCatalog.DevLegend();
            bool allKeys = true;
            for (int i = 2; i <= 11; i++)
                allKeys &= legend.Contains("F" + i);
            Check(allKeys && legend.Contains("F1"), "La légende de développement liste toutes les touches F1 à F11");
            Check(ControlsCatalog.Game.Length <= ControlsPanel.RowCount && ControlsCatalog.Simulator.Length <= ControlsPanel.RowCount && ControlsCatalog.Dev.Length <= ControlsPanel.RowCount
                && ControlsCatalog.VrGameplay.Length <= ControlsPanel.RowCount, "Chaque page tient dans les lignes du tableau");
            controls.ShowPage(3);
            yield return null;
            Check(controls.CurrentPage == 3, "La page « Gameplay VR » s'affiche");
            hub.CloseAll();

            // La légende de développement (IMGUI) tient dans l'écran, même avec le message de vitesse du jour.
            yield return new WaitForSeconds(0.3f);
            Rect box = DevShortcuts.LastBox;
            Check(box.width > 100f && box.yMin >= 0f && box.yMax <= Screen.height && box.xMin >= 0f && box.xMax <= Screen.width,
                $"La légende des touches tient dans l'écran (cadre {box.xMin:0},{box.yMin:0} → {box.xMax:0},{box.yMax:0} sur {Screen.width}×{Screen.height})");

            // --- 3. Toast : le bandeau est une barre, pas un voile sur tout l'écran ---
            GameEvents.RaiseToast("Message de test");
            yield return new WaitForSeconds(0.6f);
            ToastBannerView toast = FindAnyObjectByType<ToastBannerView>(FindObjectsInactive.Include);
            Transform bar = toast != null ? UiBind.Find(toast.transform, "Bar") : null;
            Check(bar != null, "Le bandeau de notification a une barre « Bar »");
            if (bar != null)
            {
                RectTransform barRect = (RectTransform)bar;
                RectTransform canvasRect = (RectTransform)toast.transform;
                float heightShare = barRect.rect.height / Mathf.Max(1f, canvasRect.rect.height);
                Check(heightShare < 0.2f, $"La notification n'occupe que {heightShare * 100f:0} % de la hauteur de l'écran (pas de voile sombre)");
            }

            // --- 4. Râtelier : objets exposés et textes présents dans le prefab, à la bonne échelle ---
            ArmoryRack rack = FindAnyObjectByType<ArmoryRack>();
            Check(rack != null && rack.Slots.Count == 9, $"Le râtelier a 9 emplacements ({(rack != null ? rack.Slots.Count : 0)})");
            if (rack != null)
            {
                bool baked = true, sane = true, billboard = true, scaleOk = true;
                float biggest = 0f;
                foreach (ArmorySlot slot in rack.Slots)
                {
                    Transform display = slot.transform.Find("Display");
                    Transform label = slot.transform.Find("Label");
                    baked &= display != null && label != null;
                    scaleOk &= Mathf.Abs(slot.transform.lossyScale.x - 1f) < 0.01f;
                    billboard &= label != null && label.GetComponent<FacePlayer>() != null;
                    if (display != null)
                        foreach (Renderer r in display.GetComponentsInChildren<Renderer>())
                            biggest = Mathf.Max(biggest, r.bounds.size.magnitude);
                }
                sane = biggest > 0.2f && biggest < 3f;
                Check(baked, "Chaque emplacement a son objet exposé et son texte déjà posés dans le prefab");
                Check(scaleOk, "Aucun emplacement n'est enfant d'un modèle à l'échelle ×100");
                Check(sane, $"Les objets exposés ont une taille normale (le plus grand : {biggest:0.0} m)");
                Check(billboard, "Les textes du râtelier se tournent vers le joueur");

                // Aide de touche quand on regarde un emplacement.
                ArmorySlot first = rack.Slots[0];
                Vector3 target = first.transform.position;
                Vector3 stand = target + rack.transform.forward * -2.2f;
                stand.y = rack.transform.position.y;
                mode.TeleportTo(new Pose(stand, Quaternion.LookRotation(rack.transform.forward, Vector3.up)));
                yield return new WaitForSeconds(0.3f);
                DesktopPlayerLook(mode, target);
                yield return new WaitForSeconds(0.4f);
                DesktopHud hud = FindAnyObjectByType<DesktopHud>(FindObjectsInactive.Include);
                string hint = hud != null ? hud.CurrentContextHint : "";
                Check(hint.StartsWith("E :") || hint.Contains("verrouillé"), $"Face au râtelier, l'écran indique la touche à utiliser (« {hint} »)");
            }

            // --- 5. Panneau du feu de camp : toujours tourné vers le joueur ---
            mode.TeleportToSpawn("camp");
            yield return new WaitForSeconds(0.8f);
            FacePlayer campMenu = null;
            foreach (FacePlayer f in FindObjectsByType<FacePlayer>(FindObjectsInactive.Exclude))
                if (f.name == "MenuAnchor")
                    campMenu = f;
            Check(campMenu != null, "Le panneau du feu de camp se tourne vers le joueur (FacePlayer)");
            if (campMenu != null)
                Check(FacesHead(campMenu.transform, mode.ActiveHead.position), "Le panneau du feu de camp fait face au joueur assis");

            // --- 6. Bannières, pont, objets démesurés ---
            Transform bridge = null;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name == "Decor_Bridge")
                    bridge = t;
            if (bridge != null)
            {
                Bounds bounds = new Bounds(bridge.GetChild(0).position, Vector3.zero);
                foreach (Renderer r in bridge.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                Check(bounds.min.x <= 73.6f && bounds.max.x >= 84.4f, $"Le pont enjambe toute la rivière (x {bounds.min.x:0.0} → {bounds.max.x:0.0})");
            }
            else
                Check(false, "Le pont est présent dans la scène");

            int flags = 0;
            bool flagsGrounded = true;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith("Flag") || t.Find("Pole") == null)
                    continue;
                flags++;
                Transform pole = t.Find("Pole");
                // Le bas du poteau touche le sol : le centre du poteau est à (hauteur / 2) au-dessus du pied.
                flagsGrounded &= Mathf.Abs(pole.localPosition.y - pole.localScale.y) < 0.05f;
            }
            Check(flags >= 4 && flagsGrounded, $"{flags} drapeaux sur poteau, pied posé au sol");

            // --- 7. Chargement d'une arène : panneaux orientés, plantes dégagées, tableau de l'entraînement à taille normale ---
            Services.Get<FastTravelService>().TravelTo("arena_01", true);
            yield return new WaitForSeconds(3f);
            LevelController arena1 = null;
            foreach (LevelController level in LevelController.All)
                if (!level.IsTraining && level.Definition != null && level.Definition.levelId == "arena_01")
                    arena1 = level;
            Check(arena1 != null, "L'arène 1 est chargée");
            if (arena1 != null)
            {
                Vector3 eye = arena1.transform.TransformPoint(new Vector3(0f, 1.7f, -0.5f));
                foreach (string anchorName in new[] { "BriefingAnchor", "HudAnchor" })
                {
                    Transform anchor = arena1.transform.Find("Panels/" + anchorName);
                    Check(anchor != null && FacesHead(anchor, eye, 0.95f), $"{anchorName} fait face au joueur sur la plateforme");
                }
                Transform start = arena1.transform.Find("StartTarget");
                bool clear = true;
                string blocker = "";
                Transform decor = arena1.transform.Find("Decor");
                if (start != null && decor != null)
                {
                    foreach (Transform plant in decor.GetComponentsInChildren<Transform>())
                    {
                        if (!plant.name.StartsWith("PP_") || plant.parent == null || !plant.parent.name.StartsWith("Foliage"))
                            continue;
                        float d = Vector2.Distance(new Vector2(plant.position.x, plant.position.z), new Vector2(start.position.x, start.position.z));
                        if (d < 2.5f) { clear = false; blocker = plant.name; }
                    }
                }
                Check(clear, "Aucune plante autour de la cible de départ " + blocker);
                Check(decor != null && decor.Find("Foliage_Fleurs") != null, "Les fleurs de la clairière sont rangées dans un groupe « Foliage_Fleurs » de la scène");
            }

            Services.Get<FastTravelService>().TravelTo("training", true);
            yield return new WaitForSeconds(3f);
            bool sizes = true;
            string offender = "";
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                RectTransform r = (RectTransform)canvas.transform;
                float width = r.rect.width * canvas.transform.lossyScale.x;
                if (width > 3.5f) { sizes = false; offender = $"{canvas.name} ({width:0} m)"; }
            }
            Check(sizes, "Aucun panneau d'interface dans le monde ne dépasse 3,5 m de large " + offender);

            List<string> giants = new List<string>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r is TrailRenderer || r is ParticleSystemRenderer)
                    continue;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("terrain") || n.Contains("water") || n.Contains("mountain") || n.Contains("sky") || n.Contains("quad"))
                    continue;
                if (r.bounds.size.magnitude > 60f)
                    giants.Add($"{r.name} ({r.bounds.size.magnitude:0} m)");
            }
            Check(giants.Count == 0, "Aucun objet démesuré dans les scènes chargées " + string.Join(", ", giants));

            // --- 8. Le fondu ne bloque plus personne ---
            FadeService fade = Services.Get<FadeService>();
            FadeService.FadeOperation first1 = fade.FadeOut(1f);
            yield return null;
            FadeService.FadeOperation second = fade.FadeIn(0.1f);
            float waited = 0f;
            while (!first1.Done && waited < 1f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(first1.Done, "Un fondu interrompu libère celui qui l'attendait (plus d'écran sombre bloqué)");
            yield return new WaitForSeconds(0.4f);
            Check(second.Done && fade.CurrentAlpha < 0.01f, "Le fondu suivant rend bien l'écran visible");

            Finish("Interface et décors");
        }

        /// <summary>Lance le scénario du 22/10 : cycle des cibles et tremblement du réticule.</summary>
        public static SelfTest RunCycleAndSwayScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.CycleAndSwayScenario());
            return test;
        }

        private IEnumerator CycleAndSwayScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            UnlockService progress = Services.Get<UnlockService>();
            progress.ResetProgress();
            progress.DemoUnlockAll = true; // toutes les missions jouables pour ce test

            // --- 1. Cycle des cibles dans la mission 1 (toutes difficultés : on teste Normal puis Difficile) ---
            SettingsService settings = Services.Get<SettingsService>();
            foreach ((GameDifficulty difficulty, string missionId) in new[] { (GameDifficulty.Normal, "arena_01"), (GameDifficulty.Hard, "arena_02") })
            {
                settings.SetDifficulty(difficulty);
                Services.Get<FastTravelService>().TravelTo(missionId, true);
                yield return new WaitForSeconds(3.5f);
                LevelController level = null;
                foreach (LevelController l in LevelController.All)
                    if (!l.IsTraining && l.Definition != null && l.Definition.levelId == missionId)
                        level = l;
                Check(level != null, $"[{difficulty}] La mission {missionId} est chargée");
                if (level == null)
                    continue;

                // Sur la plateforme : briefing ; tir sur la cible DÉPART ; compte à rebours puis manche.
                mode.TeleportToSpawn(missionId);
                yield return new WaitForSeconds(0.8f);
                level.StartTargetObject.Trigger();
                float wait = 0f;
                while (level.State != LevelState.Running && wait < 12f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
                Check(level.State == LevelState.Running, $"[{difficulty}] La manche démarre");

                List<Target> cycle = new List<Target>();
                foreach (Target t in level.Targets)
                    if (t != null && !(t is PenaltyTarget) && !(t is StartTarget))
                        cycle.Add(t);
                Check(cycle.Count >= 2, $"[{difficulty}] La mission a {cycle.Count} cibles");

                // Première cible touchée : elle se baisse, les autres restent levées.
                Vector3 shooter = mode.ActiveHead.position;
                cycle[0].RegisterHit(cycle[0].Face.Center, Vector3.forward, shooter);
                yield return new WaitForSeconds(0.7f);
                bool othersUp = true;
                for (int i = 1; i < cycle.Count; i++)
                    othersUp &= !cycle[i].IsLowered;
                Check(cycle[0].IsLowered && othersUp, $"[{difficulty}] La cible touchée se baisse, les autres restent levées");
                bool inert = true;
                foreach (Collider c in cycle[0].GetComponentsInChildren<Collider>())
                    inert &= !c.enabled;
                Check(inert, $"[{difficulty}] La cible baissée est inerte (colliders coupés : les flèches la traversent)");

                // Les cibles intermédiaires se baissent une à une ; tant qu'il en reste une, rien ne se relève.
                for (int i = 1; i < cycle.Count - 1; i++)
                {
                    cycle[i].RegisterHit(cycle[i].Face.Center, Vector3.forward, shooter);
                    yield return new WaitForSeconds(0.5f);
                }
                Check(cycle[0].IsLowered && !cycle[cycle.Count - 1].IsLowered, $"[{difficulty}] Tant qu'une cible reste à toucher, les autres restent baissées");

                // Dernière cible : dès que sa chute est finie, tout se relève INSTANTANÉMENT et le cycle recommence.
                Target lastTarget = cycle[cycle.Count - 1];
                lastTarget.RegisterHit(lastTarget.Face.Center, Vector3.forward, shooter);
                // Mesure en images (et non en secondes : une image lente de l'éditeur fausserait l'écart) entre la fin de la chute et le relevage.
                float hitTime = Time.time;
                int fellFrame = -1, raisedFrame = -1;
                bool allDown = false;
                while (Time.time - hitTime < 4f && raisedFrame < 0)
                {
                    bool everyoneLowered = true;
                    foreach (Target t in cycle)
                        everyoneLowered &= t.IsLowered;
                    bool anyAnimating = false;
                    foreach (Target t in cycle)
                        anyAnimating |= t.IsLowered && t.IsAnimating;
                    if (everyoneLowered)
                    {
                        allDown = true;
                        if (!anyAnimating && fellFrame < 0)
                            fellFrame = Time.frameCount;
                    }
                    if (allDown && !lastTarget.IsLowered)
                        raisedFrame = Time.frameCount;
                    yield return null;
                }
                Check(allDown, $"[{difficulty}] Dernière cible touchée : toutes sont baissées un instant");
                Check(raisedFrame >= 0 && (fellFrame < 0 || raisedFrame - fellFrame <= 3),
                    $"[{difficulty}] Le relevage suit immédiatement la fin de la chute de la dernière cible (écart {raisedFrame - fellFrame} image(s))");
                yield return new WaitForSeconds(1.0f);
                bool allUp = true;
                foreach (Target t in cycle)
                    allUp &= !t.IsLowered;
                Check(allUp, $"[{difficulty}] Puis toutes se relèvent : le cycle recommence");

                // Deuxième tour : la cible 0 peut de nouveau être touchée et se rebaisse.
                cycle[0].RegisterHit(cycle[0].Face.Center, Vector3.forward, shooter);
                yield return new WaitForSeconds(0.7f);
                Check(cycle[0].IsLowered, $"[{difficulty}] Au tour suivant, la cible se rebaisse");
                level.ForceFinishWithThreeStars();
                yield return new WaitForSeconds(1.2f);
                level.Dismiss();
                yield return new WaitForSeconds(0.3f);
                hub.CloseAll();
            }

            // --- 2. Tremblement du réticule : plus fort en difficile, avec le vent et avec la fatigue ---
            AimSway sway = Services.Get<AimSway>();
            Check(sway != null, "Le service AimSway est présent dans Systems");
            if (sway != null)
            {
                GameCatalog catalog = Services.Get<GameCatalog>();
                float easy = catalog.GetDifficulty(GameDifficulty.Easy).aimSwayMultiplier;
                float normal = catalog.GetDifficulty(GameDifficulty.Normal).aimSwayMultiplier;
                float hard = catalog.GetDifficulty(GameDifficulty.Hard).aimSwayMultiplier;
                Check(easy < normal && normal < hard, $"Multiplicateurs de difficulté croissants ({easy:0.00} < {normal:0.00} < {hard:0.00})");
                float aEasy = sway.ComputeAmplitude(easy, 4f, 3f);
                float aHard = sway.ComputeAmplitude(hard, 4f, 3f);
                Check(aHard > aEasy * 3f, $"Même vent, même fatigue : Difficile ({aHard:0.00}°) tremble bien plus que Facile ({aEasy:0.00}°)");
                Check(sway.ComputeAmplitude(1f, 8f, 3f) > sway.ComputeAmplitude(1f, 0f, 3f), "Plus de vent = plus de tremblement");
                Check(sway.ComputeAmplitude(1f, 3f, 6f) > sway.ComputeAmplitude(1f, 3f, 0f) * 2f, "Plus de temps à pleine tension = plus de tremblement");
                Check(sway.ComputeAmplitude(1f, 3f, 3f, 0f) <= 0.0001f, "Corde au repos : aucun tremblement");

                // En jeu : corde tendue à fond en difficile avec du vent → le réticule bouge, au repos il revient au centre.
                settings.SetDifficulty(GameDifficulty.Hard);
                Services.Get<DifficultyApplier>().Unfreeze();
                Services.Get<WindService>().SetManualLevel(3);
                DesktopHud hud = FindAnyObjectByType<DesktopHud>(FindObjectsInactive.Include);
                RectTransform crosshair = hud != null ? UiBind.Find(hud.transform, "Crosshair") as RectTransform : null;
                Check(crosshair != null, "Le réticule du HUD existe");
                float maxOffset = 0f;
                float elapsed = 0f;
                Vector2 first = Vector2.zero, last = Vector2.zero;
                while (elapsed < 4f)
                {
                    GameEvents.RaiseBowDrawing(mode.ActiveHead.position, 1f);
                    elapsed += Time.deltaTime;
                    yield return null;
                    if (crosshair != null)
                    {
                        maxOffset = Mathf.Max(maxOffset, crosshair.anchoredPosition.magnitude);
                        if (elapsed < 0.5f) first = sway.Offset;
                        last = sway.Offset;
                    }
                }
                Check(sway.IsActive && sway.HoldTime > 3f, $"Tremblement actif après {sway.HoldTime:0.0} s de maintien (amplitude {sway.Amplitude:0.00}°)");
                Check(maxOffset > 3f, $"Le réticule s'écarte du centre (jusqu'à {maxOffset:0} unités du Canvas)");
                Check((last - first).magnitude > 0.01f, "Le décalage varie dans le temps (mouvement aléatoire)");
                GameEvents.RaiseBowReleased(mode.ActiveHead.position, 1f, false);
                yield return new WaitForSeconds(0.4f);
                Check(!sway.IsActive && sway.Offset == Vector2.zero, "Après le tir, le réticule revient au centre");
                Services.Get<WindService>().SetManualLevel(0);
            }
            settings.SetDifficulty(GameDifficulty.Normal);
            progress.DemoUnlockAll = false;
            Finish("Cycle des cibles et tremblement");
        }

        /// <summary>Lance le scénario du ravin : on peut y tomber (rien ne bloque le rebord) et en remonter par la rampe.</summary>
        public static SelfTest RunRavineScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.RavineScenario());
            return test;
        }

        private IEnumerator RavineScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();
            DesktopPlayerController player = FindAnyObjectByType<DesktopPlayerController>();
            CharacterController body = player != null ? player.GetComponent<CharacterController>() : null;
            Check(body != null, "Le joueur clavier/souris a un Character Controller");
            if (body == null)
            {
                Finish("Ravin");
                yield break;
            }

            Services.Get<FastTravelService>().TravelTo("arena_04", true);
            yield return new WaitForSeconds(3.5f);

            // 1) Chute : depuis la plateforme de l'arène, on marche vers le vide ; rien ne doit retenir le joueur.
            Vector3 start = new Vector3(116.5f, 10.1f, 83f);
            mode.TeleportTo(new Pose(start, Quaternion.Euler(0f, 90f, 0f)));
            yield return new WaitForSeconds(0.6f);
            float t = 0f;
            while (t < 3.5f && body.transform.position.y > 4f)
            {
                body.Move(new Vector3(3f * Time.deltaTime, 0f, 0f));
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            float floorY = body.transform.position.y;
            Check(floorY < 2f, $"Le joueur tombe dans le ravin (altitude {floorY:0.0} m, x = {body.transform.position.x:0.0})");

            // 2) Remontée : on marche vers le nord par la rampe (limite de pente du Character Controller : 45°).
            Vector3 bottom = new Vector3(127.5f, 0.3f, 76f);
            mode.TeleportTo(new Pose(bottom, Quaternion.Euler(0f, 180f, 0f)));
            yield return new WaitForSeconds(0.6f);
            t = 0f;
            while (t < 14f && body.transform.position.y < 9.4f)
            {
                body.Move(new Vector3(0f, 0f, -3f * Time.deltaTime));
                t += Time.deltaTime;
                yield return null;
            }
            Vector3 top = body.transform.position;
            Check(top.y > 9.4f && top.z < 58f, $"La rampe se remonte à pied (altitude {top.y:0.0} m en {t:0.0} s, z = {top.z:0.0})");
            Finish("Ravin");
        }

        private static bool FacesHead(Transform panel, Vector3 head, float minDot = 0.97f)
        {
            // Un Canvas ou un texte 3D se lit depuis son côté −Z : la normale de lecture est donc −forward.
            Vector3 toHead = head - panel.position;
            toHead.y = 0f;
            Vector3 readable = -panel.forward;
            readable.y = 0f;
            if (toHead.sqrMagnitude < 0.0001f || readable.sqrMagnitude < 0.0001f)
                return false;
            return Vector3.Dot(toHead.normalized, readable.normalized) >= minDot;
        }

        // Oriente la vue du joueur au clavier vers un point.
        private static void DesktopPlayerLook(PlayerModeService mode, Vector3 point)
        {
            DesktopPlayerController player = FindAnyObjectByType<DesktopPlayerController>();
            if (player == null || mode.ActiveHead == null)
                return;
            Vector3 dir = point - mode.ActiveHead.position;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(dir.y / Mathf.Max(0.01f, dir.magnitude)) * Mathf.Rad2Deg;
            player.SetLookAngles(yaw, pitch);
        }
    }
}
#endif
