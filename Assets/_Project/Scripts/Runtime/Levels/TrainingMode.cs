// TrainingMode : stand d'entraînement sans contrainte (§P11 du tutoriel).
//
// OÙ LE POSER : sur la racine de l'arène « Arena_Training » (à côté du LevelController).
// RÉGLAGES : « Stats Panel » ← le prefab TrainingPanel placé sur BriefingAnchor ; « Level » ← Level_Training.
//
// L'entraînement ne compte pas pour la progression : flèches et temps illimités, tout l'équipement
// utilisable (verrous ignorés), vent réglable à la main, ligne de trajectoire toujours affichée.
// Ce composant collecte les statistiques du dernier tir (points, distance, vitesse, tension) et les
// impacts du mini-blason, et expose les actions des boutons du panneau.

using System;
using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Logique et statistiques de l'entraînement.</summary>
    [DisallowMultipleComponent]
    public class TrainingMode : MonoBehaviour
    {
        [SerializeField] private TrainingStatsPanel statsPanel;
        [SerializeField] private LevelDefinition level;

        private LevelController controller;

        /// <summary>Dernier tir : points, distance, vitesse de départ, tension.</summary>
        public int LastPoints { get; private set; }
        public int LastRing { get; private set; }
        public float LastDistance { get; private set; }
        public float LastSpeed { get; private set; }
        public float LastTension { get; private set; }
        public int ShotCount { get; private set; }
        public int TotalPoints { get; private set; }

        /// <summary>Annoncé à chaque changement de statistique.</summary>
        public event Action Changed;

        /// <summary>Annoncé à chaque impact sur une cible : (position normalisée dans le blason, anneau).</summary>
        public event Action<Vector2, int> ImpactRecorded;

        public TrainingStatsPanel StatsPanel
        {
            get => statsPanel;
            set => statsPanel = value;
        }

        private void Awake() => controller = GetComponent<LevelController>();

        private void OnEnable()
        {
            GameEvents.ShotScored += OnShotScored;
            GameEvents.ArrowShot += OnArrowShot;
            GameEvents.EquipmentChanged += RaiseChanged;
        }

        private void OnDisable()
        {
            GameEvents.ShotScored -= OnShotScored;
            GameEvents.ArrowShot -= OnArrowShot;
            GameEvents.EquipmentChanged -= RaiseChanged;
        }

        /// <summary>L'entraînement est-il en cours (joueur sur la plateforme) ?</summary>
        public bool IsActive => controller != null && controller.State == LevelState.Running;

        private void OnArrowShot(Component arrow, float speed, float tension)
        {
            if (!IsActive)
                return;
            LastSpeed = speed;
            LastTension = tension;
            RaiseChanged();
        }

        private void OnShotScored(ShotResult shot, int added)
        {
            if (!IsActive)
                return;
            LastPoints = shot.Points;
            LastRing = shot.Ring;
            LastDistance = shot.Distance;
            ShotCount++;
            if (shot.Points > 0)
                TotalPoints += shot.Points;

            Target target = shot.Target as Target;
            if (target != null && target.Face != null && target.Face.Definition != null)
            {
                float outer = target.Face.Definition.ringRadii[target.Face.Definition.ringRadii.Length - 1];
                Vector3 local = target.Face.transform.InverseTransformPoint(shot.WorldPoint);
                ImpactRecorded?.Invoke(new Vector2(local.x, local.y) / outer, shot.Ring);
            }
            RaiseChanged();
        }

        private void RaiseChanged() => Changed?.Invoke();

        // ---------------------------------------------------------------- actions des boutons

        /// <summary>Remet les statistiques et le mini-blason à zéro.</summary>
        public void ResetStats()
        {
            LastPoints = 0; LastRing = 0; LastDistance = 0f; LastSpeed = 0f; LastTension = 0f;
            ShotCount = 0; TotalPoints = 0;
            Services.Get<ScoreService>()?.ResetRun();
            RaiseChanged();
        }

        public void CycleBow(int direction) => Services.Get<EquipmentService>()?.CycleBow(direction);
        public void CycleArrow(int direction) => Services.Get<EquipmentService>()?.CycleArrow(direction);

        /// <summary>Règle le vent : 0 aucun, 1 faible, 2 moyen, 3 fort.</summary>
        public void SetWind(int level)
        {
            Services.Get<WindService>()?.SetManualLevel(level);
            RaiseChanged();
        }

        /// <summary>Passe au niveau de vent suivant (boucle).</summary>
        public void CycleWind()
        {
            WindService wind = Services.Get<WindService>();
            if (wind == null) return;
            int next = wind.IsManual ? (wind.ManualLevel + 1) % 4 : 1;
            SetWind(next);
        }
    }
}
