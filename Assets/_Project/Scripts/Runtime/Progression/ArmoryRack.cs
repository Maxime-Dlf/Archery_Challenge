// ArmoryRack : râtelier d'armes du hub (6 arcs et 3 types de flèches exposés).
//
// OÙ LE POSER : sur la racine du prefab ArmoryRack (§P6.3). Il retrouve seul ses emplacements.
// RÉGLAGE : « Locked Material » ← M_Locked (gris).
//
// - VR : on saisit (Grip) l'objet exposé pour l'équiper (XR Simple Interactable de chaque emplacement).
// - Clavier : on regarde l'emplacement (à moins de 4 m) et on appuie sur E.
// - Les emplacements non débloqués sont grisés ; ils prennent leurs couleurs au déblocage.

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using UnityEngine;

namespace ArcheryChallenge.Progression
{
    /// <summary>Présentoir d'équipement.</summary>
    [DisallowMultipleComponent]
    public class ArmoryRack : MonoBehaviour
    {
        [SerializeField] private Material lockedMaterial;
        [SerializeField] private List<ArmorySlot> slots = new List<ArmorySlot>();
        [SerializeField, Min(1f)] private float interactDistance = 4f;

        private static readonly List<ArmoryRack> Registry = new List<ArmoryRack>();

        public IReadOnlyList<ArmorySlot> Slots => slots;

        private void Awake()
        {
            if (slots.Count == 0)
                slots.AddRange(GetComponentsInChildren<ArmorySlot>(true));
        }

        private void OnEnable()
        {
            Registry.Add(this);
            GameEvents.EquipmentChanged += RefreshAll;
            GameEvents.ProgressionChanged += RefreshAll;
            RefreshAll();
        }

        private void OnDisable()
        {
            Registry.Remove(this);
            GameEvents.EquipmentChanged -= RefreshAll;
            GameEvents.ProgressionChanged -= RefreshAll;
        }

        /// <summary>Emplacement visé par le regard de la tête donnée (à moins de la distance d'interaction), ou null.</summary>
        public ArmorySlot FindLookedSlot(Transform head)
        {
            if (head == null)
                return null;
            // Le socle du râtelier a lui aussi un collider qui entoure les emplacements : un simple Raycast tomberait
            // dessus avant d'atteindre un emplacement. On examine donc tous les impacts, du plus proche au plus loin.
            RaycastHit[] hits = Physics.RaycastAll(head.position, head.forward, interactDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                ArmorySlot slot = hit.collider.GetComponentInParent<ArmorySlot>();
                if (slot != null && slots.Contains(slot))
                    return slot;
            }
            return null;
        }

        /// <summary>Aide affichée à l'écran quand on regarde un emplacement (« E : équiper … »), ou chaîne vide.</summary>
        public static string FindHint(Transform head)
        {
            foreach (ArmoryRack rack in Registry)
            {
                ArmorySlot slot = rack != null ? rack.FindLookedSlot(head) : null;
                if (slot == null)
                    continue;
                return slot.IsUnlocked
                    ? $"E : équiper « {slot.ItemName} »"
                    : $"« {slot.ItemName} » verrouillé : {slot.DescribeUnlock()}";
            }
            return "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry.Clear();

        /// <summary>Remet à jour tous les emplacements.</summary>
        public void RefreshAll()
        {
            foreach (ArmorySlot slot in slots)
                if (slot != null)
                    slot.Refresh(lockedMaterial);
        }

        private void Update()
        {
            IArcherInput input = Services.Get<IArcherInput>();
            if (input == null || !input.InteractPressedThisFrame)
                return;
            // Rayon depuis la caméra : on cherche l'emplacement visé.
            ArmorySlot slot = FindLookedSlot(input.Head);
            if (slot != null)
                slot.Activate();
        }
    }
}
