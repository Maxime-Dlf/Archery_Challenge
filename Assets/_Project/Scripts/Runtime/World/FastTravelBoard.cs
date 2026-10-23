// FastTravelBoard : panneau du hub qui liste les destinations de téléportation rapide.
//
// OÙ LE POSER : sur la racine de « Hub/FastTravelBoard ». Le panneau d'interface (FastTravelPanel)
// est placé sur cet objet. Bouton « Collect Destinations » dans l'Inspector : retrouve toutes les
// destinations de la scène World.

using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Liste des destinations affichées par un panneau de téléportation rapide.</summary>
    [DisallowMultipleComponent]
    public class FastTravelBoard : MonoBehaviour
    {
        [SerializeField] private List<FastTravelDestination> destinations = new List<FastTravelDestination>();

        public IReadOnlyList<FastTravelDestination> Destinations => destinations;

        /// <summary>Remplit la liste avec toutes les destinations de la scène.</summary>
        public void CollectDestinations()
        {
            destinations.Clear();
            destinations.AddRange(FindObjectsByType<FastTravelDestination>(FindObjectsInactive.Include));
            destinations.Sort((a, b) => string.CompareOrdinal(a.DestinationId, b.DestinationId));
        }
    }
}
