// JsonFileStore : lecture et écriture sûres de fichiers JSON.
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// GARANTIES
// - Écriture ATOMIQUE : on écrit d'abord dans « fichier.tmp », puis on remplace le vrai fichier
//   d'un seul geste. Si le jeu plante ou si le casque s'éteint en plein milieu, l'ancien fichier
//   reste intact (jamais de fichier à moitié écrit).
// - Fichier corrompu : s'il ne peut pas être lu, il est renommé « fichier.json.bak » (pour que
//   l'équipe puisse l'examiner) et le chargement renvoie null : le jeu repart avec des valeurs par
//   défaut au lieu de planter.
// - Encodage UTF-8 sans BOM : les noms accentués ou non latins (« Élodie », « 李 ») sont conservés.

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArcheryChallenge.Save
{
    /// <summary>Stockage JSON dans le dossier de données de l'application.</summary>
    public static class JsonFileStore
    {
        private static string rootOverride;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Dossier racine des sauvegardes (Application.persistentDataPath par défaut).</summary>
        public static string Root => string.IsNullOrEmpty(rootOverride) ? Application.persistentDataPath : rootOverride;

        /// <summary>Remplace le dossier racine (tests). Passer null pour revenir au dossier normal.</summary>
        public static void SetRootForTests(string path) => rootOverride = path;

        /// <summary>Chemin complet d'un fichier relatif à la racine.</summary>
        public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Vrai si le fichier existe.</summary>
        public static bool Exists(string relative) => File.Exists(PathOf(relative));

        /// <summary>
        /// Lit et désérialise un fichier. Renvoie null s'il n'existe pas ou s'il est illisible
        /// (dans ce cas il est mis de côté en « .bak »).
        /// </summary>
        public static T Read<T>(string relative) where T : class
        {
            string path = PathOf(relative);
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    throw new IOException("Fichier vide.");
                T value = JsonUtility.FromJson<T>(json);
                if (value == null)
                    throw new IOException("Contenu JSON invalide.");
                return value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileStore] Fichier illisible « {relative} » ({e.Message}) : mis de côté en .bak.");
                BackupCorrupted(path);
                return null;
            }
        }

        /// <summary>Sérialise puis écrit un objet de façon atomique. Renvoie faux en cas d'échec.</summary>
        public static bool Write<T>(string relative, T value) where T : class
        {
            string path = PathOf(relative);
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(value, true);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json, Utf8NoBom);

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonFileStore] Écriture impossible de « {relative} » : {e.Message}");
                return false;
            }
        }

        /// <summary>Supprime un fichier (sans erreur s'il n'existe pas).</summary>
        public static void Delete(string relative)
        {
            string path = PathOf(relative);
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning($"[JsonFileStore] Suppression impossible : {e.Message}"); }
        }

        /// <summary>Supprime un dossier entier avec son contenu.</summary>
        public static void DeleteFolder(string relative)
        {
            string path = PathOf(relative);
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (Exception e) { Debug.LogWarning($"[JsonFileStore] Suppression du dossier impossible : {e.Message}"); }
        }

        private static void BackupCorrupted(string path)
        {
            try
            {
                string backup = path + ".bak";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(path, backup);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileStore] Copie .bak impossible : {e.Message}");
            }
        }
    }
}
