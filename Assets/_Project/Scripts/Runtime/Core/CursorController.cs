// CursorController : verrouillage du curseur de la souris (mode clavier/souris).
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// En jeu, le curseur est verrouillé au centre de l'écran (on regarde avec la souris). Dès qu'un menu
// s'ouvre, il doit être libéré pour cliquer. Plutôt que de laisser chaque menu écrire dans
// Cursor.lockState (et se marcher dessus), chaque menu « demande » un curseur libre avec
// RequestFree(this) et le rend avec Release(this). Le curseur est libre tant qu'au moins une demande
// est en cours. Maintenir Alt libère aussi temporairement le curseur (géré par DesktopPlayerController).

using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Compteur de demandes de curseur libre.</summary>
    public static class CursorController
    {
        private static readonly HashSet<object> FreeRequests = new HashSet<object>();

        /// <summary>Vrai si au moins un menu demande le curseur libre.</summary>
        public static bool IsFreeRequested => FreeRequests.Count > 0;

        /// <summary>Un menu demande à pouvoir cliquer.</summary>
        public static void RequestFree(object owner)
        {
            if (owner != null)
                FreeRequests.Add(owner);
        }

        /// <summary>Le menu n'a plus besoin du curseur.</summary>
        public static void Release(object owner)
        {
            if (owner != null)
                FreeRequests.Remove(owner);
        }

        /// <summary>Applique l'état voulu au curseur système.</summary>
        /// <param name="wantLocked">Vrai pour verrouiller (jeu), faux pour libérer.</param>
        public static void Apply(bool wantLocked)
        {
            CursorLockMode mode = wantLocked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode)
                Cursor.lockState = mode;
            if (Cursor.visible == wantLocked)
                Cursor.visible = !wantLocked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLaunch() => FreeRequests.Clear();
    }
}
