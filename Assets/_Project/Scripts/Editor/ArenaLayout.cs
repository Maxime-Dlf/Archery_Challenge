// ArenaLayout : données de placement des arènes et des zones du monde (§P8-A.1 et §P8-C du tutoriel).
//
// Ce fichier est la « source de vérité » pour les générateurs : coordonnées du camp, du hub et du stand
// d'entraînement ; position, orientation et cibles de chaque arène. Positions des cibles en LOCAL de la
// racine d'arène (X = gauche/droite vu depuis la plateforme, Y = hauteur, Z = distance devant).

using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Une cible d'une arène.</summary>
    public struct TargetSpec
    {
        public string Prefab;       // Target_Static, Target_Moving, Target_PopUp, Target_Penalty
        public Vector3 Position;    // locale à la racine de l'arène
        public string Pattern;      // Lateral, Vertical, Orbit (cibles mobiles)
        public float Amplitude;
        public float Speed;
        public float Phase;
        public float Radius;
        public float ExtraPitch;    // inclinaison supplémentaire (blason tourné vers le haut)
        public int Wave;            // 0 = pas de vague ; 1 à 3 = vague

        public TargetSpec(string prefab, float x, float y, float z, string pattern = null, float amplitude = 0f, float speed = 0f, float phase = 0f, float radius = 0f, float pitch = 0f, int wave = 0)
        {
            Prefab = prefab; Position = new Vector3(x, y, z); Pattern = pattern; Amplitude = amplitude; Speed = speed; Phase = phase; Radius = radius; ExtraPitch = pitch; Wave = wave;
        }
    }

    /// <summary>Description complète d'une arène.</summary>
    public class ArenaSpec
    {
        public string SceneName;
        public string LevelFile;
        public string SpawnId;
        public string Display;
        public Vector3 Position;
        public float RotationY;
        public bool IsTraining;
        public Rect WorldRect;      // zone de jeu (X min, Z min, largeur, profondeur) pour les exclusions
        public float ArrivalDistance = 6f;  // distance (m) entre le point d'arrivée de la téléportation rapide et la plateforme
        public Vector3 PortalLocalOffset;   // position du point de détection (ArenaPortal) par rapport à la plateforme, en local (0 = sur le stand)
        public float LoadRadius = 40f;      // rayon d'apparition de l'arène autour du point de détection (m)
        public float UnloadRadius = 60f;    // rayon de disparition (plus grand : évite les chargements en boucle)
        public List<TargetSpec> Targets = new List<TargetSpec>();

        public Vector3 LocalToWorld(Vector3 local) => Position + Quaternion.Euler(0f, RotationY, 0f) * local;

        /// <summary>Position monde du point de détection d'apparition de l'arène (ArenaPortal).</summary>
        public Vector3 PortalPosition => LocalToWorld(PortalLocalOffset);
    }

    /// <summary>Tableau de toutes les arènes et zones.</summary>
    public static class ArenaLayout
    {
        // Rectangles (X min, Z min, largeur, profondeur) des zones.
        public static readonly Rect Camp = new Rect(15, 15, 20, 20);
        public static readonly Rect Hub = new Rect(50, 15, 40, 30);
        public static readonly Rect Training = new Rect(95, 15, 50, 25);
        public static readonly Rect ForestNW = new Rect(15, 130, 43, 18);
        public static readonly Rect Plateau = new Rect(60, 114, 88, 34);
        public static readonly Rect Ravine = new Rect(122, 56, 16, 50);
        public static readonly Rect Separator = new Rect(102, 64, 5, 36);

        // Chemin principal (X, Z) : voir §P8-A.1.
        public static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(25, 25), new Vector2(40, 28), new Vector2(50, 30), new Vector2(70, 30) },
            new[] { new Vector2(70, 30), new Vector2(70, 45), new Vector2(64, 55), new Vector2(52, 67) },
            new[] { new Vector2(52, 70), new Vector2(58, 85), new Vector2(60, 109) },
            new[] { new Vector2(60, 112), new Vector2(66, 100), new Vector2(66, 84) },
            // Du pont (79, 60) vers le stand du Ravin : le chemin CONTOURNE par le sud la colline séparatrice (x 99–110, z ≥ 61)
            // au lieu de la gravir, puis remonte vers la plateforme le long de son flanc est (hors de la pente).
            new[] { new Vector2(66, 74), new Vector2(70, 62), new Vector2(79, 60), new Vector2(98, 60), new Vector2(103, 57.5f), new Vector2(111, 57.5f), new Vector2(112.5f, 66), new Vector2(111, 77) },
            // Du Ravin au Tournoi : le chemin longe le flanc est de la grande colline du Ravin puis la contourne par le nord (z ≈ 106,7),
            // traverse la rivière sur le second pont (z = 106,74, posé dans la scène World), et grimpe la petite butte (10 → 13 m) avant
            // l'arrivée de l'arène 5 (60, 131). La butte (marche de 3 m entre z = 109 et 113) est gravie EN DIAGONALE sur 10 m pour que la pente
            // reste douce (≈ 17° au lieu de 35°). Il reste à l'ouest des gradins du Tournoi (qui commencent à x = 71, z 116–120).
            new[] { new Vector2(110, 84), new Vector2(112, 100), new Vector2(110, 105), new Vector2(102, 106.74f), new Vector2(90, 106.74f), new Vector2(79.16f, 106.74f), new Vector2(74, 107), new Vector2(71, 108), new Vector2(62, 113), new Vector2(60.5f, 117), new Vector2(60, 123), new Vector2(60, 130) },
            new[] { new Vector2(70, 30), new Vector2(90, 27), new Vector2(97, 27) }
        };

        public static readonly ArenaSpec[] Arenas = BuildArenas();

        private static ArenaSpec[] BuildArenas()
        {
            ArenaSpec training = new ArenaSpec
            {
                SceneName = "Arena_Training", LevelFile = "Level_Training", SpawnId = "training", Display = "Stand d'entraînement",
                Position = new Vector3(100, 10, 27), RotationY = 90, IsTraining = true, WorldRect = Training
            };
            training.Targets.Add(new TargetSpec("Target_Static", -4, 0, 5));
            training.Targets.Add(new TargetSpec("Target_Static", -1.5f, 0, 10));
            training.Targets.Add(new TargetSpec("Target_Static", 1.5f, 0, 20));
            training.Targets.Add(new TargetSpec("Target_Static", 4, 0, 30));
            training.Targets.Add(new TargetSpec("Target_Moving", 0, 0, 15, "Lateral", 2f, 1.5f));

            ArenaSpec a1 = new ArenaSpec
            {
                SceneName = "Arena_01", LevelFile = "Level_01", SpawnId = "arena_01", Display = "La Clairière",
                Position = new Vector3(46, 10, 67), RotationY = 270, WorldRect = new Rect(15, 52, 35, 30)
            };
            a1.Targets.Add(new TargetSpec("Target_Static", -2, 0, 10));
            a1.Targets.Add(new TargetSpec("Target_Static", 0, 0, 15));
            a1.Targets.Add(new TargetSpec("Target_Static", 2, 0, 20));

            ArenaSpec a2 = new ArenaSpec
            {
                SceneName = "Arena_02", LevelFile = "Level_02", SpawnId = "arena_02", Display = "La Forêt champêtre",
                Position = new Vector3(56, 10, 109), RotationY = 270, WorldRect = new Rect(15, 92, 45, 35)
            };
            a2.Targets.Add(new TargetSpec("Target_Static", -4, 0, 15));
            a2.Targets.Add(new TargetSpec("Target_Static", 3, 0, 18));
            a2.Targets.Add(new TargetSpec("Target_PopUp", 1, 0, 20));
            a2.Targets.Add(new TargetSpec("Target_Static", -1, 0, 22));
            a2.Targets.Add(new TargetSpec("Target_Static", 5, 0, 26));
            a2.Targets.Add(new TargetSpec("Target_Static", -5, 0, 30));

            ArenaSpec a3 = new ArenaSpec
            {
                SceneName = "Arena_03", LevelFile = "Level_03", SpawnId = "arena_03", Display = "La Rivière",
                Position = new Vector3(66, 10, 79), RotationY = 90, WorldRect = new Rect(62, 62, 40, 38)
            };
            a3.Targets.Add(new TargetSpec("Target_Static", -6, 0, 24));
            a3.Targets.Add(new TargetSpec("Target_Static", 6, 0, 32));
            a3.Targets.Add(new TargetSpec("Target_Moving", 0, 0, 21, "Lateral", 5f, 1.5f, 0f));
            a3.Targets.Add(new TargetSpec("Target_Moving", -2, 0, 27, "Lateral", 5f, 1.8f, 0.33f));
            a3.Targets.Add(new TargetSpec("Target_Moving", 2, 0, 35, "Lateral", 4f, 2f, 0.66f));

            // Stand du Ravin avancé de 4 m vers le vide (le rebord est à x = 122) et cibles posées sur des piliers de 8,5 m :
            // depuis la plateforme (œil à 11,7 m) la ligne de visée passe au-dessus du rebord (10 m) jusqu'à chaque cible.
            // Positions MONDE des cibles : (130, 90), (132, 83), (133, 97), (136, 74) sur piliers, puis (143, 75) et (145, 100) au sol.
            ArenaSpec a4 = new ArenaSpec
            {
                SceneName = "Arena_04", LevelFile = "Level_04", SpawnId = "arena_04", Display = "Le Ravin",
                Position = new Vector3(114, 10, 80), RotationY = 90, WorldRect = new Rect(106, 52, 42, 56), ArrivalDistance = 3.5f
            };
            a4.Targets.Add(new TargetSpec("Target_Static", -10, -1.5f, 16));
            a4.Targets.Add(new TargetSpec("Target_PopUp", -3, -1.5f, 18));
            a4.Targets.Add(new TargetSpec("Target_Moving", -17, 0, 19, "Vertical", 2.5f, 1f));
            a4.Targets.Add(new TargetSpec("Target_Static", 6, -1.5f, 22));
            a4.Targets.Add(new TargetSpec("Target_Static", 5, 0, 29));
            a4.Targets.Add(new TargetSpec("Target_Static", -20, 0, 31));

            ArenaSpec a5 = new ArenaSpec
            {
                SceneName = "Arena_05", LevelFile = "Level_05", SpawnId = "arena_05", Display = "Le Tournoi",
                Position = new Vector3(66, 13, 131), RotationY = 90, WorldRect = new Rect(60, 114, 88, 34),
                // L'arène fait 58 m de long : le point de détection est au MILIEU de l'arène (25 m devant le stand) et le rayon est grand,
                // pour que les gradins et les cibles apparaissent bien avant l'arrivée du joueur.
                PortalLocalOffset = new Vector3(0f, 0f, 25f), LoadRadius = 70f, UnloadRadius = 90f
            };
            a5.Targets.Add(new TargetSpec("Target_Static", -4, 0, 10, wave: 1));
            a5.Targets.Add(new TargetSpec("Target_Static", 0, 0, 15, wave: 1));
            a5.Targets.Add(new TargetSpec("Target_Static", 4, 0, 20, wave: 1));
            a5.Targets.Add(new TargetSpec("Target_Static", -2, 0, 25, wave: 1));
            a5.Targets.Add(new TargetSpec("Target_Moving", 0, 0, 28, "Lateral", 6f, 1.5f, 0f, wave: 2));
            a5.Targets.Add(new TargetSpec("Target_Moving", -5, 0, 35, "Orbit", 0f, 1.5f, 0f, 2f, wave: 2));
            a5.Targets.Add(new TargetSpec("Target_Moving", 3, 0, 40, "Lateral", 4f, 1.5f, 0.5f, wave: 2));
            a5.Targets.Add(new TargetSpec("Target_Penalty", -1, 0, 27, wave: 2));
            a5.Targets.Add(new TargetSpec("Target_Penalty", 2, 0, 33, wave: 2));
            a5.Targets.Add(new TargetSpec("Target_PopUp", -6, 0, 30, wave: 3));
            a5.Targets.Add(new TargetSpec("Target_PopUp", 6, 0, 34, wave: 3));
            a5.Targets.Add(new TargetSpec("Target_PopUp", 0, 0, 45, wave: 3));
            a5.Targets.Add(new TargetSpec("Target_Static", 0, 0, 50, wave: 3));
            a5.Targets.Add(new TargetSpec("Target_Penalty", -3, 0, 44, wave: 3));
            a5.Targets.Add(new TargetSpec("Target_Penalty", 3, 0, 47, wave: 3));

            return new[] { training, a1, a2, a3, a4, a5 };
        }

        /// <summary>Retrouve une arène par nom de scène.</summary>
        public static ArenaSpec Find(string sceneName)
        {
            foreach (ArenaSpec a in Arenas)
                if (a.SceneName == sceneName)
                    return a;
            return null;
        }
    }
}
