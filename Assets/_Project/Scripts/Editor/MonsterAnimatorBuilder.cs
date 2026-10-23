// MonsterAnimatorBuilder : crée les contrôleurs d'animation des monstres (§P10.2–P10.3 du tutoriel).
//
// Menu : Tools → Archery Challenge → Create Monster Animators.
// Les contrôleurs d'origine du pack RPG Monster DUO enchaînent toutes les animations en démonstration et
// n'ont aucun paramètre : on en génère de nouveaux dont les états n'ont AUCUNE transition automatique ;
// c'est le script MonsterBrain qui choisit l'animation (CrossFade sur le nom de l'état).
// États : IdleNormal, WalkFWD, RunFWD, Attack01, GetHit, Dizzy, Die, SenseSomethingST, Taunt, Victory
// (+ Defend pour la tortue). IdleNormal est l'état par défaut.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de contrôleurs d'animation.</summary>
    public static class MonsterAnimatorBuilder
    {
        private const string Root = "Assets/RPG Monster DUO PBR Polyart/Animations";
        private const string Output = "Assets/_Project/Art/Animations";

        [MenuItem("Tools/Archery Challenge/Create Monster Animators")]
        public static void BuildAll()
        {
            BuildUtils.EnsureFolder(Output);
            Build("Monster_Slime", "Slime", new Dictionary<string, string>
            {
                { "IdleNormal", "IdleNormal_Slime_Anim" }, { "WalkFWD", "Walk_Slime_Anim" }, { "RunFWD", "Run_Slime_Anim" },
                { "Attack01", "Attack01_Slime_Anim" }, { "GetHit", "GetHit_Slime_Anim" }, { "Dizzy", "Dizzy_Slime_Anim" },
                { "Die", "Die_Slime_Anim" }, { "SenseSomethingST", "SenseSomethingStart_Slime_Anim" },
                { "Taunt", "Taunt_Slime_Anim" }, { "Victory", "Victory_Slime_Anim" }
            });
            Build("Monster_Turtle", "TurtleShell", new Dictionary<string, string>
            {
                { "IdleNormal", "IdleNormal_TurtleShell_Anim" }, { "WalkFWD", "Walk_TurtleShell_Anim" }, { "RunFWD", "Run_TurtleShell_Anim" },
                { "Attack01", "Attack01_TurtleShell_Anim" }, { "GetHit", "GetHit_TurtleShell_Anim" }, { "Dizzy", "Dizzy_TurtleShell_Anim" },
                { "Die", "Die_TurtleShell_Anim" }, { "SenseSomethingST", "SenseSomethingStart_TurtleShell_Anim" },
                { "Taunt", "Taunt_TurtleShell_Anim" }, { "Victory", "Victory_TurtleShell_Anim" }, { "Defend", "Defend_TurtleShell_Anim" }
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[MonsterAnimatorBuilder] Contrôleurs des monstres créés.");
        }

        private static void Build(string controllerName, string folder, Dictionary<string, string> states)
        {
            string path = $"{Output}/{controllerName}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            int index = 0;
            foreach (var pair in states)
            {
                AnimationClip clip = LoadClip($"{Root}/{folder}/{pair.Value}.fbx");
                AnimatorState state = machine.AddState(pair.Key, new Vector3(300f, index * 55f, 0f));
                state.motion = clip;
                if (pair.Key == "IdleNormal")
                    machine.defaultState = state;
                index++;
            }
            EditorUtility.SetDirty(controller);
        }

        // Le clip d'animation est un sous-asset du FBX (on ignore le clip d'aperçu « __preview__ »).
        private static AnimationClip LoadClip(string fbxPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            Debug.LogWarning($"[MonsterAnimatorBuilder] Aucun clip dans {fbxPath}");
            return null;
        }
    }
}
