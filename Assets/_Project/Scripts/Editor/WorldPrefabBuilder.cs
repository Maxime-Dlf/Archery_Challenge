// WorldPrefabBuilder : prefabs du monde (torche, râtelier, barrière, bannière de vent, modèle d'arène, monstres).
//
// Menu : Tools → Archery Challenge → Build World Prefabs.
// Reproduit §P6.3 (râtelier), §P7.5–P7.7 (cible de départ, bannière de vent, modèle d'arène), §P8-B.5
// (barrière), §P9.5 (torche) et §P10.4–10.5 (monstres).

using ArcheryChallenge.Archery;
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
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des prefabs du monde.</summary>
    public static class WorldPrefabBuilder
    {
        private const string MegaKit = "Assets/Fantasy_Megakit/Exports/FBX/";
        private const string Folder = BuildUtils.PrefabRoot + "/World";

        [MenuItem("Tools/Archery Challenge/Build World Prefabs")]
        public static void BuildAll()
        {
            BuildUtils.EnsureFolder(Folder);
            VfxBuilder.BuildAll();
            BuildTorch();
            BuildArmoryRack();
            BuildBannerPosts();
            BuildGate();
            BuildWindIndicator();
            BuildArenaTemplate();
            MonsterAnimatorBuilder.BuildAll();
            BuildMonsters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WorldPrefabBuilder] Prefabs du monde générés.");
        }

        public static GameObject Fbx(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(MegaKit + name + ".fbx");
        public static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        /// <summary>Instancie un modèle (FBX ou prefab) comme enfant, avec un collider boîte facultatif.</summary>
        public static GameObject Place(GameObject model, Transform parent, string name, Vector3 position, Vector3 euler, float scale = 1f, bool collider = false)
        {
            if (model == null)
                return null;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.name = name;
            go.transform.localPosition = position;
            // Les FBX portent souvent leur propre échelle/rotation racine (unités Blender) : on la conserve.
            go.transform.localRotation = Quaternion.Euler(euler) * model.transform.localRotation;
            go.transform.localScale = model.transform.localScale * scale;
            if (collider)
                AddBoxCollider(go);
            return go;
        }

        /// <summary>Ajoute un Box Collider englobant les rendus d'un objet.</summary>
        public static void AddBoxCollider(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        }

        /// <summary>
        /// Orientation d'un panneau (Canvas ou texte 3D) pour qu'il fasse face à « viewer » : son côté lisible (−Z)
        /// regarde le spectateur, sans pencher (rotation autour de la verticale seulement).
        /// </summary>
        public static Quaternion FaceAwayFrom(Vector3 panelPosition, Vector3 viewer)
        {
            Vector3 away = panelPosition - viewer;
            away.y = 0f;
            return away.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        /// <summary>
        /// Panneau d'information sur pied : deux poteaux, une planche et, devant, l'interface (prefab d'UI).
        /// Le côté lisible regarde dans la direction −Z de « rotation » (locale au parent). Le tableau est enraciné au sol.
        /// </summary>
        /// <param name="uiScale">Échelle du panneau d'interface (1 = 1 m de large).</param>
        /// <returns>L'ancre « UiAnchor » qui porte l'interface.</returns>
        public static Transform BuildBoardStructure(Transform parent, string name, Vector3 localPosition, Quaternion rotation,
            string panelPrefabName, float uiScale, out GameObject board)
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.38f, 0.25f, 0.13f));
            board = BuildUtils.Child(parent, name, localPosition);
            board.transform.localRotation = rotation;
            // Centre de la planche : au moins à 1,7 m, plus haut pour un grand tableau (le bas reste à 0,9 m du sol).
            float plankHeight = uiScale * 0.71f;
            float centerY = Mathf.Max(1.7f, 0.9f + plankHeight * 0.5f);
            float postHeight = centerY + plankHeight * 0.5f + 0.1f;
            float halfWidth = uiScale * 1.13f * 0.5f - 0.03f;
            foreach (float x in new[] { -halfWidth, halfWidth })
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(board.transform, false);
                post.transform.localPosition = new Vector3(x, postHeight * 0.5f, 0.16f);
                post.transform.localScale = new Vector3(0.16f, postHeight, 0.16f);
                post.GetComponent<Renderer>().sharedMaterial = wood;
            }
            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Plank";
            plank.transform.SetParent(board.transform, false);
            // Planche reculée de 8 cm derrière l'interface (évite le z-fighting avec le Canvas).
            plank.transform.localPosition = new Vector3(0f, centerY, 0.12f);
            plank.transform.localScale = new Vector3(uiScale * 1.13f, plankHeight, 0.08f);
            plank.GetComponent<Renderer>().sharedMaterial = wood;

            GameObject uiAnchor = BuildUtils.Child(board.transform, "UiAnchor", new Vector3(0f, centerY, 0f));
            uiAnchor.transform.localScale = Vector3.one * uiScale;
            GameObject ui = (GameObject)PrefabUtility.InstantiatePrefab(Prefab($"{BuildUtils.PrefabRoot}/UI/{panelPrefabName}.prefab"), uiAnchor.transform);
            ui.transform.localPosition = Vector3.zero;
            ui.transform.localRotation = Quaternion.identity;
            Transform close = UiBind.Find(ui.transform, "BtnClose");
            if (close != null)
                close.gameObject.SetActive(false);
            return uiAnchor.transform;
        }

        /// <summary>
        /// Ajoute à une ancre de panneau (enfant « BoardFrame ») une planche derrière l'interface et deux poteaux jusqu'au
        /// sol, pour qu'un écran d'information ne flotte pas dans le vide.
        /// </summary>
        /// <param name="anchor">Ancre qui porte le Canvas.</param>
        /// <param name="anchorScale">Échelle uniforme de l'ancre (les dimensions sont données en mètres).</param>
        /// <param name="anchorHeight">Hauteur de l'ancre au-dessus du sol (m).</param>
        /// <summary>Position locale du panneau de briefing / résultat (à gauche de la plateforme).</summary>
        public static readonly Vector3 BriefingAnchorPosition = new Vector3(-2.2f, 1.7f, 3.4f);

        /// <summary>Position locale du HUD d'arène (à droite de la plateforme, assez près du regard pour rester dans le champ).</summary>
        public static readonly Vector3 HudAnchorPosition = new Vector3(1.9f, 1.7f, 2.8f);

        /// <summary>Position des yeux du joueur au centre de la plateforme (repère local de l'arène).</summary>
        public static readonly Vector3 PlatformEye = new Vector3(0f, 1.7f, -0.5f);

        public static void AddBoardFrame(Transform anchor, float anchorScale, float anchorHeight)
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.38f, 0.25f, 0.13f));
            GameObject frame = BuildUtils.Child(anchor, "BoardFrame");
            float inv = 1f / anchorScale;

            GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Plank";
            plank.transform.SetParent(frame.transform, false);
            // Planche reculée de 8 cm derrière l'interface : collée au Canvas, elle clignote (z-fighting) surtout en VR (profondeur 16 bits).
            plank.transform.localPosition = new Vector3(0f, 0f, 0.11f * inv);
            plank.transform.localScale = new Vector3(1.15f * inv, 0.72f * inv, 0.06f * inv);
            Object.DestroyImmediate(plank.GetComponent<Collider>());
            plank.GetComponent<Renderer>().sharedMaterial = wood;

            foreach (float x in new[] { -0.55f, 0.55f })
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(frame.transform, false);
                // Du bas de la planche jusqu'au sol (le sol est à « anchorHeight » sous l'ancre).
                float length = anchorHeight - 0.3f;
                post.transform.localPosition = new Vector3(x * inv, -(0.3f + length * 0.5f) * inv, 0.16f * inv);
                post.transform.localScale = new Vector3(0.12f * inv, length * inv, 0.12f * inv);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.GetComponent<Renderer>().sharedMaterial = wood;
            }
        }

        // ---------------------------------------------------------------- torche

        private static void BuildTorch()
        {
            GameObject root = new GameObject("Torch_Lit");
            Place(Fbx("Torch_Metal"), root.transform, "Visual", Vector3.zero, Vector3.zero, 1.2f);
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            post.transform.localScale = new Vector3(0.06f, 0.9f, 0.06f);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.GetComponent<Renderer>().sharedMaterial = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.35f, 0.23f, 0.12f));

            GameObject lightObject = BuildUtils.Child(root.transform, "Light", new Vector3(0f, 0.35f, 0.15f));
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.6f, 0.24f);
            light.shadows = LightShadows.None;
            light.enabled = false;

            GameObject flame = null;
            GameObject flamePrefab = Prefab(BuildUtils.PrefabRoot + "/VFX/VFX_TorchFlame.prefab");
            if (flamePrefab != null)
            {
                flame = (GameObject)PrefabUtility.InstantiatePrefab(flamePrefab, root.transform);
                flame.name = "Flame";
                flame.transform.localPosition = new Vector3(0f, 0.3f, 0.15f);
                flame.SetActive(false);
            }

            NightLight nightLight = root.AddComponent<NightLight>();
            BuildUtils.Set(nightLight, "lightSource", light);
            BuildUtils.Set(nightLight, "flame", flame);
            BuildUtils.Set(nightLight, "alwaysOn", false);
            BuildUtils.SaveAsPrefab(root, Folder + "/Torch_Lit.prefab");
        }

        // ---------------------------------------------------------------- râtelier

        private static void BuildArmoryRack()
        {
            GameObject root = new GameObject("ArmoryRack");
            Place(Fbx("WeaponStand"), root.transform, "Visual", Vector3.zero, Vector3.zero, 1.6f, true);
            // Les modèles du pack ont une échelle de 100 : on n'y accroche JAMAIS d'enfant (ils hériteraient de ×100).
            // Tous les emplacements sont donc des enfants directs de la racine du râtelier (échelle 1).
            Place(Fbx("Barrel_Holder"), root.transform, "ArrowBarrel", new Vector3(1.6f, 0f, 0f), Vector3.zero, 1f, true);

            float[] xs = { -0.9f, -0.55f, -0.2f, 0.15f, 0.5f, 0.85f };
            string[] bows = { "Bow_Apprentice", "Bow_Hunter", "Bow_Warden", "Bow_Sylvan", "Bow_Thorn", "Bow_Royal" };
            for (int i = 0; i < 6; i++)
            {
                GameObject slot = BuildUtils.Child(root.transform, "BowSlot_" + i, new Vector3(xs[i], 1.1f, 0.1f));
                BoxCollider box = slot.AddComponent<BoxCollider>();
                box.size = new Vector3(0.3f, 1.2f, 0.2f);
                ArmorySlot component = slot.AddComponent<ArmorySlot>();
                BowDefinition definition = AssetDatabase.LoadAssetAtPath<BowDefinition>($"{BuildUtils.DataRoot}/Bows/{bows[i]}.asset");
                BuildUtils.Set(component, "itemType", (int)EquipmentKind.Bow);
                BuildUtils.Set(component, "item", definition);
                BuildUtils.Set(component, "displayScale", 0.8f);
                slot.AddComponent<XRSimpleInteractable>();
                BakeSlotContent(slot, definition != null ? definition.prefab : null, EquipmentKind.Bow, 0.8f, definition != null ? definition.displayName : "",
                    new Vector3(0f, 0.72f + (i % 3) * 0.17f, 0f));
            }

            string[] arrows = { "Arrow_Wood", "Arrow_Ash", "Arrow_Carbon" };
            float[] ax = { -0.12f, 0f, 0.12f };
            for (int i = 0; i < 3; i++)
            {
                GameObject slot = BuildUtils.Child(root.transform, "ArrowSlot_" + i, new Vector3(1.6f + ax[i], 0.6f, 0f));
                BoxCollider box = slot.AddComponent<BoxCollider>();
                box.size = new Vector3(0.12f, 0.7f, 0.12f);
                box.center = new Vector3(0f, 0.3f, 0f);
                ArmorySlot component = slot.AddComponent<ArmorySlot>();
                ArrowDefinition definition = AssetDatabase.LoadAssetAtPath<ArrowDefinition>($"{BuildUtils.DataRoot}/Arrows/{arrows[i]}.asset");
                BuildUtils.Set(component, "itemType", (int)EquipmentKind.Arrow);
                BuildUtils.Set(component, "item", definition);
                BuildUtils.Set(component, "displayScale", 0.8f);
                slot.AddComponent<XRSimpleInteractable>();
                BakeSlotContent(slot, definition != null ? definition.prefab : null, EquipmentKind.Arrow, 0.8f, definition != null ? definition.displayName : "",
                    new Vector3(0f, 0.95f + i * 0.17f, 0f));
            }

            ArmoryRack rack = root.AddComponent<ArmoryRack>();
            BuildUtils.Set(rack, "lockedMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{BuildUtils.MaterialRoot}/M_Locked.mat"));
            BuildUtils.SaveAsPrefab(root, Folder + "/ArmoryRack.prefab");
        }

        // Pose dans le prefab l'objet exposé (« Display ») et son texte (« Label ») : ils sont ainsi visibles et
        // déplaçables dans l'éditeur, au lieu d'être créés par le code au lancement du jeu.
        private static void BakeSlotContent(GameObject slot, GameObject itemPrefab, EquipmentKind kind, float displayScale, string text, Vector3 labelPosition)
        {
            Transform visual = itemPrefab != null ? itemPrefab.transform.Find("Visual") : null;
            if (visual != null)
            {
                GameObject display = Object.Instantiate(visual.gameObject);
                display.name = "Display";
                display.transform.SetParent(slot.transform, false);
                display.transform.localPosition = Vector3.zero;
                display.transform.localScale = visual.localScale * displayScale;
                // Une flèche exposée se dresse à la verticale (pointe en haut) pour tenir dans le tonneau.
                display.transform.localRotation = kind == EquipmentKind.Arrow ? Quaternion.Euler(-90f, 0f, 0f) : visual.localRotation;
                foreach (Collider c in display.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
            }

            GameObject label = BuildUtils.Child(slot.transform, "Label", labelPosition);
            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 1.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(0.7f, 0.16f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 1.4f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            label.AddComponent<FacePlayer>();
        }

        // ---------------------------------------------------------------- drapeaux

        /// <summary>Matériau de tissu visible des deux côtés (copie de MI_Banner, « Render Face : Both »).</summary>
        public static Material DoubleSidedBannerMaterial()
        {
            const string path = BuildUtils.MaterialRoot + "/M_Banner_DoubleSided.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            GameObject banner = Fbx("Banner_1");
            Material source = null;
            foreach (Renderer r in banner.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.name == "MI_Banner")
                        source = m;
            if (source == null)
                return BuildUtils.EnsureMaterial("M_Banner_DoubleSided", "Universal Render Pipeline/Lit", new Color(0.7f, 0.15f, 0.12f));
            BuildUtils.EnsureFolder(BuildUtils.MaterialRoot);
            Material copy = new Material(source) { name = "M_Banner_DoubleSided" };
            if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 0f);          // 0 = Both (URP Lit)
            copy.SetFloat("_CullMode", 0f);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>Remplace le tissu (MI_Banner) d'un objet par le matériau double face.</summary>
        public static void MakeBannersDoubleSided(GameObject instance)
        {
            Material doubleSided = DoubleSidedBannerMaterial();
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name == "MI_Banner")
                    {
                        mats[i] = doubleSided;
                        changed = true;
                    }
                }
                if (changed)
                    r.sharedMaterials = mats;
            }
        }

        // Deux drapeaux sur poteau : le pied du poteau touche le sol (origine du prefab = au sol) et le tissu se voit des deux côtés.
        private static void BuildBannerPosts()
        {
            // Hauteur sous l'origine du modèle (voir le pack) : le pied du poteau est à −1,55 m (modèle 1) ou −1,24 m (modèle 2).
            (string name, float footDepth)[] models = { ("Banner_1", 1.55f), ("Banner_2", 1.24f) };
            foreach (var model in models)
            {
                GameObject root = new GameObject("Flag_" + model.name);
                GameObject visual = Place(Fbx(model.name), root.transform, "Visual", new Vector3(0f, model.footDepth, 0f), Vector3.zero, 1f);
                MakeBannersDoubleSided(visual);
                // Le support du pack est un bras de potence fait pour se fixer à un mur : on lui ajoute un vrai poteau
                // qui descend jusqu'au sol (sans lui, le bras et son crochet flottent dans le vide).
                float poleHeight = model.footDepth + 0.8f;
                Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.35f, 0.23f, 0.12f));
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(root.transform, false);
                pole.transform.localPosition = new Vector3(-0.06f, poleHeight * 0.5f, 0f);
                pole.transform.localScale = new Vector3(0.13f, poleHeight * 0.5f, 0.13f);
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.GetComponent<Renderer>().sharedMaterial = wood;
                // Socle : le poteau est planté dans une base de bois qui cache la jonction avec le sol.
                GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                foot.name = "Foot";
                foot.transform.SetParent(root.transform, false);
                foot.transform.localPosition = new Vector3(-0.06f, 0.08f, 0f);
                foot.transform.localScale = new Vector3(0.45f, 0.08f, 0.45f);
                Object.DestroyImmediate(foot.GetComponent<Collider>());
                foot.GetComponent<Renderer>().sharedMaterial = wood;
                BuildUtils.SaveAsPrefab(root, $"{Folder}/Flag_{model.name}.prefab");
            }

            // Drapeau suspendu (sans poteau) pour les poutres : tissu seul, double face.
            foreach (string cloth in new[] { "Banner_1_Cloth", "Banner_2_Cloth" })
            {
                GameObject root = new GameObject("Hanging_" + cloth);
                GameObject visual = Place(Fbx(cloth), root.transform, "Visual", Vector3.zero, Vector3.zero, 1f);
                MakeBannersDoubleSided(visual);
                BuildUtils.SaveAsPrefab(root, $"{Folder}/Hanging_{cloth}.prefab");
            }
        }

        // ---------------------------------------------------------------- barrière

        private static void BuildGate()
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.35f, 0.23f, 0.12f));
            GameObject root = new GameObject("ProgressionGate");
            foreach (float x in new[] { -2f, 2f })
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = x < 0 ? "PostLeft" : "PostRight";
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(x, 1f, 0f);
                post.transform.localScale = new Vector3(0.2f, 2f, 0.2f);
                post.GetComponent<Renderer>().sharedMaterial = wood;
            }

            GameObject hinge = BuildUtils.Child(root.transform, "Hinge", new Vector3(-1.9f, 0f, 0f));
            GameObject door = BuildUtils.Child(hinge.transform, "Door", new Vector3(1.9f, 0f, 0f));
            BoxCollider doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.center = new Vector3(0f, 0.75f, 0f);
            doorCollider.size = new Vector3(3.7f, 1.5f, 0.25f);
            for (int i = 0; i < 3; i++)
            {
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "Plank" + i;
                plank.transform.SetParent(door.transform, false);
                plank.transform.localPosition = new Vector3(0f, 0.3f + i * 0.45f, 0f);
                plank.transform.localScale = new Vector3(3.7f, 0.14f, 0.08f);
                Object.DestroyImmediate(plank.GetComponent<Collider>());
                plank.GetComponent<Renderer>().sharedMaterial = wood;
            }
            foreach (float x in new[] { -1.7f, 0f, 1.7f })
            {
                GameObject slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = "Slat";
                slat.transform.SetParent(door.transform, false);
                slat.transform.localPosition = new Vector3(x, 0.75f, 0f);
                slat.transform.localScale = new Vector3(0.12f, 1.4f, 0.1f);
                Object.DestroyImmediate(slat.GetComponent<Collider>());
                slat.GetComponent<Renderer>().sharedMaterial = wood;
            }

            GameObject sign = new GameObject("Sign");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0f, 2.3f, -0.15f);
            TextMeshPro text = sign.AddComponent<TextMeshPro>();
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(4f, 1f);
            text.color = new Color(1f, 0.9f, 0.6f);
            text.text = "Réussissez …";

            ProgressionGate gate = root.AddComponent<ProgressionGate>();
            BuildUtils.Set(gate, "hinge", hinge.transform);
            BuildUtils.Set(gate, "openAngle", 100f);
            BuildUtils.Set(gate, "openDuration", 1.5f);
            BuildUtils.Set(gate, "signText", text);
            BuildUtils.SaveAsPrefab(root, Folder + "/ProgressionGate.prefab");
        }

        // ---------------------------------------------------------------- bannière de vent

        private static void BuildWindIndicator()
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.35f, 0.23f, 0.12f));
            GameObject root = new GameObject("WindIndicator");
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 2f, 0.08f);
            pole.GetComponent<Renderer>().sharedMaterial = wood;

            GameObject pivot = BuildUtils.Child(root.transform, "Pivot", new Vector3(0f, 3.7f, 0f));
            // La bannière du pack est verticale : on la couche pour qu'elle parte du mât vers +Z.
            GameObject cloth = Place(Fbx("Banner_1_Cloth"), pivot.transform, "Cloth", new Vector3(0f, 0f, 1.15f), new Vector3(90f, 0f, 0f), 1f);
            MakeBannersDoubleSided(cloth);

            WindIndicator indicator = root.AddComponent<WindIndicator>();
            BuildUtils.Set(indicator, "pivot", pivot.transform);
            BuildUtils.Set(indicator, "flutterAmplitude", 12f);
            BuildUtils.Set(indicator, "flutterSpeed", 3f);
            BuildUtils.SaveAsPrefab(root, Folder + "/WindIndicator.prefab");
        }

        // ---------------------------------------------------------------- modèle d'arène

        private static void BuildArenaTemplate()
        {
            Material wood = BuildUtils.EnsureMaterial("M_Wood", "Universal Render Pipeline/Lit", new Color(0.4f, 0.27f, 0.14f));
            Material white = BuildUtils.EnsureMaterial("M_White", "Universal Render Pipeline/Unlit", Color.white);

            GameObject root = new GameObject("ArenaRoot_Template");
            root.tag = "ArenaRoot";

            // Plateforme de tir (cube aplati : on n'y met jamais d'enfant, voir tutoriel).
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            platform.transform.localScale = new Vector3(6f, 0.3f, 4f);
            platform.layer = BuildUtils.Layer("Teleport");
            platform.GetComponent<Renderer>().sharedMaterial = wood;
            platform.AddComponent<SurfaceTag>().Surface = SurfaceType.Wood;

            // Point d'arrivée de la téléportation (simple repère) ; c'est l'estrade entière qui sert d'ancre au laser.
            // Voir ArenaTeleportSurface : une ancre en collider « trigger » est invisible pour le laser de téléportation.
            BuildUtils.Child(root.transform, "TeleportAnchor", new Vector3(0f, 0.3f, -0.5f));
            ArenaTeleportSurface.Ensure(root.transform);

            GameObject bounds = BuildUtils.Child(root.transform, "Bounds");
            BoxCollider boundsBox = bounds.AddComponent<BoxCollider>();
            boundsBox.size = new Vector3(6f, 3f, 4f);
            boundsBox.center = new Vector3(0f, 1.5f, 0f);
            bounds.layer = BuildUtils.Layer("PlatformBounds");
            PlatformBounds platformBounds = bounds.AddComponent<PlatformBounds>();

            GameObject spawn = BuildUtils.Child(root.transform, "PlayerSpawn", new Vector3(0f, 0.3f, -1f));
            spawn.tag = "SpawnPoint";
            spawn.AddComponent<PlayerSpawnPoint>();

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "ShootingLine";
            line.transform.SetParent(root.transform, false);
            line.transform.localPosition = new Vector3(0f, 0.31f, 1.8f);
            line.transform.localScale = new Vector3(6f, 0.01f, 0.06f);
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.GetComponent<Renderer>().sharedMaterial = white;

            GameObject startTarget = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(BuildUtils.PrefabRoot + "/Targets/StartTarget.prefab"), root.transform);
            startTarget.name = "StartTarget";
            startTarget.transform.localPosition = new Vector3(2.5f, 0.3f, 4f);
            startTarget.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject targets = BuildUtils.Child(root.transform, "Targets");
            GameObject wind = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(Folder + "/WindIndicator.prefab"), root.transform);
            wind.name = "WindIndicator";
            wind.transform.localPosition = new Vector3(-4f, 0f, 3f);

            // Panneaux d'information : de part et d'autre de la plateforme, ORIENTÉS VERS LE JOUEUR (debout au centre,
            // la tête à 1,7 m). Un Canvas se lit depuis son côté −Z : son axe +Z doit donc s'éloigner du joueur.
            Vector3 eye = PlatformEye;
            GameObject panels = BuildUtils.Child(root.transform, "Panels");
            // Chaque panneau repose sur un cadre en bois (planche et deux poteaux plantés au sol) : plus d'écran qui flotte.
            // Positions choisies pour que chaque panneau tienne ENTIER dans le champ du joueur (±40° autour de son regard).
            GameObject briefingAnchor = BuildUtils.Child(panels.transform, "BriefingAnchor", BriefingAnchorPosition);
            briefingAnchor.transform.rotation = FaceAwayFrom(briefingAnchor.transform.position, eye);
            briefingAnchor.transform.localScale = Vector3.one * 1.5f;
            AddBoardFrame(briefingAnchor.transform, 1.5f, 1.7f);
            GameObject hudAnchor = BuildUtils.Child(panels.transform, "HudAnchor", HudAnchorPosition);
            hudAnchor.transform.rotation = FaceAwayFrom(hudAnchor.transform.position, eye);
            hudAnchor.transform.localScale = Vector3.one * 1.5f;
            AddBoardFrame(hudAnchor.transform, 1.5f, 1.7f);

            foreach (string panel in new[] { "BriefingPanel", "LevelResultPanel" })
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefab($"{BuildUtils.PrefabRoot}/UI/{panel}.prefab"), briefingAnchor.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }
            GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(Prefab($"{BuildUtils.PrefabRoot}/UI/HudPanel.prefab"), hudAnchor.transform);
            hud.transform.localPosition = Vector3.zero;
            hud.transform.localRotation = Quaternion.identity;

            BuildUtils.Child(root.transform, "Lights");
            BuildUtils.Child(root.transform, "Decor");

            LevelController level = root.AddComponent<LevelController>();
            BuildUtils.Set(level, "platformBounds", platformBounds);
            BuildUtils.Set(level, "startTarget", startTarget.GetComponent<StartTarget>());
            BuildUtils.Set(level, "targetsRoot", targets.transform);
            BuildUtils.Set(level, "windIndicator", wind.GetComponent<WindIndicator>());
            BuildUtils.Set(level, "briefingAnchor", briefingAnchor.transform);
            BuildUtils.Set(level, "hudAnchor", hudAnchor.transform);
            BuildUtils.SaveAsPrefab(root, Folder + "/ArenaRoot_Template.prefab");
        }

        // ---------------------------------------------------------------- monstres

        private static void BuildMonsters()
        {
            BuildMonster("Monster_Slime", "Assets/RPG Monster DUO PBR Polyart/Prefabs/PolyartDefault/SlimePolyart.prefab", "Monster_Slime",
                0.8f, 0.4f, 0.9f, 1f, 0.4f, new Vector3(0f, 0.45f, 0f), 0.45f, 0.9f, 2, 1.5f, 0.4f);
            BuildMonster("Monster_Turtle", "Assets/RPG Monster DUO PBR Polyart/Prefabs/PolyartDefault/TurtleShellPolyart.prefab", "Monster_Turtle",
                1f, 0.6f, 1f, 0.8f, 0.6f, new Vector3(0f, 0.5f, 0f), 0.5f, 1.4f, 2, 1.5f, 0.6f);
        }

        private static void BuildMonster(string name, string sourcePrefab, string definitionName, float scale, float agentRadius, float agentHeight,
            float speed, float stopDistance, Vector3 colliderCenter, float colliderRadius, float colliderHeight, int colliderDirection, float stoppingDistance, float radius)
        {
            GameObject source = Prefab(sourcePrefab);
            if (source == null)
            {
                Debug.LogWarning($"[WorldPrefabBuilder] Prefab de monstre introuvable : {sourcePrefab}");
                return;
            }
            GameObject root = new GameObject(name);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * scale;

            // Contrôleur d'animation propre, sans transitions automatiques.
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/_Project/Art/Animations/{name}.controller");
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = radius;
            agent.height = agentHeight;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = colliderCenter;
            capsule.radius = colliderRadius;
            capsule.height = colliderHeight;
            capsule.direction = colliderDirection == 2 ? 2 : 1;
            if (name.Contains("Turtle"))
                capsule.direction = 2;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            MonsterDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{BuildUtils.DataRoot}/Monsters/{definitionName}.asset");
            MonsterHealth health = root.AddComponent<MonsterHealth>();
            BuildUtils.Set(health, "definition", definition);
            MonsterBrain brain = root.AddComponent<MonsterBrain>();
            BuildUtils.Set(brain, "animator", animator);
            BuildUtils.Set(brain, "definition", definition);
            root.AddComponent<SurfaceTag>().Surface = SurfaceType.Flesh;

            int layer = BuildUtils.Layer("Monster");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
            BuildUtils.SaveAsPrefab(root, $"{BuildUtils.PrefabRoot}/Monsters/{name}.prefab");
        }
    }
}
