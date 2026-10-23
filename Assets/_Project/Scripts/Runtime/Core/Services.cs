// Services : registre global des services du jeu (petit « annuaire »).
//
// OÙ LE POSER : n'est pas un composant. Les services (MonoBehaviour de l'objet « Systems » de la
// scène Boot) s'y inscrivent eux-mêmes dans leur Awake via Services.Register(this).
//
// POURQUOI
// Plutôt que de chercher les objets avec FindObjectOfType (lent, fragile), chaque service se
// déclare une fois ; les autres scripts le retrouvent ensuite avec Services.Get<T>(). C'est
// l'équivalent allégé d'un « singleton » par service, mais sans variable statique dans chaque classe.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Registre statique des services de jeu, indexés par leur type.</summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();

        /// <summary>Inscrit (ou remplace) le service du type <typeparamref name="T"/>.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                return;
            Map[typeof(T)] = service;
        }

        /// <summary>Retire le service s'il est bien celui qui est inscrit (évite de retirer un remplaçant).</summary>
        public static void Unregister<T>(T service) where T : class
        {
            if (Map.TryGetValue(typeof(T), out object current) && ReferenceEquals(current, service))
                Map.Remove(typeof(T));
        }

        /// <summary>Renvoie le service du type demandé, ou null s'il n'existe pas (encore).</summary>
        public static T Get<T>() where T : class
        {
            if (Map.TryGetValue(typeof(T), out object service))
            {
                // Un MonoBehaviour détruit n'est pas « null » pour le compilateur : on le teste via Unity.
                if (service is UnityEngine.Object unityObject && unityObject == null)
                {
                    Map.Remove(typeof(T));
                    return null;
                }
                return (T)service;
            }
            return null;
        }

        /// <summary>Essaie de récupérer un service ; renvoie faux s'il est absent.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        /// <summary>Vide le registre (tests, relance du jeu dans l'éditeur).</summary>
        public static void Clear() => Map.Clear();

        // Sans rechargement de domaine entre deux Play (option de l'éditeur), le registre garderait
        // des références à des objets détruits : on le vide au tout début de chaque lancement.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLaunch() => Map.Clear();
    }
}
