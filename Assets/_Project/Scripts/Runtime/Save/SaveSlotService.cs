// SaveSlotService : les 3 emplacements de sauvegarde de chaque profil (saves/<idProfil>/slotN.json).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Un emplacement contient toute la progression : arènes réussies, étoiles, meilleurs scores,
// équipement débloqué et choisi, heure du jour, points de vie, statistiques, temps de jeu.
//
// SANS PROFIL ACTIF (test direct d'une scène, mode développement) : un emplacement « invité »
// est créé en mémoire pour que le jeu fonctionne, mais il n'est JAMAIS écrit sur le disque.

using System;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Save
{
    /// <summary>Service des emplacements de sauvegarde.</summary>
    [DisallowMultipleComponent]
    public class SaveSlotService : MonoBehaviour
    {
        /// <summary>Nombre d'emplacements par profil.</summary>
        public const int SlotCount = 3;

        [Tooltip("Intervalle (s) de sauvegarde automatique du temps de jeu.")]
        [SerializeField, Min(10f)] private float autosaveInterval = 60f;

        private SlotData current;
        private bool isGuest;
        private float nextAutosave;

        /// <summary>
        /// Emplacement en cours. Jamais null : un emplacement invité (non écrit) est fourni
        /// quand aucun profil n'a été choisi.
        /// </summary>
        public SlotData Current
        {
            get
            {
                if (current == null)
                    CreateGuest();
                return current;
            }
        }

        /// <summary>Vrai si l'emplacement courant est un invité (non sauvegardé).</summary>
        public bool IsGuest => isGuest || current == null;

        /// <summary>Vrai quand un vrai emplacement est chargé.</summary>
        public bool HasRealSlot => current != null && !isGuest;

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        private void Update()
        {
            if (!HasRealSlot)
                return;
            // Le temps de jeu ne court pas pendant la pause (timeScale = 0).
            current.playTimeSeconds += Time.deltaTime;
            if (Time.unscaledTime >= nextAutosave)
            {
                nextAutosave = Time.unscaledTime + autosaveInterval;
                Save();
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();

        private static string PathFor(string profileId, int slot) => $"saves/{profileId}/slot{slot}.json";

        /// <summary>Résumé d'un emplacement (vide si absent) pour l'écran de choix.</summary>
        public SlotSummary GetSummary(string profileId, int slot)
        {
            SlotSummary summary = new SlotSummary { SlotIndex = slot };
            SlotData data = JsonFileStore.Read<SlotData>(PathFor(profileId, slot));
            if (data == null)
                return summary;

            summary.Exists = true;
            summary.PlayTimeSeconds = data.playTimeSeconds;
            summary.LastPlayedUtc = data.updatedUtc;
            for (int i = 0; i < data.levels.Count; i++)
            {
                if (data.levels[i].passed) summary.LevelsPassed++;
                summary.TotalStars += data.levels[i].bestStars;
            }
            return summary;
        }

        /// <summary>Charge un emplacement existant. Renvoie faux s'il n'existe pas.</summary>
        public bool Load(string profileId, int slot)
        {
            SlotData data = JsonFileStore.Read<SlotData>(PathFor(profileId, slot));
            if (data == null)
                return false;
            Activate(data, profileId, slot, false);
            return true;
        }

        /// <summary>Crée (ou écrase) un emplacement avec une partie neuve.</summary>
        public SlotData NewGame(string profileId, int slot, ArcheryChallenge.Core.GameCatalog catalog)
        {
            string now = DateTime.UtcNow.ToString("o");
            SlotData data = new SlotData
            {
                profileId = profileId,
                slotIndex = slot,
                createdUtc = now,
                updatedUtc = now,
                selectedBow = catalog != null ? catalog.defaultBowId : "bow_apprentice",
                selectedArrow = catalog != null ? catalog.defaultArrowId : "arrow_wood"
            };
            data.unlockedBows.Add(data.selectedBow);
            data.unlockedArrows.Add(data.selectedArrow);
            Activate(data, profileId, slot, false);
            Save();
            return data;
        }

        /// <summary>Supprime un emplacement.</summary>
        public void Delete(string profileId, int slot)
        {
            JsonFileStore.Delete(PathFor(profileId, slot));
            if (HasRealSlot && current.profileId == profileId && current.slotIndex == slot)
                current = null;
        }

        /// <summary>Écrit l'emplacement courant (sans effet pour un invité).</summary>
        public void Save()
        {
            if (!HasRealSlot)
                return;
            current.updatedUtc = DateTime.UtcNow.ToString("o");
            JsonFileStore.Write(PathFor(current.profileId, current.slotIndex), current);
        }

        /// <summary>Ferme l'emplacement courant (retour au choix de profil).</summary>
        public void Close()
        {
            Save();
            current = null;
            isGuest = false;
        }

        /// <summary>Crée une partie « invité » en mémoire (tests et mode développement).</summary>
        public SlotData CreateGuest()
        {
            GameCatalog catalog = Services.Get<GameCatalog>();
            string now = DateTime.UtcNow.ToString("o");
            SlotData data = new SlotData
            {
                profileId = "guest",
                slotIndex = 1,
                createdUtc = now,
                updatedUtc = now,
                selectedBow = catalog != null ? catalog.defaultBowId : "bow_apprentice",
                selectedArrow = catalog != null ? catalog.defaultArrowId : "arrow_wood"
            };
            data.unlockedBows.Add(data.selectedBow);
            data.unlockedArrows.Add(data.selectedArrow);
            Activate(data, "guest", 1, true);
            return data;
        }

        private void Activate(SlotData data, string profileId, int slot, bool guest)
        {
            if (data.stats == null) data.stats = new PlayerStats();
            if (data.levels == null) data.levels = new System.Collections.Generic.List<LevelProgress>();
            if (data.unlockedBows == null) data.unlockedBows = new System.Collections.Generic.List<string>();
            if (data.unlockedArrows == null) data.unlockedArrows = new System.Collections.Generic.List<string>();
            if (data.trophies == null) data.trophies = new System.Collections.Generic.List<string>();
            data.profileId = profileId;
            data.slotIndex = slot;
            current = data;
            isGuest = guest;
            nextAutosave = Time.unscaledTime + autosaveInterval;

            if (!guest)
            {
                SettingsService settings = Services.Get<SettingsService>();
                if (settings != null)
                {
                    settings.Data.lastSlot = slot;
                    settings.MarkChanged();
                }
            }
            GameEvents.RaiseProgressionChanged();
            GameEvents.RaiseEquipmentChanged();
        }
    }
}
