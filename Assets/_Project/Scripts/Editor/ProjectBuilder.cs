// ProjectBuilder : régénère TOUT le contenu du jeu en une seule commande.
//
// Menu : Tools → Archery Challenge → Build Everything.
// Ordre : données → sons → prefabs de jeu → interface → prefabs du monde → arènes → monde → scène Boot.
// Idempotent. Durée : 1 à 3 minutes (terrain, NavMesh, sons procéduraux).
//
// ⚠ Cette commande remet les VALEURS PAR DÉFAUT du tutoriel dans les fichiers de données et recrée les
// prefabs générés. Ne la relancez pas si l'équipe a modifié ces fichiers à la main.

using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Orchestrateur des générateurs.</summary>
    public static class ProjectBuilder
    {
        [MenuItem("Tools/Archery Challenge/Build Everything")]
        private static void BuildEverythingMenu()
        {
            if (BuildUtils.ConfirmSceneRegeneration("TOUT le contenu (données, prefabs, scènes World, Boot et arènes)"))
                BuildEverything();
        }

        public static void BuildEverything()
        {
            DataBuilder.BuildAll();
            AudioBuilder.BuildAll();
            PrefabBuilder.BuildAll();
            UiPrefabBuilder.BuildAll();
            WorldPrefabBuilder.BuildAll();
            ArenaBuilder.BuildAll();
            WorldBuilder.BuildScene();
            BootBuilder.BuildAll();
            VfxBuilder.WireImpactFeedback();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectBuilder] Tout le contenu a été régénéré.");
        }
    }
}
