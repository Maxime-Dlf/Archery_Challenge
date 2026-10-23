// ProfileService : gestion des profils joueurs (profiles.json).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Un profil = un identifiant unique (GUID) + un nom affiché (1 à 20 caractères). Chaque profil
// possède ses 3 emplacements de sauvegarde (voir SaveSlotService). Les profils ne sont pas
// protégés par mot de passe (décision de l'équipe : jeu de salon).

using System;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Save
{
    /// <summary>Service des profils joueurs.</summary>
    [DisallowMultipleComponent]
    public class ProfileService : MonoBehaviour
    {
        private const string FileName = "profiles.json";

        private ProfilesFile file;
        private ProfileData current;

        /// <summary>Tous les profils connus.</summary>
        public IReadOnlyList<ProfileData> Profiles
        {
            get { EnsureLoaded(); return file.profiles; }
        }

        /// <summary>Profil sélectionné (null tant qu'aucun n'est choisi).</summary>
        public ProfileData Current => current;

        private void Awake()
        {
            Services.Register(this);
            EnsureLoaded();
        }

        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Charge profiles.json une seule fois.</summary>
        public void EnsureLoaded()
        {
            if (file != null)
                return;
            file = JsonFileStore.Read<ProfilesFile>(FileName) ?? new ProfilesFile();
            if (file.profiles == null)
                file.profiles = new List<ProfileData>();
        }

        /// <summary>Cherche un profil par identifiant.</summary>
        public ProfileData Find(string id)
        {
            EnsureLoaded();
            for (int i = 0; i < file.profiles.Count; i++)
                if (file.profiles[i].id == id) return file.profiles[i];
            return null;
        }

        /// <summary>
        /// Crée un profil. Le nom est nettoyé et validé ; renvoie null s'il est invalide.
        /// </summary>
        public ProfileData Create(string name)
        {
            EnsureLoaded();
            if (!NameValidator.TryClean(name, out string cleaned))
                return null;

            string now = DateTime.UtcNow.ToString("o");
            ProfileData profile = new ProfileData
            {
                id = Guid.NewGuid().ToString("N"),
                name = cleaned,
                createdUtc = now,
                lastPlayedUtc = now
            };
            file.profiles.Add(profile);
            Persist();
            return profile;
        }

        /// <summary>Renomme un profil existant (renvoie faux si le nom est invalide ou le profil inconnu).</summary>
        public bool Rename(string id, string newName)
        {
            ProfileData profile = Find(id);
            if (profile == null || !NameValidator.TryClean(newName, out string cleaned))
                return false;
            profile.name = cleaned;
            Persist();
            if (current == profile)
                GameEvents.RaiseProfileChanged();
            return true;
        }

        /// <summary>Supprime un profil et tous ses emplacements de sauvegarde.</summary>
        public bool Delete(string id)
        {
            ProfileData profile = Find(id);
            if (profile == null)
                return false;
            file.profiles.Remove(profile);
            JsonFileStore.DeleteFolder("saves/" + id);
            if (current == profile)
                current = null;
            Persist();
            GameEvents.RaiseProfileChanged();
            return true;
        }

        /// <summary>Sélectionne le profil actif.</summary>
        public bool Select(string id)
        {
            ProfileData profile = Find(id);
            if (profile == null)
                return false;
            current = profile;
            profile.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            Persist();

            SettingsService settings = Services.Get<SettingsService>();
            if (settings != null)
            {
                settings.Data.lastProfileId = id;
                settings.MarkChanged();
            }
            GameEvents.RaiseProfileChanged();
            return true;
        }

        /// <summary>Désélectionne le profil (retour au feu de camp).</summary>
        public void Deselect()
        {
            current = null;
            GameEvents.RaiseProfileChanged();
        }

        private void Persist() => JsonFileStore.Write(FileName, file);
    }
}
