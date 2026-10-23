// FoliageBuilder : pose des plantes (fleurs, herbes, champignons, cailloux) comme de VRAIS objets de la scène.
//
// Chaque plante est une instance de prefab visible dans la Hiérarchie, rangée dans un groupe nommé
// (« Foliage_Fleurs », « Foliage_Champignons »…). L'équipe peut donc les déplacer, supprimer ou dupliquer à la
// main, comme n'importe quel objet. Ce générateur ne fait que fournir une base de départ cohérente :
//  - aucune plante ne bloque les flèches ni le joueur (colliders désactivés) ;
//  - aucune plante ne pousse sur la plateforme, devant les cibles, sur la cible de départ ou les panneaux.
// (Les arbres, eux, sont des instances de terrain : ils se modifient au pinceau « Paint Trees » du Terrain.)

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Pose de plantes décoratives dans une scène.</summary>
    public static class FoliageBuilder
    {
        /// <summary>Dossier des prefabs de plantes du pack Pure Poly.</summary>
        public const string PurePoly = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";

        /// <summary>Crée (ou retrouve) un groupe nommé sous un parent.</summary>
        public static Transform Group(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        /// <summary>
        /// Dissémine « count » exemplaires d'un prefab dans un rectangle.
        /// </summary>
        /// <param name="parent">Groupe qui reçoit les plantes.</param>
        /// <param name="prefabFile">Nom du prefab Pure Poly (sans extension).</param>
        /// <param name="area">Rectangle (x min, z min, largeur, profondeur) dans le repère du parent (ou du monde si worldSpace).</param>
        /// <param name="isAllowed">Test d'exclusion : reçoit (x, z) et renvoie faux pour refuser l'emplacement.</param>
        /// <param name="groundY">Hauteur du sol en (x, z) ; null = 0.</param>
        /// <param name="worldSpace">Vrai si « area » et « groundY » sont en coordonnées monde.</param>
        /// <returns>Nombre d'objets posés.</returns>
        public static int Scatter(Transform parent, string prefabFile, int count, Rect area, Func<float, float, bool> isAllowed,
            Func<float, float, float> groundY, float scaleMin, float scaleMax, bool worldSpace = false)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PurePoly + prefabFile + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[FoliageBuilder] Prefab introuvable : " + prefabFile);
                return 0;
            }

            int placed = 0;
            int attempts = 0;
            while (placed < count && attempts < count * 12)
            {
                attempts++;
                float x = Random.Range(area.xMin, area.xMax);
                float z = Random.Range(area.yMin, area.yMax);
                if (isAllowed != null && !isAllowed(x, z))
                    continue;
                float y = groundY != null ? groundY(x, z) : 0f;
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = $"{prefabFile}_{placed:00}";
                Vector3 position = new Vector3(x, y, z);
                if (worldSpace)
                    go.transform.position = position;
                else
                    go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(scaleMin, scaleMax);
                MakeHarmless(go);
                placed++;
            }
            return placed;
        }

        /// <summary>Désactive les colliders (les plantes ne doivent ni arrêter une flèche ni gêner la marche) et rend l'objet statique.</summary>
        public static void MakeHarmless(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>Distance d'un point (x, z) à un segment [a, b] (plan XZ).</summary>
        public static float DistanceToSegment(float x, float z, Vector2 a, Vector2 b)
        {
            Vector2 p = new Vector2(x, z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// Test d'exclusion pour une arène (repère local : le joueur est à l'origine et regarde +Z) : plateforme et
        /// recul, cible de départ, panneaux, pied de chaque cible et couloir de tir de la plateforme à chaque cible.
        /// </summary>
        public static Func<float, float, bool> ArenaExclusion(ArenaSpec spec)
        {
            List<(Vector2 center, float radius)> circles = new List<(Vector2, float)>
            {
                (new Vector2(2.5f, 4f), 3.2f),      // cible DÉPART
                (new Vector2(-2.6f, 3.2f), 1.8f),   // panneau d'information
                (new Vector2(2.8f, 2f), 1.8f)       // panneau de score
            };
            List<Vector2> targets = new List<Vector2>();
            foreach (TargetSpec t in spec.Targets)
            {
                circles.Add((new Vector2(t.Position.x, t.Position.z), 1.8f));
                targets.Add(new Vector2(t.Position.x, t.Position.z));
            }

            return (x, z) =>
            {
                if (Mathf.Abs(x) < 4.8f && z > -8f && z < 3.8f)
                    return false;                   // plateforme, ligne de tir et zone d'arrivée
                foreach (var c in circles)
                    if (Vector2.Distance(new Vector2(x, z), c.center) < c.radius)
                        return false;
                foreach (Vector2 target in targets)
                    if (DistanceToSegment(x, z, Vector2.zero, target) < 1.2f)
                        return false;               // couloir de tir
                return true;
            };
        }
    }
}
