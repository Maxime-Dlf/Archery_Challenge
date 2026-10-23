// ArenaBuilder : génère les scènes d'arènes (Arena_Training, Arena_01 … Arena_05).
//
// Menu : Tools → Archery Challenge → Build Arenas.
// Pour chaque arène (§P8-C) : instance du modèle ArenaRoot_Template placée et orientée dans le monde,
// cibles aux positions du tableau, réglages des cibles mobiles, vagues du tournoi, décor et torches.
// Chaque arène est une scène séparée (chargée en additif par ArenaPortal) : une par membre de l'équipe.

using System;
using System.Collections.Generic;
using ArcheryChallenge.Audio;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des scènes d'arènes.</summary>
    public static class ArenaBuilder
    {
        private const string PurePoly = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";

        [MenuItem("Tools/Archery Challenge/Build Arenas")]
        private static void BuildAllMenu()
        {
            if (BuildUtils.ConfirmSceneRegeneration("les six scènes d'arènes"))
                BuildAll();
        }

        public static void BuildAll()
        {
            foreach (ArenaSpec arena in ArenaLayout.Arenas)
                Build(arena);
            BootBuilder.UpdateBuildScenes();
            Debug.Log("[ArenaBuilder] Arènes générées.");
        }

        public static void Build(ArenaSpec spec)
        {
            BuildUtils.EnsureFolder(BuildUtils.SceneRoot + "/Arenas");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/World/ArenaRoot_Template.prefab");
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(template);
            root.name = spec.IsTraining ? "Arena_Training" : spec.SceneName;
            root.transform.position = spec.Position;
            root.transform.rotation = Quaternion.Euler(0f, spec.RotationY, 0f);

            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{BuildUtils.DataRoot}/Levels/{spec.LevelFile}.asset");
            LevelController controller = root.GetComponent<LevelController>();
            controller.Definition = level;
            EditorUtility.SetDirty(controller);

            // Point d'apparition de l'arène.
            Transform spawn = root.transform.Find("PlayerSpawn");
            BuildUtils.Set(spawn.GetComponent<PlayerSpawnPoint>(), "spawnId", spec.SpawnId);

            Transform targetsRoot = root.transform.Find("Targets");
            if (spec.IsTraining)
                SetupTraining(root, controller, targetsRoot);
            PlaceTargets(spec, root.transform, targetsRoot, controller);

            Transform decor = root.transform.Find("Decor");
            Transform lights = root.transform.Find("Lights");
            BuildTorches(lights);
            BuildDecor(spec, root.transform, decor);

            if (spec.SceneName == "Arena_05")
            {
                TargetWaveSpawner waves = root.AddComponent<TargetWaveSpawner>();
                List<TargetWave> list = new List<TargetWave>();
                float[] times = { 0f, 20f, 40f };
                for (int i = 1; i <= 3; i++)
                    list.Add(new TargetWave { root = targetsRoot.Find("Wave_" + i), startTime = times[i - 1] });
                waves.Waves = list.ToArray();
                EditorUtility.SetDirty(waves);

                GameObject crowd = BuildUtils.Child(root.transform, "Ambience_Crowd", new Vector3(0f, 2f, 25f));
                AmbienceZone zone = crowd.AddComponent<AmbienceZone>();
                zone.DayBank = "Crowd";
                zone.NightBank = "Crowd";
                zone.IsGlobal = false;
            }

            // Cycle des cibles (touchée → baissée → toutes touchées → toutes relevées) : toutes les missions, pas l'entraînement.
            if (!spec.IsTraining)
                root.AddComponent<TargetCycle>();

            controller.CollectTargets();
            EditorUtility.SetDirty(controller);

            string path = $"{BuildUtils.SceneRoot}/Arenas/{spec.SceneName}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ---------------------------------------------------------------- entraînement

        private static void SetupTraining(GameObject root, LevelController controller, Transform targetsRoot)
        {
            // Pas de manche : cible DÉPART, briefing, résultat et HUD d'arène désactivés.
            Transform start = root.transform.Find("StartTarget");
            if (start != null)
                start.gameObject.SetActive(false);
            BuildUtils.Set(controller, "startTarget", null);

            // Briefing, résultat et HUD d'arène n'ont pas de sens ici : on les masque. Le cadre en bois (« BoardFrame »)
            // du panneau d'information reste : il porte le panneau d'entraînement ; celui du HUD disparaît avec lui.
            Transform briefingAnchor = root.transform.Find("Panels/BriefingAnchor");
            foreach (Transform child in briefingAnchor)
                if (child.name != "BoardFrame")
                    child.gameObject.SetActive(false);
            Transform hudAnchor = root.transform.Find("Panels/HudAnchor");
            foreach (Transform child in hudAnchor)
                child.gameObject.SetActive(false);

            GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/UI/TrainingPanel.prefab"), briefingAnchor);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localRotation = Quaternion.identity;

            TrainingMode training = root.AddComponent<TrainingMode>();
            training.StatsPanel = panel.GetComponent<TrainingStatsPanel>();
            BuildUtils.Set(training, "level", controller.Definition);
            EditorUtility.SetDirty(training);

            // Panneau de tutoriel à l'arrivée : un vrai tableau sur pied, tourné vers le point d'arrivée du joueur
            // (6 m derrière la plateforme). L'échelle est portée par l'ancre : celle du Canvas reste 0,001.
            Vector3 boardPosition = new Vector3(-3.4f, 0f, -3.4f);
            Quaternion boardRotation = WorldPrefabBuilder.FaceAwayFrom(boardPosition, new Vector3(0f, 0f, -6f));
            WorldPrefabBuilder.BuildBoardStructure(root.transform, "TutorialBoard", boardPosition, boardRotation, "TutorialBoard", 1.6f, out _);

            // Repères de distance sur le sol : plaque plate et texte.
            Transform decor = root.transform.Find("Decor");
            foreach (int d in new[] { 5, 10, 20, 30 })
            {
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = $"Marker_{d}m";
                plate.transform.SetParent(decor, false);
                plate.transform.localPosition = new Vector3(-6f, 0.01f, d);
                plate.transform.localScale = new Vector3(1f, 0.02f, 0.3f);
                Object.DestroyImmediate(plate.GetComponent<Collider>());
                plate.GetComponent<Renderer>().sharedMaterial = BuildUtils.EnsureMaterial("M_White", "Universal Render Pipeline/Unlit", Color.white);
                GameObject label = new GameObject("Label");
                label.transform.SetParent(plate.transform, false);
                label.transform.localPosition = new Vector3(0f, 25f, 0f);
                label.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                label.transform.localScale = new Vector3(1f, 50f, 3.3f);
                TextMeshPro text = label.AddComponent<TextMeshPro>();
                text.text = d + " m";
                text.fontSize = 2f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.rectTransform.sizeDelta = new Vector2(2f, 1f);
            }

            // Mannequin d'entraînement.
            GameObject dummy = WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Dummy"), decor, "Dummy", new Vector3(6f, 0f, 12f), new Vector3(0f, 180f, 0f), 1f);
            if (dummy != null)
            {
                foreach (MeshFilter filter in dummy.GetComponentsInChildren<MeshFilter>())
                {
                    MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    filter.gameObject.AddComponent<SurfaceTag>().Surface = SurfaceType.Straw;
                }
            }
        }

        // ---------------------------------------------------------------- cibles

        private static void PlaceTargets(ArenaSpec spec, Transform root, Transform targetsRoot, LevelController controller)
        {
            Dictionary<int, Transform> waveRoots = new Dictionary<int, Transform>();
            foreach (TargetSpec t in spec.Targets)
            {
                Transform parent = targetsRoot;
                if (t.Wave > 0)
                {
                    if (!waveRoots.TryGetValue(t.Wave, out parent))
                    {
                        parent = BuildUtils.Child(targetsRoot, "Wave_" + t.Wave).transform;
                        waveRoots[t.Wave] = parent;
                    }
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildUtils.PrefabRoot}/Targets/{t.Prefab}.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"{t.Prefab}_{t.Position.z:0}m";
                instance.transform.localPosition = t.Position;
                instance.transform.localRotation = Quaternion.Euler(t.ExtraPitch, 180f, 0f);

                if (t.Pattern != null)
                {
                    TargetMover mover = instance.GetComponent<TargetMover>();
                    if (mover != null)
                    {
                        System.Enum.TryParse(t.Pattern, out TargetPattern pattern);
                        BuildUtils.Set(mover, "pattern", (int)pattern);
                        BuildUtils.Set(mover, "amplitude", t.Amplitude);
                        BuildUtils.Set(mover, "baseSpeed", t.Speed);
                        BuildUtils.Set(mover, "phaseOffset", t.Phase);
                        if (pattern == TargetPattern.Orbit)
                            BuildUtils.Set(mover, "radius", t.Radius);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- torches et décor

        private static void BuildTorches(Transform lights)
        {
            GameObject torch = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/World/Torch_Lit.prefab");
            Vector3[] spots =
            {
                new Vector3(-3.4f, 1.8f, -2.2f), new Vector3(3.4f, 1.8f, -2.2f),
                new Vector3(-3.4f, 1.8f, 2.4f), new Vector3(3.4f, 1.8f, 2.4f),
                new Vector3(-6f, 1.8f, 12f), new Vector3(6f, 1.8f, 12f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(torch, lights);
                t.name = "Torch" + i;
                t.transform.localPosition = spots[i];
            }
        }

        private static void BuildDecor(ArenaSpec spec, Transform root, Transform decor)
        {
            // Drapeaux à l'entrée de toutes les arènes : poteaux plantés au sol, tissu visible des deux côtés.
            PlaceFlag(decor, "FlagL", "Flag_Banner_1", new Vector3(-3.9f, 0f, -2.6f), 200f);
            PlaceFlag(decor, "FlagR", "Flag_Banner_2", new Vector3(3.9f, 0f, -2.6f), 160f);
            Random.InitState(spec.SceneName.GetHashCode());

            // Plantes : de vrais objets rangés par groupe, jamais sur la plateforme, devant les cibles, ni sur la cible de départ.
            Func<float, float, bool> allowed = FoliageBuilder.ArenaExclusion(spec);
            switch (spec.SceneName)
            {
                case "Arena_01":
                    Transform flowers = FoliageBuilder.Group(decor, "Foliage_Fleurs");
                    FoliageBuilder.Scatter(flowers, "PP_Daffodil_03", 22, new Rect(-12, 4, 24, 20), allowed, null, 2f, 3f);
                    FoliageBuilder.Scatter(flowers, "PP_Hyacinth_04", 22, new Rect(-12, 4, 24, 20), allowed, null, 2f, 3f);
                    Transform mushrooms = FoliageBuilder.Group(decor, "Foliage_Champignons");
                    FoliageBuilder.Scatter(mushrooms, "PP_Mushroom_Fantasy_Orange_09", 5, new Rect(-12, 6, 24, 18), allowed, null, 1.5f, 2.2f);
                    FoliageBuilder.Scatter(mushrooms, "PP_Mushroom_Fantasy_Purple_05", 5, new Rect(-12, 6, 24, 18), allowed, null, 1.5f, 2.2f);
                    WorldPrefabBuilder.Place(Prefab(PurePoly + "PP_Rock_Moss_Grown_09.prefab"), decor, "RockL", new Vector3(-5f, 0f, 24f), Vector3.zero, 0.8f, true);
                    WorldPrefabBuilder.Place(Prefab(PurePoly + "PP_Rock_Moss_Grown_11.prefab"), decor, "RockR", new Vector3(5f, 0f, 25f), new Vector3(0f, 90f, 0f), 0.8f, true);
                    break;
                case "Arena_02":
                    for (int i = -8; i <= 8; i++)
                        WorldPrefabBuilder.Place(Prefab(PurePoly + "PP_Small_Fence_04.prefab"), decor, "Fence" + i, new Vector3(i * 1.0f, 0f, 12f), new Vector3(0f, 90f, 0f), 1f);
                    Transform sunflowers = FoliageBuilder.Group(decor, "Foliage_Tournesols");
                    FoliageBuilder.Scatter(sunflowers, "PP_Sunflower_04", 26, new Rect(-10, 4, 20, 24), (x, z) => allowed(x, z) && Mathf.Abs(z - 12f) > 1f, null, 2f, 3.2f);
                    Transform grass2 = FoliageBuilder.Group(decor, "Foliage_Herbes");
                    FoliageBuilder.Scatter(grass2, "PP_Grass_11", 14, new Rect(-10, 4, 20, 24), allowed, null, 1.5f, 2.5f);
                    WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Barrel_Apples"), decor, "Apples", new Vector3(-4.5f, 0f, 8f), Vector3.zero, 1f, true);
                    WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Crate_Wooden"), decor, "Crate", new Vector3(4.5f, 0f, 8f), new Vector3(0f, 20f, 0f), 1f, true);
                    break;
                case "Arena_03":
                    Transform grass3 = FoliageBuilder.Group(decor, "Foliage_Herbes");
                    FoliageBuilder.Scatter(grass3, "PP_Grass_15", 30, new Rect(-12, 8, 24, 9), allowed, null, 1.5f, 2.5f);
                    Transform pebbles = FoliageBuilder.Group(decor, "Foliage_Cailloux");
                    FoliageBuilder.Scatter(pebbles, "PP_Cemetery_Pebbles_03", 12, new Rect(-12, 8, 24, 9), allowed, null, 2f, 3f);
                    WorldPrefabBuilder.Place(Prefab(PurePoly + "PP_Rock_Moss_Grown_09.prefab"), decor, "RockWater", new Vector3(-3f, 0f, 13f), Vector3.zero, 0.5f);
                    break;
                case "Arena_04":
                    // Quelques rochers en retrait du bord (le rebord est à 8 m du centre de la plateforme : x = 122), à l'écart des
                    // couloirs de tir vers les cibles et ESPACÉS : le joueur peut s'approcher du vide et tomber dans le ravin ;
                    // aucun mur de rochers ne le retient. (Le fond a une rampe : voir TerrainBuilder.)
                    int rockIndex = 0;
                    foreach (float rockX in new[] { -12f, -8f, 8f, 12f })
                        WorldPrefabBuilder.Place(Prefab(PurePoly + (rockIndex++ % 2 == 0 ? "PP_Rock_Moss_Grown_09" : "PP_Rock_Moss_Grown_11") + ".prefab"), decor, "EdgeRock" + rockIndex,
                            new Vector3(rockX, 0f, 4.5f), new Vector3(0f, Random.Range(0f, 360f), 0f), 0.55f, true);
                    break;
                case "Arena_05":
                    BuildTournament(decor);
                    break;
            }
        }

        private static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // Pose un drapeau sur poteau (prefab Flag_*) dont le pied touche le sol.
        private static void PlaceFlag(Transform parent, string name, string prefabName, Vector3 localPosition, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildUtils.PrefabRoot}/World/{prefabName}.prefab");
            if (prefab == null)
                return;
            GameObject flag = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            flag.name = name;
            flag.transform.localPosition = localPosition;
            flag.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Tribunes, estrade d'honneur, palissade et podium du tournoi.
        private static void BuildTournament(Transform decor)
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.38f, 0.25f, 0.13f));
            foreach (float side in new[] { -12f, 12f })
            {
                for (int step = 0; step < 3; step++)
                {
                    GameObject stair = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stair.name = $"Tribune_{side}_{step}";
                    stair.transform.SetParent(decor, false);
                    stair.transform.localPosition = new Vector3(side + Mathf.Sign(side) * step * 1.2f, 0.25f + step * 0.5f, 25f);
                    stair.transform.localScale = new Vector3(1.2f, 0.5f + step * 0.5f, 40f);
                    stair.GetComponent<Renderer>().sharedMaterial = wood;
                }
                for (int b = 0; b < 6; b++)
                    WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Bench"), decor, $"Bench_{side}_{b}",
                        new Vector3(side + Mathf.Sign(side) * 1.2f, 1.5f, 8f + b * 6f), new Vector3(0f, 90f, 0f), 1f);
                for (int s = 0; s < 4; s++)
                {
                    GameObject tent = WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Stall_Empty"), decor, $"Tent_{side}_{s}",
                        new Vector3(side * 1.9f, 0f, 10f + s * 10f), new Vector3(0f, side > 0 ? -90f : 90f, 0f), 1.3f, true);
                    if (tent != null)
                        WorldPrefabBuilder.MakeBannersDoubleSided(tent);
                }
            }

            // Drapeaux (poteaux plantés au sol, tissu double face) et palissade en fond de piste.
            for (int i = -6; i <= 6; i++)
                WorldPrefabBuilder.Place(Prefab(PurePoly + "PP_Small_Fence_01.prefab"), decor, "Palisade" + i, new Vector3(i * 1.9f, 0f, 52f), new Vector3(0f, 0f, 0f), 1f);
            for (int i = -2; i <= 2; i++)
                PlaceFlag(decor, "Flag" + i, i % 2 == 0 ? "Flag_Banner_1" : "Flag_Banner_2", new Vector3(i * 3.2f, 0f, 50.6f), 180f);

            // Podium du vainqueur : 3 marches à côté de la plateforme.
            float[] heights = { 0.7f, 1.0f, 0.5f };
            for (int i = 0; i < 3; i++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Podium" + i;
                step.transform.SetParent(decor, false);
                step.transform.localPosition = new Vector3(-6f + i * 1.1f, heights[i] * 0.5f, -1f);
                step.transform.localScale = new Vector3(1f, heights[i], 1f);
                step.GetComponent<Renderer>().sharedMaterial = wood;
            }
        }
    }
}
