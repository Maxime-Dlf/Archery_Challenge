// PanelOpener : touche E (clavier/souris) près d'un tableau pour ouvrir le panneau global correspondant.
//
// OÙ LE POSER : sur la racine d'un tableau du hub ou du camp (MenuBoard, LeaderboardBoard, FastTravelBoard,
// panneau du feu de camp, tableau des commandes). RÉGLAGE : « Kind » = le panneau à ouvrir ; « Distance » = 4 m.
//
// En VR on touche directement les boutons du tableau (rayon ou doigt). Au clavier/souris, les tableaux
// sont des décors lisibles : on les regarde et on appuie sur E pour ouvrir la même interface sous forme
// d'écran flottant. Quand on regarde un tableau, l'écran affiche « E : … » (voir DesktopHud).

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>Panneau global ouvrable depuis un tableau.</summary>
    public enum PanelKind
    {
        Profile,
        MainMenu,
        Leaderboard,
        FastTravel,
        Controls
    }

    /// <summary>Ouvre un panneau global quand on appuie sur E en le regardant.</summary>
    [DisallowMultipleComponent]
    public class PanelOpener : MonoBehaviour
    {
        private static readonly List<PanelOpener> Registry = new List<PanelOpener>();

        [SerializeField] private PanelKind kind = PanelKind.MainMenu;
        [SerializeField, Min(1f)] private float distance = 4f;
        [SerializeField, Range(5f, 80f)] private float maxAngle = 40f;
        [SerializeField] private Transform lookTarget;

        public PanelKind Kind
        {
            get => kind;
            set => kind = value;
        }

        /// <summary>Phrase d'aide affichée quand on regarde ce tableau (« E : ouvrir le menu »).</summary>
        public string Hint
        {
            get
            {
                switch (kind)
                {
                    case PanelKind.Profile: return "E : choisir son profil et sa sauvegarde";
                    case PanelKind.Leaderboard: return "E : consulter le classement";
                    case PanelKind.FastTravel: return "E : voyager rapidement";
                    case PanelKind.Controls: return "E : afficher toutes les commandes";
                    default: return "E : ouvrir le menu du jeu";
                }
            }
        }

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        /// <summary>Vrai si le joueur (tête donnée) regarde ce tableau d'assez près pour l'utiliser.</summary>
        public bool IsLookedAt(Transform head)
        {
            if (head == null)
                return false;
            Transform target = lookTarget != null ? lookTarget : transform;
            Vector3 toTarget = target.position + Vector3.up * 1.4f - head.position;
            return toTarget.magnitude <= distance && Vector3.Angle(head.forward, toTarget) <= maxAngle;
        }

        /// <summary>Aide du tableau regardé par le joueur, ou chaîne vide.</summary>
        public static string FindHint(Transform head)
        {
            foreach (PanelOpener opener in Registry)
                if (opener != null && opener.IsLookedAt(head))
                    return opener.Hint;
            return "";
        }

        private void Update()
        {
            IArcherInput input = Services.Get<IArcherInput>();
            if (input == null || !input.InteractPressedThisFrame || input.Head == null)
                return;
            if (!IsLookedAt(input.Head))
                return;

            UiHub hub = Services.Get<UiHub>();
            if (hub == null)
                return;
            switch (kind)
            {
                case PanelKind.Profile: hub.OpenProfileMenu(); break;
                case PanelKind.Leaderboard: hub.OpenLeaderboard(); break;
                case PanelKind.FastTravel: hub.OpenFastTravel(); break;
                case PanelKind.Controls: hub.OpenControls(); break;
                default: hub.OpenMainMenu(); break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry.Clear();
    }
}
