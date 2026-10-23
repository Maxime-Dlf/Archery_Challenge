// ToolsMenu : petits menus d'éditeur (Tools → Archery Challenge).
//
//  • Open Save Folder : ouvre le dossier des sauvegardes JSON dans l'Explorateur.
//  • Play From Boot   : (case à cocher) le bouton Play démarre toujours depuis la scène Boot,
//                       quelle que soit la scène ouverte.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Menus utilitaires.</summary>
    public static class ToolsMenu
    {
        private const string PlayFromBootMenu = "Tools/Archery Challenge/Play From Boot";
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string PrefKey = "ArcheryChallenge.PlayFromBoot";

        [MenuItem("Tools/Archery Challenge/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            string path = Application.persistentDataPath;
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// Ouvre le monde ET les six arènes ensemble dans la Hiérarchie. En jeu, les arènes se chargent seulement quand
        /// on s'en approche (économie de mémoire sur Quest) ; pour les modifier à la main (cibles, plantes, décor), on
        /// les ouvre toutes ici : elles sont déjà aux bonnes coordonnées du monde.
        /// </summary>
        [MenuItem("Tools/Archery Challenge/Edit World + Arenas (open all scenes)")]
        public static void OpenWorldAndArenas()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/World.unity", OpenSceneMode.Single);
            foreach (ArenaSpec arena in ArenaLayout.Arenas)
                EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Arenas/{arena.SceneName}.unity", OpenSceneMode.Additive);
            Debug.Log("[ToolsMenu] World + 6 arènes ouverts : modifiez-les à la main (ctrl+S enregistre chaque scène).");
        }

        [MenuItem(PlayFromBootMenu)]
        public static void TogglePlayFromBoot()
        {
            bool enabled = !EditorPrefs.GetBool(PrefKey, false);
            EditorPrefs.SetBool(PrefKey, enabled);
            Apply(enabled);
        }

        [MenuItem(PlayFromBootMenu, true)]
        public static bool TogglePlayFromBootValidate()
        {
            Menu.SetChecked(PlayFromBootMenu, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }

        [InitializeOnLoadMethod]
        private static void ApplyOnLoad() => Apply(EditorPrefs.GetBool(PrefKey, false));

        private static void Apply(bool enabled)
        {
            if (enabled)
            {
                SceneAsset boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
                EditorSceneManager.playModeStartScene = boot;
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        /// <summary>Active « Play From Boot » par code (utilisé par les générateurs).</summary>
        public static void EnablePlayFromBoot()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Apply(true);
        }
    }
}
