// PrefabBuilder : assemble les prefabs de jeu (arcs, flèches, cibles, joueur clavier/souris).
//
// Menu : Tools → Archery Challenge → Build Prefabs.
// Reproduit par code les étapes du tutoriel §P3.5 à §P5.5 et §P4.4 : hiérarchies, positions des
// repères, composants et valeurs. Idempotent : relancer recrée les prefabs à l'identique.
// Prérequis : Tools → Archery Challenge → Build Data (les fichiers de données doivent exister).

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Input;
using ArcheryChallenge.Player;
using ArcheryChallenge.Targets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des prefabs de gameplay.</summary>
    public static class PrefabBuilder
    {
        private const string BowModels = "Assets/URP GanzSe Free Weapons Pack/Prefabs/GREAT BOWS/";
        private const string ArrowModels = "Assets/URP GanzSe Free Weapons Pack/Prefabs/ARROWS/";
        private const string TargetModel = "Assets/Glowing Rifts/Archery Target/ArcheryTarget.prefab";

        [MenuItem("Tools/Archery Challenge/Build Prefabs")]
        public static void BuildAll()
        {
            DataBuilder.BuildAll();
            EnsureMaterials();
            BuildArrows();
            BuildBows();
            BuildTargets();
            BuildDesktopPlayer();
            DataBuilder.BuildCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PrefabBuilder] Prefabs de gameplay générés.");
        }

        // ---------------------------------------------------------------- matériaux

        public static void EnsureMaterials()
        {
            BuildUtils.EnsureMaterial("M_BowString", "Universal Render Pipeline/Unlit", new Color(0.91f, 0.875f, 0.78f));
            BuildUtils.EnsureMaterial("M_Trajectory", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 1f, 1f, 0.4f), true);
            BuildUtils.EnsureMaterial("M_ArrowTrail", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 1f, 1f, 0.47f), true);
            Material penalty = BuildUtils.EnsureMaterial("M_Penalty", "Universal Render Pipeline/Lit", new Color(0.75f, 0.22f, 0.17f));
            if (penalty.HasProperty("_EmissionColor"))
            {
                penalty.EnableKeyword("_EMISSION");
                penalty.SetColor("_EmissionColor", new Color(0.35f, 0.055f, 0.055f));
            }
            BuildUtils.EnsureMaterial("M_Locked", "Universal Render Pipeline/Lit", new Color(0.33f, 0.33f, 0.33f));
            BuildUtils.EnsureMaterial("M_Particle", "Universal Render Pipeline/Particles/Unlit", Color.white, true);
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{BuildUtils.MaterialRoot}/{name}.mat");

        // ---------------------------------------------------------------- flèches

        private static void BuildArrows()
        {
            BuildArrow("Arrow_Wood", "FREE Arrow Medium Color 1");
            BuildArrow("Arrow_Ash", "FREE Arrow Medium Color 2");
            BuildArrow("Arrow_Carbon", "FREE Arrow Medium Color 3");
        }

        private static void BuildArrow(string name, string model)
        {
            GameObject root = new GameObject(name);
            root.layer = BuildUtils.Layer("Arrow");

            // Modèle : encoche à l'origine, pointe vers +Z, taille 74 cm.
            GameObject visual = InstantiateModel(ArrowModels + model + ".prefab", root.transform, "Visual");
            visual.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.5f;
            StripColliders(visual);

            GameObject nock = BuildUtils.Child(root.transform, "Nock");
            GameObject tip = BuildUtils.Child(root.transform, "Tip", new Vector3(0f, 0f, 0.74f));
            GameObject grip = BuildUtils.Child(root.transform, "GripAttach", new Vector3(0f, 0f, 0.08f));
            GameObject trailObject = BuildUtils.Child(root.transform, "Trail", new Vector3(0f, 0f, 0.05f));

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.01f), new Keyframe(1f, 0f));
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = Mat("M_ArrowTrail");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.03f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, 0.37f);
            capsule.radius = 0.012f;
            capsule.height = 0.74f;

            Arrow arrow = root.AddComponent<Arrow>();
            ArrowDefinition definition = AssetDatabase.LoadAssetAtPath<ArrowDefinition>($"{BuildUtils.DataRoot}/Arrows/{name}.asset");
            BuildUtils.Set(arrow, "definition", definition);
            BuildUtils.Set(arrow, "tip", tip.transform);
            BuildUtils.Set(arrow, "nock", nock.transform);
            BuildUtils.Set(arrow, "gripAttach", grip.transform);
            BuildUtils.Set(arrow, "trail", trail);
            BuildUtils.Set(arrow, "lifeTimeAfterImpact", 20f);

            SetLayerRecursive(root, BuildUtils.Layer("Arrow"));
            GameObject prefab = BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Archery/{name}.prefab");
            definition.prefab = prefab;
            EditorUtility.SetDirty(definition);
        }

        // ---------------------------------------------------------------- arcs

        private struct BowSpec
        {
            public string Name, Model;
            public float Scale;
            public Vector3 Top, Bottom, Rest, ArrowRest;
        }

        private static void BuildBows()
        {
            BowSpec[] specs =
            {
                new BowSpec { Name = "Bow_Apprentice", Model = "FREE GREAT BOW 2 COLOR 2", Scale = 0.55f, Top = new Vector3(0, 0.71f, -0.22f), Bottom = new Vector3(0, -0.71f, -0.22f), Rest = new Vector3(0, 0, -0.22f), ArrowRest = new Vector3(-0.02f, 0.03f, 0) },
                new BowSpec { Name = "Bow_Hunter", Model = "FREE GREAT BOW 2 COLOR 3", Scale = 0.55f, Top = new Vector3(0, 0.71f, -0.22f), Bottom = new Vector3(0, -0.71f, -0.22f), Rest = new Vector3(0, 0, -0.22f), ArrowRest = new Vector3(-0.02f, 0.03f, 0) },
                new BowSpec { Name = "Bow_Warden", Model = "FREE GREAT BOW 1 COLOR 2", Scale = 0.65f, Top = new Vector3(0, 0.70f, -0.17f), Bottom = new Vector3(0, -0.70f, -0.17f), Rest = new Vector3(0, 0, -0.17f), ArrowRest = new Vector3(-0.05f, 0.04f, 0) },
                new BowSpec { Name = "Bow_Sylvan", Model = "FREE GREAT BOW 1 COLOR 1", Scale = 0.65f, Top = new Vector3(0, 0.70f, -0.17f), Bottom = new Vector3(0, -0.70f, -0.17f), Rest = new Vector3(0, 0, -0.17f), ArrowRest = new Vector3(-0.05f, 0.04f, 0) },
                new BowSpec { Name = "Bow_Thorn", Model = "FREE GREAT BOW 3 COLOR 2", Scale = 0.65f, Top = new Vector3(0, 0.77f, -0.18f), Bottom = new Vector3(0, -0.67f, -0.24f), Rest = new Vector3(0, 0.05f, -0.21f), ArrowRest = new Vector3(-0.08f, 0.05f, 0) },
                new BowSpec { Name = "Bow_Royal", Model = "FREE GREAT BOW 3 COLOR 1", Scale = 0.65f, Top = new Vector3(0, 0.77f, -0.18f), Bottom = new Vector3(0, -0.67f, -0.24f), Rest = new Vector3(0, 0.05f, -0.21f), ArrowRest = new Vector3(-0.08f, 0.05f, 0) },
            };
            foreach (BowSpec spec in specs)
                BuildBow(spec);
        }

        private static void BuildBow(BowSpec spec)
        {
            GameObject root = new GameObject(spec.Name);

            GameObject visual = InstantiateModel(BowModels + spec.Model + ".prefab", root.transform, "Visual");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(270f, 270f, 0f);
            visual.transform.localScale = Vector3.one * spec.Scale;
            StripColliders(visual);

            GameObject grip = BuildUtils.Child(root.transform, "GripAttach");
            GameObject arrowRest = BuildUtils.Child(root.transform, "ArrowRest", spec.ArrowRest);
            GameObject top = BuildUtils.Child(root.transform, "StringTop", spec.Top);
            GameObject bottom = BuildUtils.Child(root.transform, "StringBottom", spec.Bottom);
            GameObject rest = BuildUtils.Child(root.transform, "StringRest", spec.Rest);
            GameObject nockPoint = BuildUtils.Child(rest.transform, "NockPoint");

            // Corde dessinée par le script (3 points).
            GameObject stringObject = BuildUtils.Child(root.transform, "String");
            LineRenderer stringLine = stringObject.AddComponent<LineRenderer>();
            stringLine.positionCount = 3;
            stringLine.widthMultiplier = 0.004f;
            stringLine.useWorldSpace = true;
            stringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stringLine.receiveShadows = false;
            stringLine.sharedMaterial = Mat("M_BowString");
            BowStringRenderer stringRenderer = stringObject.AddComponent<BowStringRenderer>();
            BuildUtils.Set(stringRenderer, "top", top.transform);
            BuildUtils.Set(stringRenderer, "bottom", bottom.transform);
            BuildUtils.Set(stringRenderer, "nock", nockPoint.transform);
            BowString bowString = stringObject.AddComponent<BowString>();
            BuildUtils.Set(bowString, "grabRadius", 0.15f);

            // Zone d'encochage permissive.
            GameObject nockZoneObject = BuildUtils.Child(root.transform, "NockZone", spec.Rest);
            SphereCollider zone = nockZoneObject.AddComponent<SphereCollider>();
            zone.isTrigger = true;
            zone.radius = 0.25f;
            NockZone nockZone = nockZoneObject.AddComponent<NockZone>();

            // Aperçu de trajectoire.
            GameObject trajectory = BuildUtils.Child(root.transform, "Trajectory");
            LineRenderer trajLine = trajectory.AddComponent<LineRenderer>();
            trajLine.widthMultiplier = 0.01f;
            trajLine.useWorldSpace = true;
            trajLine.positionCount = 0;
            trajLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajLine.receiveShadows = false;
            trajLine.sharedMaterial = Mat("M_Trajectory");
            TrajectoryPreview preview = trajectory.AddComponent<TrajectoryPreview>();

            GameObject desktopAnchor = BuildUtils.Child(root.transform, "DesktopViewAnchor");

            // Physique et saisie XR.
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = Vector3.zero;
            capsule.radius = 0.035f;
            capsule.height = 0.30f;

            XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.attachTransform = grip.transform;
            grab.useDynamicAttach = false;
            grab.throwOnDetach = false;
            grab.selectMode = InteractableSelectMode.Single;

            Bow bow = root.AddComponent<Bow>();
            BowDefinition definition = AssetDatabase.LoadAssetAtPath<BowDefinition>($"{BuildUtils.DataRoot}/Bows/{spec.Name}.asset");
            BuildUtils.Set(bow, "definition", definition);
            BuildUtils.Set(bow, "stringRest", rest.transform);
            BuildUtils.Set(bow, "nockPoint", nockPoint.transform);
            BuildUtils.Set(bow, "arrowRest", arrowRest.transform);
            BuildUtils.Set(bow, "grabInteractable", grab);
            BuildUtils.Set(bow, "desktopViewAnchor", desktopAnchor.transform);
            BuildUtils.Set(nockZone, "bow", bow);
            BuildUtils.Set(preview, "bow", bow);
            BuildUtils.Set(preview, "maxPoints", 40);
            BuildUtils.Set(preview, "timeStep", 0.05f);

            SetLayerRecursive(root, BuildUtils.Layer("Bow"));
            nockZoneObject.layer = BuildUtils.Layer("ArrowZone");

            GameObject prefab = BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Archery/{spec.Name}.prefab");
            definition.prefab = prefab;
            EditorUtility.SetDirty(definition);
        }

        // ---------------------------------------------------------------- cibles

        private static void BuildTargets()
        {
            TargetDefinition standard = AssetDatabase.LoadAssetAtPath<TargetDefinition>($"{BuildUtils.DataRoot}/Targets/Target_Standard.asset");
            TargetDefinition penalty = AssetDatabase.LoadAssetAtPath<TargetDefinition>($"{BuildUtils.DataRoot}/Targets/Target_Penalty.asset");

            // --- Target_Static ---
            GameObject root = new GameObject("Target_Static");
            GameObject visual = InstantiateModel(TargetModel, root.transform, "Visual");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            GameObject face = BuildUtils.Child(root.transform, "Face", new Vector3(0.07f, 1.05f, 0.10f), Quaternion.Euler(-18f, 0f, 0f));
            TargetFace targetFace = face.AddComponent<TargetFace>();
            targetFace.Definition = standard;
            EditorUtility.SetDirty(targetFace);

            Target target = root.AddComponent<Target>();
            BuildUtils.Set(target, "face", targetFace);
            BuildUtils.Set(target, "hitsToBreak", 0);

            // Matières : socle en bois, blason en paille.
            Transform stand = FindDeep(visual.transform, "Stand");
            Transform blason = FindDeep(visual.transform, "Target");
            if (stand != null) AddSurface(stand.gameObject, ArcheryChallenge.Core.SurfaceType.Wood);
            if (blason != null) AddSurface(blason.gameObject, ArcheryChallenge.Core.SurfaceType.Straw);
            SetLayerRecursive(root, BuildUtils.Layer("Target"));
            string staticPath = $"{BuildUtils.PrefabRoot}/Targets/Target_Static.prefab";
            BuildUtils.EnsureFolder($"{BuildUtils.PrefabRoot}/Targets");
            GameObject staticPrefab = PrefabUtility.SaveAsPrefabAsset(root, staticPath);
            Object.DestroyImmediate(root);

            // --- Target_Moving (variante) ---
            GameObject moving = (GameObject)PrefabUtility.InstantiatePrefab(staticPrefab);
            moving.name = "Target_Moving";
            TargetMover mover = moving.AddComponent<TargetMover>();
            BuildUtils.Set(mover, "pattern", (int)TargetPattern.Lateral);
            BuildUtils.Set(mover, "amplitude", 3f);
            BuildUtils.Set(mover, "baseSpeed", 1.5f);
            BuildUtils.Set(mover, "pauseAtEnds", 0.3f);
            BuildUtils.Set(mover, "phaseOffset", 0f);
            PrefabUtility.SaveAsPrefabAsset(moving, $"{BuildUtils.PrefabRoot}/Targets/Target_Moving.prefab");
            Object.DestroyImmediate(moving);

            // --- Target_PopUp (variante) ---
            GameObject popup = (GameObject)PrefabUtility.InstantiatePrefab(staticPrefab);
            popup.name = "Target_PopUp";
            TargetMover popMover = popup.AddComponent<TargetMover>();
            BuildUtils.Set(popMover, "pattern", (int)TargetPattern.PopUp);
            BuildUtils.Set(popMover, "upTime", 2.5f);
            BuildUtils.Set(popMover, "downTime", 1.5f);
            BuildUtils.Set(popMover, "foldAxis", (int)FoldAxis.X);
            BuildUtils.Set(popup.GetComponent<Target>(), "hitsToBreak", 1);
            PrefabUtility.SaveAsPrefabAsset(popup, $"{BuildUtils.PrefabRoot}/Targets/Target_PopUp.prefab");
            Object.DestroyImmediate(popup);

            BuildPenaltyTarget(penalty);
            BuildStartTarget(standard);
        }

        private static void BuildPenaltyTarget(TargetDefinition penaltyDefinition)
        {
            GameObject root = new GameObject("Target_Penalty");
            GameObject shield = InstantiateModel("Assets/Fantasy_Megakit/Exports/FBX/Shield_Wooden.fbx", root.transform, "Visual");
            shield.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            shield.transform.localScale = shield.transform.localScale * 1.8f; // on garde l'échelle d'import du FBX
            foreach (Renderer r in shield.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = Mat("M_Penalty");

            // Collider précis sur le bouclier (non convexe) : les flèches se plantent sur la face visible.
            foreach (MeshFilter filter in shield.GetComponentsInChildren<MeshFilter>())
            {
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                AddSurface(filter.gameObject, ArcheryChallenge.Core.SurfaceType.Wood);
            }

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.6f, -0.08f);
            post.transform.localScale = new Vector3(0.06f, 0.6f, 0.06f);
            AddSurface(post, ArcheryChallenge.Core.SurfaceType.Wood);

            PenaltyTarget penalty = root.AddComponent<PenaltyTarget>();
            BuildUtils.Set(penalty, "penalty", 15);
            BuildUtils.Set(penalty, "definition", penaltyDefinition);

            SetLayerRecursive(root, BuildUtils.Layer("Target"));
            BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Targets/Target_Penalty.prefab");
        }

        private static void BuildStartTarget(TargetDefinition standard)
        {
            GameObject root = new GameObject("StartTarget");
            GameObject visual = InstantiateModel(TargetModel, root.transform, "Visual");
            visual.transform.localScale = Vector3.one * 0.45f;
            Transform blason = FindDeep(visual.transform, "Target");
            if (blason != null) AddSurface(blason.gameObject, ArcheryChallenge.Core.SurfaceType.Straw);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            text.text = "DÉPART";
            text.fontSize = 2.4f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.878f, 0.69f, 0.29f);
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            // Le texte se tourne toujours vers le joueur, où qu'il se trouve.
            label.AddComponent<ArcheryChallenge.World.FacePlayer>();

            root.AddComponent<StartTarget>();
            SetLayerRecursive(root, BuildUtils.Layer("Target"));
            BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Targets/StartTarget.prefab");
        }

        // ---------------------------------------------------------------- joueur clavier/souris

        private static void BuildDesktopPlayer()
        {
            GameObject root = new GameObject("DesktopPlayer");
            root.layer = BuildUtils.Layer("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;

            GameObject pivot = BuildUtils.Child(root.transform, "CameraPivot", new Vector3(0f, 1.7f, 0f));
            GameObject camObject = BuildUtils.Child(pivot.transform, "DesktopCamera");
            camObject.tag = "MainCamera";
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 250f;
            camObject.AddComponent<AudioListener>();

            GameObject holder = BuildUtils.Child(camObject.transform, "BowHolder", new Vector3(-0.30f, -0.32f, 0.78f), Quaternion.Euler(0f, 0f, -10f));

            DesktopPlayerController move = root.AddComponent<DesktopPlayerController>();
            BuildUtils.Set(move, "walkSpeed", 3f);
            BuildUtils.Set(move, "runSpeed", 6f);
            BuildUtils.Set(move, "jumpHeight", 1f);
            BuildUtils.Set(move, "gravity", -15f);
            BuildUtils.Set(move, "mouseSensitivity", 0.12f);
            BuildUtils.Set(move, "cameraPivot", pivot.transform);

            root.AddComponent<DesktopArcherInput>();

            DesktopBowView view = root.AddComponent<DesktopBowView>();
            BuildUtils.Set(view, "holder", holder.transform);
            BuildUtils.Set(view, "viewCamera", cam);
            BuildUtils.Set(view, "drawBackDistance", 0.30f);
            BuildUtils.Set(view, "aimFov", 55f);

            BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Player/DesktopPlayer.prefab");
        }

        // ---------------------------------------------------------------- outils

        private static GameObject InstantiateModel(string path, Transform parent, string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"[PrefabBuilder] Modèle introuvable : {path}");
                GameObject placeholder = new GameObject(name);
                placeholder.transform.SetParent(parent, false);
                return placeholder;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            return instance;
        }

        private static void StripColliders(GameObject go)
        {
            // Les modèles décoratifs (arcs, flèches) ne doivent pas avoir leurs propres colliders.
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void AddSurface(GameObject go, ArcheryChallenge.Core.SurfaceType surface)
        {
            SurfaceTag tag = go.GetComponent<SurfaceTag>();
            if (tag == null)
                tag = go.AddComponent<SurfaceTag>();
            tag.Surface = surface;
        }

        /// <summary>Cherche un enfant (profondeur quelconque) par son nom.</summary>
        public static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }
    }
}
