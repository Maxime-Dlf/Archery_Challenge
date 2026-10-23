// EquipmentService : arc et flèches actuellement choisis.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Le choix est enregistré dans l'emplacement de sauvegarde. Le joueur peut revenir à tout moment
// à un équipement plus faible. En entraînement (ou en mode démo), tout l'équipement est utilisable
// même s'il n'est pas débloqué : voir <see cref="IgnoreLocks"/>.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Progression
{
    /// <summary>Service de l'équipement équipé.</summary>
    [DisallowMultipleComponent]
    public class EquipmentService : MonoBehaviour
    {
        /// <summary>Quand vrai (entraînement), tout l'équipement peut être choisi.</summary>
        public bool IgnoreLocks { get; set; }

        private GameCatalog Catalog => Services.Get<GameCatalog>();
        private SaveSlotService Slots => Services.Get<SaveSlotService>();
        private UnlockService Unlocks => Services.Get<UnlockService>();

        /// <summary>Arc équipé (jamais null si le catalogue est chargé).</summary>
        public BowDefinition SelectedBow
        {
            get
            {
                GameCatalog catalog = Catalog;
                if (catalog == null) return null;
                BowDefinition bow = catalog.GetBow(Slots != null ? Slots.Current.selectedBow : null);
                return bow != null ? bow : catalog.GetBow(catalog.defaultBowId);
            }
        }

        /// <summary>Flèche équipée.</summary>
        public ArrowDefinition SelectedArrow
        {
            get
            {
                GameCatalog catalog = Catalog;
                if (catalog == null) return null;
                ArrowDefinition arrow = catalog.GetArrow(Slots != null ? Slots.Current.selectedArrow : null);
                return arrow != null ? arrow : catalog.GetArrow(catalog.defaultArrowId);
            }
        }

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Vrai si le joueur peut équiper cet arc maintenant.</summary>
        public bool CanUseBow(BowDefinition bow) =>
            bow != null && (IgnoreLocks || Unlocks == null || Unlocks.IsBowUnlocked(bow.id));

        /// <summary>Vrai si le joueur peut équiper cette flèche maintenant.</summary>
        public bool CanUseArrow(ArrowDefinition arrow) =>
            arrow != null && (IgnoreLocks || Unlocks == null || Unlocks.IsArrowUnlocked(arrow.id));

        /// <summary>Équipe un arc. Renvoie faux s'il est verrouillé ou inconnu.</summary>
        public bool SelectBow(string id)
        {
            BowDefinition bow = Catalog != null ? Catalog.GetBow(id) : null;
            if (!CanUseBow(bow) || Slots == null)
                return false;
            if (Slots.Current.selectedBow == id)
                return true;
            Slots.Current.selectedBow = id;
            Slots.Save();
            GameEvents.RaiseEquipmentChanged();
            return true;
        }

        /// <summary>Équipe un type de flèche. Renvoie faux s'il est verrouillé ou inconnu.</summary>
        public bool SelectArrow(string id)
        {
            ArrowDefinition arrow = Catalog != null ? Catalog.GetArrow(id) : null;
            if (!CanUseArrow(arrow) || Slots == null)
                return false;
            if (Slots.Current.selectedArrow == id)
                return true;
            Slots.Current.selectedArrow = id;
            Slots.Save();
            GameEvents.RaiseEquipmentChanged();
            return true;
        }

        /// <summary>Passe à l'arc suivant (ou précédent) parmi ceux utilisables, en boucle.</summary>
        public void CycleBow(int direction)
        {
            GameCatalog catalog = Catalog;
            if (catalog == null || catalog.bows.Length == 0) return;
            int start = System.Array.FindIndex(catalog.bows, b => b != null && b.id == SelectedBow?.id);
            for (int step = 1; step <= catalog.bows.Length; step++)
            {
                int index = ((start + direction * step) % catalog.bows.Length + catalog.bows.Length) % catalog.bows.Length;
                if (CanUseBow(catalog.bows[index]))
                {
                    SelectBow(catalog.bows[index].id);
                    return;
                }
            }
        }

        /// <summary>Passe à la flèche suivante (ou précédente) parmi celles utilisables, en boucle.</summary>
        public void CycleArrow(int direction)
        {
            GameCatalog catalog = Catalog;
            if (catalog == null || catalog.arrows.Length == 0) return;
            int start = System.Array.FindIndex(catalog.arrows, a => a != null && a.id == SelectedArrow?.id);
            for (int step = 1; step <= catalog.arrows.Length; step++)
            {
                int index = ((start + direction * step) % catalog.arrows.Length + catalog.arrows.Length) % catalog.arrows.Length;
                if (CanUseArrow(catalog.arrows[index]))
                {
                    SelectArrow(catalog.arrows[index].id);
                    return;
                }
            }
        }
    }
}
