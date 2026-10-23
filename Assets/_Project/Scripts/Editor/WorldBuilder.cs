// WorldBuilder : assemble la scène World (terrain, camp, hub, barrières, portails, monstres, ambiance).
//
// Menu : Tools → Archery Challenge → Build World.
// Reproduit les phases P8-A, P8-B, P9 (soleil, lune, ciel), P10 (NavMesh, monstres) et P14.6 (ambiances)
// du tutoriel. La scène produite est un point de départ complet : l'équipe peut la retoucher à la main
// (le terrain se repeint au pinceau, les objets se déplacent, etc.).

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Audio;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Monsters;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Targets;
using ArcheryChallenge.UI;
using ArcheryChallenge.World;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de la scène World.</summary>
    public static class WorldBuilder
    {
        private const string ScenePath = BuildUtils.SceneRoot + "/World.unity";
        private static Terrain terrain;
        private static Material woodMaterial;

        private static float GroundY(float x, float z) =>
            terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : TerrainBuilder.GroundLevel;

        [MenuItem("Tools/Archery Challenge/Build World")]
        private static void BuildAllMenu()
        {
            if (BuildUtils.ConfirmSceneRegeneration("la scène World"))
                BuildAll();
        }

        public static void BuildAll()
        {
            WorldPrefabBuilder.BuildAll();
            BuildScene();
        }

        public static void BuildScene()
        {
            woodMaterial = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.38f, 0.25f, 0.13f));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- terrain ---
            TerrainData data = TerrainBuilder.Build(out string report);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.layer = BuildUtils.Layer("Environment");
            terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 100f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            terrain.treeDistance = 150f;
            terrain.treeBillboardDistance = 1000f; // pas de billboard : les arbres Pure Poly n'ont pas le shader Soft Occlusion
            terrain.detailObjectDistance = 40f;
            terrainObject.AddComponent<SurfaceTag>().Surface = SurfaceType.Dirt;
            TeleportationArea area = terrainObject.AddComponent<TeleportationArea>();
            area.interactionLayers = UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");

            GameObject root = new GameObject("--- WORLD ---");
            BuildWater(root.transform);
            GameObject boundary = new GameObject("WorldBoundary");
            boundary.AddComponent<WorldBoundary>().CreateWalls();

            BuildSkyAndSun();
            BuildCamp(root.transform);
            BuildHub(root.transform);
            BuildGates(root.transform);
            BuildArenaPortals(root.transform);
            BuildBridgeAndDecor(root.transform);
            BuildFoliage(root.transform);
            BuildAudioZones(root.transform);
            BuildNavMeshAndMonsters(root.transform);

            // Les éléments immobiles sont « Static » (batching statique, §P16.3).
            MarkStatic(root.transform, "Decor");

            ConfigureLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BootBuilder.UpdateBuildScenes();
            Debug.Log("[WorldBuilder] World généré : " + report);
        }

        // ---------------------------------------------------------------- ciel, soleil, éclairage

        private static void BuildSkyAndSun()
        {
            Material sky = BuildUtils.EnsureMaterial("M_Sky", "Skybox/Procedural", Color.white);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.04f);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 1f);

            GameObject sunMoon = new GameObject("SunMoon");
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(sunMoon.transform, false);
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Hard;
            sunLight.intensity = 1.2f;
            sunLight.lightmapBakeType = LightmapBakeType.Realtime;
            GameObject moon = new GameObject("Moon");
            moon.transform.SetParent(sunMoon.transform, false);
            Light moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.shadows = LightShadows.None;
            moonLight.color = new Color(0.62f, 0.71f, 1f);
            moonLight.intensity = 0.25f;
            moonLight.lightmapBakeType = LightmapBakeType.Realtime;

            SunMoonController controller = sunMoon.AddComponent<SunMoonController>();
            BuildUtils.Set(controller, "sun", sunLight);
            BuildUtils.Set(controller, "moon", moonLight);
            BuildUtils.Set(controller, "profile", AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>($"{BuildUtils.DataRoot}/TimeOfDay/TimeOfDay_Default.asset"));
            BuildUtils.Set(controller, "skyMaterial", sky);

            RenderSettings.skybox = sky;
            RenderSettings.reflectionIntensity = 0.12f; // reflets du ciel atténués : sol mat, pas d'aspect plastique
            RenderSettings.sun = sunLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
        }

        private static void ConfigureLighting()
        {
            // Pas de lumière précalculée : le soleil bouge, tout est temps réel (§P9.4).
            LightingSettings settings = new LightingSettings { name = "World_LightingSettings", bakedGI = false, realtimeGI = false };
            string path = BuildUtils.SceneRoot.Replace("Scenes", "Art") + "/World_LightingSettings.lighting";
            BuildUtils.EnsureFolder("Assets/_Project/Art");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(settings, path);
            Lightmapping.lightingSettings = settings;
        }

        // ---------------------------------------------------------------- eau

        private static void BuildWater(Transform parent)
        {
            Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pure Poly/Free Low Poly Nature Pack/Materials/PP_Water.mat");
            GameObject waterRoot = new GameObject("Water");
            waterRoot.transform.SetParent(parent, false);
            CreateWater(waterRoot.transform, "Water_River", new Vector3(79f, 8.6f, 81f), new Vector3(0.7f, 1f, 6.3f), water);
            CreateWater(waterRoot.transform, "Water_Pond", new Vector3(79f, 8.6f, 50f), new Vector3(1.4f, 1f, 1.4f), water);
            CreateWater(waterRoot.transform, "Water_Source", new Vector3(79f, 8.6f, 112f), new Vector3(0.9f, 1f, 0.9f), water);
        }

        private static void CreateWater(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.position = position;
            plane.transform.localScale = scale;
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            int water = LayerMask.NameToLayer("Water");
            plane.layer = water >= 0 ? water : 0;
            if (material != null)
                plane.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- camp

        private static void BuildCamp(Transform parent)
        {
            Vector3 center = new Vector3(25f, GroundY(25f, 25f), 25f);
            GameObject camp = new GameObject("Camp");
            camp.transform.SetParent(parent, false);
            camp.transform.position = center;

            // Feu : 3 bûches en étoile + cercle de pierres.
            GameObject fire = new GameObject("Campfire");
            fire.transform.SetParent(camp.transform, false);
            for (int i = 0; i < 3; i++)
                WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Anvil_Log"), fire.transform, "Log" + i, Vector3.zero, new Vector3(0f, i * 120f, 0f), 0.6f);
            GameObject rock = WorldPrefabBuilder.Prefab("Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Rock_Pile_Forest_Moss_05.prefab");
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                WorldPrefabBuilder.Place(rock, fire.transform, "Stone" + i, new Vector3(Mathf.Cos(a) * 1.2f, 0f, Mathf.Sin(a) * 1.2f), new Vector3(0f, i * 45f, 0f), 0.06f);
            }
            // Flammes et lumière permanente.
            GameObject fireLight = BuildUtils.Child(fire.transform, "FireLight", new Vector3(0f, 0.6f, 0f));
            Light light = fireLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.24f);
            light.range = 8f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            GameObject flame = (GameObject)PrefabUtility.InstantiatePrefab(WorldPrefabBuilder.Prefab(BuildUtils.PrefabRoot + "/VFX/VFX_Campfire.prefab"), fire.transform);
            flame.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            NightLight nightLight = fireLight.AddComponent<NightLight>();
            BuildUtils.Set(nightLight, "lightSource", light);
            BuildUtils.Set(nightLight, "alwaysOn", true);
            GameObject campAmbience = BuildUtils.Child(fire.transform, "Ambience_Campfire", Vector3.zero);
            AmbienceZone zone = campAmbience.AddComponent<AmbienceZone>();
            zone.DayBank = "Campfire";
            zone.NightBank = "Campfire";

            // Banc, point d'apparition « camp ».
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Bench"), camp.transform, "Seat", new Vector3(0f, 0f, -2.5f), Vector3.zero, 1f, true);
            GameObject spawn = BuildUtils.Child(camp.transform, "PlayerSpawn_Camp", new Vector3(0f, 0f, -3.4f));
            spawn.tag = "SpawnPoint";
            PlayerSpawnPoint spawnPoint = spawn.AddComponent<PlayerSpawnPoint>();
            BuildUtils.Set(spawnPoint, "spawnId", "camp");
            CampfireSeat seat = spawn.AddComponent<CampfireSeat>();
            BuildUtils.Set(seat, "seatedViewOffset", -0.5f);

            // Tente, couchage, accessoires.
            GameObject tent = WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Stall_Empty"), camp.transform, "Tent", new Vector3(-5f, 0f, 4f), new Vector3(0f, 130f, 0f), 1.4f, true);
            if (tent != null)
                WorldPrefabBuilder.MakeBannersDoubleSided(tent);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Bed_Twin1"), camp.transform, "Bedroll", new Vector3(-5f, 0f, 1.5f), new Vector3(0f, 80f, 0f), 0.8f, true);
            GameObject props = new GameObject("Props");
            props.transform.SetParent(camp.transform, false);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Bag"), props.transform, "Bag", new Vector3(2.4f, 0f, -1.2f), new Vector3(0f, 25f, 0f), 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Barrel"), props.transform, "Barrel", new Vector3(4f, 0f, 2f), Vector3.zero, 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Crate_Wooden"), props.transform, "Crate", new Vector3(4.6f, 0f, 3.1f), new Vector3(0f, 35f, 0f), 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Bucket_Wooden_1"), props.transform, "Bucket", new Vector3(3.2f, 0f, 3.4f), Vector3.zero, 1f);

            // Panneau des profils au-dessus du feu (MenuAnchor).
            GameObject menuAnchor = BuildUtils.Child(camp.transform, "MenuAnchor", new Vector3(0f, 2.0f, 2.4f));
            menuAnchor.transform.localScale = Vector3.one * 2.4f;
            GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(WorldPrefabBuilder.Prefab(BuildUtils.PrefabRoot + "/UI/CampfireProfileMenu.prefab"), menuAnchor.transform);
            menu.transform.localPosition = Vector3.zero;
            menu.transform.localRotation = Quaternion.identity;
            // Les boutons de fermeture n'ont pas de sens sur un panneau de décor.
            Transform close = UiBind.Find(menu.transform, "BtnClose");
            if (close != null) close.gameObject.SetActive(false);
            PanelOpener opener = camp.AddComponent<PanelOpener>();
            opener.Kind = PanelKind.Profile;
            BuildUtils.Set(opener, "lookTarget", menuAnchor.transform);
            BuildUtils.Set(opener, "distance", 9f);

            // Panneau indiquant le village.
            BuildSign(camp.transform, "SignToHub", new Vector3(6f, 0f, 1f), "Village des archers →", Quaternion.Euler(0f, 90f, 0f));

            // Le panneau flottant du feu de camp se tourne toujours vers le joueur.
            menuAnchor.AddComponent<FacePlayer>();

            // Tableau des commandes (gestes VR et touches), lisible depuis le banc sans ouvrir de menu.
            BuildBoard(camp.transform, "ControlsBoard", new Vector3(6.5f, 0f, -3.5f), camp.transform.TransformPoint(new Vector3(0f, 0f, -3.4f)), "ControlsPanel", PanelKind.Controls, 2.6f);
        }

        // Place une instance d'un prefab du dossier Prefabs/World (drapeaux…) ; « hanging » : suspendu, sans pied au sol.
        private static GameObject PlaceWorldPrefab(string prefabName, Transform parent, string name, Vector3 localPosition, float yaw, bool hanging = false)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildUtils.PrefabRoot}/World/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[WorldBuilder] Prefab introuvable : " + prefabName);
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return instance;
        }

        private static void BuildSign(Transform parent, string name, Vector3 localPosition, string text, Quaternion localRotation)
        {
            GameObject sign = BuildUtils.Child(parent, name, localPosition, localRotation);
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(sign.transform, false);
            post.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            post.transform.localScale = new Vector3(0.14f, 2.2f, 0.14f);
            post.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(sign.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.9f, -0.1f);
            board.transform.localScale = new Vector3(1.8f, 0.45f, 0.06f);
            board.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            GameObject label = new GameObject("Text");
            label.transform.SetParent(sign.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.9f, -0.15f);
            TextMeshPro t = label.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = 2.2f;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(1.7f, 0.4f);
            t.color = new Color(1f, 0.92f, 0.7f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 1f;
            t.fontSizeMax = 2.4f;
        }

        // ---------------------------------------------------------------- hub

        private static void BuildHub(Transform parent)
        {
            Vector3 center = new Vector3(70f, GroundY(70f, 30f), 30f);
            GameObject hub = new GameObject("Hub");
            hub.transform.SetParent(parent, false);
            hub.transform.position = center;

            GameObject spawn = BuildUtils.Child(hub.transform, "PlayerSpawn_Hub", new Vector3(0f, 0f, -8f));
            spawn.tag = "SpawnPoint";
            BuildUtils.Set(spawn.AddComponent<PlayerSpawnPoint>(), "spawnId", "hub");

            // Tableaux : orientés vers la place (le côté lisible du panneau regarde le centre).
            Transform menuUi = BuildBoard(hub.transform, "MenuBoard", new Vector3(-8f, 0f, 5f), center, "MainMenuPanel", PanelKind.MainMenu);
            BuildBoard(hub.transform, "LeaderboardBoard", new Vector3(0f, 0f, 12f), center, "LeaderboardPanel", PanelKind.Leaderboard);
            Transform travelUi = BuildBoard(hub.transform, "FastTravelBoard", new Vector3(-10f, 0f, -4f), center, "FastTravelPanel", PanelKind.FastTravel);
            FastTravelBoard travelBoard = travelUi.parent.gameObject.AddComponent<FastTravelBoard>();
            travelBoard.CollectDestinations();

            // Tableau des commandes près de l'arrivée des joueurs.
            BuildBoard(hub.transform, "ControlsBoard", new Vector3(-6f, 0f, -10f), center, "ControlsPanel", PanelKind.Controls, 2.6f);

            // Râtelier d'armes.
            GameObject rack = (GameObject)PrefabUtility.InstantiatePrefab(WorldPrefabBuilder.Prefab(BuildUtils.PrefabRoot + "/World/ArmoryRack.prefab"), hub.transform);
            rack.name = "ArmoryRack";
            rack.transform.localPosition = new Vector3(8f, 0f, 5f);
            FaceCenter(rack.transform, center, true);

            // Portail d'entraînement : arche avec bannière.
            GameObject gate = BuildUtils.Child(hub.transform, "TrainingGate", new Vector3(19f, 0f, -3f), Quaternion.Euler(0f, 90f, 0f));
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(gate.transform, false);
                post.transform.localPosition = new Vector3(x, 1.6f, 0f);
                post.transform.localScale = new Vector3(0.3f, 3.2f, 0.3f);
                post.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            }
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(gate.transform, false);
            beam.transform.localPosition = new Vector3(0f, 3.3f, 0f);
            beam.transform.localScale = new Vector3(4.8f, 0.3f, 0.3f);
            beam.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            // Drapeaux suspendus à la poutre (tissu seul, visible des deux côtés).
            PlaceWorldPrefab("Hanging_Banner_2_Cloth", gate.transform, "BannerLeft", new Vector3(-1.5f, 3.15f, -0.2f), 0f, true);
            PlaceWorldPrefab("Hanging_Banner_1_Cloth", gate.transform, "BannerRight", new Vector3(1.5f, 3.15f, -0.2f), 0f, true);
            GameObject gateLabel = new GameObject("Text");
            gateLabel.transform.SetParent(gate.transform, false);
            gateLabel.transform.localPosition = new Vector3(0f, 3.9f, -0.25f);
            TextMeshPro label = gateLabel.AddComponent<TextMeshPro>();
            label.text = "Stand d'entraînement";
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4.6f, 0.7f);
            label.color = new Color(1f, 0.9f, 0.6f);

            // Étals, tonneaux, bannières.
            Transform decor = BuildUtils.Child(hub.transform, "Decor").transform;
            GameObject stall = WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Stall_Empty"), decor, "Stall1", new Vector3(14f, 0f, 8f), new Vector3(0f, -120f, 0f), 1.4f, true);
            if (stall != null)
                WorldPrefabBuilder.MakeBannersDoubleSided(stall);
            GameObject cart = WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Stall_Cart_Empty"), decor, "Cart", new Vector3(-14f, 0f, 8f), new Vector3(0f, 110f, 0f), 1.2f, true);
            if (cart != null)
                WorldPrefabBuilder.MakeBannersDoubleSided(cart);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Table_Large"), decor, "Table", new Vector3(12f, 0f, -8f), new Vector3(0f, 20f, 0f), 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Barrel_Apples"), decor, "Apples", new Vector3(13.2f, 0f, 8.5f), Vector3.zero, 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Barrel"), decor, "Barrel1", new Vector3(-15f, 0f, 4f), Vector3.zero, 1f, true);
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Crate_Wooden"), decor, "Crate1", new Vector3(-15.8f, 0f, 5f), new Vector3(0f, 15f, 0f), 1f, true);
            // Drapeaux sur poteaux plantés au sol aux quatre coins de la place.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                PlaceWorldPrefab(i % 2 == 0 ? "Flag_Banner_1" : "Flag_Banner_2", decor, "Flag" + i,
                    new Vector3(Mathf.Cos(a) * 11f, 0f, Mathf.Sin(a) * 9f), -a * Mathf.Rad2Deg + 90f);
            }

            // Torches : 10 autour de la place.
            GameObject torchPrefab = WorldPrefabBuilder.Prefab(BuildUtils.PrefabRoot + "/World/Torch_Lit.prefab");
            Transform torches = BuildUtils.Child(hub.transform, "Torches").transform;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                GameObject torch = (GameObject)PrefabUtility.InstantiatePrefab(torchPrefab, torches);
                torch.name = "Torch" + i;
                Vector3 pos = center + new Vector3(Mathf.Cos(a) * 17f, 0f, Mathf.Sin(a) * 12f);
                pos.y = GroundY(pos.x, pos.z) + 1.8f;
                torch.transform.position = pos;
            }

            // Trophée (désactivé : le script de victoire l'active).
            GameObject trophy = BuildUtils.Child(hub.transform, "Trophy", new Vector3(0f, 0f, 8f));
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(trophy.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            pedestal.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            pedestal.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            WorldPrefabBuilder.Place(WorldPrefabBuilder.Fbx("Chalice"), trophy.transform, "Chalice", new Vector3(0f, 1f, 0f), Vector3.zero, 3f);
            TrophyDisplay trophyDisplay = hub.AddComponent<TrophyDisplay>();
            trophyDisplay.Trophy = trophy;
            EditorUtility.SetDirty(trophyDisplay);
            trophy.SetActive(false);

            BuildSign(hub.transform, "SignToArena1", new Vector3(0f, 0f, 15f), "La Clairière ↑", Quaternion.identity);
        }

        // Construit un tableau (deux poteaux, planche) orienté vers le centre, avec son panneau d'interface.
        private static Transform BuildBoard(Transform parent, string name, Vector3 localPosition, Vector3 hubCenter, string panelPrefab, PanelKind kind, float uiScale = 1.9f)
        {
            Vector3 worldPosition = parent.TransformPoint(localPosition);
            Quaternion rotation = WorldPrefabBuilder.FaceAwayFrom(worldPosition, hubCenter);
            Transform uiAnchor = WorldPrefabBuilder.BuildBoardStructure(parent, name, localPosition, rotation, panelPrefab, uiScale, out GameObject board);

            PanelOpener opener = board.AddComponent<PanelOpener>();
            opener.Kind = kind;
            BuildUtils.Set(opener, "lookTarget", uiAnchor);
            return uiAnchor;
        }

        // Tourne un objet pour que sa face lisible (côté −Z) regarde le centre.
        private static void FaceCenter(Transform t, Vector3 center, bool flatten)
        {
            Vector3 away = t.position - center;
            if (flatten) away.y = 0f;
            if (away.sqrMagnitude < 0.01f) return;
            t.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        // ---------------------------------------------------------------- barrières et portails

        private static void BuildGates(Transform parent)
        {
            GameObject gatePrefab = WorldPrefabBuilder.Prefab(BuildUtils.PrefabRoot + "/World/ProgressionGate.prefab");
            Transform gates = BuildUtils.Child(parent, "--- GATES ---").transform;
            (string name, Vector2 pos, string level)[] list =
            {
                ("Gate_A2", new Vector2(55f, 78f), "Level_01"),
                ("Gate_A3", new Vector2(66f, 92f), "Level_02"),
                ("Gate_A4", new Vector2(86f, 60f), "Level_03"),
                ("Gate_A5", new Vector2(112f, 106f), "Level_04")
            };
            foreach (var g in list)
            {
                GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, gates);
                gate.name = g.name;
                float y = GroundY(g.pos.x, g.pos.y);
                gate.transform.position = new Vector3(g.pos.x, y, g.pos.y);
                gate.transform.rotation = Quaternion.Euler(0f, PathHeading(g.pos), 0f);
                ProgressionGate component = gate.GetComponent<ProgressionGate>();
                component.RequiredLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{BuildUtils.DataRoot}/Levels/{g.level}.asset");
                EditorUtility.SetDirty(component);
            }
        }

        // Direction du chemin au point donné : la barrière lui est perpendiculaire (son axe Z suit le chemin).
        private static float PathHeading(Vector2 point)
        {
            float best = float.MaxValue;
            Vector2 direction = Vector2.up;
            foreach (Vector2[] path in ArenaLayout.Paths)
            {
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                    float d = Vector2.Distance(point, a + ab * t);
                    if (d < best)
                    {
                        best = d;
                        direction = ab.normalized;
                    }
                }
            }
            return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        }

        private static void BuildArenaPortals(Transform parent)
        {
            Transform arenas = BuildUtils.Child(parent, "--- ARENAS ---").transform;
            foreach (ArenaSpec arena in ArenaLayout.Arenas)
            {
                string portalName = arena.IsTraining ? "Portal_Training" : "Portal_A" + arena.SceneName.Substring(arena.SceneName.Length - 1);
                GameObject portal = BuildUtils.Child(arenas, portalName, arena.PortalPosition);
                ArenaPortal component = portal.AddComponent<ArenaPortal>();
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{BuildUtils.DataRoot}/Levels/{arena.LevelFile}.asset");
                component.SceneName = arena.SceneName;
                component.Level = level;
                BuildUtils.Set(component, "loadRadius", arena.LoadRadius);
                BuildUtils.Set(component, "unloadRadius", arena.UnloadRadius);

                // Arrivée derrière la plateforme (6 m ; 3,5 m au Ravin pour rester hors de la colline), face aux cibles.
                Vector3 arrival = arena.Position + Quaternion.Euler(0f, arena.RotationY, 0f) * new Vector3(0f, 0f, -arena.ArrivalDistance);
                arrival.y = GroundY(arrival.x, arrival.z);
                GameObject arrivalObject = BuildUtils.Child(portal.transform, "Arrival");
                arrivalObject.transform.position = arrival;
                arrivalObject.transform.rotation = Quaternion.Euler(0f, arena.RotationY, 0f);
                FastTravelDestination destination = arrivalObject.AddComponent<FastTravelDestination>();
                destination.DestinationId = arena.IsTraining ? "training" : arena.SpawnId;
                destination.DisplayName = arena.Display;
                destination.RequiredLevel = arena.IsTraining ? null : level;
                EditorUtility.SetDirty(destination);
            }

            // Retour au village.
            GameObject hubPoint = BuildUtils.Child(arenas, "FastTravel_Hub", new Vector3(70f, GroundY(70f, 22f), 22f));
            FastTravelDestination hubDestination = hubPoint.AddComponent<FastTravelDestination>();
            hubDestination.DestinationId = "hub";
            hubDestination.DisplayName = "Village des archers";
            EditorUtility.SetDirty(hubDestination);
        }

        // ---------------------------------------------------------------- pont et décor

        private static void BuildBridgeAndDecor(Transform parent)
        {
            Transform decor = BuildUtils.Child(parent, "--- DECOR ---").transform;
            Transform bridge = BuildUtils.Child(decor, "Decor_Bridge").transform;
            string pp = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";
            // Le pont enjambe la rivière entre les berges (x ≈ 73 et 85, z = 60) : rampe gauche, QUATRE tronçons plats,
            // rampe droite. Les pivots des pièces du pack sont à l'une des extrémités : gauche = bord gauche de la
            // pièce, tronçon plat et rampe droite = bord droit. Chaque pièce touche la précédente (≈ 12,2 m au total).
            const float leftLength = 2.29f, middleLength = 1.99f, riverZ = 60f, riverX = 79f;
            const int middleCount = 4;
            float bridgeLength = leftLength + middleCount * middleLength + 1.98f;
            float x0 = riverX - bridgeLength * 0.5f;
            float bridgeY = GroundY(x0, riverZ);
            Place(pp + "PP_Bridge_15_Left.prefab", bridge, "Left", new Vector3(x0, bridgeY, riverZ), Vector3.zero);
            for (int i = 0; i < middleCount; i++)
                Place(pp + "PP_Bridge_15_Middle.prefab", bridge, "Middle" + i, new Vector3(x0 + leftLength + middleLength * (i + 1), bridgeY, riverZ), Vector3.zero);
            Place(pp + "PP_Bridge_15_Right.prefab", bridge, "Right", new Vector3(x0 + leftLength + middleLength * middleCount + 1.98f, bridgeY, riverZ), Vector3.zero);

            Transform rocks = BuildUtils.Child(decor, "Decor_Rocks").transform;
            Random.InitState(77);
            string[] rockPrefabs = { "PP_Rock_Moss_Grown_09", "PP_Rock_Moss_Grown_11", "PP_Rock_Pile_Forest_Moss_05", "PP_Rock_Pile_Forest_Moss_10" };
            Vector2[] rockSpots =
            {
                new Vector2(30, 52), new Vector2(46, 90), new Vector2(18, 100), new Vector2(92, 52), new Vector2(104, 48), new Vector2(120, 50),
                new Vector2(140, 52), new Vector2(139, 70), new Vector2(139, 98), new Vector2(60, 140),
                new Vector2(100, 112), new Vector2(40, 40), new Vector2(110, 40), new Vector2(150, 30), new Vector2(20, 60)
            };
            foreach (Vector2 spot in rockSpots)
            {
                string rockName = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
                GameObject rock = Place(pp + rockName + ".prefab", rocks, rockName, new Vector3(spot.x, GroundY(spot.x, spot.y), spot.y), new Vector3(0f, Random.Range(0f, 360f), 0f), Random.Range(0.5f, 0.9f));
                if (rock != null) rock.AddComponent<SurfaceTag>().Surface = SurfaceType.Stone;
            }

            // Clôtures le long du chemin, par endroits.
            Transform fences = BuildUtils.Child(decor, "Decor_Fences").transform;
            for (int i = 0; i < 12; i++)
            {
                float x = 38f + i * 1.9f;
                float z = 33.5f;
                Place(pp + "PP_Small_Fence_01.prefab", fences, "Fence" + i, new Vector3(x, GroundY(x, z), z), new Vector3(0f, 0f, 0f), 1f);
            }
        }

        // ---------------------------------------------------------------- plantes

        // Les plantes du monde ouvert sont de vrais objets rangés par groupe sous « --- FOLIAGE --- » : on les
        // déplace, supprime ou duplique à la main. Les zones de jeu (camp, village, entraînement, arènes) et les
        // chemins restent dégagés ; les arènes ont leurs propres plantes dans leur scène.
        private static void BuildFoliage(Transform parent)
        {
            Transform foliage = BuildUtils.Child(parent, "--- FOLIAGE ---").transform;
            Random.InitState(4242);

            System.Func<float, float, bool> allowed = (x, z) =>
            {
                float h = GroundY(x, z);
                if (h < 9.8f || h > 16f)
                    return false;                                   // eau, berges, ravin, montagnes
                if (TerrainBuilder.DistanceToPath(x, z) < 1.8f)
                    return false;                                   // chemins
                if (ArenaLayout.Camp.Contains(new Vector2(x, z)) || ArenaLayout.Hub.Contains(new Vector2(x, z)) || ArenaLayout.Training.Contains(new Vector2(x, z)))
                    return false;                                   // sol de terre battue
                foreach (ArenaSpec arena in ArenaLayout.Arenas)
                    if (!arena.IsTraining && arena.WorldRect.Contains(new Vector2(x, z)))
                        return false;                               // les arènes ont leurs propres plantes
                return true;
            };
            Rect map = new Rect(8, 8, 144, 144);

            Transform flowers = FoliageBuilder.Group(foliage, "Foliage_Fleurs");
            FoliageBuilder.Scatter(flowers, "PP_Daffodil_03", 45, map, allowed, GroundY, 2f, 3f, true);
            FoliageBuilder.Scatter(flowers, "PP_Hyacinth_04", 45, map, allowed, GroundY, 2f, 3f, true);
            FoliageBuilder.Scatter(flowers, "PP_Sunflower_04", 20, map, allowed, GroundY, 2f, 3.2f, true);

            Transform grass = FoliageBuilder.Group(foliage, "Foliage_Herbes");
            FoliageBuilder.Scatter(grass, "PP_Grass_11", 40, map, allowed, GroundY, 1.6f, 2.6f, true);
            FoliageBuilder.Scatter(grass, "PP_Grass_15", 40, map, allowed, GroundY, 1.6f, 2.6f, true);

            // Champignons : en lisière de forêt, loin des zones de jeu.
            System.Func<float, float, bool> forestEdge = (x, z) =>
            {
                float edge = Mathf.Min(Mathf.Min(x, TerrainBuilder.MapSize - x), Mathf.Min(z, TerrainBuilder.MapSize - z));
                return edge > 12f && edge < 40f && allowed(x, z);
            };
            Transform mushrooms = FoliageBuilder.Group(foliage, "Foliage_Champignons");
            foreach (string name in new[] { "PP_Mushroom_Fantasy_Orange_09", "PP_Mushroom_Fantasy_Orange_10", "PP_Mushroom_Fantasy_Purple_05", "PP_Mushroom_Fantasy_Purple_08" })
                FoliageBuilder.Scatter(mushrooms, name, 10, map, forestEdge, GroundY, 1.6f, 2.4f, true);

            Transform pebbles = FoliageBuilder.Group(foliage, "Foliage_Cailloux");
            FoliageBuilder.Scatter(pebbles, "PP_Cemetery_Pebbles_03", 14, map, allowed, GroundY, 2f, 3f, true);
            FoliageBuilder.Scatter(pebbles, "PP_Cemetery_Pebbles_09", 14, map, allowed, GroundY, 2f, 3f, true);
        }

        private static GameObject Place(string path, Transform parent, string name, Vector3 position, Vector3 euler, float scale = 1f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[WorldBuilder] Prefab introuvable : " + path);
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ---------------------------------------------------------------- ambiances sonores

        private static void BuildAudioZones(Transform parent)
        {
            Transform audio = BuildUtils.Child(parent, "--- AUDIO ---").transform;
            GameObject forest = BuildUtils.Child(audio, "Ambience_Forest", new Vector3(80f, 10f, 80f));
            AmbienceZone forestZone = forest.AddComponent<AmbienceZone>();
            forestZone.DayBank = "ForestDay";
            forestZone.NightBank = "ForestNight";
            forestZone.IsGlobal = true;

            foreach (float z in new[] { 60f, 85f, 108f })
            {
                GameObject river = BuildUtils.Child(audio, "Ambience_River_" + z, new Vector3(79f, 9f, z));
                AmbienceZone riverZone = river.AddComponent<AmbienceZone>();
                riverZone.DayBank = "River";
                riverZone.NightBank = "River";
                riverZone.IsGlobal = false;
            }

            // Réverbérations : forêt champêtre et ravin.
            GameObject reverb1 = BuildUtils.Child(audio, "Reverb_Forest", new Vector3(37f, 10f, 109f));
            AudioReverbZone zone1 = reverb1.AddComponent<AudioReverbZone>();
            zone1.reverbPreset = AudioReverbPreset.Forest;
            zone1.minDistance = 15f;
            zone1.maxDistance = 30f;
            GameObject reverb2 = BuildUtils.Child(audio, "Reverb_Ravine", new Vector3(130f, 5f, 80f));
            AudioReverbZone zone2 = reverb2.AddComponent<AudioReverbZone>();
            zone2.reverbPreset = AudioReverbPreset.Mountains;
            zone2.minDistance = 20f;
            zone2.maxDistance = 40f;
        }

        // ---------------------------------------------------------------- NavMesh et monstres

        private static void BuildNavMeshAndMonsters(Transform parent)
        {
            GameObject navObject = new GameObject("NavMesh");
            navObject.transform.SetParent(parent, false);
            NavMeshSurface surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << 0) | (1 << LayerMask.NameToLayer("Environment"));

            (string name, Vector3 center, Vector3 size)[] volumes =
            {
                ("NoMonster_Camp", new Vector3(25, 10, 25), new Vector3(24, 10, 24)),
                ("NoMonster_Hub", new Vector3(70, 10, 30), new Vector3(44, 10, 34)),
                ("NoMonster_Training", new Vector3(120, 10, 27.5f), new Vector3(54, 10, 29)),
                ("NoMonster_A1", new Vector3(32.5f, 10, 67), new Vector3(39, 10, 34)),
                ("NoMonster_A2", new Vector3(37.5f, 10, 109.5f), new Vector3(49, 10, 39)),
                ("NoMonster_A3", new Vector3(82, 10, 81), new Vector3(44, 10, 42)),
                ("NoMonster_A4", new Vector3(127, 5, 80), new Vector3(46, 30, 60)),
                ("NoMonster_A5", new Vector3(104, 13, 131), new Vector3(92, 10, 38))
            };
            foreach (var v in volumes)
            {
                GameObject go = new GameObject(v.name);
                go.transform.SetParent(navObject.transform, false);
                NavMeshModifierVolume volume = go.AddComponent<NavMeshModifierVolume>();
                // Boîtes très hautes : aucune surface (toit de tente, rocher, sommet d'arbre) ne reste praticable dans la zone.
                volume.center = new Vector3(v.center.x, v.center.y + 10f, v.center.z);
                volume.size = new Vector3(v.size.x, 80f, v.size.z);
                volume.area = 1; // Not Walkable
            }

            // Points d'apparition.
            GameObject slime = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/Monsters/Monster_Slime.prefab");
            GameObject turtle = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/Monsters/Monster_Turtle.prefab");
            Transform monsters = BuildUtils.Child(parent, "--- MONSTERS ---").transform;
            (string name, Vector2 pos, GameObject prefab, int min, int max)[] spawners =
            {
                ("Spawner_ForestNW_1", new Vector2(30, 140), slime, 2, 3),
                ("Spawner_ForestNW_2", new Vector2(48, 138), turtle, 1, 1),
                ("Spawner_CampWest", new Vector2(25, 44), slime, 2, 2),
                ("Spawner_Pond", new Vector2(97, 50), slime, 2, 3),
                ("Spawner_RavineSouth", new Vector2(130, 46), turtle, 1, 1),
                ("Spawner_A1A2", new Vector2(30, 87), slime, 1, 2)
            };
            foreach (var s in spawners)
            {
                GameObject go = BuildUtils.Child(monsters, s.name, new Vector3(s.pos.x, GroundY(s.pos.x, s.pos.y) + 0.1f, s.pos.y));
                MonsterSpawner spawner = go.AddComponent<MonsterSpawner>();
                spawner.MonsterPrefab = s.prefab;
                BuildUtils.Set(spawner, "countMin", s.min);
                BuildUtils.Set(spawner, "countMax", s.max);
                BuildUtils.Set(spawner, "respawnDelay", 180f);
                BuildUtils.Set(spawner, "wanderRadius", 10f);
                BuildUtils.Set(spawner, "activationDistance", 60f);
            }

            // Cuisson du NavMesh (les colliders des arbres de terrain sont pris en compte).
            surface.BuildNavMesh();
            if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
            {
                BuildUtils.EnsureFolder(BuildUtils.SceneRoot + "/World");
                AssetDatabase.CreateAsset(surface.navMeshData, BuildUtils.SceneRoot + "/World/World_NavMesh.asset");
            }
        }

        // ---------------------------------------------------------------- outils

        private static void MarkStatic(Transform root, string namePart)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Decor"))
                    foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }
    }
}
