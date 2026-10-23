// FastTravelDestination : destination de téléportation rapide (le hub ou l'arrivée d'une arène).
//
// OÙ LE POSER : sur l'objet « Arrival » placé 6 m derrière chaque plateforme (§P8-B.6) ; sa rotation Y
// est celle que le joueur aura en arrivant (face aux cibles).
// RÉGLAGES : « Destination Id » (hub, training, arena_01 …), « Display Name » (affiché dans les
// menus), « Required Level » (vide = toujours disponible). Une destination n'apparaît que si son
// arène est DÉBLOQUÉE (même non réussie).

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Point d'arrivée d'une téléportation rapide.</summary>
    [DisallowMultipleComponent]
    public class FastTravelDestination : MonoBehaviour
    {
        [SerializeField] private string destinationId = "hub";
        [SerializeField] private string displayName = "Village des archers";
        [SerializeField] private LevelDefinition requiredLevel;

        private static readonly List<FastTravelDestination> Registry = new List<FastTravelDestination>();

        public static IReadOnlyList<FastTravelDestination> All => Registry;

        public string DestinationId
        {
            get => destinationId;
            set => destinationId = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public LevelDefinition RequiredLevel
        {
            get => requiredLevel;
            set => requiredLevel = value;
        }

        /// <summary>Position et orientation d'arrivée (rotation limitée au lacet).</summary>
        public Pose ArrivalPose => new Pose(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        /// <summary>Vrai si le joueur peut s'y rendre (arène débloquée).</summary>
        public bool IsAvailable
        {
            get
            {
                if (requiredLevel == null)
                    return true;
                UnlockService unlocks = Services.Get<UnlockService>();
                return unlocks == null || unlocks.IsLevelUnlocked(requiredLevel);
            }
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this))
                Registry.Add(this);
        }

        private void OnDisable() => Registry.Remove(this);

        /// <summary>Cherche une destination par identifiant.</summary>
        public static FastTravelDestination Find(string id)
        {
            for (int i = 0; i < Registry.Count; i++)
                if (Registry[i] != null && Registry[i].destinationId == id)
                    return Registry[i];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Registry.Clear();

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "Voyage : " + destinationId);
        }
#endif
    }
}
