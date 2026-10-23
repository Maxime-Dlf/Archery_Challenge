// BootBuilder : assemble le prefab « Systems » et câble la scène Boot.
//
// Menu : Tools → Archery Challenge → Build Boot Scene.
// Reproduit les étapes humaines du tutoriel §P2.3, §P3.8, §P4.3–P4.5 : services dans la scène,
// joueur VR équipé (corps, carquois dorsal, étui), joueur clavier/souris, simulateur XR.
// Idempotent : les objets créés par ce générateur sont recréés à chaque exécution.

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Player;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de la scène Boot et du prefab Systems.</summary>
    public static class BootBuilder
    {
        public const string BootPath = BuildUtils.SceneRoot + "/Boot.unity";
        private const string SystemsPath = BuildUtils.PrefabRoot + "/Systems/Systems.prefab";
        private const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/XR Interaction Simulator/XR Interaction Simulator.prefab";

        // Services posés sur l'objet Systems (types cherchés par nom : un service absent est ignoré).
        private static readonly string[] ServiceTypes =
        {
            "ArcheryChallenge.Core.GameBootstrap",
            "ArcheryChallenge.Core.SceneService",
            "ArcheryChallenge.Core.FadeService",
            "ArcheryChallenge.Core.PlayerModeService",
            "ArcheryChallenge.Core.DevShortcuts",
            "ArcheryChallenge.Core.PlatformQualitySettings",
            "ArcheryChallenge.Save.SettingsService",
            "ArcheryChallenge.Save.ProfileService",
            "ArcheryChallenge.Save.SaveSlotService",
            "ArcheryChallenge.Save.LeaderboardService",
            "ArcheryChallenge.Progression.EquipmentService",
            "ArcheryChallenge.Progression.UnlockService",
            "ArcheryChallenge.Targets.ScoreService",
            "ArcheryChallenge.Archery.WindService",
            "ArcheryChallenge.Archery.AimSway",
            "ArcheryChallenge.Archery.HapticsService",
            "ArcheryChallenge.Archery.ArrowPool",
            "ArcheryChallenge.Levels.DifficultyApplier",
            "ArcheryChallenge.World.TimeOfDayService",
            "ArcheryChallenge.World.FastTravelService",
            "ArcheryChallenge.Audio.AudioService",
            "ArcheryChallenge.VFX.ImpactFeedback",
            "ArcheryChallenge.Player.PlayerHealth",
            "ArcheryChallenge.UI.ToastService",
        };

        [MenuItem("Tools/Archery Challenge/Build Boot Scene")]
        private static void BuildAllMenu()
        {
            if (BuildUtils.ConfirmSceneRegeneration("la scène Boot"))
                BuildAll();
        }

        public static void BuildAll()
        {
            BuildSystemsPrefab();
            SetupBootScene();
            ToolsMenu.EnablePlayFromBoot();
            DisableAutomaticSimulator();
        }

        // ---------------------------------------------------------------- Systems

        public static GameObject BuildSystemsPrefab()
        {
            GameObject root = new GameObject("Systems");
            foreach (string typeName in ServiceTypes)
                BuildUtils.AddByName(root, typeName);

            Component fade = root.GetComponent<FadeService>();
            if (fade != null)
                BuildUtils.Set(fade, "fadeMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{BuildUtils.MaterialRoot}/M_ScreenFade.mat"));

            Component time = root.GetComponent<ArcheryChallenge.World.TimeOfDayService>();
            if (time != null)
                BuildUtils.Set(time, "profile", AssetDatabase.LoadAssetAtPath<ArcheryChallenge.World.TimeOfDayProfile>($"{BuildUtils.DataRoot}/TimeOfDay/TimeOfDay_Default.asset"));
            Component audio = root.GetComponent<ArcheryChallenge.Audio.AudioService>();
            if (audio != null)
                BuildUtils.SetArray(audio, "banks", AudioBuilder.LoadBanks());

            GameBootstrap bootstrap = root.GetComponent<GameBootstrap>();
            BuildUtils.Set(bootstrap, "catalog", AssetDatabase.LoadAssetAtPath<GameCatalog>($"{BuildUtils.DataRoot}/GameCatalog.asset"));
            BuildUtils.Set(bootstrap, "worldScene", "World");
            BuildUtils.Set(bootstrap, "defaultSpawnId", "camp");

            return BuildUtils.SaveAsPrefab(root, SystemsPath);
        }

        // ---------------------------------------------------------------- Boot

        public static void SetupBootScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);

            // Nettoyage des objets temporaires de la phase P1.
            DestroyByName(scene, "TEMP_Floor");
            DestroyByName(scene, "Directional Light");
            DestroyByName(scene, "Systems");
            DestroyByName(scene, "DesktopPlayer");
            DestroyByName(scene, "XR Interaction Simulator");
            DestroyByName(scene, "UI_Global");
            DestroyByName(scene, "XRI Global Keyboard Manager");

            // Systems.
            GameObject systemsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath);
            GameObject systems = (GameObject)PrefabUtility.InstantiatePrefab(systemsPrefab, scene);
            systems.transform.SetAsFirstSibling();

            // Joueur clavier/souris (désactivé : c'est PlayerModeService qui l'active).
            GameObject desktopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildUtils.PrefabRoot}/Player/DesktopPlayer.prefab");
            GameObject desktop = (GameObject)PrefabUtility.InstantiatePrefab(desktopPrefab, scene);
            desktop.SetActive(false);

            // Interface globale (pause, réglages, HUD, bandeau) et clavier virtuel de XRI.
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildUtils.PrefabRoot + "/UI/UI_Global.prefab");
            GameObject ui = uiPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, scene) : null;
            GameObject keyboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.5.1/Spatial Keyboard/Prefabs/XRI Global Keyboard Manager.prefab");
            GameObject keyboardManager = keyboardPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(keyboardPrefab, scene) : null;

            // Simulateur XR (désactivé jusqu'à F2).
            GameObject simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath);
            GameObject simulator = null;
            if (simulatorPrefab != null)
            {
                simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab, scene);
                simulator.SetActive(false);
            }

            GameObject rig = FindRoot(scene, "XR Origin (XR Rig)");
            if (rig == null)
            {
                Debug.LogError("[BootBuilder] « XR Origin (XR Rig) » introuvable dans Boot.");
                return;
            }
            EquipXrRig(rig);
            if (keyboardManager != null)
            {
                // Le clavier virtuel suit la caméra VR (le rig est désactivé au démarrage : pas de « Camera.main » à l'Awake).
                Transform xrCamera = FindDeep(rig.transform, "Main Camera");
                foreach (MonoBehaviour mb in keyboardManager.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GlobalNonNativeKeyboard")
                        BuildUtils.Set(mb, "m_CameraTransform", xrCamera);
                }
            }
            if (ui != null)
            {
                Transform wrist = ui.transform.Find("WristHud");
                Transform leftHand = FindDeep(rig.transform, "Left Controller");
                if (wrist != null && leftHand != null)
                    BuildUtils.Set(wrist.GetComponent<ArcheryChallenge.UI.WristHud>(), "wristAnchor", leftHand);
            }

            // Câblage de PlayerModeService.
            PlayerModeService mode = systems.GetComponent<PlayerModeService>();
            BuildUtils.Set(mode, "xrRig", rig);
            BuildUtils.Set(mode, "desktopRig", desktop);
            BuildUtils.Set(mode, "simulator", simulator);
            BuildUtils.Set(mode, "startupMode", (int)StartupMode.Auto);

            // Les deux rigs démarrent désactivés : le service choisit selon le casque.
            rig.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BootBuilder] Scène Boot câblée.");
        }

        /// <summary>Ajoute au XR Origin le corps, le carquois dorsal, l'étui et le pilote d'entrées (§P3.8, §P4.3).</summary>
        private static void EquipXrRig(GameObject rig)
        {
            Transform head = FindDeep(rig.transform, "Main Camera");
            Transform left = FindDeep(rig.transform, "Left Controller");
            Transform right = FindDeep(rig.transform, "Right Controller");

            Transform old = rig.transform.Find("Body");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            GameObject body = BuildUtils.Child(rig.transform, "Body");
            BodyFollower follower = body.AddComponent<BodyFollower>();
            BuildUtils.Set(follower, "head", head);

            GameObject quiver = BuildUtils.Child(body.transform, "BackQuiver", new Vector3(0f, -0.25f, -0.20f));
            BoxCollider box = quiver.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.40f, 0.45f, 0.30f);
            BackQuiver backQuiver = quiver.AddComponent<BackQuiver>();
            BuildUtils.Set(backQuiver, "leftHand", left);
            BuildUtils.Set(backQuiver, "rightHand", right);
            BuildUtils.Set(backQuiver, "grabMargin", 0.10f);
            quiver.layer = BuildUtils.Layer("ArrowZone");

            GameObject holsterObject = BuildUtils.Child(body.transform, "BowHolster", new Vector3(-0.20f, -0.30f, -0.12f));
            BowHolster holster = holsterObject.AddComponent<BowHolster>();
            BuildUtils.Set(holster, "returnDelay", 0.5f);
            BuildUtils.Set(holster, "spawnSelectedBowOnStart", true);

            if (rig.GetComponent<ComfortSettingsApplier>() == null)
                rig.AddComponent<ComfortSettingsApplier>();

            XRArcherInput input = rig.GetComponent<XRArcherInput>();
            if (input == null)
                input = rig.AddComponent<XRArcherInput>();
            BuildUtils.Set(input, "head", head);
            BuildUtils.Set(input, "leftHand", left);
            BuildUtils.Set(input, "rightHand", right);
            BuildUtils.Set(input, "bowHolster", holster);
            BuildUtils.Set(input, "backQuiver", backQuiver);

            // Pilote du rig VR sans casque (F2) : clavier/souris simples, tête à hauteur normale, arc en main.
            if (rig.GetComponent<SimulatedRigDriver>() == null)
                rig.AddComponent<SimulatedRigDriver>();

            // Garde-fou de la locomotion : Character Controller inutilisable tant que la tête n'est pas suivie (VrBodyGuard).
            if (rig.GetComponent<VrBodyGuard>() == null)
                rig.AddComponent<VrBodyGuard>();
        }

        // ---------------------------------------------------------------- scene list

        /// <summary>Ajoute les scènes du projet à la Scene List (Boot en premier).</summary>
        public static void UpdateBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootPath, true)
            };
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { BuildUtils.SceneRoot });
            List<string> paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != BootPath && !path.Contains("/Sandbox/"))
                    paths.Add(path);
            }
            paths.Sort();
            // World juste après Boot.
            paths.Sort((a, b) => a.EndsWith("/World.unity") ? -1 : b.EndsWith("/World.unity") ? 1 : string.CompareOrdinal(a, b));
            foreach (string p in paths)
                scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void DisableAutomaticSimulator()
        {
            // §P1.7 : le simulateur n'est plus lancé automatiquement, la touche F2 le gère (PlayerModeService).
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject XRDeviceSimulatorSettings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object settings = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (settings == null)
                    continue;
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty p = so.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab");
                if (p != null)
                {
                    p.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
        }

        // ---------------------------------------------------------------- outils

        private static void DestroyByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name || root.name.StartsWith(name + " ("))
                    Object.DestroyImmediate(root);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }
    }
}
