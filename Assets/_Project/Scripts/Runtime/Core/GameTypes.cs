// GameTypes : énumérations et petites structures partagées par tout le jeu.
//
// Ce fichier ne contient aucun comportement : seulement du vocabulaire commun (difficultés,
// états d'une manche, résultat d'un tir…) pour que tous les scripts parlent le même langage.

using System;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Les trois niveaux de difficulté choisis dans le menu.</summary>
    public enum GameDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>Façon dont le joueur est actuellement piloté.</summary>
    public enum PlayerMode
    {
        /// <summary>Clavier et souris (rig « DesktopPlayer »).</summary>
        Desktop,
        /// <summary>Casque VR réel (rig « XR Origin »).</summary>
        VR,
        /// <summary>Rig VR piloté par le XR Interaction Simulator (touche F2).</summary>
        Simulator
    }

    /// <summary>Main du joueur : sert au choix de la main d'arc et du carquois.</summary>
    public enum Handedness
    {
        Left,
        Right
    }

    /// <summary>États d'une manche (voir §P7.1 du tutoriel).</summary>
    public enum LevelState
    {
        Locked,
        Available,
        Briefing,
        Countdown,
        Running,
        Finished
    }

    /// <summary>Matière d'un collider : décide du son et de l'effet à l'impact.</summary>
    public enum SurfaceType
    {
        Dirt,
        Wood,
        Straw,
        Stone,
        Flesh,
        Metal
    }

    /// <summary>Type d'objet d'équipement (râtelier, déblocages).</summary>
    public enum EquipmentKind
    {
        Bow,
        Arrow
    }

    /// <summary>Résultat d'un impact de flèche sur une cible, calculé par <c>Target</c>.</summary>
    public struct ShotResult
    {
        /// <summary>Points bruts de la flèche (0 à 10 ; −pénalité pour un bouclier rouge).</summary>
        public int Points;
        /// <summary>Anneau touché (10 = centre … 1 = bord, 0 = raté / pied).</summary>
        public int Ring;
        /// <summary>Vrai si la flèche est dans le petit cercle central « X ».</summary>
        public bool IsX;
        /// <summary>Vrai pour une cible pénalisante.</summary>
        public bool IsPenalty;
        /// <summary>Vrai si la cible touchée était en mouvement (bonus × 1,5).</summary>
        public bool TargetWasMoving;
        /// <summary>Distance de tir en mètres (tireur → cible).</summary>
        public float Distance;
        /// <summary>Point d'impact dans le monde.</summary>
        public Vector3 WorldPoint;
        /// <summary>Cible touchée (peut être null).</summary>
        public Component Target;
    }

    /// <summary>Informations sur un impact de flèche (n'importe quelle surface).</summary>
    public struct ArrowImpactInfo
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Collider Collider;
        public SurfaceType Surface;
        public float Speed;
        public bool HitTarget;
        public ShotResult Shot;
    }

    /// <summary>Sérialisable : un couple identifiant → entier (JsonUtility ne gère pas les dictionnaires).</summary>
    [Serializable]
    public struct IdInt
    {
        public string id;
        public int value;
    }
}
