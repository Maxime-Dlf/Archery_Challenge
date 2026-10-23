// SceneContentTests : vérifie que le contenu du jeu est bien POSÉ DANS LES SCÈNES (visible et modifiable dans l'éditeur)
// et non fabriqué par le code au lancement : cibles, plantes, drapeaux, panneaux, textes du râtelier.

using System.Collections.Generic;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.Tests.EditMode
{
    public class SceneContentTests
    {
        private const string Scenes = "Assets/_Project/Scenes/";

        private static Scene OpenAdditive(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        private static void Close(Scene scene) => EditorSceneManager.CloseScene(scene, true);

        private static List<T> Find<T>(Scene scene) where T : Component
        {
            List<T> list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        [TestCase("Arena_01", 3)]
        [TestCase("Arena_02", 6)]
        [TestCase("Arena_03", 5)]
        [TestCase("Arena_04", 6)]
        [TestCase("Arena_05", 15)]
        [TestCase("Arena_Training", 5)]
        public void ArenaScenesContainTheirTargetsAsSceneObjects(string sceneName, int expectedTargets)
        {
            Scene scene = OpenAdditive($"{Scenes}Arenas/{sceneName}.unity");
            try
            {
                // La cible DÉPART est aussi un Target : on ne compte que les cibles de la manche.
                int count = 0;
                foreach (Target target in Find<Target>(scene))
                    if (!(target is StartTarget))
                        count++;
                Assert.AreEqual(expectedTargets, count, sceneName + " : cibles posées dans la scène");
                Assert.AreEqual(1, Find<LevelController>(scene).Count, sceneName + " : un LevelController");
            }
            finally
            {
                Close(scene);
            }
        }

        [TestCase("Arena_01", "Foliage_Fleurs")]
        [TestCase("Arena_02", "Foliage_Tournesols")]
        [TestCase("Arena_03", "Foliage_Herbes")]
        public void ArenaFoliageIsGroupedAndHarmless(string sceneName, string group)
        {
            Scene scene = OpenAdditive($"{Scenes}Arenas/{sceneName}.unity");
            try
            {
                Transform found = null;
                foreach (Transform t in Find<Transform>(scene))
                    if (t.name == group)
                        found = t;
                Assert.IsNotNull(found, sceneName + " : groupe " + group);
                Assert.Greater(found.childCount, 5, "plantes posées comme objets");
                foreach (Collider c in found.GetComponentsInChildren<Collider>(true))
                    Assert.IsFalse(c.enabled, "une plante ne doit pas bloquer les flèches : " + c.name);
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void ClearingAroundTheStartTargetIsRespected()
        {
            Scene scene = OpenAdditive($"{Scenes}Arenas/Arena_01.unity");
            try
            {
                Transform start = null, decor = null;
                foreach (Transform t in Find<Transform>(scene))
                {
                    if (t.name == "StartTarget") start = t;
                    if (t.name == "Decor" && t.parent != null && t.parent.name == "Arena_01") decor = t;
                }
                Assert.IsNotNull(start);
                Assert.IsNotNull(decor);
                foreach (Transform plant in decor.GetComponentsInChildren<Transform>())
                {
                    if (plant.parent == null || !plant.parent.name.StartsWith("Foliage"))
                        continue;
                    Vector3 local = start.parent.InverseTransformPoint(plant.position) - start.localPosition;
                    Assert.Greater(new Vector2(local.x, local.z).magnitude, 2.5f, plant.name + " est trop près de la cible de départ");
                }
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void WorldHasFoliageBannersBoardsAndABridgeInTheScene()
        {
            Scene scene = OpenAdditive(Scenes + "World.unity");
            try
            {
                Transform foliage = null, bridge = null;
                int flags = 0, controlsBoards = 0, facePlayers = 0;
                foreach (Transform t in Find<Transform>(scene))
                {
                    if (t.name == "--- FOLIAGE ---") foliage = t;
                    if (t.name == "Decor_Bridge") bridge = t;
                    if (t.name.StartsWith("Flag") && t.Find("Pole") != null) flags++;
                    if (t.name == "ControlsBoard") controlsBoards++;
                }
                facePlayers = Find<FacePlayer>(scene).Count;
                Assert.IsNotNull(foliage, "groupe --- FOLIAGE ---");
                Assert.GreaterOrEqual(foliage.GetComponentsInChildren<Transform>().Length, 150, "plantes du monde posées comme objets");
                Assert.IsNotNull(bridge);
                Assert.GreaterOrEqual(bridge.childCount, 6, "pont : rampe, quatre tronçons, rampe");
                Assert.GreaterOrEqual(flags, 4, "drapeaux sur poteau");
                Assert.AreEqual(2, controlsBoards, "tableaux des commandes (camp et village)");
                Assert.GreaterOrEqual(facePlayers, 10, "textes et panneaux flottants tournés vers le joueur");

                // Aucun enfant du râtelier ne doit hériter d'une échelle ×100.
                foreach (ArmorySlot slot in Find<ArmorySlot>(scene))
                {
                    Assert.AreEqual(1f, slot.transform.lossyScale.x, 0.01f, slot.name + " : échelle");
                    Assert.IsNotNull(slot.transform.Find("Display"), slot.name + " : Display posé dans le prefab");
                    Assert.IsNotNull(slot.transform.Find("Label"), slot.name + " : Label posé dans le prefab");
                }
            }
            finally
            {
                Close(scene);
            }
        }

        [Test]
        public void NoWorldSpaceCanvasIsGiantInAnyScene()
        {
            foreach (string path in new[] { Scenes + "World.unity", Scenes + "Arenas/Arena_Training.unity", Scenes + "Arenas/Arena_01.unity", Scenes + "Arenas/Arena_05.unity" })
            {
                Scene scene = OpenAdditive(path);
                try
                {
                    foreach (Canvas canvas in Find<Canvas>(scene))
                    {
                        if (canvas.renderMode != RenderMode.WorldSpace)
                            continue;
                        RectTransform rect = (RectTransform)canvas.transform;
                        float width = rect.rect.width * canvas.transform.lossyScale.x;
                        Assert.Less(width, 3.5f, $"{path} : {canvas.name} fait {width:0.0} m de large");
                    }
                }
                finally
                {
                    Close(scene);
                }
            }
        }

        [Test]
        public void InfoPanelsFaceThePlayerOnEveryArena()
        {
            foreach (string name in new[] { "Arena_01", "Arena_02", "Arena_03", "Arena_04", "Arena_05" })
            {
                Scene scene = OpenAdditive($"{Scenes}Arenas/{name}.unity");
                try
                {
                    LevelController level = Find<LevelController>(scene)[0];
                    Vector3 eye = level.transform.TransformPoint(new Vector3(0f, 1.7f, -0.5f));
                    foreach (string anchorName in new[] { "BriefingAnchor", "HudAnchor" })
                    {
                        Transform anchor = level.transform.Find("Panels/" + anchorName);
                        Assert.IsNotNull(anchor, name + " : " + anchorName);
                        Vector3 toEye = eye - anchor.position;
                        toEye.y = 0f;
                        Vector3 readable = -anchor.forward;
                        readable.y = 0f;
                        Assert.Greater(Vector3.Dot(toEye.normalized, readable.normalized), 0.95f, $"{name} : {anchorName} ne fait pas face à la plateforme");
                    }
                }
                finally
                {
                    Close(scene);
                }
            }
        }

        [Test]
        public void RavineHasAClimbableRampAndRemainsDeep()
        {
            // Profil de la rampe au milieu de sa largeur : du rebord (≈ 10 m) au fond (≈ 0 m), sans marche ni paroi.
            float x = ArcheryChallenge.EditorTools.TerrainBuilder.RavineRamp.center.x;
            float previous = ArcheryChallenge.EditorTools.TerrainBuilder.Height(x, 54f);
            Assert.GreaterOrEqual(previous, 9.5f, "haut de la rampe au niveau du sol");
            float steepest = 0f;
            for (float z = 54.5f; z <= 76f; z += 0.5f)
            {
                float h = ArcheryChallenge.EditorTools.TerrainBuilder.Height(x, z);
                steepest = Mathf.Max(steepest, Mathf.Atan2(Mathf.Abs(h - previous), 0.5f) * Mathf.Rad2Deg);
                previous = h;
            }
            Assert.LessOrEqual(previous, 0.6f, "bas de la rampe au fond du ravin");
            Assert.LessOrEqual(steepest, 35f, $"pente maximale {steepest:0}° : le joueur doit pouvoir la remonter (limite 45°)");
            // Le reste du ravin reste profond : on peut toujours y tomber.
            Assert.LessOrEqual(ArcheryChallenge.EditorTools.TerrainBuilder.Height(134f, 90f), 1f, "fond du ravin");
        }

        [Test]
        public void NothingBlocksTheRimOfTheRavine()
        {
            Scene world = OpenAdditive(Scenes + "World.unity");
            Scene arena = OpenAdditive(Scenes + "Arenas/Arena_04.unity");
            try
            {
                Physics.SyncTransforms();
                // Rayons horizontaux à hauteur de poitrine sur les 4,5 derniers mètres avant le vide (le rebord est à x = 122),
                // tout le long du ravin : rien ne doit empêcher d'y tomber.
                foreach (float z in new[] { 64f, 68f, 72f, 76f, 80f, 84f, 88f, 92f, 96f, 100f })
                {
                    Vector3 origin = new Vector3(119f, 11.2f, z);
                    RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.right, 4.5f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.collider is TerrainCollider || hit.collider.GetComponentInParent<Target>() != null)
                            continue;
                        Assert.Fail($"{hit.collider.name} bloque le rebord du ravin en z = {z} (x = {hit.point.x:0.0})");
                    }
                }
            }
            finally
            {
                Close(arena);
                Close(world);
            }
        }

        [Test]
        public void PathsStayOffTheSteepSeparatorHill()
        {
            // Pente maximale rencontrée sur chaque chemin (échantillons tous les 0,5 m, pont exclu).
            float[] worst = new float[ArcheryChallenge.EditorTools.ArenaLayout.Paths.Length];
            for (int index = 0; index < worst.Length; index++)
            {
                Vector2[] path = ArcheryChallenge.EditorTools.ArenaLayout.Paths[index];
                for (int i = 0; i < path.Length - 1; i++)
                {
                    float length = Vector2.Distance(path[i], path[i + 1]);
                    float previous = ArcheryChallenge.EditorTools.TerrainBuilder.Height(path[i].x, path[i].y);
                    for (float d = 0.5f; d <= length; d += 0.5f)
                    {
                        Vector2 p = Vector2.Lerp(path[i], path[i + 1], d / length);
                        float h = ArcheryChallenge.EditorTools.TerrainBuilder.Height(p.x, p.y);
                        bool onBridge = p.x > 72f && p.x < 87f && (Mathf.Abs(p.y - 60f) < 2f || Mathf.Abs(p.y - 106.74f) < 2f); // les ponts enjambent la rivière : leurs berges ne comptent pas
                        if (!onBridge && h > 9.4f && previous > 9.4f)
                            worst[index] = Mathf.Max(worst[index], Mathf.Atan2(Mathf.Abs(h - previous), 0.5f) * Mathf.Rad2Deg);
                        previous = h;
                    }
                }
            }
            // Le chemin de la rivière au ravin (n° 4) est quasi plat : il contourne la colline au lieu de la gravir.
            Assert.LessOrEqual(worst[4], 8f, $"chemin de la rivière au ravin : pente de {worst[4]:0}°");
            // Les autres chemins restent praticables (limite du Character Controller : 45°) ; seul le talus du plateau du tournoi est raide.
            for (int index = 0; index < worst.Length; index++)
                Assert.LessOrEqual(worst[index], 40f, $"chemin {index} : pente de {worst[index]:0}°");

            // Et le chemin de la rivière au ravin passe bien au sud de la colline (qui commence à z = 61 avec sa pente).
            bool crossesHill = false;
            foreach (Vector2[] path in ArcheryChallenge.EditorTools.ArenaLayout.Paths)
                for (int i = 0; i < path.Length - 1; i++)
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        Vector2 p = Vector2.Lerp(path[i], path[i + 1], t);
                        if (p.x > 100f && p.x < 109f && p.y > 61f && p.y < 100f)
                            crossesHill = true;
                    }
            Assert.IsFalse(crossesHill, "un chemin traverse encore la colline séparatrice");
        }

        [Test]
        public void PathToArena5CrossesTheBridgeClimbsTheHillAndAvoidsTheBleachers()
        {
            Vector2[] path = ArcheryChallenge.EditorTools.ArenaLayout.Paths[5];

            // 1. Il passe par le second pont de la rivière et finit à l'arrivée de l'arène 5.
            Scene world = OpenAdditive(Scenes + "World.unity");
            Scene arena = OpenAdditive(Scenes + "Arenas/Arena_05.unity");
            try
            {
                Transform bridge = null;
                Transform portal = null;
                foreach (Transform t in Find<Transform>(world))
                {
                    if (t.name == "Decor_Bridge_2") bridge = t;
                    if (t.name == "Portal_A5") portal = t;
                }
                Assert.IsNotNull(bridge, "second pont posé dans World");
                Vector3 bridgeCenter = bridge.Find("Middle1").position;
                float nearestToBridge = float.MaxValue;
                for (int i = 0; i < path.Length - 1; i++)
                    for (float t = 0f; t <= 1f; t += 0.01f)
                        nearestToBridge = Mathf.Min(nearestToBridge, Vector2.Distance(Vector2.Lerp(path[i], path[i + 1], t), new Vector2(bridgeCenter.x, bridgeCenter.z)));
                Assert.Less(nearestToBridge, 1.5f, "le chemin passe sur le pont");
                Assert.Less(Vector2.Distance(path[path.Length - 1], new Vector2(60f, 131f)), 2.5f, "le chemin finit à l'arrivée de l'arène 5");

                // 2. Il ne traverse aucun gradin, banc, tente ni palissade de l'arène 5 (marge de 1 m).
                int hits = 0;
                string firstHit = "";
                foreach (Transform decor in Find<Transform>(arena))
                {
                    if (!(decor.name.StartsWith("Tribune") || decor.name.StartsWith("Bench") || decor.name.StartsWith("Tent") || decor.name.StartsWith("Palisade")))
                        continue;
                    Renderer r = decor.GetComponentInChildren<Renderer>();
                    if (r == null) continue;
                    Bounds b = r.bounds;
                    b.Expand(new Vector3(2f, 100f, 2f));
                    for (int i = 0; i < path.Length - 1; i++)
                        for (float t = 0f; t <= 1f; t += 0.01f)
                        {
                            Vector2 p = Vector2.Lerp(path[i], path[i + 1], t);
                            if (b.Contains(new Vector3(p.x, b.center.y, p.y)))
                            {
                                hits++;
                                if (firstHit.Length == 0) firstHit = decor.name;
                            }
                        }
                }
                Assert.AreEqual(0, hits, "le chemin traverse " + firstHit);

                // 3. Le point de détection de l'arène est au milieu de l'arène (pas sur le stand) et son rayon est grand.
                Assert.IsNotNull(portal, "Portal_A5");
                Assert.Greater(portal.position.x, 80f, "point de détection au milieu de l'arène (stand en x = 66)");
                Assert.Less(portal.position.x, 100f);
                ArcheryChallenge.World.ArenaPortal component = portal.GetComponent<ArcheryChallenge.World.ArenaPortal>();
                Assert.GreaterOrEqual(component.LoadRadius, 60f, "rayon d'apparition");
                Transform arrival = portal.Find("Arrival");
                Assert.IsNotNull(arrival);
                Assert.Less(Vector2.Distance(new Vector2(arrival.position.x, arrival.position.z), new Vector2(60f, 131f)), 1.5f, "l'arrivée reste derrière le stand");
                Assert.Less(Vector2.Distance(new Vector2(arrival.position.x, arrival.position.z), new Vector2(portal.position.x, portal.position.z)), component.LoadRadius,
                    "le joueur qui arrive est dans le rayon d'apparition");
            }
            finally
            {
                Close(arena);
                Close(world);
            }

            // 4. Le terrain réel (pas seulement la formule) est praticable sous le chemin : pente ≤ 32° hors berges du pont.
            UnityEngine.TerrainData data = AssetDatabase.LoadAssetAtPath<UnityEngine.TerrainData>("Assets/_Project/Art/Terrain/World_TerrainData.asset");
            Assert.IsNotNull(data);
            float worstSlope = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float length = Vector2.Distance(path[i], path[i + 1]);
                float previous = data.GetInterpolatedHeight(path[i].x / 160f, path[i].y / 160f);
                for (float d = 0.5f; d <= length; d += 0.5f)
                {
                    Vector2 p = Vector2.Lerp(path[i], path[i + 1], d / length);
                    float h = data.GetInterpolatedHeight(p.x / 160f, p.y / 160f);
                    bool onBridge = p.x > 72f && p.x < 87f && Mathf.Abs(p.y - 106.74f) < 2f;
                    if (!onBridge)
                        worstSlope = Mathf.Max(worstSlope, Mathf.Atan2(Mathf.Abs(h - previous), 0.5f) * Mathf.Rad2Deg);
                    previous = h;
                }
            }
            Assert.LessOrEqual(worstSlope, 32f, $"pente maximale sous le chemin vers l'arène 5 : {worstSlope:0}°");
        }

        [Test]
        public void EveryRavineTargetIsVisibleFromTheShootingPlatform()
        {
            Scene world = OpenAdditive(Scenes + "World.unity");
            Scene arena = OpenAdditive(Scenes + "Arenas/Arena_04.unity");
            try
            {
                Physics.SyncTransforms();
                LevelController level = Find<LevelController>(arena)[0];
                Vector3 eye = level.transform.TransformPoint(new Vector3(0f, 1.7f, 0f));
                int mask = ~((1 << LayerMask.NameToLayer("Target")) | (1 << LayerMask.NameToLayer("Player")));
                int checkedTargets = 0;
                foreach (Target t in Find<Target>(arena))
                {
                    if (t is StartTarget || t.Face == null)
                        continue;
                    Vector3 center = t.Face.Center;
                    // Une cible verticale est vérifiée au plus bas de sa course.
                    if (t.Mover != null && t.Mover.Pattern == TargetPattern.Vertical)
                        center -= Vector3.up * t.Mover.Amplitude;
                    bool blocked = Physics.Linecast(eye, center, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore);
                    Assert.IsFalse(blocked, $"{t.name} est masquée depuis la plateforme par « {(blocked ? hit.collider.name : "")} » en {(blocked ? hit.point.ToString("F1") : "")}");
                    checkedTargets++;
                }
                Assert.AreEqual(6, checkedTargets, "les 6 cibles du ravin sont vérifiées");
                // Le stand est avancé : la plateforme est à 8 m du rebord (x = 122), pas à 12 m.
                Assert.AreEqual(114f, level.transform.position.x, 0.01f, "stand avancé de 4 m");
            }
            finally
            {
                Close(arena);
                Close(world);
            }
        }

        [Test]
        public void AimSwayFormulaGrowsWithDifficultyWindAndFatigue()
        {
            GameObject host = new GameObject("AimSwayTest");
            try
            {
                ArcheryChallenge.Archery.AimSway sway = host.AddComponent<ArcheryChallenge.Archery.AimSway>();
                float easy = sway.ComputeAmplitude(0.35f, 3f, 3f);
                float normal = sway.ComputeAmplitude(1f, 3f, 3f);
                float hard = sway.ComputeAmplitude(1.6f, 3f, 3f);
                Assert.Less(easy, normal);
                Assert.Less(normal, hard);
                Assert.Greater(sway.ComputeAmplitude(1f, 10f, 3f), sway.ComputeAmplitude(1f, 0f, 3f), "le vent augmente le tremblement");
                Assert.Greater(sway.ComputeAmplitude(1f, 3f, 8f), sway.ComputeAmplitude(1f, 3f, 0f), "la fatigue augmente le tremblement");
                Assert.AreEqual(sway.ComputeAmplitude(1f, 3f, 10f), sway.ComputeAmplitude(1f, 3f, 99f), 0.0001f, "la fatigue est plafonnée");
                Assert.AreEqual(0f, sway.ComputeAmplitude(1f, 3f, 3f, 0f), 0.0001f, "aucun tremblement corde au repos");
                // Valeurs de la documentation : Difficile, vent 6 m/s, 4 s de maintien ≈ 1,4° ; Facile sans vent ≈ 0,2°.
                Assert.AreEqual(1.41f, sway.ComputeAmplitude(1.6f, 6f, 4f), 0.1f);
                Assert.AreEqual(0.23f, sway.ComputeAmplitude(0.35f, 0f, 4f), 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ArenaBoardsStayWithinTheShootersFieldOfView()
        {
            // Depuis le centre de la plateforme, un panneau plus de 38° à côté du regard sort du champ d'une fenêtre 16/9 ou d'un casque.
            foreach (string name in new[] { "Arena_01", "Arena_02", "Arena_03", "Arena_04", "Arena_05", "Arena_Training" })
            {
                Scene scene = OpenAdditive($"{Scenes}Arenas/{name}.unity");
                try
                {
                    LevelController level = Find<LevelController>(scene)[0];
                    Vector3 eye = new Vector3(0f, 1.7f, -0.5f);
                    foreach (string anchorName in new[] { "BriefingAnchor", "HudAnchor" })
                    {
                        Transform anchor = level.transform.Find("Panels/" + anchorName);
                        Assert.IsNotNull(anchor, name + " : " + anchorName);
                        Vector3 local = level.transform.InverseTransformPoint(anchor.position) - eye;
                        float angle = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                        float halfWidth = 0.5f * 1.5f * 1f;   // demi-largeur maximale d'un panneau de 1 m posé à l'échelle 1,5
                        float edge = angle + Mathf.Atan2(halfWidth, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                        Assert.Less(edge, 42f, $"{name} : le bord de {anchorName} est à {edge:0}° du regard (limite 42°)");
                    }
                }
                finally
                {
                    Close(scene);
                }
            }
        }

        [Test]
        public void ArenaHudsAreManagedSoTheyAlwaysStayInView()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/HudPanel.prefab");
            Assert.IsNotNull(prefab, "prefab HudPanel");
            Assert.IsNotNull(prefab.GetComponent<WorldHudBoard>(), "HudPanel porte WorldHudBoard (masqué au PC, suit le regard en casque)");
            foreach (string name in new[] { "Arena_01", "Arena_02", "Arena_03", "Arena_04", "Arena_05" })
            {
                Scene scene = OpenAdditive($"{Scenes}Arenas/{name}.unity");
                try
                {
                    Assert.AreEqual(1, Find<WorldHudBoard>(scene).Count, name + " : un HUD d'arène géré par WorldHudBoard");
                }
                finally
                {
                    Close(scene);
                }
            }
        }

        [Test]
        public void BootRigHasTheSimulatedRigDriverAndPlayerModeService()
        {
            Scene boot = OpenAdditive(Scenes + "Boot.unity");
            try
            {
                Assert.AreEqual(1, Find<ArcheryChallenge.Player.SimulatedRigDriver>(boot).Count, "Boot : un SimulatedRigDriver sur le XR Origin");
                ArcheryChallenge.Player.SimulatedRigDriver driver = Find<ArcheryChallenge.Player.SimulatedRigDriver>(boot)[0];
                Assert.IsNotNull(driver.GetComponent<ArcheryChallenge.Input.XRArcherInput>(), "le pilote est sur le même objet que XRArcherInput");
                Assert.IsNotNull(driver.GetComponent<UnityEngine.CharacterController>(), "et que le Character Controller");
                Assert.GreaterOrEqual(driver.EyeHeight, 1.5f, "hauteur des yeux normale");
            }
            finally
            {
                Close(boot);
            }
        }

        [Test]
        public void TheGameIsEasierAndTheWindGentler()
        {
            // Objectifs de points (Normal) : première étoile de chaque arène au plus égale aux seuils réduits.
            int[] firstStar = { 36, 42, 48, 54, 72 };
            for (int i = 0; i < 5; i++)
            {
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/_Project/Data/Levels/Level_0{i + 1}.asset");
                DifficultyProfile normal = AssetDatabase.LoadAssetAtPath<DifficultyProfile>("Assets/_Project/Data/Difficulty/Difficulty_Normal.asset");
                Assert.LessOrEqual(level.GetThreshold(1, normal.starThresholdMultiplier), firstStar[i] + 1, $"Level_0{i + 1} : seuil de la 1re étoile en Normal");
            }

            DifficultyProfile easy = AssetDatabase.LoadAssetAtPath<DifficultyProfile>("Assets/_Project/Data/Difficulty/Difficulty_Easy.asset");
            DifficultyProfile hard = AssetDatabase.LoadAssetAtPath<DifficultyProfile>("Assets/_Project/Data/Difficulty/Difficulty_Hard.asset");
            DifficultyProfile norm = AssetDatabase.LoadAssetAtPath<DifficultyProfile>("Assets/_Project/Data/Difficulty/Difficulty_Normal.asset");
            Assert.Less(easy.starThresholdMultiplier, norm.starThresholdMultiplier);
            Assert.Less(norm.starThresholdMultiplier, hard.starThresholdMultiplier);
            Assert.LessOrEqual(hard.starThresholdMultiplier, 1f, "même en Difficile, les seuils ne dépassent plus ceux de référence");

            // Vent : le plus fort possible (Difficile × arène 5) reste sous 7 m/s au lieu de 12.
            LevelDefinition last = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Project/Data/Levels/Level_05.asset");
            Assert.LessOrEqual(hard.windMax * last.windMultiplier, 7f, "vent maximal d'une manche");
            Assert.LessOrEqual(norm.windMax, 2.5f, "vent maximal en Normal");
            Assert.AreEqual(0f, easy.windMax, "pas de vent en Facile");
        }

        [Test]
        public void VrRigHasTheBodyGuardAndLocomotion()
        {
            // Le rig VR de Boot porte le garde-fou du Character Controller et l'objet « Locomotion » qu'il suspend.
            Scene boot = OpenAdditive(Scenes + "Boot.unity");
            try
            {
                Transform rig = null;
                Transform locomotion = null;
                foreach (Transform t in Find<Transform>(boot))
                {
                    if (t.name == "XR Origin (XR Rig)") rig = t;
                    if (t.name == "Locomotion") locomotion = t;
                }
                Assert.IsNotNull(rig, "XR Origin (XR Rig)");
                Assert.IsNotNull(rig.GetComponent<ArcheryChallenge.Player.VrBodyGuard>(), "VrBodyGuard sur le XR Origin");
                Assert.IsNotNull(rig.GetComponent<CharacterController>(), "Character Controller sur le XR Origin");
                Assert.IsNotNull(locomotion, "objet Locomotion");
                Assert.IsTrue(locomotion.IsChildOf(rig), "Locomotion est sous le XR Origin");
            }
            finally
            {
                Close(boot);
            }
        }

        [Test]
        public void EveryClosableMenuHasACloseCross()
        {
            // Chaque panneau global fermable porte une croix « X » (BtnClose) en haut à droite ; le voyage rapide n'a plus de bouton « Fermer ».
            foreach (string panel in new[] { "PauseMenu", "LeaderboardPanel", "FastTravelPanel", "MainMenuPanel", "ControlsPanel", "CampfireProfileMenu" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/{panel}.prefab");
                Assert.IsNotNull(prefab, panel);
                Transform close = null;
                foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                    if (t.name == "BtnClose") close = t;
                Assert.IsNotNull(close, panel + " : croix de fermeture (BtnClose)");
                TMPro.TMP_Text label = close.GetComponentInChildren<TMPro.TMP_Text>(true);
                Assert.AreEqual("X", label != null ? label.text : "", panel + " : la fermeture est une croix");
                RectTransform rect = (RectTransform)close;
                RectTransform canvasRect = (RectTransform)prefab.transform;
                Assert.Greater(rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f, canvasRect.sizeDelta.x * 0.8f, panel + " : la croix est à droite");
                Assert.Greater(rect.anchoredPosition.y, -canvasRect.sizeDelta.y * 0.2f, panel + " : la croix est en haut");
            }

            // Voyage rapide : 7 boutons de 60 + 6 intervalles tiennent dans la liste, qui reste dans le panneau.
            GameObject travel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FastTravelPanel.prefab");
            RectTransform content = (RectTransform)travel.transform.Find("Content");
            UnityEngine.UI.VerticalLayoutGroup layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            float needed = 7 * 60f + 6 * layout.spacing;
            Assert.GreaterOrEqual(content.sizeDelta.y, needed, "la liste contient les 7 destinations");
            Assert.LessOrEqual(-content.anchoredPosition.y + content.sizeDelta.y, ((RectTransform)travel.transform).sizeDelta.y, "la liste reste dans le panneau");
            foreach (UnityEngine.UI.Button b in travel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                TMPro.TMP_Text l = b.GetComponentInChildren<TMPro.TMP_Text>(true);
                Assert.AreNotEqual("Fermer", l != null ? l.text : "", "plus de bouton « Fermer » au voyage rapide");
            }
        }

        [Test]
        public void VrGameplayControlsPageExistsAndCoversTheXrGestures()
        {
            // Page « Gameplay VR » de l'écran Commandes : le bouton existe dans le prefab et tous les gestes y sont.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/ControlsPanel.prefab");
            Assert.IsNotNull(prefab.transform.Find("BtnPageVr"), "bouton de la page Gameplay VR");
            Assert.LessOrEqual(ControlsCatalog.VrGameplay.Length, ArcheryChallenge.UI.ControlsPanel.RowCount, "la page tient dans le tableau");
            string all = "";
            foreach (ControlEntry e in ControlsCatalog.VrGameplay)
                all += e.Action + "|" + e.Desktop + "|" + e.Vr + "|";
            foreach (string word in new[] { "Stick gauche", "Stick droit", "Grip", "épaule", "dos", "gâchette", "Menu", "Bouton Y", "Relâcher", "main gauche" })
                StringAssert.Contains(word, all, "geste absent de la page Gameplay VR : " + word);
        }

        [Test]
        public void ArenaPlatformIsATeleportAnchorThatTheLaserCanHit()
        {
            // Le laser ignore les colliders « trigger » : l'ancre d'origine (petite boîte trigger) était invisible, donc impossible de se
            // téléporter sur l'estrade. L'estrade entière doit être l'ancre, avec un collider plein.
            GameObject root = new GameObject("ArenaRoot_Test");
            try
            {
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "Platform";
                platform.transform.SetParent(root.transform, false);
                GameObject arrival = new GameObject("TeleportAnchor");
                arrival.transform.SetParent(root.transform, false);
                arrival.transform.localPosition = new Vector3(0f, 0.3f, -0.5f);
                arrival.AddComponent<BoxCollider>().isTrigger = true;
                arrival.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();

                var anchor = ArcheryChallenge.Levels.ArenaTeleportSurface.Ensure(root.transform);
                Assert.IsNotNull(anchor, "l'estrade devient une ancre de téléportation");
                Assert.AreSame(platform.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(), anchor);
                Assert.AreSame(arrival.transform, anchor.teleportAnchorTransform, "le joueur arrive au point d'arrivée");
                Assert.IsFalse(platform.GetComponent<Collider>().isTrigger, "collider plein : le laser peut le toucher");
                Assert.IsNull(arrival.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(), "l'ancienne ancre trigger est retirée");

                Assert.AreSame(anchor, ArcheryChallenge.Levels.ArenaTeleportSurface.Ensure(root.transform), "idempotent");
                Assert.AreEqual(1, platform.GetComponents<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>().Length);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnlyEasyShowsThePredictedArrowPath()
        {
            // La ligne de trajectoire prévue est réservée au mode Facile (le réticule de visée VR, lui, est affiché partout).
            foreach (var (name, expected) in new[] { ("Easy", true), ("Normal", false), ("Hard", false) })
            {
                var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcheryChallenge.Levels.DifficultyProfile>($"Assets/_Project/Data/Difficulty/Difficulty_{name}.asset");
                Assert.IsNotNull(profile, name);
                Assert.AreEqual(expected, profile.showTrajectory, "trajectoire prévue en " + name);
            }
        }

        [Test]
        public void SprintIsTheDefaultWalkingMode()
        {
            // Allure rapide par défaut en VR (4 m/s) ; au clavier/souris, Maj ralentit (cases « Run By Default »).
            Assert.GreaterOrEqual(new ArcheryChallenge.Save.SettingsData().moveSpeed, 4f, "vitesse VR par défaut");
            GameObject desktop = null;
            Scene boot = OpenAdditive(Scenes + "Boot.unity");
            try
            {
                foreach (Transform t in Find<Transform>(boot))
                    if (t.name == "DesktopPlayer") desktop = t.gameObject;
                Assert.IsNotNull(desktop, "DesktopPlayer");
                var controller = desktop.GetComponent<ArcheryChallenge.Player.DesktopPlayerController>();
                var so = new SerializedObject(controller);
                Assert.IsTrue(so.FindProperty("runByDefault").boolValue, "le joueur clavier/souris court par défaut");
            }
            finally
            {
                Close(boot);
            }
        }

        [Test]
        public void ControlsCatalogCoversAllKeys()
        {
            string all = "";
            foreach (ControlEntry e in ControlsCatalog.Game)
                all += e.Desktop + "|" + e.Vr + "|";
            foreach (string key in new[] { "R ", "E ", "T", "Échap", "Maj", "Espace", "Clic gauche", "Clic droit", "F1", "Grip", "Stick", "Menu", "Y" })
                StringAssert.Contains(key.Trim(), all, "touche absente du catalogue : " + key);
            string legend = ControlsCatalog.DevLegend();
            for (int i = 1; i <= 11; i++)
                StringAssert.Contains("F" + i, legend);
        }
    }
}
