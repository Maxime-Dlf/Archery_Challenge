// ArenaTeleportSurface : rend l'estrade (plateforme de tir) d'une arène téléportable au laser.
//
// OÙ L'UTILISER : n'est pas un composant (classe statique). Appelée par LevelController.Awake sur la racine de chaque arène, et par le
// générateur du modèle d'arène (WorldPrefabBuilder).
// OBJETS ATTENDUS SOUS LA RACINE D'ARÈNE : « Platform » (cube aplati avec son collider PLEIN), « TeleportAnchor » (point d'arrivée :
// là où le joueur se retrouve, tourné vers les cibles) ; à défaut de « TeleportAnchor », « PlayerSpawn » sert de point d'arrivée.
//
// POURQUOI : le laser de téléportation ignore les colliders « trigger ». L'ancre d'origine (une petite boîte trigger de 1 m) était donc
// invisible pour le laser, et l'estrade elle-même, sur le calque « Teleport », n'était pas une zone de téléportation : le laser restait
// rouge sur toute l'estrade et il était impossible de s'y téléporter. Désormais l'ESTRADE ENTIÈRE est l'ancre : viser n'importe quel point de
// l'estrade téléporte le joueur au point d'arrivée de l'arène, face aux cibles. L'opération est idempotente (sans effet si déjà faite),
// donc les scènes d'arène déjà générées sont corrigées au chargement sans être modifiées.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ArcheryChallenge.Levels
{
    /// <summary>Estrade d'arène téléportable.</summary>
    public static class ArenaTeleportSurface
    {
        /// <summary>
        /// Fait de l'objet « Platform » une ancre de téléportation vers « TeleportAnchor ». Renvoie l'ancre créée ou déjà présente
        /// (null si la racine n'a pas d'estrade ou de point d'arrivée).
        /// </summary>
        public static TeleportationAnchor Ensure(Transform arenaRoot)
        {
            if (arenaRoot == null)
                return null;
            Transform platform = arenaRoot.Find("Platform");
            Transform arrival = arenaRoot.Find("TeleportAnchor");
            if (arrival == null)
                arrival = arenaRoot.Find("PlayerSpawn");
            if (platform == null || arrival == null)
                return null;

            // L'ancienne ancre (petit trigger) ne sert plus : elle est retirée, le point d'arrivée reste.
            TeleportationAnchor oldAnchor = arrival.GetComponent<TeleportationAnchor>();
            if (oldAnchor != null)
            {
                Collider oldCollider = arrival.GetComponent<Collider>();
                Remove(oldAnchor);
                if (oldCollider != null && oldCollider.isTrigger)
                    Remove(oldCollider);
            }

            TeleportationAnchor anchor = platform.GetComponent<TeleportationAnchor>();
            if (anchor == null)
                anchor = platform.gameObject.AddComponent<TeleportationAnchor>();
            anchor.teleportAnchorTransform = arrival;
            anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
            anchor.interactionLayers = InteractionLayerMask.GetMask("Teleport");
            return anchor;
        }

        private static void Remove(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
