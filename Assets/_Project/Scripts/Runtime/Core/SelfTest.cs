// SelfTest : scénarios de test automatiques joués DANS le jeu (éditeur et builds Development).
//
// OÙ LE POSER : n'est pas posé à la main. Le menu Tools → Archery Challenge → Run Self Test (en Play)
// ou la console de développement lance les scénarios ; chaque étape écrit « [SelfTest] ✔ … » ou
// « [SelfTest] ✘ … » dans la Console.
//
// POURQUOI : sans casque ni testeur, ces scénarios pilotent le joueur par les mêmes entrées que
// le clavier/souris (injections de DesktopArcherInput) et vérifient la chaîne complète : prise de la
// flèche, tension, tir, vol physique, impact, score, étoiles, déblocages.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Input;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Monsters;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Scénarios de test automatiques.</summary>
    public partial class SelfTest : MonoBehaviour
    {
        private readonly List<string> report = new List<string>();
        private int failures;

        /// <summary>Résultat texte du dernier scénario.</summary>
        public static string LastReport { get; private set; } = "";

        /// <summary>Nombre d'échecs du dernier scénario.</summary>
        public static int LastFailures { get; private set; } = -1;

        /// <summary>Vrai tant qu'un scénario tourne.</summary>
        public static bool Running { get; private set; }

        /// <summary>Lance le scénario de tir au clavier/souris sur les cibles du monde.</summary>
        public static SelfTest RunDesktopShooting()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.DesktopShootingScenario());
            return test;
        }

        /// <summary>Lance une manche complète de la campagne (arène 1) avec tirs automatiques.</summary>
        public static SelfTest RunCampaignRound()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.CampaignRoundScenario());
            return test;
        }

        /// <summary>Lance le scénario profils, sauvegardes JSON et classement.</summary>
        public static SelfTest RunSaveScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.SaveScenario());
            return test;
        }

        /// <summary>Lance le scénario VR (simulateur XR) : arc à l'épaule, flèche au carquois, encochage, tir.</summary>
        public static SelfTest RunVrScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.VrScenario());
            return test;
        }

        /// <summary>Lance le scénario du monde : barrières, voyage rapide, jour/nuit, monstres et PV.</summary>
        public static SelfTest RunWorldScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.WorldScenario());
            return test;
        }

        /// <summary>Lance le scénario d'entraînement (équipement libre, statistiques de tir, vent manuel).</summary>
        public static SelfTest RunTrainingScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.TrainingScenario());
            return test;
        }

        private void Check(bool condition, string message)
        {
            string line = (condition ? "✔ " : "✘ ") + message;
            report.Add(line);
            if (!condition)
                failures++;
            Debug.Log("[SelfTest] " + line);
        }

        private void Finish(string name)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {name} : {(failures == 0 ? "RÉUSSI" : failures + " échec(s)")} ===");
            foreach (string line in report)
                sb.AppendLine(line);
            LastReport = sb.ToString();
            LastFailures = failures;
            Running = false;
            Debug.Log("[SelfTest] " + sb);
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- scénario : tir au PC

        private IEnumerator DesktopShootingScenario()
        {
            Running = true;
            LastFailures = -1;

            // Attente du démarrage complet du jeu.
            GameBootstrap bootstrap = Services.Get<GameBootstrap>();
            float wait = 0f;
            while ((bootstrap == null || !bootstrap.IsReady) && wait < 20f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(bootstrap != null && bootstrap.IsReady, "Le jeu démarre (Boot → World, joueur placé)");

            PlayerModeService mode = Services.Get<PlayerModeService>();
            Check(mode != null && mode.Mode == PlayerMode.Desktop, "Mode clavier/souris actif sans casque");
            DesktopArcherInput input = Services.Get<DesktopArcherInput>();
            DesktopBowView view = FindAnyObjectByType<DesktopBowView>();
            Check(input != null && view != null, "Pilote clavier/souris et vue de l'arc présents");
            if (input == null || view == null || mode == null)
            {
                Finish("Tir au clavier/souris");
                yield break;
            }

            // Le menu des profils s'ouvre tout seul au démarrage (curseur libre, arc masqué) : on le referme comme le ferait le joueur.
            UiHub hub = Services.Get<UiHub>();
            if (hub != null)
            {
                hub.AutoOpenProfile = false;
                hub.CloseAll();
            }
            yield return new WaitForSeconds(0.5f);
            Check(view.CurrentBow != null, "L'arc équipé est affiché à l'écran");

            // Les arènes ne se chargent qu'à l'approche du joueur : on part du stand d'entraînement (cibles toujours présentes).
            Services.Get<FastTravelService>()?.TravelTo("training", true);
            yield return new WaitForSeconds(3.5f);

            // Cible la plus proche devant le joueur.
            Target target = null;
            float best = float.MaxValue;
            foreach (Target t in Target.All)
            {
                if (t is PenaltyTarget || t is StartTarget || t.Face == null) continue;
                float d = Vector3.Distance(mode.ActiveHead.position, t.Face.Center);
                if (d < best) { best = d; target = t; }
            }
            Check(target != null, "Une cible existe dans le monde");
            if (target == null)
            {
                Finish("Tir au clavier/souris");
                yield break;
            }

            // Orientation du joueur vers la cible (compensation de la chute de la flèche).
            ScoreService score = Services.Get<ScoreService>();
            score.ResetRun();
            score.AcceptShots = true;
            AimAt(mode, view, target.Face.Center);
            yield return null;
            yield return null;

            // R : prendre et encocher une flèche.
            input.ForceNock();
            yield return null;
            yield return null;
            Check(view.CurrentBow.HasArrow, "R : une flèche est prise et encochée");

            // Clic gauche maintenu : tension croissante.
            int shotsBefore = score.ArrowsShot;
            bool impact = false;
            ArrowImpactInfo impactInfo = default;
            void OnImpact(ArrowImpactInfo i) { impact = true; impactInfo = i; }
            GameEvents.ArrowImpacted += OnImpact;

            input.ForceFire(true);
            float drawTime = view.CurrentBow.Definition.desktopDrawTime + 0.25f;
            yield return new WaitForSeconds(drawTime);
            Check(view.Tension > 0.95f, $"La tension monte jusqu'à 100 % (valeur {view.Tension:P0})");
            AimAt(mode, view, target.Face.Center);
            yield return null;

            // Relâcher : tir.
            input.ForceFire(false);
            yield return null;
            yield return null;
            Check(Arrow.FlyingCount > 0 || impact, "Relâcher : la flèche part (en vol)");
            Check(score.ArrowsShot == shotsBefore + 1, "La flèche tirée est comptée");

            // Vol puis impact.
            float flight = 0f;
            while (!impact && flight < 6f)
            {
                flight += Time.deltaTime;
                yield return null;
            }
            GameEvents.ArrowImpacted -= OnImpact;
            Check(impact, $"La flèche se plante (impact après {flight:0.00} s)");
            Check(impact && impactInfo.HitTarget, "Elle touche une cible");
            Check(score.Score > 0, $"Le score augmente (score = {score.Score}, anneau {score.LastShot.Ring})");

            Finish("Tir au clavier/souris");
        }

        // ---------------------------------------------------------------- scénario : manche complète

        private IEnumerator WaitReady()
        {
            GameBootstrap bootstrap = Services.Get<GameBootstrap>();
            float wait = 0f;
            while ((bootstrap == null || !bootstrap.IsReady) && wait < 30f)
            {
                wait += Time.unscaledDeltaTime;
                bootstrap = Services.Get<GameBootstrap>();
                yield return null;
            }
            yield return new WaitForSeconds(1.5f); // fin du fondu d'entrée
        }

        // Tire une flèche vers une cible (R, tension complète, relâcher) et attend l'impact.
        private IEnumerator ShootAt(Target target, DesktopArcherInput input, DesktopBowView view, PlayerModeService mode)
        {
            AimAt(mode, view, target.Face.Center);
            yield return null;
            input.ForceNock();
            yield return null;
            yield return null;
            input.ForceFire(true);
            float drawTime = view.CurrentBow != null ? view.CurrentBow.Definition.desktopDrawTime + 0.2f : 1f;
            yield return new WaitForSeconds(drawTime);
            AimAt(mode, view, target.Face.Center);
            yield return null;
            input.ForceFire(false);
            float t = 0f;
            yield return null;
            while (Arrow.FlyingCount > 0 && t < 4f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.15f);
        }

        private IEnumerator CampaignRoundScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            DesktopArcherInput input = Services.Get<DesktopArcherInput>();
            DesktopBowView view = FindAnyObjectByType<DesktopBowView>();
            UnlockService unlocks = Services.Get<UnlockService>();
            SettingsService settings = Services.Get<SettingsService>();
            UiHub hub = Services.Get<UiHub>();
            if (hub != null)
            {
                hub.AutoOpenProfile = false;
                hub.CloseAll();
            }

            settings.SetDifficulty(GameDifficulty.Easy);
            unlocks.ResetProgress();
            Check(!unlocks.IsLevelUnlocked("arena_02"), "L'arène 2 est verrouillée au départ");
            Check(unlocks.IsLevelUnlocked("arena_01"), "L'arène 1 est disponible au départ");

            // Voyage rapide vers l'arène 1 puis placement sur la plateforme.
            FastTravelService travel = Services.Get<FastTravelService>();
            travel.TravelTo("arena_01", true);
            yield return new WaitForSeconds(2.5f);
            LevelController level = null;
            foreach (LevelController l in LevelController.All)
                if (l.Definition != null && l.Definition.levelId == "arena_01")
                    level = l;
            Check(level != null, "L'arène 1 est chargée après le voyage rapide");
            if (level == null)
            {
                Finish("Manche complète");
                yield break;
            }

            mode.TeleportToSpawn("arena_01");
            yield return new WaitForSeconds(0.6f);
            Check(level.State == LevelState.Briefing, $"Sur la plateforme : briefing (état {level.State})");

            // Départ par la cible DÉPART.
            level.StartTargetObject.Trigger();
            yield return new WaitForSeconds(0.5f);
            Check(level.State == LevelState.Countdown, $"Compte à rebours lancé (état {level.State})");
            yield return new WaitForSeconds(3.4f);
            Check(level.State == LevelState.Running, $"La manche démarre (état {level.State})");
            Check(ArrowAllowance.Remaining > 0, $"Flèches disponibles : {ArrowAllowance.Remaining}");
            Check(level.TimeLeft > 0f && level.TimeLeft < level.TotalTime, $"La minuterie tourne ({level.TimeLeft:0.0}/{level.TotalTime:0} s)");

            // Tirs sur les cibles tant que la manche dure.
            int shots = 0;
            List<Target> targets = new List<Target>(level.Targets);
            while (level.State == LevelState.Running && shots < 20)
            {
                Target t = targets[shots % targets.Count];
                yield return ShootAt(t, input, view, mode);
                shots++;
            }
            yield return new WaitForSeconds(1.2f);
            Check(level.State == LevelState.Finished, $"La manche se termine (état {level.State}) après {shots} tirs");

            LevelResult result = level.LastResult;
            Check(result != null, "Un résultat est produit");
            if (result != null)
            {
                Check(result.Score > 0, $"Score final {result.Score} (étoiles {result.Stars}, précision {result.Accuracy:P0}, bonus de temps {result.TimeBonus})");
                Check(result.ArrowsShot > 0, $"Flèches tirées : {result.ArrowsShot}");
                if (!result.Passed)
                {
                    Debug.Log("[SelfTest] Manche non réussie par les tirs automatiques : on valide les déblocages avec la triche de test.");
                    level.TeleportCheat();
                }
            }
            level.Dismiss();
            yield return new WaitForSeconds(0.3f);
            Check(unlocks.IsLevelPassed("arena_01"), "L'arène 1 est marquée réussie");
            Check(unlocks.IsLevelUnlocked("arena_02"), "L'arène 2 est maintenant débloquée");
            Check(unlocks.IsBowUnlocked("bow_hunter"), "L'arc de chasse est débloqué");

            Finish("Manche complète");
        }

        // ---------------------------------------------------------------- scénario : VR (simulateur)

        private IEnumerator VrScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            if (hub != null)
            {
                hub.AutoOpenProfile = false;
                hub.CloseAll();
            }
            Services.Get<SettingsService>().SetDifficulty(GameDifficulty.Easy);
            ArrowAllowance.SetUnlimited();

            // Bascule vers le rig VR piloté par le simulateur (touche F2 en jeu).
            mode.ToggleSimulator();
            // Ce scénario place lui-même les mains : le pilote clavier/souris du rig simulé ne doit pas les déplacer.
            SimulatedRigDriver rigDriver = mode.XrRig != null ? mode.XrRig.GetComponent<SimulatedRigDriver>() : null;
            if (rigDriver != null)
                rigDriver.HandsPaused = true;
            yield return new WaitForSeconds(1.5f);
            Check(mode.Mode == PlayerMode.Simulator && mode.XrRig.activeInHierarchy, "F2 : le rig VR (simulateur) est actif");
            XRArcherInput xr = Services.Get<XRArcherInput>();
            Check(xr != null && xr.IsActive, "Le pilote VR XRArcherInput est enregistré");
            Check(mode.DesktopRig != null && !mode.DesktopRig.activeSelf, "Le joueur clavier/souris est désactivé");
            if (xr == null)
            {
                Finish("Tir en VR (simulateur)");
                yield break;
            }

            // On fige les poses pilotées par le simulateur : les mains sont déplacées par le test.
            foreach (Behaviour b in xr.GetComponentsInChildren<Behaviour>(true))
                if (b != null && b.GetType().Name == "TrackedPoseDriver")
                    b.enabled = false;

            Bow bow = xr.BowHolster.CurrentBow;
            Check(bow != null, "L'arc équipé apparaît dans l'étui d'épaule");
            if (bow == null)
            {
                Finish("Tir en VR (simulateur)");
                yield break;
            }

            // Cible : la plus proche devant le joueur.
            Target target = null;
            float best = float.MaxValue;
            foreach (Target t in Target.All)
            {
                if (t is PenaltyTarget || t is StartTarget || t.Face == null) continue;
                float d = Vector3.Distance(xr.Head.position, t.Face.Center);
                if (d < best) { best = d; target = t; }
            }

            // 1) Saisie de l'arc par la main gauche (sélection XRI réelle).
            Transform left = xr.GetHand(Handedness.Left);
            Transform right = xr.GetHand(Handedness.Right);
            left.position = bow.transform.position;
            UnityEngine.XR.Interaction.Toolkit.XRInteractionManager manager = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor leftInteractor = left.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            Check(manager != null && leftInteractor != null, "XR Interaction Manager et interacteur de la main gauche présents");
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)leftInteractor, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)bow.GrabInteractable);
            yield return new WaitForSeconds(0.3f);
            Check(bow.IsHeld, "L'arc est saisi (XR Grab Interactable)");
            Check(bow.BowHand == Handedness.Left, $"Main d'arc détectée : {bow.BowHand}");

            // Visée : la manette gauche regarde la cible, légèrement relevée pour la chute.
            Vector3 aimPoint = target != null ? target.Face.Center : xr.Head.position + xr.Head.forward * 20f;
            float distance = Vector3.Distance(left.position, aimPoint);
            Vector3 raised = aimPoint + Vector3.up * (0.5f * 9.81f * Mathf.Pow(distance / 28f, 2f));
            left.position = xr.Head.position + xr.Head.forward * 0.45f - Vector3.up * 0.15f + xr.Head.right * -0.15f;
            left.rotation = Quaternion.LookRotation(raised - left.position, Vector3.up);
            yield return new WaitForSeconds(0.3f);
            Check(Vector3.Angle(bow.transform.forward, left.forward) < 8f, $"L'arc suit la main d'arc (écart {Vector3.Angle(bow.transform.forward, left.forward):0.0}°)");

            // 2) Flèche au carquois dorsal : main droite dans le dos + Grip.
            BackQuiver quiver = xr.BackQuiver;
            right.position = quiver.transform.position;
            xr.ForceGrip(Handedness.Right, 1);
            yield return new WaitForSeconds(0.25f);
            Arrow held = quiver.GetHeldArrow(Handedness.Right);
            Check(held != null, "Main dans le dos + Grip : une flèche apparaît dans la main");

            // 3) Encochage : la flèche approche de la corde.
            right.position = bow.StringRestPosition + bow.transform.right * 0.04f;
            right.rotation = left.rotation;
            yield return new WaitForSeconds(0.35f);
            Check(bow.HasArrow, "La flèche s'encoche toute seule près de la corde");

            // 4) Tension : la main recule le long de l'axe de l'arc.
            for (int i = 1; i <= 20; i++)
            {
                right.position = bow.StringRestPosition - bow.transform.forward * (0.45f * i / 20f);
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            Check(bow.Tension > 0.6f, $"La corde est tendue (tension {bow.Tension:P0})");

            // 5) Relâchement : le Grip est lâché, la flèche part et se plante.
            int shotsBefore = Services.Get<ScoreService>().ArrowsShot;
            bool impact = false;
            ArrowImpactInfo info = default;
            void OnImpact(ArrowImpactInfo i) { impact = true; info = i; }
            GameEvents.ArrowImpacted += OnImpact;
            xr.ForceGrip(Handedness.Right, 0);
            yield return new WaitForSeconds(0.2f);
            Check(Arrow.FlyingCount > 0 || impact, "Relâcher le Grip : la flèche part");
            float wait = 0f;
            while (!impact && wait < 5f) { wait += Time.deltaTime; yield return null; }
            GameEvents.ArrowImpacted -= OnImpact;
            Check(impact, $"La flèche se plante ({info.Surface}{(info.HitTarget ? ", cible touchée" : "")})");
            Check(Services.Get<ScoreService>().ArrowsShot == shotsBefore + 1, "Le tir VR est compté");
            Check(!bow.HasArrow && bow.Tension < 0.05f, "La corde est revenue au repos");

            // 6) Arc lâché : il revient à l'épaule après le délai.
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)leftInteractor, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)bow.GrabInteractable);
            yield return new WaitForSeconds(1.2f);
            Check(!bow.IsHeld && bow.transform.parent == xr.BowHolster.transform, "Arc lâché : il retourne dans l'étui d'épaule");

            xr.ForceGrip(Handedness.Right, -1);
            foreach (Behaviour b in xr.GetComponentsInChildren<Behaviour>(true))
                if (b != null && b.GetType().Name == "TrackedPoseDriver")
                    b.enabled = true;
            if (rigDriver != null)
                rigDriver.HandsPaused = false;
            mode.ToggleSimulator();
            Finish("Tir en VR (simulateur)");
        }

        // ---------------------------------------------------------------- scénario : entraînement

        private IEnumerator TrainingScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            DesktopArcherInput input = Services.Get<DesktopArcherInput>();
            DesktopBowView view = FindAnyObjectByType<DesktopBowView>();
            UnlockService unlocks = Services.Get<UnlockService>();
            EquipmentService equipment = Services.Get<EquipmentService>();
            UiHub hub = Services.Get<UiHub>();
            if (hub != null)
            {
                hub.AutoOpenProfile = false;
                hub.CloseAll();
            }
            unlocks.ResetProgress();
            Check(!unlocks.IsBowUnlocked("bow_royal") && !equipment.SelectBow("bow_royal"), "Hors entraînement, l'arc royal est verrouillé");

            Services.Get<FastTravelService>().TravelTo("training", true);
            yield return new WaitForSeconds(2.5f);
            mode.TeleportToSpawn("training");
            yield return new WaitForSeconds(0.8f);
            LevelController level = null;
            foreach (LevelController l in LevelController.All)
                if (l.IsTraining) level = l;
            Check(level != null && level.State == LevelState.Running, $"Sur la plateforme d'entraînement : mode libre actif ({level?.State})");
            Check(ArrowAllowance.Unlimited, "Flèches illimitées à l'entraînement");
            Check(equipment.SelectBow("bow_royal") && equipment.SelectedBow.id == "bow_royal", "Tout l'équipement est utilisable à l'entraînement (arc royal)");
            Check(equipment.SelectArrow("arrow_carbon"), "Flèches en carbone sélectionnables");
            yield return new WaitForSeconds(0.5f);
            Check(view.CurrentBow != null && view.CurrentBow.Definition.id == "bow_royal", "L'arc affiché à l'écran change avec l'équipement");

            TrainingMode training = level.GetComponent<TrainingMode>();
            Check(training != null && training.StatsPanel != null, "TrainingMode et son panneau de statistiques sont présents");
            training.SetWind(3);
            Check(Services.Get<WindService>().Speed > 1f, $"Le vent réglé à la main souffle ({Services.Get<WindService>().Speed:0.0} m/s)");
            training.SetWind(0);

            Target target = null;
            float best = float.MaxValue;
            foreach (Target t in level.Targets)
            {
                if (t == null || t.Face == null || t.Mover != null) continue;
                float d = Vector3.Distance(mode.ActiveHead.position, t.Face.Center);
                if (d < best) { best = d; target = t; }
            }
            yield return ShootAt(target, input, view, mode);
            Check(training.ShotCount == 1 && training.LastSpeed > 40f, $"Statistiques : {training.ShotCount} tir, {training.LastSpeed:0} m/s, {training.LastDistance:0.0} m, {training.LastPoints} pts");
            training.ResetStats();
            Check(training.ShotCount == 0 && Services.Get<ScoreService>().Score == 0, "Réinitialiser remet les statistiques à zéro");

            // Quitter le stand : l'équipement verrouillé redevient interdit.
            mode.TeleportToSpawn("hub");
            yield return new WaitForSeconds(0.8f);
            Check(level.State == LevelState.Available && !equipment.IgnoreLocks, "En quittant le stand, les verrous reviennent");
            Check(equipment.SelectedBow.id == "bow_apprentice", "L'arc verrouillé est remplacé par l'arc de départ");
            Finish("Entraînement");
        }

        // ---------------------------------------------------------------- scénario : monde

        private IEnumerator WorldScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UnlockService unlocks = Services.Get<UnlockService>();
            UiHub hub = Services.Get<UiHub>();
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            FastTravelService travel = Services.Get<FastTravelService>();
            if (hub != null)
            {
                hub.AutoOpenProfile = false;
                hub.CloseAll();
            }
            unlocks.ResetProgress();
            unlocks.DemoUnlockAll = false;

            // --- Barrières de progression et voyage rapide ---
            ProgressionGate[] gates = FindObjectsByType<ProgressionGate>(FindObjectsInactive.Include);
            Check(gates.Length == 4, $"4 barrières de progression ({gates.Length})");
            bool allClosed = true;
            foreach (ProgressionGate g in gates) allClosed &= !g.IsOpen;
            Check(allClosed, "Toutes les barrières sont fermées au départ");
            var available = travel.GetAvailableDestinations();
            Check(available.Count == 3, $"Voyage rapide au départ : hub, entraînement, arène 1 ({available.Count} destinations)");

            unlocks.DemoUnlockAll = true;
            yield return new WaitForSeconds(0.3f);
            bool allOpen = true;
            foreach (ProgressionGate g in gates) allOpen &= g.IsOpen;
            Check(allOpen, "Mode démo (F5) : les barrières s'ouvrent");
            Check(travel.GetAvailableDestinations().Count == 7, $"Mode démo : 7 destinations ({travel.GetAvailableDestinations().Count})");
            unlocks.DemoUnlockAll = false;
            yield return new WaitForSeconds(0.2f);

            // --- Jour et nuit ---
            mode.TeleportToSpawn("hub");
            yield return new WaitForSeconds(0.5f);
            time.SetHour(22f);
            time.Paused = true;
            yield return new WaitForSeconds(1.2f);
            SunMoonController sunMoon = FindAnyObjectByType<SunMoonController>();
            Check(sunMoon != null && !sunMoon.Sun.enabled && sunMoon.Moon.enabled, "À 22 h : le soleil est éteint, la lune éclaire");
            int lit = 0, flames = 0;
            foreach (NightLight nl in NightLight.All)
            {
                if (nl.LightSource != null && nl.LightSource.enabled) lit++;
            }
            int alwaysOn = 0;
            foreach (NightLight nl in NightLight.All) if (nl.AlwaysOn) alwaysOn++;
            Check(lit > 0 && lit <= TimeOfDayService.MaxActiveLights + alwaysOn, $"La nuit : {lit} lumières actives (plafond {TimeOfDayService.MaxActiveLights} + {alwaysOn} feu de camp)");

            // Rayon d'allumage : les lanternes à moins de 100 m du joueur s'allument, celles au-delà restent éteintes.
            Vector3 eye = mode.ActiveHead.position;
            int nearTorches = 0, litNear = 0, litFar = 0;
            foreach (NightLight nl in NightLight.All)
            {
                if (nl.AlwaysOn) continue;
                bool near = (nl.transform.position - eye).magnitude <= time.LightRadius;
                if (near) nearTorches++;
                if (nl.LightSource != null && nl.LightSource.enabled)
                {
                    if (near) litNear++; else litFar++;
                }
            }
            Check(Mathf.Approximately(time.LightRadius, 100f), $"Rayon d'allumage des lanternes : {time.LightRadius:0} m");
            Check(litFar == 0, $"Aucune lanterne éclairée au-delà de {time.LightRadius:0} m ({litFar})");
            Check(litNear == Mathf.Min(nearTorches, TimeOfDayService.MaxActiveLights), $"Les lanternes à moins de {time.LightRadius:0} m sont allumées ({litNear} sur {nearTorches}, plafond {TimeOfDayService.MaxActiveLights})");
            foreach (NightLight nl in NightLight.All) if (!nl.AlwaysOn) flames++;
            Check(flames >= 10, $"{flames} torches enregistrées (leurs flammes restent visibles)");
            time.SetHour(12f);
            yield return new WaitForSeconds(1.2f);
            Check(sunMoon.Sun.enabled && !sunMoon.Moon.enabled, "À midi : le soleil éclaire, la lune est éteinte");
            time.Paused = false;

            // --- Monstres et points de vie ---
            PlayerHealth health = Services.Get<PlayerHealth>();
            health.FullHeal();
            health.Damage(4, mode.ActiveRoot.position);
            Check(health.Health == 6, $"Un coup de tortue retire 4 PV ({health.Health}/10)");
            yield return new WaitForSeconds(1.2f);
            MonsterSpawner[] spawners = FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Include);
            Check(spawners.Length == 6, $"6 points d'apparition de monstres ({spawners.Length})");
            if (spawners.Length > 0)
            {
                mode.TeleportTo(new Pose(new Vector3(30f, 10.1f, 132f), Quaternion.identity)); // forêt nord-ouest
                yield return new WaitForSeconds(2.5f);
                MonsterBrain slime = null;
                foreach (MonsterBrain m in FindObjectsByType<MonsterBrain>(FindObjectsInactive.Exclude))
                    if (m.Definition != null && m.Definition.id == "slime") slime = m;
                Check(slime != null, "Les slimes de la forêt apparaissent à l'approche du joueur");
                if (slime != null)
                {
                    int before = health.Health;
                    slime.GetComponent<MonsterHealth>().ApplyDamage(1, slime.transform.position);
                    yield return null;
                    Check(slime.IsDead, "Une flèche suffit à tuer un slime");
                    Check(health.Health == Mathf.Min(10, before + 2), $"Éliminer un slime rend 2 PV ({before} → {health.Health})");
                }
            }

            // --- Mort : réapparition au hub ---
            health.Damage(99, mode.ActiveRoot.position);
            yield return new WaitForSeconds(2.8f);
            Vector3 hubPosition = Vector3.zero;
            if (PlayerSpawnPoint.TryFind("hub", out PlayerSpawnPoint hubSpawn))
                hubPosition = hubSpawn.transform.position;
            Check(health.Health == 10 && Vector3.Distance(new Vector3(mode.ActiveRoot.position.x, 0, mode.ActiveRoot.position.z), new Vector3(hubPosition.x, 0, hubPosition.z)) < 2f,
                "À 0 PV : fondu, réapparition au hub avec 10 PV");
            Finish("Monde");
        }

        // ---------------------------------------------------------------- scénario : sauvegarde

        private IEnumerator SaveScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            // On travaille dans un dossier isolé pour ne pas toucher aux vraies sauvegardes.
            string temp = System.IO.Path.Combine(Application.temporaryCachePath, "selftest_" + System.Guid.NewGuid().ToString("N"));
            JsonFileStore.SetRootForTests(temp);

            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            LeaderboardService board = Services.Get<LeaderboardService>();
            UnlockService unlocks = Services.Get<UnlockService>();
            GameCatalog catalog = Services.Get<GameCatalog>();

            ProfileData profile = profiles.Create("  Élodie   l'Archère ");
            Check(profile != null && profile.name == "Élodie l'Archère", $"Profil créé avec un nom nettoyé : « {profile?.name} »");
            Check(profiles.Create("   ") == null, "Un nom vide est refusé");
            Check(NameValidator.TryClean(new string('é', 40), out string cut) && NameValidator.Length(cut) == 20, "Un nom de 40 caractères est coupé à 20");
            Check(profiles.Select(profile.id), "Le profil est sélectionné");

            slots.NewGame(profile.id, 1, catalog);
            unlocks.Unlock("bow_hunter");
            slots.Current.stats.arrowsShot = 12;
            slots.Save();
            Check(JsonFileStore.Exists("profiles.json"), "profiles.json existe");
            Check(JsonFileStore.Exists($"saves/{profile.id}/slot1.json"), "saves/<profil>/slot1.json existe");
            Check(!slots.GetSummary(profile.id, 2).Exists, "Le slot 2 est vide");

            // Rechargement : la progression est retrouvée.
            slots.NewGame(profile.id, 2, catalog);
            Check(slots.Load(profile.id, 1) && slots.Current.unlockedBows.Contains("bow_hunter"), "Rechargement du slot 1 : l'arc de chasse est débloqué");

            // Classement : tri, égalités, filtre, UTF-8.
            board.Clear();
            LevelResult r1 = new LevelResult { LevelId = "arena_01", Score = 90, Difficulty = GameDifficulty.Normal, Stars = 2, Accuracy = 0.8f, XCount = 3 };
            LevelResult r2 = new LevelResult { LevelId = "arena_01", Score = 120, Difficulty = GameDifficulty.Hard, Stars = 3, Accuracy = 0.9f, XCount = 1 };
            LevelResult r3 = new LevelResult { LevelId = "arena_01", Score = 90, Difficulty = GameDifficulty.Normal, Stars = 2, Accuracy = 0.7f, XCount = 5 };
            board.Add("Élodie", profile.id, r1);
            board.Add("李明", "", r2);
            board.Add("Zoé", "", r3);
            var top = board.Query("arena_01", null, 10);
            Check(top.Count == 3 && top[0].playerName == "李明" && top[1].playerName == "Zoé" && top[2].playerName == "Élodie",
                $"Tri : score, puis X : {string.Join(" > ", top.ConvertAll(e => e.playerName))}");
            Check(board.Query("arena_01", GameDifficulty.Hard, 10).Count == 1, "Filtre de difficulté");
            Check(board.Add("   ", "", r1) == null, "Un nom vide n'est pas enregistré au classement");
            string json = System.IO.File.ReadAllText(JsonFileStore.PathOf("leaderboard.json"), System.Text.Encoding.UTF8);
            Check(json.Contains("Élodie") && json.Contains("李明"), "leaderboard.json conserve les accents et les idéogrammes (UTF-8)");
            Check(json.Contains("\"difficulty\": \"Hard\""), "La difficulté est enregistrée dans le JSON");

            // Fichier corrompu : repli sur .bak.
            System.IO.File.WriteAllText(JsonFileStore.PathOf("settings.json"), "{ ceci n'est pas du json");
            Check(JsonFileStore.Read<SettingsData>("settings.json") == null && System.IO.File.Exists(JsonFileStore.PathOf("settings.json") + ".bak"), "Un fichier corrompu est mis de côté en .bak");

            JsonFileStore.SetRootForTests(null);
            try { System.IO.Directory.Delete(temp, true); } catch { }
            Finish("Sauvegarde et classement");
        }

        /// <summary>Oriente le joueur clavier/souris vers un point en corrigeant la chute de la flèche.</summary>
        private static void AimAt(PlayerModeService mode, DesktopBowView view, Vector3 point)
        {
            DesktopPlayerController player = mode.Desktop;
            if (player == null)
                return;
            Vector3 origin = player.CameraTransform.position;
            float speed = 25f;
            if (view.CurrentBow != null && view.CurrentBow.Definition != null)
                speed = view.CurrentBow.Definition.maxSpeed;
            float distance = Vector3.Distance(origin, point);
            float time = distance / Mathf.Max(5f, speed);
            Vector3 aimPoint = point + Vector3.up * (0.5f * Mathf.Abs(Physics.gravity.y) * time * time);
            Vector3 dir = (aimPoint - origin).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            player.SetLookAngles(yaw, pitch);
        }
    }
}
#endif
