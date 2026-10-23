// IArcherInput : ce que le jeu demande à « celui qui pilote le tireur », quel que soit le mode.
//
// OÙ LE POSER : n'est pas un composant. Implémentée par XRArcherInput (casque VR / simulateur) et
// DesktopArcherInput (clavier + souris). Le pilote actif s'enregistre dans le registre de services :
// Services.Get<IArcherInput>().
//
// PRINCIPE « UNE LOGIQUE, DEUX PILOTES » : l'arc (Bow), les arènes, les menus ne savent jamais si
// le joueur est en VR ou au clavier. Ils lisent cette interface pour les actions communes.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Input
{
    /// <summary>Entrées communes aux deux modes de jeu.</summary>
    public interface IArcherInput
    {
        /// <summary>Vrai quand ce pilote est celui du joueur actuel.</summary>
        bool IsActive { get; }

        /// <summary>Mode de jeu de ce pilote.</summary>
        PlayerMode Mode { get; }

        /// <summary>Caméra du joueur (tête en VR, caméra à l'écran au PC).</summary>
        Transform Head { get; }

        /// <summary>Action « interagir » : touche E au PC, rien en VR (les interactions passent par les mains).</summary>
        bool InteractPressedThisFrame { get; }

        /// <summary>Menu / pause : Échap au PC, bouton Menu de la manette gauche en VR.</summary>
        bool MenuPressedThisFrame { get; }

        /// <summary>Téléportation rapide : T au PC, bouton Y en VR.</summary>
        bool FastTravelPressedThisFrame { get; }
    }
}
