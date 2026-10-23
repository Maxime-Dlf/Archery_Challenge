// TerrainBuilder : génère le terrain 160 × 160 m par calcul (relief, textures, arbres, eau).
//
// Reproduit par code les étapes §P8-A.3 à §P8-A.7 du tutoriel (sculpture, texturage, arbres), à partir
// du plan de la carte (ArenaLayout). Le résultat est un TerrainData enregistré dans
// Assets/_Project/Art/Terrain/World_TerrainData.asset, que l'équipe peut retoucher au pinceau.
//
// Relief : sol à 10 m ; bordure de montagnes (12 m, 25 à 45 m de haut) ; rivière (lit à 7 m, eau à 8,6 m) ;
// ravin (fond à 0 m, deux corniches à 5 m) ; plateau du tournoi (13 m) avec colline d'arrêt (25 m) ;
// colline séparatrice (16 m) ; petites collines (+1 à +3 m) dans les zones libres.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur du terrain du monde ouvert.</summary>
    public static class TerrainBuilder
    {
        public const float MapSize = 160f;
        public const float TerrainHeight = 60f;
        public const float GroundLevel = 10f;
        private const int HeightmapResolution = 513;
        private const int AlphamapResolution = 512;
        private const string PurePoly = "Assets/Pure Poly/Free Low Poly Nature Pack/";

        /// <summary>
        /// Rampe d'accès au fond du ravin (X min, Z min, largeur, profondeur) : un chemin en pente douce (≈ 29°) qui part du
        /// bord sud du ravin et descend jusqu'au fond, pour qu'un joueur tombé puisse remonter à pied.
        /// </summary>
        /// <summary>Hauteur (m) du dessus des piliers du ravin qui portent les cibles basses (le rebord est à 10 m).</summary>
        public const float PillarHeight = 8.5f;

        /// <summary>Piliers du ravin (X, Z) : un par cible basse, plus hauts pour que les cibles dépassent du rebord vu depuis la plateforme.</summary>
        public static readonly Vector2[] RavinePillars = { new Vector2(132, 83), new Vector2(136, 74), new Vector2(130, 90) };

        public static readonly Rect RavineRamp = new Rect(123.5f, 52f, 8f, 22f);

        private static readonly Vector2 RiverTop = new Vector2(79f, 112f);
        private static readonly Vector2 RiverBottom = new Vector2(79f, 50f);

        // ---------------------------------------------------------------- fonctions de forme

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float SmoothStep(float a, float b, float x) => Smooth((x - a) / (b - a));

        /// <summary>Poids 1 dans le rectangle, qui retombe à 0 sur « falloff » mètres à l'extérieur.</summary>
        private static float RectWeight(float x, float z, Rect r, float falloff)
        {
            float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax);
            float dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothStep(0f, Mathf.Max(0.01f, falloff), d);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distance au chemin principal le plus proche.</summary>
        public static float DistanceToPath(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float best = float.MaxValue;
            foreach (Vector2[] path in ArenaLayout.Paths)
                for (int i = 0; i < path.Length - 1; i++)
                    best = Mathf.Min(best, DistanceToSegment(p, path[i], path[i + 1]));
            return best;
        }

        private static float RiverDistance(float x, float z)
        {
            // Distance signée au lit de la rivière (négative à l'intérieur).
            Vector2 p = new Vector2(x, z);
            float channel = DistanceToSegment(p, RiverBottom, RiverTop) - 3f;
            float pond = Vector2.Distance(p, RiverBottom) - 6f;
            float source = Vector2.Distance(p, RiverTop) - 4f;
            return Mathf.Min(channel, Mathf.Min(pond, source));
        }

        // Zones où le sol reste plat : rectangles de jeu élargis de 8 m (l'arrivée, 6 m derrière chaque plateforme, y est comprise).
        private static Rect[] Protected => new[]
        {
            Inflate(ArenaLayout.Camp, 4), Inflate(ArenaLayout.Hub, 6), Inflate(ArenaLayout.Training, 6),
            Inflate(new Rect(15, 52, 35, 30), 9), Inflate(new Rect(15, 92, 45, 35), 9), Inflate(new Rect(62, 62, 40, 38), 9), Inflate(new Rect(106, 52, 42, 56), 9)
        };

        private static Rect Inflate(Rect r, float margin) => new Rect(r.x - margin, r.y - margin, r.width + 2f * margin, r.height + 2f * margin);

        // ---------------------------------------------------------------- relief

        public static float Height(float x, float z)
        {
            float h = GroundLevel;

            // Petites collines dans les zones libres.
            float free = 1f;
            foreach (Rect r in Protected)
                free = Mathf.Min(free, 1f - RectWeight(x, z, r, 8f));
            free = Mathf.Min(free, 1f - RectWeight(x, z, ArenaLayout.Plateau, 6f));
            free = Mathf.Min(free, SmoothStep(3f, 7f, DistanceToPath(x, z)));
            float hills = Mathf.PerlinNoise(x * 0.045f + 3.1f, z * 0.045f + 7.7f);
            hills = Mathf.Max(0f, hills - 0.45f) / 0.55f;
            h += free * hills * 3f;

            // Plateau du tournoi (13 m), colline d'arrêt jusqu'à 25 m à l'est.
            float plateau = RectWeight(x, z, ArenaLayout.Plateau, 6f);
            h = Mathf.Lerp(h, 13f, plateau);
            float hillZone = RectWeight(x, z, new Rect(125, 114, 23, 34), 4f);
            h += hillZone * 12f * SmoothStep(125f, 140f, x) * plateau;

            // Colline séparatrice (16 m) entre l'arène 3 et le ravin.
            h += RectWeight(x, z, ArenaLayout.Separator, 3f) * 6f;

            // Rivière : lit à 7 m, berges douces.
            float river = 1f - SmoothStep(0f, 2.8f, RiverDistance(x, z));
            h = Mathf.Lerp(h, 7f, river);

            // Ravin : fond à 0 m, falaises raides, deux corniches à 5 m.
            float ravine = 1f - SmoothStep(0f, 1.2f, RectDistance(x, z, ArenaLayout.Ravine));
            h = Mathf.Lerp(h, 0f, ravine);
            foreach (Vector2 ledge in RavinePillars)
            {
                float w = 1f - SmoothStep(2f, 3.6f, Vector2.Distance(new Vector2(x, z), ledge));
                h = Mathf.Lerp(h, PillarHeight, w);
            }

            // Rampe d'accès : du rebord sud (10 m, z = 56) au fond (0 m, z = 74), sans paroi verticale.
            float ramp = RectWeight(x, z, RavineRamp, 1.2f);
            if (ramp > 0f)
                h = Mathf.Lerp(h, GroundLevel * (1f - Mathf.Clamp01((z - 56f) / 18f)), ramp);

            // Montagnes de bordure (12 m) : 25 à 45 m de haut.
            float edgeDistance = Mathf.Min(Mathf.Min(x, MapSize - x), Mathf.Min(z, MapSize - z));
            float edge = 1f - Mathf.Clamp01(edgeDistance / 12f);
            float noise = Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.13f + 10f, z * 0.13f) * 0.4f;
            h += Smooth(edge) * (15f + 20f * noise);

            return h;
        }

        private static float RectDistance(float x, float z, Rect r)
        {
            float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax);
            float dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------------------------------------------------------------- construction

        /// <summary>Crée le TerrainData complet (relief, textures, arbres) et l'enregistre.</summary>
        public static TerrainData Build(out string report)
        {
            BuildUtils.EnsureFolder("Assets/_Project/Art/Terrain");
            string path = "Assets/_Project/Art/Terrain/World_TerrainData.asset";
            AssetDatabase.DeleteAsset(path);

            TerrainData data = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                alphamapResolution = AlphamapResolution,
                baseMapResolution = 256
            };
            data.size = new Vector3(MapSize, TerrainHeight, MapSize);
            AssetDatabase.CreateAsset(data, path);

            // --- relief ---
            float[,] heights = new float[HeightmapResolution, HeightmapResolution];
            float step = MapSize / (HeightmapResolution - 1);
            for (int zi = 0; zi < HeightmapResolution; zi++)
                for (int xi = 0; xi < HeightmapResolution; xi++)
                    heights[zi, xi] = Mathf.Clamp01(Height(xi * step, zi * step) / TerrainHeight);
            data.SetHeights(0, 0, heights);

            // --- textures ---
            TerrainLayer[] layers = LoadLayers();
            data.terrainLayers = layers;
            PaintTextures(data, heights);

            // --- arbres ---
            int treeCount = PlantTrees(data);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            report = $"Terrain {MapSize}×{MapSize} m, {treeCount} arbres";
            return data;
        }

        private static TerrainLayer[] LoadLayers()
        {
            string[] names =
            {
                "Cauliflower_Green", "Pebbles_Light_Brown_Ground", "Flatstones_Grey",
                "Pebbles_Mid_Brown_Ground", "Cauliflower_Dark_Brown", "Flatstones_Brown"
            };
            TerrainLayer[] layers = new TerrainLayer[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{PurePoly}Terrain/Terrain Layers/{names[i]}.terrainlayer");
                if (layers[i] == null)
                    Debug.LogWarning($"[TerrainBuilder] TerrainLayer introuvable : {names[i]}");
            }
            return layers;
        }

        private static float ForestDensity(float x, float z)
        {
            float edgeDistance = Mathf.Min(Mathf.Min(x, MapSize - x), Mathf.Min(z, MapSize - z));
            float d = SmoothStep(0f, 1f, (34f - edgeDistance) / 34f) * 0.95f;
            d = Mathf.Max(d, RectWeight(x, z, ArenaLayout.ForestNW, 6f) * 0.85f);
            d = Mathf.Max(d, RectWeight(x, z, new Rect(35, 15, 15, 32), 4f) * 0.3f);     // entre camp et hub
            d = Mathf.Max(d, RectWeight(x, z, new Rect(15, 92, 45, 35), 0f) * 0.22f);   // forêt champêtre
            d = Mathf.Max(d, RectWeight(x, z, new Rect(15, 83, 30, 8), 3f) * 0.35f);    // entre arènes 1 et 2
            d = Mathf.Max(d, RectWeight(x, z, new Rect(88, 42, 17, 16), 3f) * 0.3f);    // autour de l'étang
            d = Mathf.Max(d, 0.05f);
            float noise = Mathf.PerlinNoise(x * 0.09f + 40f, z * 0.09f + 11f);
            return Mathf.Clamp01(d * (0.55f + noise * 0.9f));
        }

        private static void PaintTextures(TerrainData data, float[,] heights)
        {
            int n = AlphamapResolution;
            int layerCount = data.terrainLayers.Length;
            float[,,] maps = new float[n, n, layerCount];
            for (int zi = 0; zi < n; zi++)
                for (int xi = 0; xi < n; xi++)
                    FillSplat(maps, zi, xi, zi, xi, layerCount);
            data.SetAlphamaps(0, 0, maps);
        }

        // Calcule les poids des calques de texture pour la cellule (xi, zi) de la carte et les écrit en (mapZ, mapX) du tableau.
        private static void FillSplat(float[,,] maps, int zi, int xi, int mapZ, int mapX, int layerCount)
        {
            float cell = MapSize / AlphamapResolution;
            float x = (xi + 0.5f) * cell;
            float z = (zi + 0.5f) * cell;
            float h = Height(x, z);

            // Pente (en degrés) par différences finies.
            float hx = Height(x + 1f, z) - Height(x - 1f, z);
            float hz = Height(x, z + 1f) - Height(x, z - 1f);
            float slope = Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) * 0.5f) * Mathf.Rad2Deg;

            float[] w = new float[layerCount];
            w[0] = 1f; // herbe partout

            Apply(w, 4, Mathf.Clamp01((ForestDensity(x, z) - 0.2f) * 2f) * 0.8f);                                   // sous-bois
            Apply(w, 5, Mathf.Max(RectWeight(x, z, ArenaLayout.Hub, 3f), Mathf.Max(RectWeight(x, z, ArenaLayout.Camp, 3f), RectWeight(x, z, new Rect(95, 15, 20, 25), 2f)))); // sol du hub et du camp
            Apply(w, 3, Mathf.Max(1f - SmoothStep(8.4f, 9.9f, h), 1f - SmoothStep(0.1f, 1.2f, RectDistance(x, z, ArenaLayout.Ravine))) * (h < 9.9f ? 1f : 0f)); // berges, lit, ravin
            Apply(w, 2, Mathf.Max(SmoothStep(26f, 40f, slope), SmoothStep(22f, 34f, h)));                           // roche
            Apply(w, 1, 1f - SmoothStep(1.1f, 2.1f, DistanceToPath(x, z)));                                         // chemin
            Apply(w, 1, RectWeight(x, z, RavineRamp, 0.4f));                                                       // rampe du ravin : terre battue

            float sum = 0f;
            for (int l = 0; l < layerCount; l++) sum += w[l];
            for (int l = 0; l < layerCount; l++)
                maps[mapZ, mapX, l] = w[l] / sum;
        }

        /// <summary>
        /// Recalcule EN PLACE une zone d'un terrain existant (relief, textures, arbres) sans toucher au reste : sert aux
        /// correctifs (nouveau chemin, piliers du ravin) pour ne pas écraser les retouches faites à la main ailleurs.
        /// </summary>
        /// <param name="data">Terrain à modifier (déjà dans la scène).</param>
        /// <param name="worldRect">Zone (X min, Z min, largeur, profondeur) en mètres.</param>
        /// <param name="heights">Recalculer le relief.</param>
        /// <param name="splat">Recalculer les textures.</param>
        /// <param name="removeTreesOnPaths">Retirer les arbres posés sur un chemin dans la zone.</param>
        /// <returns>Résumé de ce qui a été fait.</returns>
        public static string PatchRegion(TerrainData data, Rect worldRect, bool heights, bool splat, bool removeTreesOnPaths)
        {
            string summary = "";
            if (heights)
            {
                int res = data.heightmapResolution;
                float step = MapSize / (res - 1);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(worldRect.xMin / step));
                int z0 = Mathf.Max(0, Mathf.FloorToInt(worldRect.yMin / step));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt(worldRect.xMax / step));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt(worldRect.yMax / step));
                float[,] block = new float[z1 - z0 + 1, x1 - x0 + 1];
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                        block[zi - z0, xi - x0] = Mathf.Clamp01(Height(xi * step, zi * step) / TerrainHeight);
                data.SetHeights(x0, z0, block);
                summary += $"relief {block.GetLength(1)}×{block.GetLength(0)} ; ";
            }
            if (splat)
            {
                int n = data.alphamapResolution;
                float cell = MapSize / n;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(worldRect.xMin / cell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt(worldRect.yMin / cell));
                int x1 = Mathf.Min(n - 1, Mathf.CeilToInt(worldRect.xMax / cell));
                int z1 = Mathf.Min(n - 1, Mathf.CeilToInt(worldRect.yMax / cell));
                int layerCount = data.terrainLayers.Length;
                float[,,] block = new float[z1 - z0 + 1, x1 - x0 + 1, layerCount];
                for (int zi = z0; zi <= z1; zi++)
                    for (int xi = x0; xi <= x1; xi++)
                        FillSplat(block, zi, xi, zi - z0, xi - x0, layerCount);
                data.SetAlphamaps(x0, z0, block);
                summary += $"textures {block.GetLength(1)}×{block.GetLength(0)} ; ";
            }
            if (removeTreesOnPaths)
            {
                TreeInstance[] trees = data.treeInstances;
                List<TreeInstance> kept = new List<TreeInstance>(trees.Length);
                int removed = 0;
                foreach (TreeInstance tree in trees)
                {
                    float tx = tree.position.x * MapSize;
                    float tz = tree.position.z * MapSize;
                    bool inside = worldRect.Contains(new Vector2(tx, tz));
                    if (inside && (DistanceToPath(tx, tz) < 3.4f || RectDistance(tx, tz, RavineRamp) < 2.5f))
                    {
                        removed++;
                        continue;
                    }
                    kept.Add(tree);
                }
                if (removed > 0)
                    data.SetTreeInstances(kept.ToArray(), true);
                summary += $"{removed} arbre(s) retiré(s) ; ";
            }
            EditorUtility.SetDirty(data);
            return summary.TrimEnd(' ', ';');
        }

        /// <summary>
        /// Recalcule relief, textures et arbres AUTOUR de chemins précis (l'ancien tracé et le nouveau), sans toucher au reste du terrain :
        /// les retouches faites à la main ailleurs sont conservées. Le relief et les textures sont refaits dans une bande autour des
        /// polylignes ; les arbres posés sur le réseau de chemins actuel (ArenaLayout.Paths) sont retirés.
        /// </summary>
        /// <param name="polylines">Tracés à retraiter (typiquement l'ancien et le nouveau).</param>
        /// <param name="heightBand">Demi-largeur (m) de la bande de relief : 7,5 m couvre la zone aplanie autour d'un chemin.</param>
        /// <param name="splatBand">Demi-largeur (m) de la bande de textures.</param>
        public static string PatchAroundPaths(TerrainData data, Vector2[][] polylines, float heightBand, float splatBand)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2[] line in polylines)
                foreach (Vector2 p in line)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            float reach = Mathf.Max(heightBand, splatBand) + 1f;
            Rect box = Rect.MinMaxRect(min.x - reach, min.y - reach, max.x + reach, max.y + reach);

            // Relief.
            int res = data.heightmapResolution;
            float step = MapSize / (res - 1);
            int hx0 = Mathf.Max(0, Mathf.FloorToInt(box.xMin / step)), hz0 = Mathf.Max(0, Mathf.FloorToInt(box.yMin / step));
            int hx1 = Mathf.Min(res - 1, Mathf.CeilToInt(box.xMax / step)), hz1 = Mathf.Min(res - 1, Mathf.CeilToInt(box.yMax / step));
            float[,] heights = data.GetHeights(hx0, hz0, hx1 - hx0 + 1, hz1 - hz0 + 1);
            int heightCells = 0;
            for (int zi = hz0; zi <= hz1; zi++)
                for (int xi = hx0; xi <= hx1; xi++)
                {
                    float x = xi * step, z = zi * step;
                    if (DistanceToPolylines(x, z, polylines) > heightBand)
                        continue;
                    heights[zi - hz0, xi - hx0] = Mathf.Clamp01(Height(x, z) / TerrainHeight);
                    heightCells++;
                }
            data.SetHeights(hx0, hz0, heights);

            // Textures.
            int n = data.alphamapResolution;
            float cell = MapSize / n;
            int sx0 = Mathf.Max(0, Mathf.FloorToInt(box.xMin / cell)), sz0 = Mathf.Max(0, Mathf.FloorToInt(box.yMin / cell));
            int sx1 = Mathf.Min(n - 1, Mathf.CeilToInt(box.xMax / cell)), sz1 = Mathf.Min(n - 1, Mathf.CeilToInt(box.yMax / cell));
            int layerCount = data.terrainLayers.Length;
            float[,,] maps = data.GetAlphamaps(sx0, sz0, sx1 - sx0 + 1, sz1 - sz0 + 1);
            int splatCells = 0;
            for (int zi = sz0; zi <= sz1; zi++)
                for (int xi = sx0; xi <= sx1; xi++)
                {
                    if (DistanceToPolylines((xi + 0.5f) * cell, (zi + 0.5f) * cell, polylines) > splatBand)
                        continue;
                    FillSplat(maps, zi, xi, zi - sz0, xi - sx0, layerCount);
                    splatCells++;
                }
            data.SetAlphamaps(sx0, sz0, maps);

            // Arbres sur le réseau de chemins actuel.
            TreeInstance[] trees = data.treeInstances;
            List<TreeInstance> kept = new List<TreeInstance>(trees.Length);
            int removed = 0;
            foreach (TreeInstance tree in trees)
            {
                float tx = tree.position.x * MapSize, tz = tree.position.z * MapSize;
                if (box.Contains(new Vector2(tx, tz)) && DistanceToPath(tx, tz) < 3.4f)
                {
                    removed++;
                    continue;
                }
                kept.Add(tree);
            }
            if (removed > 0)
                data.SetTreeInstances(kept.ToArray(), true);

            EditorUtility.SetDirty(data);
            return $"relief {heightCells} points ; textures {splatCells} cellules ; {removed} arbre(s) retiré(s)";
        }

        private static float DistanceToPolylines(float x, float z, Vector2[][] polylines)
        {
            Vector2 p = new Vector2(x, z);
            float best = float.MaxValue;
            foreach (Vector2[] line in polylines)
                for (int i = 0; i < line.Length - 1; i++)
                    best = Mathf.Min(best, DistanceToSegment(p, line[i], line[i + 1]));
            return best;
        }

        // Pose un calque avec le poids donné : les calques précédents sont atténués d'autant.
        private static void Apply(float[] weights, int layer, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f)
                return;
            for (int i = 0; i < weights.Length; i++)
                weights[i] *= 1f - amount;
            weights[layer] += amount;
        }

        // ---------------------------------------------------------------- arbres

        private static int PlantTrees(TerrainData data)
        {
            string[] names = { "PP_Tree_02", "PP_Tree_10", "PP_Birch_Tree_05", "PP_Birch_Tree_06" };
            BuildUtils.EnsureFolder(BuildUtils.PrefabRoot + "/World/Trees");
            List<TreePrototype> prototypes = new List<TreePrototype>();
            foreach (string name in names)
            {
                GameObject variant = MakeTreeVariant(name);
                if (variant != null)
                    prototypes.Add(new TreePrototype { prefab = variant });
            }
            if (prototypes.Count == 0)
                return 0;
            data.treePrototypes = prototypes.ToArray();

            // Segments tir : de chaque plateforme à chaque cible (aucun arbre sur la ligne de visée).
            List<(Vector2, Vector2)> sightLines = new List<(Vector2, Vector2)>();
            foreach (ArenaSpec arena in ArenaLayout.Arenas)
            {
                Vector3 origin = arena.Position;
                foreach (TargetSpec t in arena.Targets)
                {
                    Vector3 world = arena.LocalToWorld(t.Position);
                    sightLines.Add((new Vector2(origin.x, origin.z), new Vector2(world.x, world.z)));
                }
            }

            Random.InitState(20261020);
            List<TreeInstance> trees = new List<TreeInstance>();
            const float spacing = 3.4f;
            for (float x = 2f; x < MapSize - 2f; x += spacing)
            {
                for (float z = 2f; z < MapSize - 2f; z += spacing)
                {
                    float px = x + Random.Range(-1.4f, 1.4f);
                    float pz = z + Random.Range(-1.4f, 1.4f);
                    if (Random.value > ForestDensity(px, pz))
                        continue;
                    if (!TreeAllowed(px, pz, sightLines))
                        continue;

                    float height = Height(px, pz);
                    if (height < 8.4f)
                        continue; // pas d'arbre dans l'eau ni au fond du ravin
                    float hx = Height(px + 1f, pz) - Height(px - 1f, pz);
                    float hz = Height(px, pz + 1f) - Height(px, pz - 1f);
                    if (Mathf.Atan(Mathf.Sqrt(hx * hx + hz * hz) * 0.5f) * Mathf.Rad2Deg > 38f)
                        continue;

                    TreeInstance tree = new TreeInstance
                    {
                        position = new Vector3(px / MapSize, 0f, pz / MapSize),
                        prototypeIndex = Random.Range(0, prototypes.Count),
                        widthScale = Random.Range(0.8f, 1.3f),
                        heightScale = Random.Range(0.8f, 1.3f),
                        rotation = Random.Range(0f, Mathf.PI * 2f),
                        color = Color.white,
                        lightmapColor = Color.white
                    };
                    trees.Add(tree);
                }
            }
            data.SetTreeInstances(trees.ToArray(), true);
            return trees.Count;
        }

        // Zones où aucun arbre ne doit pousser.
        private static bool TreeAllowed(float x, float z, List<(Vector2, Vector2)> sightLines)
        {
            Vector2 p = new Vector2(x, z);
            if (DistanceToPath(x, z) < 3.6f)
                return false;
            if (RiverDistance(x, z) < 2.5f)
                return false;
            if (RectDistance(x, z, ArenaLayout.Ravine) < 3f)
                return false;
            if (RectDistance(x, z, RavineRamp) < 2.5f)
                return false;

            foreach (Rect r in new[] { ArenaLayout.Camp, ArenaLayout.Hub, ArenaLayout.Training })
                if (RectDistance(x, z, r) < 1f)
                    return false;
            foreach (ArenaSpec arena in ArenaLayout.Arenas)
            {
                // Plateforme et zone de tir dégagées (sauf la forêt champêtre qui garde des arbres entre les cibles).
                Vector2 platform = new Vector2(arena.Position.x, arena.Position.z);
                if (Vector2.Distance(p, platform) < 8f)
                    return false;
                if (arena.SceneName != "Arena_02" && RectDistance(x, z, arena.WorldRect) < 0.5f)
                    return false;
            }
            foreach (var line in sightLines)
                if (DistanceToSegment(p, line.Item1, line.Item2) < 1.8f)
                    return false;
            return true;
        }

        private static GameObject MakeTreeVariant(string name)
        {
            string source = $"{PurePoly}Prefabs/{name}.prefab";
            string target = $"{BuildUtils.PrefabRoot}/World/Trees/{name}_Terrain.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            if (existing != null)
                return existing;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (prefab == null)
            {
                Debug.LogWarning($"[TerrainBuilder] Arbre introuvable : {source}");
                return null;
            }
            // Les arbres de terrain n'acceptent pas les Mesh Collider : on les remplace par une capsule.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            CapsuleCollider capsule = instance.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 6f;
            capsule.center = new Vector3(0f, 3f, 0f);
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, target);
            Object.DestroyImmediate(instance);
            return variant;
        }
    }
}
