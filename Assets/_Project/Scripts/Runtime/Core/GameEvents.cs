// GameEvents : bus d'événements global du jeu.
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// PRINCIPE
// Un script qui « annonce » quelque chose (la flèche : « j'ai touché ») ne connaît pas ceux qui
// réagissent (score, HUD, audio, VFX). Il appelle simplement GameEvents.RaiseXxx(...). Chaque
// réacteur s'abonne dans OnEnable (+=) et se désabonne dans OnDisable (-=).
//
// Tous les événements sont remis à zéro au lancement du jeu (voir ResetOnLaunch) pour éviter les
// abonnés « fantômes » quand le rechargement de domaine est désactivé dans l'éditeur.

using System;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Événements du jeu, regroupés par thème.</summary>
    public static class GameEvents
    {
        // ---------- Tir ----------
        /// <summary>Une flèche vient d'être prise (carquois) à cette position.</summary>
        public static event Action<Vector3> ArrowPicked;
        /// <summary>Une flèche vient d'être encochée sur l'arc.</summary>
        public static event Action<Vector3> ArrowNocked;
        /// <summary>La corde est tendue : (position de la corde, tension 0..1).</summary>
        public static event Action<Vector3, float> BowDrawing;
        /// <summary>L'arc est relâché : (position, tension 0..1, flèche partie ?).</summary>
        public static event Action<Vector3, float, bool> BowReleased;
        /// <summary>Une flèche est partie : (flèche, vitesse initiale m/s, tension 0..1).</summary>
        public static event Action<Component, float, float> ArrowShot;
        /// <summary>Une flèche s'est plantée (n'importe quelle surface).</summary>
        public static event Action<ArrowImpactInfo> ArrowImpacted;

        // ---------- Score ----------
        /// <summary>Un tir a été évalué sur une cible : (résultat, points ajoutés bonus inclus).</summary>
        public static event Action<ShotResult, int> ShotScored;
        /// <summary>Le score ou le combo ont changé : (score, combo).</summary>
        public static event Action<int, int> ScoreChanged;

        // ---------- Niveaux ----------
        public static event Action<Component, LevelState> LevelStateChanged;
        /// <summary>Compte à rebours : valeur 3, 2, 1, 0 (0 = « go »).</summary>
        public static event Action<int> CountdownTick;
        /// <summary>Une manche est terminée.</summary>
        public static event Action<LevelResult> LevelFinished;
        public static event Action<GameDifficulty> DifficultyChanged;

        // ---------- Progression ----------
        public static event Action<string> LevelUnlocked;
        public static event Action<string, EquipmentKind> EquipmentUnlocked;
        public static event Action EquipmentChanged;
        public static event Action ProgressionChanged;
        public static event Action<string> TrophyUnlocked;

        // ---------- Monde ----------
        public static event Action<float> HourChanged;
        public static event Action<string> BarrierOpened;
        public static event Action<string> FastTravelled;

        // ---------- Joueur et monstres ----------
        public static event Action<int, int> PlayerHealthChanged;
        public static event Action PlayerDied;
        public static event Action<Vector3> PlayerHurt;
        public static event Action<Component> MonsterKilled;
        /// <summary>Événement sonore/visuel d'un monstre : (position, « sense », « attack », « hit », « die »).</summary>
        public static event Action<Vector3, string> MonsterEvent;

        // ---------- Interface ----------
        public static event Action<string> ToastRequested;
        public static event Action<bool> PauseChanged;
        public static event Action SettingsChanged;
        public static event Action ProfileChanged;

        // ---------- Déclencheurs ----------
        public static void RaiseArrowPicked(Vector3 p) => ArrowPicked?.Invoke(p);
        public static void RaiseArrowNocked(Vector3 p) => ArrowNocked?.Invoke(p);
        public static void RaiseBowDrawing(Vector3 p, float t) => BowDrawing?.Invoke(p, t);
        public static void RaiseBowReleased(Vector3 p, float t, bool fired) => BowReleased?.Invoke(p, t, fired);
        public static void RaiseArrowShot(Component a, float speed, float t) => ArrowShot?.Invoke(a, speed, t);
        public static void RaiseArrowImpacted(ArrowImpactInfo i) => ArrowImpacted?.Invoke(i);
        public static void RaiseShotScored(ShotResult r, int added) => ShotScored?.Invoke(r, added);
        public static void RaiseScoreChanged(int s, int c) => ScoreChanged?.Invoke(s, c);
        public static void RaiseLevelStateChanged(Component l, LevelState s) => LevelStateChanged?.Invoke(l, s);
        public static void RaiseCountdownTick(int v) => CountdownTick?.Invoke(v);
        public static void RaiseLevelFinished(LevelResult r) => LevelFinished?.Invoke(r);
        public static void RaiseDifficultyChanged(GameDifficulty d) => DifficultyChanged?.Invoke(d);
        public static void RaiseLevelUnlocked(string id) => LevelUnlocked?.Invoke(id);
        public static void RaiseEquipmentUnlocked(string id, EquipmentKind k) => EquipmentUnlocked?.Invoke(id, k);
        public static void RaiseEquipmentChanged() => EquipmentChanged?.Invoke();
        public static void RaiseProgressionChanged() => ProgressionChanged?.Invoke();
        public static void RaiseTrophyUnlocked(string id) => TrophyUnlocked?.Invoke(id);
        public static void RaiseHourChanged(float h) => HourChanged?.Invoke(h);
        public static void RaiseBarrierOpened(string id) => BarrierOpened?.Invoke(id);
        public static void RaiseFastTravelled(string id) => FastTravelled?.Invoke(id);
        public static void RaisePlayerHealthChanged(int hp, int max) => PlayerHealthChanged?.Invoke(hp, max);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerHurt(Vector3 from) => PlayerHurt?.Invoke(from);
        public static void RaiseMonsterKilled(Component m) => MonsterKilled?.Invoke(m);
        public static void RaiseMonsterEvent(Vector3 p, string kind) => MonsterEvent?.Invoke(p, kind);
        public static void RaiseToast(string text) => ToastRequested?.Invoke(text);
        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
        public static void RaiseSettingsChanged() => SettingsChanged?.Invoke();
        public static void RaiseProfileChanged() => ProfileChanged?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLaunch()
        {
            ArrowPicked = null; ArrowNocked = null; BowDrawing = null; BowReleased = null;
            ArrowShot = null; ArrowImpacted = null; ShotScored = null; ScoreChanged = null;
            LevelStateChanged = null; CountdownTick = null; LevelFinished = null; DifficultyChanged = null;
            LevelUnlocked = null; EquipmentUnlocked = null; EquipmentChanged = null; ProgressionChanged = null;
            TrophyUnlocked = null; HourChanged = null; BarrierOpened = null; FastTravelled = null;
            PlayerHealthChanged = null; PlayerDied = null; PlayerHurt = null; MonsterKilled = null;
            MonsterEvent = null; ToastRequested = null; PauseChanged = null; SettingsChanged = null;
            ProfileChanged = null;
        }
    }

    /// <summary>Résultat final d'une manche (envoyé par <c>LevelController</c>).</summary>
    [Serializable]
    public class LevelResult
    {
        public string LevelId;
        public string LevelName;
        public int Score;
        public int Stars;
        public bool Passed;
        /// <summary>Précision 0..1 : flèches dans la cible / flèches tirées.</summary>
        public float Accuracy;
        public int ArrowsShot;
        public int ArrowsHit;
        public int XCount;
        public int BestCombo;
        public int TimeBonus;
        public float TimeLeft;
        public GameDifficulty Difficulty;
        public string BowId;
        public string ArrowId;
        public bool IsTraining;
        public string[] NewRewards = new string[0];
    }
}
