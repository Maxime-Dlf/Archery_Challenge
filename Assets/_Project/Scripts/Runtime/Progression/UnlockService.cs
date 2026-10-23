// UnlockService : arènes réussies, étoiles, récompenses et équipement débloqué.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// RÈGLES (§P0.1 et §P7.3 du tutoriel)
// - Une arène est DISPONIBLE quand l'arène exigée (requiredLevelId) est RÉUSSIE, c'est-à-dire
//   terminée avec au moins « starsToPass » étoiles. L'arène 1 est disponible dès le départ.
// - Réussir une arène accorde ses récompenses (arcs, flèches, trophée). Les récompenses « bonus »
//   exigent plus d'étoiles (ex. Arc épineux : 3 étoiles à l'arène 4).
// - Le « mode démo » (touche F5) débloque tout sans toucher à la sauvegarde.

using System;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Progression
{
    /// <summary>Service de progression et de déblocages.</summary>
    [DisallowMultipleComponent]
    public class UnlockService : MonoBehaviour
    {
        /// <summary>Identifiants spéciaux de récompense.</summary>
        public const string TrophyId = "trophy";
        public const string GoldTrophyId = "trophy_gold";

        private bool demoUnlockAll;

        /// <summary>Tout est débloqué (mode démo F5 ou entraînement).</summary>
        public bool DemoUnlockAll
        {
            get => demoUnlockAll;
            set
            {
                if (demoUnlockAll == value) return;
                demoUnlockAll = value;
                GameEvents.RaiseProgressionChanged();
                GameEvents.RaiseEquipmentChanged();
            }
        }

        private SaveSlotService Slots => Services.Get<SaveSlotService>();
        private GameCatalog Catalog => Services.Get<GameCatalog>();

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        // ----- Interrogations -----

        /// <summary>Vrai si l'arène a été réussie (assez d'étoiles).</summary>
        public bool IsLevelPassed(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return true;
            LevelProgress p = Slots?.Current.FindLevel(levelId);
            return p != null && p.passed;
        }

        /// <summary>Vrai si l'arène est disponible (jouable).</summary>
        public bool IsLevelUnlocked(LevelDefinition level)
        {
            if (level == null)
                return false;
            if (demoUnlockAll || level.isTraining || string.IsNullOrEmpty(level.requiredLevelId))
                return true;
            return IsLevelPassed(level.requiredLevelId);
        }

        /// <summary>Même question à partir d'un identifiant de niveau.</summary>
        public bool IsLevelUnlocked(string levelId)
        {
            LevelDefinition level = Catalog != null ? Catalog.GetLevel(levelId) : null;
            return level != null && IsLevelUnlocked(level);
        }

        public int GetStars(string levelId) => Slots?.Current.FindLevel(levelId)?.bestStars ?? 0;
        public int GetBestScore(string levelId) => Slots?.Current.FindLevel(levelId)?.bestScore ?? 0;

        public bool IsBowUnlocked(string id) =>
            demoUnlockAll || (Slots != null && Slots.Current.unlockedBows.Contains(id));

        public bool IsArrowUnlocked(string id) =>
            demoUnlockAll || (Slots != null && Slots.Current.unlockedArrows.Contains(id));

        public bool HasTrophy(string id) => Slots != null && Slots.Current.trophies.Contains(id);

        /// <summary>Nombre d'arènes de campagne réussies.</summary>
        public int CountPassedLevels()
        {
            GameCatalog catalog = Catalog;
            if (catalog == null) return 0;
            int count = 0;
            foreach (LevelDefinition l in catalog.GetCampaignLevels())
                if (IsLevelPassed(l.levelId)) count++;
            return count;
        }

        // ----- Modifications -----

        /// <summary>Débloque un équipement. Renvoie vrai s'il était nouveau.</summary>
        public bool Unlock(string itemId)
        {
            SaveSlotService slots = Slots;
            if (slots == null || string.IsNullOrEmpty(itemId))
                return false;

            SlotData slot = slots.Current;
            if (itemId == TrophyId || itemId == GoldTrophyId)
            {
                if (slot.trophies.Contains(itemId)) return false;
                slot.trophies.Add(itemId);
                GameEvents.RaiseTrophyUnlocked(itemId);
                return true;
            }
            if (itemId.StartsWith("bow_", StringComparison.Ordinal))
            {
                if (slot.unlockedBows.Contains(itemId)) return false;
                slot.unlockedBows.Add(itemId);
                GameEvents.RaiseEquipmentUnlocked(itemId, EquipmentKind.Bow);
                return true;
            }
            if (itemId.StartsWith("arrow_", StringComparison.Ordinal))
            {
                if (slot.unlockedArrows.Contains(itemId)) return false;
                slot.unlockedArrows.Add(itemId);
                GameEvents.RaiseEquipmentUnlocked(itemId, EquipmentKind.Arrow);
                return true;
            }
            return false; // « credits » et autres : pas d'objet à débloquer
        }

        /// <summary>
        /// Enregistre le résultat d'une manche : meilleurs scores, réussite, récompenses. Met à jour
        /// <paramref name="result"/> (Passed, NewRewards) et sauvegarde.
        /// </summary>
        public void ApplyResult(LevelDefinition level, LevelResult result)
        {
            SaveSlotService slots = Slots;
            if (level == null || result == null || slots == null || level.isTraining)
                return;

            SlotData slot = slots.Current;
            LevelProgress progress = slot.GetOrCreateLevel(level.levelId);
            progress.attempts++;
            progress.bestScore = Mathf.Max(progress.bestScore, result.Score);
            progress.bestStars = Mathf.Max(progress.bestStars, result.Stars);
            progress.bestAccuracy = Mathf.Max(progress.bestAccuracy, result.Accuracy);

            bool wasPassed = progress.passed;
            result.Passed = result.Stars >= level.starsToPass;
            if (result.Passed)
                progress.passed = true;

            slot.stats.arrowsShot += result.ArrowsShot;
            slot.stats.arrowsHit += result.ArrowsHit;
            slot.stats.xCount += result.XCount;
            slot.stats.bestCombo = Mathf.Max(slot.stats.bestCombo, result.BestCombo);

            List<string> granted = new List<string>();
            if (result.Passed)
            {
                foreach (LevelReward reward in level.rewards)
                {
                    if (result.Stars < reward.minStars) continue;
                    if (Unlock(reward.itemId))
                        granted.Add(reward.itemId);
                }
                if (!wasPassed)
                {
                    // Arène suivante : on annonce celles qui deviennent disponibles.
                    GameCatalog catalog = Catalog;
                    if (catalog != null)
                        foreach (LevelDefinition next in catalog.GetCampaignLevels())
                            if (next.requiredLevelId == level.levelId)
                                GameEvents.RaiseLevelUnlocked(next.levelId);
                }
                // Dernière arène de la campagne réussie = victoire.
                GameCatalog cat = Catalog;
                if (cat != null)
                {
                    List<LevelDefinition> campaign = cat.GetCampaignLevels();
                    if (campaign.Count > 0 && campaign[campaign.Count - 1] == level)
                        slot.victory = true;
                }
            }
            result.NewRewards = granted.ToArray();

            slots.Save();
            GameEvents.RaiseProgressionChanged();
        }

        /// <summary>Remet à zéro la progression de l'emplacement courant (utile aux tests).</summary>
        public void ResetProgress()
        {
            SaveSlotService slots = Slots;
            if (slots == null) return;
            SlotData slot = slots.Current;
            slot.levels.Clear();
            slot.unlockedBows.Clear();
            slot.unlockedArrows.Clear();
            slot.trophies.Clear();
            GameCatalog catalog = Catalog;
            slot.selectedBow = catalog != null ? catalog.defaultBowId : "bow_apprentice";
            slot.selectedArrow = catalog != null ? catalog.defaultArrowId : "arrow_wood";
            slot.unlockedBows.Add(slot.selectedBow);
            slot.unlockedArrows.Add(slot.selectedArrow);
            slot.victory = false;
            GameEvents.RaiseProgressionChanged();
            GameEvents.RaiseEquipmentChanged();
        }
    }
}
