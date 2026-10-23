// EvolutionPatch : correctifs appliqués EN PLACE aux scènes existantes (sans les régénérer).
//
// Menu : Tools → Archery Challenge → Apply Patch (ravin, chemin, cycle des cibles, tremblement).
//
// POURQUOI : les générateurs « Build … » repartent de zéro et écraseraient tout ce que l'équipe a posé ou déplacé à la
// main dans World et les arènes. Ce correctif ne modifie que ce qui est nécessaire, et seulement là où c'est nécessaire :
//   1. Terrain : relief des piliers du ravin et textures du chemin de la rivière au ravin (zones limitées) ; arbres
//      retirés seulement s'ils sont sur le nouveau chemin.
//   2. Arène 4 : stand avancé de 4 m, cibles remontées sur les piliers, rochers du rebord écartés.
//   3. World : portail et point d'arrivée de l'arène 4.
//   4. Cycle des cibles : composant TargetCycle ajouté à chaque arène de mission (pas à l'entraînement).
//   5. Tremblement du réticule : service AimSway ajouté à Systems ; multiplicateurs ajoutés aux difficultés.
// Il est IDEMPOTENT : on peut le relancer sans rien casser. Les correctifs qui REPOSENT des objets de scène (terrain,
// arène 4, portail) ne s'appliquent qu'UNE fois : leur identifiant est noté dans Assets/_Project/Data/AppliedPatches.txt,
// ce qui protège les retouches faites à la main depuis (supprimer la ligne pour rejouer un correctif).
//   5b. Panneaux d'arène (briefing, résultat, HUD) rapprochés du regard : ils étaient coupés par le bord de l'écran.
//   6. Équilibrage : objectifs de points et vent réduits (valeurs de données, rejouable sans risque).
//   6b. HUD d'arène : composant WorldHudBoard sur le prefab HudPanel (toujours dans le champ).
//   7. Boot : pilote du rig VR sans casque (SimulatedRigDriver) ajouté au XR Origin.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Targets;
using ArcheryChallenge.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Correctifs en place du 22/10.</summary>
    public static class EvolutionPatch
    {
        private const string TerrainPath = "Assets/_Project/Art/Terrain/World_TerrainData.asset";
        private const string PurePoly = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";

        [MenuItem("Tools/Archery Challenge/Apply Patch (ravine, path, target cycle, aim sway)")]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        /// <summary>Applique tous les correctifs et renvoie le compte rendu.</summary>
        public static string Apply()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("[EvolutionPatch] " + RunOnce("r3-terrain", PatchTerrain));
            report.AppendLine("[EvolutionPatch] " + RunOnce("r3-arena4", PatchArena4));
            report.AppendLine("[EvolutionPatch] " + RunOnce("r3-portal", PatchWorldPortal));
            report.AppendLine("[EvolutionPatch] " + PatchTargetCycle());
            report.AppendLine("[EvolutionPatch] " + PatchSystemsAndDifficulty());
            report.AppendLine("[EvolutionPatch] " + RunOnce("r4-boards", PatchArenaBoards));
            report.AppendLine("[EvolutionPatch] " + RunOnce("r5-path5", PatchPathToArena5));
            report.AppendLine("[EvolutionPatch] " + RunOnce("r5-portal5", PatchPortalA5));
            report.AppendLine("[EvolutionPatch] " + PatchHudBoard());
            report.AppendLine("[EvolutionPatch] " + PatchCloseButtons());
            report.AppendLine("[EvolutionPatch] " + PatchBalance());
            report.AppendLine("[EvolutionPatch] " + PatchBootRig());
            report.AppendLine("[EvolutionPatch] " + PatchControlsVrPage());
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ---------------------------------------------------------------- registre des correctifs déjà appliqués

        private const string LedgerPath = "Assets/_Project/Data/AppliedPatches.txt";

        private static string RunOnce(string id, System.Func<string> patch)
        {
            if (File.Exists(LedgerPath) && File.ReadAllLines(LedgerPath).Any(line => line.Trim() == id))
                return $"{id} : déjà appliqué (retouches manuelles préservées)";
            string result = patch();
            File.AppendAllText(LedgerPath, id + "\n");
            AssetDatabase.ImportAsset(LedgerPath);
            return result;
        }

        // ---------------------------------------------------------------- terrain

        private static string PatchTerrain()
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            if (data == null)
                return "Terrain : TerrainData introuvable (" + TerrainPath + ")";

            // Piliers du ravin (relief + textures alentour) et chemin contournant la colline (textures + arbres).
            string a = TerrainBuilder.PatchRegion(data, new Rect(124f, 68f, 18f, 32f), true, true, false);
            string b = TerrainBuilder.PatchRegion(data, new Rect(94f, 50f, 26f, 36f), false, true, true);
            // Chemin du Ravin au Tournoi : contourne la mare de la source de la rivière par le nord.
            string c = TerrainBuilder.PatchRegion(data, new Rect(58f, 104f, 58f, 24f), false, true, true);
            AssetDatabase.SaveAssets();
            return $"Terrain : piliers ({a}) ; chemin de la colline ({b}) ; chemin de la source ({c})";
        }

        // ---------------------------------------------------------------- arène 4

        private static string PatchArena4()
        {
            ArenaSpec spec = ArenaLayout.Find("Arena_04");
            string path = BuildUtils.SceneRoot + "/Arenas/Arena_04.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            string result;
            try
            {
                GameObject root = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                    if (go.name == "Arena_04")
                        root = go;
                if (root == null)
                    return "Arène 4 : racine introuvable";

                root.transform.position = spec.Position;

                Transform targets = root.transform.Find("Targets");
                int moved = 0;
                for (int i = 0; i < spec.Targets.Count && i < targets.childCount; i++)
                {
                    TargetSpec t = spec.Targets[i];
                    Transform child = targets.GetChild(i);
                    child.localPosition = t.Position;
                    child.localRotation = Quaternion.Euler(t.ExtraPitch, 180f, 0f);
                    child.name = $"{t.Prefab}_{t.Position.z:0}m";
                    TargetMover mover = child.GetComponent<TargetMover>();
                    if (mover != null && t.Pattern != null)
                    {
                        System.Enum.TryParse(t.Pattern, out TargetPattern pattern);
                        BuildUtils.Set(mover, "pattern", (int)pattern);
                        BuildUtils.Set(mover, "amplitude", t.Amplitude);
                        BuildUtils.Set(mover, "baseSpeed", t.Speed);
                    }
                    moved++;
                }

                // Rochers du rebord : retirés puis reposés en retrait, espacés et hors des couloirs de tir.
                Transform decor = root.transform.Find("Decor");
                int removed = 0;
                for (int i = decor.childCount - 1; i >= 0; i--)
                {
                    if (decor.GetChild(i).name.StartsWith("EdgeRock"))
                    {
                        Object.DestroyImmediate(decor.GetChild(i).gameObject);
                        removed++;
                    }
                }
                int index = 0;
                foreach (float x in new[] { -12f, -8f, 8f, 12f })
                {
                    string rock = index++ % 2 == 0 ? "PP_Rock_Moss_Grown_09" : "PP_Rock_Moss_Grown_11";
                    WorldPrefabBuilder.Place(AssetDatabase.LoadAssetAtPath<GameObject>(PurePoly + rock + ".prefab"), decor, "EdgeRock" + index,
                        new Vector3(x, 0f, 4.5f), new Vector3(0f, index * 70f, 0f), 0.55f, true);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                result = $"Arène 4 : stand en {spec.Position}, {moved} cibles replacées, {removed} rochers remplacés par 4";
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            return result;
        }

        // ---------------------------------------------------------------- World : portail de l'arène 4

        private static string PatchWorldPortal()
        {
            ArenaSpec spec = ArenaLayout.Find("Arena_04");
            Scene scene = EditorSceneManager.OpenScene(BuildUtils.SceneRoot + "/World.unity", OpenSceneMode.Additive);
            string result;
            try
            {
                Transform portal = null;
                Terrain terrain = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (terrain == null)
                        terrain = go.GetComponentInChildren<Terrain>(true);
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Portal_A4")
                            portal = t;
                }
                if (portal == null)
                    return "Portail A4 : introuvable dans World";

                portal.position = spec.Position;
                Transform arrival = portal.Find("Arrival");
                if (arrival != null)
                {
                    Vector3 position = spec.Position + Quaternion.Euler(0f, spec.RotationY, 0f) * new Vector3(0f, 0f, -spec.ArrivalDistance);
                    position.y = terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : spec.Position.y;
                    arrival.position = position;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                result = $"Portail A4 en {spec.Position}, arrivée à {spec.ArrivalDistance} m derrière la plateforme";
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            return result;
        }

        // ---------------------------------------------------------------- cycle des cibles

        private static string PatchTargetCycle()
        {
            int added = 0, present = 0;
            foreach (ArenaSpec spec in ArenaLayout.Arenas)
            {
                if (spec.IsTraining)
                    continue;
                Scene scene = EditorSceneManager.OpenScene($"{BuildUtils.SceneRoot}/Arenas/{spec.SceneName}.unity", OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject go in scene.GetRootGameObjects())
                    {
                        if (go.GetComponent<LevelController>() == null)
                            continue;
                        if (go.GetComponent<TargetCycle>() == null)
                        {
                            go.AddComponent<TargetCycle>();
                            added++;
                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                        }
                        else
                        {
                            present++;
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return $"Cycle des cibles : ajouté à {added} arène(s), déjà présent sur {present}";
        }

        // ---------------------------------------------------------------- panneaux d'arène visibles en entier

        private static string PatchArenaBoards()
        {
            int moved = 0;
            foreach (ArenaSpec spec in ArenaLayout.Arenas)
            {
                Scene scene = EditorSceneManager.OpenScene($"{BuildUtils.SceneRoot}/Arenas/{spec.SceneName}.unity", OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        if (root.GetComponent<LevelController>() == null)
                            continue;
                        foreach ((string anchorName, Vector3 position) in new[]
                        {
                            ("BriefingAnchor", WorldPrefabBuilder.BriefingAnchorPosition),
                            ("HudAnchor", WorldPrefabBuilder.HudAnchorPosition)
                        })
                        {
                            Transform anchor = root.transform.Find("Panels/" + anchorName);
                            if (anchor == null)
                                continue;
                            anchor.localPosition = position;
                            anchor.localRotation = WorldPrefabBuilder.FaceAwayFrom(position, WorldPrefabBuilder.PlatformEye);
                            moved++;
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return $"Panneaux d'arène : {moved} panneaux rapprochés du regard du joueur";
        }

        // ---------------------------------------------------------------- chemin vers l'arène 5 et point de détection

        // Ancien tracé du Ravin au Tournoi (il traversait les gradins de l'arène 5).
        private static readonly Vector2[] OldPathToArena5 =
        {
            new Vector2(110, 84), new Vector2(112, 108), new Vector2(112, 112), new Vector2(95, 116),
            new Vector2(88, 121), new Vector2(78, 121), new Vector2(70, 121), new Vector2(64, 127)
        };

        // Premier essai de tracé (retouché ensuite : butte gravie trop droit) : son voisinage est retraité lui aussi.
        private static readonly Vector2[] FirstAttemptPathToArena5 =
        {
            new Vector2(110, 84), new Vector2(112, 100), new Vector2(110, 105), new Vector2(102, 106.74f), new Vector2(90, 106.74f),
            new Vector2(79.16f, 106.74f), new Vector2(70, 107), new Vector2(66, 111), new Vector2(63, 117), new Vector2(61, 123), new Vector2(60, 130)
        };

        private static string PatchPathToArena5()
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            if (data == null)
                return "Chemin vers l'arène 5 : TerrainData introuvable (" + TerrainPath + ")";
            string summary = TerrainBuilder.PatchAroundPaths(data, new[] { OldPathToArena5, FirstAttemptPathToArena5, ArenaLayout.Paths[5] }, 7.5f, 5f);
            AssetDatabase.SaveAssets();
            return "Chemin vers l'arène 5 : " + summary;
        }

        // Le point de détection d'apparition d'une arène passe au milieu de l'arène, avec des rayons plus grands. L'objet « Arrival »
        // (enfant du portail) garde sa position monde.
        private static string PatchPortalA5()
        {
            ArenaSpec spec = ArenaLayout.Find("Arena_05");
            Scene scene = EditorSceneManager.OpenScene(BuildUtils.SceneRoot + "/World.unity", OpenSceneMode.Additive);
            try
            {
                Transform portal = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Portal_A5")
                            portal = t;
                if (portal == null)
                    return "Portail A5 : introuvable dans World";
                Transform arrival = portal.Find("Arrival");
                Vector3 arrivalPosition = arrival != null ? arrival.position : Vector3.zero;
                Quaternion arrivalRotation = arrival != null ? arrival.rotation : Quaternion.identity;
                portal.position = spec.PortalPosition;
                if (arrival != null)
                    arrival.SetPositionAndRotation(arrivalPosition, arrivalRotation);
                ArenaPortal component = portal.GetComponent<ArenaPortal>();
                if (component != null)
                {
                    BuildUtils.Set(component, "loadRadius", spec.LoadRadius);
                    BuildUtils.Set(component, "unloadRadius", spec.UnloadRadius);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return $"Portail A5 en {spec.PortalPosition}, rayons {spec.LoadRadius:0} / {spec.UnloadRadius:0} m (arrivée inchangée)";
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------------------------------------------------------------- croix de fermeture des menus

        // Pause : croix ajoutée ; classement : « Fermer » devient une croix ; voyage rapide : « Fermer » retiré (il débordait sur le
        // dernier bouton) et remplacé par une croix, panneau agrandi pour les 7 destinations. Rejouable.
        private static string PatchCloseButtons()
        {
            UiFactory.Font = FontBuilder.MainFont;
            string folder = BuildUtils.PrefabRoot + "/UI/";
            string report = "";

            report += EditPrefab(folder + "PauseMenu.prefab", root =>
            {
                if (root.transform.Find("BtnClose") != null)
                    return "Pause : croix déjà présente";
                UiFactory.Button(root.transform, "BtnClose", "X", 630, 12, 54, 50, 26);
                return "Pause : croix ajoutée";
            }) + " ; ";

            report += EditPrefab(folder + "LeaderboardPanel.prefab", root =>
            {
                Transform close = root.transform.Find("BtnClose");
                if (close == null)
                    return "Classement : BtnClose introuvable";
                UiFactory.Place((RectTransform)close, 920, 14, 56, 56);
                TMPro.TMP_Text label = close.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null)
                {
                    label.text = "X";
                    label.fontSize = 28f;
                }
                return "Classement : « Fermer » remplacé par une croix";
            }) + " ; ";

            report += EditPrefab(folder + "FastTravelPanel.prefab", root =>
            {
                Transform close = root.transform.Find("BtnClose");
                TMPro.TMP_Text label = close != null ? close.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                if (close != null && label != null && label.text == "X")
                    return "Voyage rapide : croix déjà présente";
                if (close != null)
                    Object.DestroyImmediate(close.gameObject);
                ((RectTransform)root.transform).sizeDelta = new Vector2(700f, UiPrefabBuilder.FastTravelHeight);
                Transform content = root.transform.Find("Content");
                if (content != null)
                {
                    UiFactory.Place((RectTransform)content, 100, 130, 500, 470);
                    UnityEngine.UI.VerticalLayoutGroup layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                    if (layout != null)
                        layout.spacing = 8f;
                }
                UiFactory.Button(root.transform, "BtnClose", "X", 630, 12, 54, 50, 26);
                return "Voyage rapide : « Fermer » remplacé par une croix, panneau agrandi";
            }) + " ; ";

            // L'instance du panneau dans UI_Global garde sa propre taille (surcharge enregistrée à l'instanciation) : on la met à jour aussi.
            report += EditPrefab(folder + "UI_Global.prefab", root =>
            {
                Transform travel = root.transform.Find("FastTravelPanel");
                if (travel == null)
                    return "UI_Global : FastTravelPanel introuvable";
                RectTransform rect = (RectTransform)travel;
                if (Mathf.Approximately(rect.sizeDelta.y, UiPrefabBuilder.FastTravelHeight))
                    return "UI_Global : taille du voyage rapide déjà à jour";
                rect.sizeDelta = new Vector2(700f, UiPrefabBuilder.FastTravelHeight);
                return "UI_Global : taille du panneau de voyage rapide mise à jour";
            });
            AssetDatabase.SaveAssets();
            return report;
        }

        private static string EditPrefab(string path, System.Func<GameObject, string> edit)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string result = edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------------------------------------------------------------- HUD d'arène toujours dans le champ

        // Ajoute WorldHudBoard au prefab HudPanel (les HUD des 5 arènes en sont des instances). Rejouable : ne fait rien s'il est déjà là.
        private static string PatchHudBoard()
        {
            string path = BuildUtils.PrefabRoot + "/UI/HudPanel.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.GetComponent<ArcheryChallenge.UI.WorldHudBoard>() != null)
                    return "HudPanel : WorldHudBoard déjà présent";
                contents.AddComponent<ArcheryChallenge.UI.WorldHudBoard>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return "HudPanel : WorldHudBoard ajouté (HUD masqué au PC, suit le regard en casque)";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---------------------------------------------------------------- écran Commandes : page « Gameplay VR »

        // Ajoute le bouton « Gameplay VR » au prefab ControlsPanel (les écrans et panneaux du camp/hub en sont des instances) et
        // resserre les autres boutons de page. Rejouable : ne fait rien si le bouton existe déjà.
        private static string PatchControlsVrPage()
        {
            string path = BuildUtils.PrefabRoot + "/UI/ControlsPanel.prefab";
            return EditPrefab(path, root =>
            {
                if (root.transform.Find("BtnPageVr") != null)
                    return "ControlsPanel : page Gameplay VR déjà présente";
                void Place(string name, float x, float width)
                {
                    Transform t = root.transform.Find(name);
                    if (t == null)
                        return;
                    RectTransform rect = (RectTransform)t;
                    rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                }
                Place("BtnPageGame", 40f, 150f);
                Place("BtnPageSimulator", 430f, 220f);
                Place("BtnPageDev", 660f, 250f);
                UiFactory.Button(root.transform, "BtnPageVr", "Gameplay VR", 200f, 66f, 220f, 44f, 24f);
                return "ControlsPanel : bouton « Gameplay VR » ajouté";
            });
        }

        // ---------------------------------------------------------------- Boot : pilote du rig VR simulé

        private static string PatchBootRig()
        {
            Scene scene = EditorSceneManager.OpenScene(BuildUtils.SceneRoot + "/Boot.unity", OpenSceneMode.Additive);
            string result = "Boot : ";
            try
            {
                GameObject rig = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                    if (go.name == "XR Origin (XR Rig)")
                        rig = go;
                if (rig == null)
                    return result + "XR Origin introuvable";
                bool changed = false;
                if (rig.GetComponent<SimulatedRigDriver>() == null)
                {
                    rig.AddComponent<SimulatedRigDriver>();
                    changed = true;
                    result += "SimulatedRigDriver ajouté au XR Origin ; ";
                }
                else
                {
                    result += "SimulatedRigDriver déjà présent ; ";
                }
                // Garde-fou de la locomotion VR : Character Controller inutilisable tant que la tête n'est pas suivie.
                if (rig.GetComponent<VrBodyGuard>() == null)
                {
                    rig.AddComponent<VrBodyGuard>();
                    changed = true;
                    result += "VrBodyGuard ajouté au XR Origin";
                }
                else
                {
                    result += "VrBodyGuard déjà présent";
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            return result;
        }

        // ---------------------------------------------------------------- équilibrage (jeu moins difficile)

        // Objectifs de points et vent réduits : seuils des arènes × 0,75, multiplicateurs de difficulté 0,6 / 0,8 / 0,95,
        // vent Normal 1–2,5 m/s et Difficile 2–4,5 m/s, coefficients d'arène 1,25 (Ravin) et 1,5 (Tournoi).
        private static string PatchBalance()
        {
            Dictionary<string, int[]> thresholds = new Dictionary<string, int[]>
            {
                { "Level_01", new[] { 45, 63, 81 } }, { "Level_02", new[] { 52, 74, 94 } }, { "Level_03", new[] { 60, 84, 108 } },
                { "Level_04", new[] { 68, 95, 122 } }, { "Level_05", new[] { 90, 126, 162 } }
            };
            Dictionary<string, float> windMultipliers = new Dictionary<string, float> { { "Level_04", 1.25f }, { "Level_05", 1.5f } };
            foreach (var pair in thresholds)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{BuildUtils.DataRoot}/Levels/{pair.Key}.asset");
                if (level == null)
                    continue;
                level.starThresholds = pair.Value;
                if (windMultipliers.TryGetValue(pair.Key, out float multiplier))
                    level.windMultiplier = multiplier;
                if (pair.Key == "Level_01")
                    level.briefing = level.briefing.Replace("Marquez 60 points", "Marquez 45 points");
                EditorUtility.SetDirty(level);
            }

            // difficulté : multiplicateur de seuils, vent min, vent max
            Dictionary<string, float[]> difficulties = new Dictionary<string, float[]>
            {
                { "Difficulty_Easy", new[] { 0.6f, 0f, 0f } },
                { "Difficulty_Normal", new[] { 0.8f, 1f, 2.5f } },
                { "Difficulty_Hard", new[] { 0.95f, 2f, 4.5f } }
            };
            foreach (var pair in difficulties)
            {
                DifficultyProfile profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>($"{BuildUtils.DataRoot}/Difficulty/{pair.Key}.asset");
                if (profile == null)
                    continue;
                profile.starThresholdMultiplier = pair.Value[0];
                profile.windMin = pair.Value[1];
                profile.windMax = pair.Value[2];
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            return "Équilibrage : seuils d'étoiles et vent réduits";
        }

        // ---------------------------------------------------------------- tremblement

        private static string PatchSystemsAndDifficulty()
        {
            string systemsPath = BuildUtils.PrefabRoot + "/Systems/Systems.prefab";
            string note = "Systems : ";
            GameObject contents = PrefabUtility.LoadPrefabContents(systemsPath);
            try
            {
                AimSway aimSway = contents.GetComponent<AimSway>();
                if (aimSway == null)
                {
                    aimSway = contents.AddComponent<AimSway>();
                    note += "AimSway ajouté";
                }
                else
                {
                    note += "AimSway déjà présent";
                }
                // Gain du vent réduit (0,10 → 0,06) : la valeur est sérialisée dans le prefab, il faut donc la corriger ici.
                BuildUtils.Set(aimSway, "windFactor", 0.06f);
                PrefabUtility.SaveAsPrefabAsset(contents, systemsPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Dictionary<string, float> sway = new Dictionary<string, float>
            {
                { "Difficulty_Easy", 0.35f }, { "Difficulty_Normal", 1f }, { "Difficulty_Hard", 1.6f }
            };
            foreach (var pair in sway)
            {
                DifficultyProfile profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>($"{BuildUtils.DataRoot}/Difficulty/{pair.Key}.asset");
                if (profile == null)
                    continue;
                profile.aimSwayMultiplier = pair.Value;
                EditorUtility.SetDirty(profile);
            }
            return note + " ; multiplicateurs de tremblement : Facile 0,35 · Normal 1 · Difficile 1,6";
        }
    }
}
