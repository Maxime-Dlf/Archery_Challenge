// ArmorySlot : un emplacement du râtelier d'armes (un arc ou un type de flèche exposé).
//
// OÙ LE POSER : sur les objets « BowSlot_0 … BowSlot_5 » et « ArrowSlot_0 … ArrowSlot_2 » du prefab
// ArmoryRack (§P6.3), TOUS enfants directs de la racine du râtelier (jamais d'un modèle importé : les FBX du pack
// ont une échelle de 100 qui grossirait tout ce qu'ils portent).
// RÉGLAGES : « Item Type » = Bow ou Arrow ; « Item » ← le fichier de données (Bow_Apprentice…) ;
// « Display Scale » = 0,8.
// ENFANTS ATTENDUS (placés dans le prefab, donc visibles et déplaçables dans l'éditeur) :
//   « Display » : la copie du modèle 3D exposé (sans collider) ;
//   « Label »   : le texte flottant (TextMeshPro 3D) avec le composant FacePlayer pour toujours regarder le joueur.
// Si l'un d'eux manque, le composant en crée un de secours et l'indique dans la Console.
//
// Le composant ajoute tout seul un Box Collider et un XR Simple Interactable (saisie au Grip en VR).
// Au clavier, ArmoryRack détecte l'emplacement visé et la touche E appelle Activate().
// Verrouillé : l'objet exposé est grisé (matériau M_Locked) et le texte donne la condition de déblocage.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.World;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcheryChallenge.Progression
{
    /// <summary>Emplacement exposant un équipement.</summary>
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class ArmorySlot : MonoBehaviour
    {
        [SerializeField] private EquipmentKind itemType = EquipmentKind.Bow;
        [SerializeField] private ScriptableObject item;
        [SerializeField, Min(0.1f)] private float displayScale = 0.8f;

        private GameObject display;
        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private TextMeshPro label;
        private XRSimpleInteractable interactable;
        private bool built;

        public EquipmentKind ItemType => itemType;

        public ScriptableObject Item
        {
            get => item;
            set => item = value;
        }

        public float DisplayScale
        {
            get => displayScale;
            set => displayScale = value;
        }

        /// <summary>Identifiant de l'équipement exposé.</summary>
        public string ItemId
        {
            get
            {
                if (item is BowDefinition b) return b.id;
                if (item is ArrowDefinition a) return a.id;
                return "";
            }
        }

        /// <summary>Nom affiché de l'équipement.</summary>
        public string ItemName
        {
            get
            {
                if (item is BowDefinition b) return b.displayName;
                if (item is ArrowDefinition a) return a.displayName;
                return "";
            }
        }

        /// <summary>Vrai si l'équipement exposé est débloqué pour le joueur.</summary>
        public bool IsUnlocked
        {
            get
            {
                UnlockService unlocks = Services.Get<UnlockService>();
                return unlocks == null || (itemType == EquipmentKind.Bow ? unlocks.IsBowUnlocked(ItemId) : unlocks.IsArrowUnlocked(ItemId));
            }
        }

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
        }

        private void OnEnable()
        {
            if (interactable != null)
                interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelected);
        }

        private void OnSelected(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args) => Activate();

        /// <summary>Retrouve (ou, à défaut, crée) l'objet exposé et son texte (appelé par ArmoryRack).</summary>
        public void Build()
        {
            if (built)
                return;
            built = true;

            Transform existingDisplay = transform.Find("Display");
            if (existingDisplay != null)
                display = existingDisplay.gameObject;
            else
                display = CreateFallbackDisplay();

            if (display != null)
            {
                renderers = display.GetComponentsInChildren<Renderer>();
                originalMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                    originalMaterials[i] = renderers[i].sharedMaterials;
            }

            Transform existingLabel = transform.Find("Label");
            if (existingLabel != null)
                label = existingLabel.GetComponent<TextMeshPro>();
            if (label == null)
                label = CreateFallbackLabel();
        }

        // Secours : si le prefab n'a pas d'objet « Display », on copie le modèle du prefab de l'arc ou de la flèche.
        private GameObject CreateFallbackDisplay()
        {
            GameObject prefab = item is BowDefinition b ? b.prefab : item is ArrowDefinition a ? a.prefab : null;
            Transform visual = prefab != null ? prefab.transform.Find("Visual") : null;
            if (visual == null)
                return null;
            Debug.LogWarning($"[ArmorySlot] « {name} » n'a pas d'objet Display : copie de secours créée à l'exécution (relancez Build World Prefabs).", this);

            GameObject copy = Instantiate(visual.gameObject, transform);
            copy.name = "Display";
            copy.transform.localPosition = Vector3.zero;
            // L'échelle tient compte de celle du parent : un parent agrandi ne doit pas agrandir l'objet exposé.
            float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            copy.transform.localScale = visual.localScale * displayScale / parentScale;
            copy.transform.localRotation = itemType == EquipmentKind.Arrow ? Quaternion.Euler(-90f, 0f, 0f) : visual.localRotation;
            foreach (Collider c in copy.GetComponentsInChildren<Collider>())
                Destroy(c);
            return copy;
        }

        private TextMeshPro CreateFallbackLabel()
        {
            Debug.LogWarning($"[ArmorySlot] « {name} » n'a pas d'objet Label : texte de secours créé à l'exécution.", this);
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, itemType == EquipmentKind.Bow ? 0.8f : 0.6f, 0f);
            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            text.fontSize = 1.4f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.7f, 0.2f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.6f;
            text.fontSizeMax = 1.4f;
            labelObject.AddComponent<FacePlayer>();
            return text;
        }

        /// <summary>Met à jour l'aspect (verrouillé ou non, sélectionné ou non).</summary>
        public void Refresh(Material lockedMaterial)
        {
            Build();
            bool unlocked = IsUnlocked;
            EquipmentService equipment = Services.Get<EquipmentService>();
            bool selected = equipment != null && (itemType == EquipmentKind.Bow
                ? equipment.SelectedBow != null && equipment.SelectedBow.id == ItemId
                : equipment.SelectedArrow != null && equipment.SelectedArrow.id == ItemId);

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    if (unlocked || lockedMaterial == null)
                        renderers[i].sharedMaterials = originalMaterials[i];
                    else
                    {
                        Material[] locked = new Material[originalMaterials[i].Length];
                        for (int m = 0; m < locked.Length; m++) locked[m] = lockedMaterial;
                        renderers[i].sharedMaterials = locked;
                    }
                }
            }

            if (label != null)
            {
                string text = ItemName;
                if (!unlocked)
                    text += "\n<size=70%>(verrouillé)</size>";
                label.text = text;
                label.color = !unlocked ? new Color(0.7f, 0.7f, 0.7f) : selected ? new Color(1f, 0.85f, 0.3f) : Color.white;
            }
        }

        /// <summary>Équipe l'objet exposé (si débloqué), sinon explique comment l'obtenir.</summary>
        public void Activate()
        {
            EquipmentService equipment = Services.Get<EquipmentService>();
            if (equipment == null)
                return;

            bool ok = itemType == EquipmentKind.Bow ? equipment.SelectBow(ItemId) : equipment.SelectArrow(ItemId);
            if (ok)
            {
                GameEvents.RaiseToast("Équipé : " + ItemName);
                return;
            }
            GameEvents.RaiseToast("Verrouillé : " + DescribeUnlock());
        }

        /// <summary>Explique comment débloquer cet équipement (arène à réussir).</summary>
        public string DescribeUnlock()
        {
            GameCatalog catalog = Services.Get<GameCatalog>();
            if (catalog == null)
                return "progressez dans la campagne.";
            foreach (LevelDefinition level in catalog.levels)
            {
                if (level == null) continue;
                foreach (LevelReward reward in level.rewards)
                {
                    if (reward.itemId != ItemId)
                        continue;
                    string stars = reward.minStars > 0 ? $" avec {reward.minStars} étoiles" : "";
                    return $"réussissez « {level.displayName} »{stars}.";
                }
            }
            return "progressez dans la campagne.";
        }
    }
}
