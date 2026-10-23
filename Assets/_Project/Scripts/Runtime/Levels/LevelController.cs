// LevelController : machine à états d'une arène (briefing → compte à rebours → manche → résultat).
//
// OÙ LE POSER : sur la racine d'une arène (prefab ArenaRoot_Template, Tag « ArenaRoot »).
// RÉGLAGES (tutoriel §P7.7) :
//   Definition      ← Level_01 … Level_05 (ou Level_Training)
//   Platform Bounds ← l'objet « Bounds »
//   Start Target    ← « StartTarget »     Targets Root ← « Targets »
//   Wind Indicator  ← « WindIndicator »   Briefing Anchor / Hud Anchor ← « Panels/… »
//   Bouton « Auto-collect Targets » dans l'Inspector : remplit la liste des cibles.
//
// DÉROULEMENT (§P7.1)
//   Locked ──(arène précédente réussie)──► Available ──(joueur sur la plateforme)──► Briefing
//   Briefing ──(flèche sur DÉPART, ou touche E)──► Countdown 3-2-1 ──► Running
//   Running ──(temps écoulé / plus de flèches / toutes les cibles abattues)──► Finished
//   Finished : score, étoiles, précision, récompenses ; l'enregistrement du nom est fait par l'interface.
//
// Pendant une manche : déplacement limité à la plateforme, vent tiré au hasard, flèches comptées, cibles
// réglées par la difficulté. Le mode entraînement (Definition.isTraining) saute tout cela : on tire
// librement, sans minuterie ni limite de flèches.

using System;
using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Gère le déroulement d'une arène.</summary>
    [DisallowMultipleComponent]
    public class LevelController : MonoBehaviour
    {
        [SerializeField] private LevelDefinition definition;
        [SerializeField] private PlatformBounds platformBounds;
        [SerializeField] private StartTarget startTarget;
        [SerializeField] private Transform targetsRoot;
        [SerializeField] private WindIndicator windIndicator;
        [SerializeField] private Transform briefingAnchor;
        [SerializeField] private Transform hudAnchor;
        [SerializeField] private List<Target> targets = new List<Target>();

        [Tooltip("Durée du compte à rebours (s).")]
        [SerializeField, Min(0)] private int countdownSeconds = 3;

        private static readonly List<LevelController> Controllers = new List<LevelController>();

        private LevelState state = LevelState.Available;
        private float timeLeft;
        private float totalTime;
        private bool playerOnPlatform;
        private bool resultPending;
        private LevelResult lastResult;
        private int arrowsAtStart;
        private Coroutine routine;
        private TargetWaveSpawner waves;
        private int brokenCount;

        // ---------------------------------------------------------------- propriétés

        /// <summary>Toutes les arènes chargées.</summary>
        public static IReadOnlyList<LevelController> All => Controllers;

        /// <summary>Arène dont une manche est en cours (briefing, compte à rebours, jeu ou résultat), sinon null.</summary>
        public static LevelController Active { get; private set; }

        public LevelDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public LevelState State => state;
        public PlatformBounds Bounds => platformBounds;
        public Transform BriefingAnchor => briefingAnchor;
        public Transform HudAnchor => hudAnchor;
        public IReadOnlyList<Target> Targets => targets;
        public StartTarget StartTargetObject => startTarget;
        public LevelResult LastResult => lastResult;

        /// <summary>Vrai tant que le joueur est sur la plateforme.</summary>
        public bool PlayerOnPlatform => playerOnPlatform;

        /// <summary>Temps restant en secondes (0 si illimité).</summary>
        public float TimeLeft => timeLeft;

        /// <summary>Durée totale de la manche (0 si illimitée).</summary>
        public float TotalTime => totalTime;

        /// <summary>Flèches restantes (-1 si illimitées).</summary>
        public int ArrowsLeft => ArrowAllowance.Unlimited ? -1 : ArrowAllowance.Remaining;

        public bool IsTraining => definition != null && definition.isTraining;
        public bool IsRunning => state == LevelState.Running;

        /// <summary>Multiplicateur de seuils de la difficulté actuelle.</summary>
        public float StarMultiplier
        {
            get
            {
                DifficultyApplier applier = Services.Get<DifficultyApplier>();
                return applier != null ? applier.StarMultiplier : 1f;
            }
        }

        /// <summary>Étoiles que vaudrait le score actuel.</summary>
        public int CurrentStars
        {
            get
            {
                ScoreService score = Services.Get<ScoreService>();
                return score != null ? StarRating.Evaluate(definition, score.Score, StarMultiplier) : 0;
            }
        }

        // ---------------------------------------------------------------- cycle de vie

        private void Awake()
        {
            if (targets.Count == 0 && targetsRoot != null)
                CollectTargets();
            waves = GetComponent<TargetWaveSpawner>();
        }

        private void OnEnable()
        {
            if (!Controllers.Contains(this))
                Controllers.Add(this);
            StartTarget.Triggered += OnStartTargetTriggered;
            Target.Broken += OnTargetBroken;
            GameEvents.ProgressionChanged += RefreshLock;
            RefreshLock();
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
            StartTarget.Triggered -= OnStartTargetTriggered;
            Target.Broken -= OnTargetBroken;
            GameEvents.ProgressionChanged -= RefreshLock;
            if (Active == this)
                EndSession(false);
        }

        /// <summary>Remplit la liste des cibles à partir des enfants de « Targets Root ».</summary>
        public void CollectTargets()
        {
            targets.Clear();
            if (targetsRoot == null)
                return;
            foreach (Target t in targetsRoot.GetComponentsInChildren<Target>(true))
                if (t != startTarget)
                    targets.Add(t);
        }

        private void RefreshLock()
        {
            if (state != LevelState.Available && state != LevelState.Locked)
                return;
            UnlockService unlocks = Services.Get<UnlockService>();
            bool unlocked = definition == null || unlocks == null || unlocks.IsLevelUnlocked(definition);
            SetState(unlocked ? LevelState.Available : LevelState.Locked);
        }

        private void SetState(LevelState newState)
        {
            if (state == newState)
                return;
            state = newState;
            GameEvents.RaiseLevelStateChanged(this, newState);
        }

        // ---------------------------------------------------------------- boucle

        private void Update()
        {
            IArcherInput input = Services.Get<IArcherInput>();
            Transform head = input != null ? input.Head : null;
            bool onPlatform = head != null && platformBounds != null && platformBounds.ContainsHorizontally(head.position)
                              && Mathf.Abs(head.position.y - platformBounds.transform.position.y) < 4f;

            if (onPlatform != playerOnPlatform)
            {
                playerOnPlatform = onPlatform;
                OnPlatformChanged();
            }

            switch (state)
            {
                case LevelState.Briefing:
                    if (input != null && input.InteractPressedThisFrame)
                        BeginCountdown();
                    break;
                case LevelState.Running:
                    TickRunning();
                    break;
            }
        }

        private void OnPlatformChanged()
        {
            if (state == LevelState.Locked)
                return;

            if (playerOnPlatform)
            {
                // Une seule arène à la fois : on n'ouvre pas le briefing si une autre est active.
                if (Active != null && Active != this)
                    return;
                if (state == LevelState.Available)
                {
                    if (IsTraining)
                        BeginTraining();
                    else
                        EnterBriefing();
                }
            }
            else if (state == LevelState.Briefing)
            {
                SetState(LevelState.Available);
                EndSession(false);
            }
            else if (state == LevelState.Running && IsTraining)
            {
                EndTraining();
            }
        }

        private void EnterBriefing()
        {
            Active = this;
            SetState(LevelState.Briefing);
            ScoreService score = Services.Get<ScoreService>();
            if (score != null)
            {
                score.ResetRun();
                score.AcceptShots = false;
            }
        }

        private void OnStartTargetTriggered(StartTarget target)
        {
            if (target == startTarget && state == LevelState.Briefing)
                BeginCountdown();
        }

        /// <summary>Lance le compte à rebours (appelé par la cible DÉPART ou la touche E).</summary>
        public void BeginCountdown()
        {
            if (state != LevelState.Briefing)
                return;
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            SetState(LevelState.Countdown);
            for (int i = countdownSeconds; i > 0; i--)
            {
                GameEvents.RaiseCountdownTick(i);
                yield return new WaitForSeconds(1f);
            }
            GameEvents.RaiseCountdownTick(0);
            routine = null;
            BeginRound();
        }

        // ---------------------------------------------------------------- entraînement

        private void BeginTraining()
        {
            Active = this;
            ScoreService score = Services.Get<ScoreService>();
            if (score != null)
            {
                score.ResetRun();
                score.AcceptShots = true;
            }
            ArrowAllowance.SetUnlimited();
            TrajectoryPreview.ForceShow = true;

            EquipmentService equipment = Services.Get<EquipmentService>();
            if (equipment != null)
                equipment.IgnoreLocks = true;

            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            foreach (Target t in targets)
                if (t != null && applier != null)
                    applier.ResetTarget(t);

            timeLeft = 0f;
            totalTime = 0f;
            SetState(LevelState.Running);
            GameEvents.RaiseToast("Entraînement : flèches illimitées");
        }

        private void EndTraining()
        {
            TrajectoryPreview.ForceShow = false;
            EquipmentService equipment = Services.Get<EquipmentService>();
            if (equipment != null)
            {
                equipment.IgnoreLocks = false;
                // Un équipement verrouillé choisi à l'entraînement est remplacé par un équipement permis.
                UnlockService unlocks = Services.Get<UnlockService>();
                if (unlocks != null)
                {
                    if (equipment.SelectedBow != null && !unlocks.IsBowUnlocked(equipment.SelectedBow.id))
                        equipment.SelectBow(Services.Get<GameCatalog>().defaultBowId);
                    if (equipment.SelectedArrow != null && !unlocks.IsArrowUnlocked(equipment.SelectedArrow.id))
                        equipment.SelectArrow(Services.Get<GameCatalog>().defaultArrowId);
                }
            }
            WindService wind = Services.Get<WindService>();
            if (wind != null)
                wind.Stop();
            SetState(LevelState.Available);
            EndSession(false);
        }

        // ---------------------------------------------------------------- manche

        private void BeginRound()
        {
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            ScoreService score = Services.Get<ScoreService>();
            WindService wind = Services.Get<WindService>();

            applier?.Freeze();
            score?.ResetRun();
            if (score != null)
                score.AcceptShots = true;

            // Cibles : réarmées puis adaptées à la difficulté.
            brokenCount = 0;
            foreach (Target t in targets)
            {
                if (t == null) continue;
                t.ResetTarget();
                applier?.ApplyTo(t);
            }

            // Temps et flèches.
            totalTime = definition.timeSeconds > 0f ? (applier != null ? applier.ScaleTime(definition.timeSeconds) : definition.timeSeconds) : 0f;
            timeLeft = totalTime;
            if (definition.arrows > 0)
            {
                arrowsAtStart = applier != null ? applier.ScaleArrows(definition.arrows) : definition.arrows;
                ArrowAllowance.Set(arrowsAtStart);
            }
            else
            {
                arrowsAtStart = 0;
                ArrowAllowance.SetUnlimited();
            }

            wind?.BeginRound(applier != null ? applier.Current : null, definition.windMultiplier);
            if (windIndicator != null)
                windIndicator.gameObject.SetActive(true);

            // Joueur limité à la plateforme.
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (platformBounds != null && mode != null)
                platformBounds.Activate(mode.ActiveRoot, mode.ActiveHead);

            waves?.Begin();
            SetState(LevelState.Running);
            GameEvents.RaiseToast("C'est parti !");
        }

        private void TickRunning()
        {
            if (IsTraining)
                return;

            if (totalTime > 0f)
            {
                timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
                if (timeLeft <= 0f)
                {
                    FinishRound();
                    return;
                }
            }

            // Plus de flèches : on attend que la dernière soit arrivée avant de finir.
            if (!ArrowAllowance.Unlimited && ArrowAllowance.Remaining <= 0 && Arrow.FlyingCount == 0 && !HasNockedArrow())
            {
                FinishRound();
                return;
            }

            // Toutes les cibles destructibles abattues.
            if (brokenCount > 0 && AllBreakableTargetsDown())
                FinishRound();
        }

        private static bool HasNockedArrow()
        {
            XRArcherInput xr = Services.Get<XRArcherInput>();
            Bow bow = xr != null && xr.BowHolster != null ? xr.BowHolster.CurrentBow : null;
            if (bow != null && bow.HasArrow)
                return true;
            DesktopBowView view = FindAnyObjectByType<DesktopBowView>();
            return view != null && view.CurrentBow != null && view.CurrentBow.HasArrow;
        }

        private bool AllBreakableTargetsDown()
        {
            bool any = false;
            foreach (Target t in targets)
            {
                if (t == null || !t.gameObject.activeInHierarchy || t.HitsToBreak <= 0 || t is PenaltyTarget)
                    continue;
                any = true;
                if (!t.IsBroken)
                    return false;
            }
            return any;
        }

        private void OnTargetBroken(Target target)
        {
            if (state == LevelState.Running && targets.Contains(target))
                brokenCount++;
        }

        /// <summary>Termine la manche et calcule le résultat.</summary>
        public void FinishRound()
        {
            if (state != LevelState.Running || IsTraining)
                return;

            ScoreService score = Services.Get<ScoreService>();
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            UnlockService unlocks = Services.Get<UnlockService>();
            EquipmentService equipment = Services.Get<EquipmentService>();

            float multiplier = StarMultiplier;
            int rawScore = score != null ? score.Score : 0;

            // Bonus de temps : +1 point par seconde restante si l'objectif (1 étoile) est atteint.
            int timeBonus = 0;
            if (rawScore >= definition.GetThreshold(1, multiplier) && totalTime > 0f)
                timeBonus = Mathf.FloorToInt(timeLeft) * Mathf.Max(0, definition.timeBonusPerSecond);
            if (score != null && timeBonus > 0)
                score.AddBonus(timeBonus);

            int finalScore = score != null ? score.Score : rawScore;
            LevelResult result = new LevelResult
            {
                LevelId = definition.levelId,
                LevelName = definition.displayName,
                Score = finalScore,
                Stars = StarRating.Evaluate(definition, finalScore, multiplier),
                Accuracy = score != null ? score.Accuracy : 0f,
                ArrowsShot = score != null ? score.ArrowsShot : 0,
                ArrowsHit = score != null ? score.ArrowsHit : 0,
                XCount = score != null ? score.XCount : 0,
                BestCombo = score != null ? score.BestCombo : 0,
                TimeBonus = timeBonus,
                TimeLeft = timeLeft,
                Difficulty = applier != null ? applier.Difficulty : GameDifficulty.Normal,
                BowId = equipment != null && equipment.SelectedBow != null ? equipment.SelectedBow.id : "",
                ArrowId = equipment != null && equipment.SelectedArrow != null ? equipment.SelectedArrow.id : "",
                IsTraining = false
            };
            result.Passed = result.Stars >= definition.starsToPass;

            // Libération du joueur et nettoyage de la manche.
            if (score != null)
                score.AcceptShots = false;
            platformBounds?.Deactivate();
            ArrowAllowance.SetUnlimited();
            Services.Get<WindService>()?.Stop();
            applier?.Unfreeze();
            waves?.End();
            Services.Get<ArrowPool>()?.ReleaseAllActive();

            unlocks?.ApplyResult(definition, result);
            lastResult = result;
            resultPending = true;
            SetState(LevelState.Finished);
            GameEvents.RaiseLevelFinished(result);
        }

        /// <summary>
        /// Valide l'arène avec 3 étoiles en appliquant un résultat fictif (utilisé par les tests automatiques
        /// quand les tirs automatiques ne suffisent pas : on teste alors la chaîne de déblocage).
        /// </summary>
        public void TeleportCheat()
        {
            UnlockService unlocks = Services.Get<UnlockService>();
            if (unlocks == null || definition == null)
                return;
            LevelResult cheat = new LevelResult
            {
                LevelId = definition.levelId,
                LevelName = definition.displayName,
                Score = definition.GetThreshold(3, 1f),
                Stars = 3,
                Difficulty = GameDifficulty.Normal
            };
            unlocks.ApplyResult(definition, cheat);
        }

        /// <summary>Termine avec 3 étoiles (raccourci F6 de développement).</summary>
        public void ForceFinishWithThreeStars()
        {
            if (state == LevelState.Briefing)
                BeginCountdownImmediate();
            if (state != LevelState.Running)
                return;
            ScoreService score = Services.Get<ScoreService>();
            if (score != null)
            {
                int needed = definition.GetThreshold(3, StarMultiplier) - score.Score;
                if (needed > 0)
                    score.AddBonus(needed);
            }
            FinishRound();
        }

        private void BeginCountdownImmediate()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            BeginRound();
        }

        /// <summary>Ferme l'écran de résultat : la manche est terminée, l'arène redevient disponible.</summary>
        public void Dismiss()
        {
            if (state != LevelState.Finished)
                return;
            resultPending = false;
            SetState(LevelState.Available);
            RefreshLock();
            EndSession(true);
            // Le joueur est encore sur la plateforme : on rouvre le briefing (rejouer l'arène).
            if (playerOnPlatform && state == LevelState.Available && !IsTraining)
                EnterBriefing();
        }

        /// <summary>Quitte la manche en cours sans résultat (menu pause → Abandonner).</summary>
        public void Abort()
        {
            if (state == LevelState.Running || state == LevelState.Countdown || state == LevelState.Briefing)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                    routine = null;
                }
                Services.Get<ScoreService>()?.ResetRun();
                if (IsTraining)
                    EndTraining();
                else
                    CleanupRound();
                SetState(LevelState.Available);
                RefreshLock();
                EndSession(false);
            }
            else if (state == LevelState.Finished)
            {
                Dismiss();
            }
        }

        /// <summary>Recommence la manche depuis le briefing (menu pause → Recommencer).</summary>
        public void Restart()
        {
            if (IsTraining)
            {
                Services.Get<ScoreService>()?.ResetRun();
                return;
            }
            if (state == LevelState.Running || state == LevelState.Countdown)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                    routine = null;
                }
                CleanupRound();
                SetState(LevelState.Available);
                EnterBriefing();
            }
        }

        private void CleanupRound()
        {
            ScoreService score = Services.Get<ScoreService>();
            if (score != null)
                score.AcceptShots = false;
            platformBounds?.Deactivate();
            ArrowAllowance.SetUnlimited();
            Services.Get<WindService>()?.Stop();
            Services.Get<DifficultyApplier>()?.Unfreeze();
            waves?.End();
            Services.Get<ArrowPool>()?.ReleaseAllActive();
        }

        private void EndSession(bool afterResult)
        {
            if (Active == this)
                Active = null;
            ScoreService score = Services.Get<ScoreService>();
            if (score != null && !IsTraining)
                score.AcceptShots = true; // hors manche, le bac à sable reste actif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Controllers.Clear();
            Active = null;
        }

        /// <summary>Vrai tant que le résultat de la manche n'a pas été fermé.</summary>
        public bool ResultPending => resultPending;
    }
}
