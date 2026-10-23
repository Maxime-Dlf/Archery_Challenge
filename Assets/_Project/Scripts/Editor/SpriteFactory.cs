// SpriteFactory : dessine par code les petits sprites de l'interface (panneaux, boutons, étoiles, cœurs…).
//
// Aucun asset graphique externe n'est requis : les formes sont tracées pixel par pixel dans des PNG
// (dossier Assets/_Project/UI/Sprites). L'équipe peut les remplacer par des sprites plus soignés
// (par exemple le Kenney UI Pack) sans toucher au code : seuls les noms de prefab comptent.
// Menu : appelé par Tools → Archery Challenge → Build UI.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de sprites d'interface.</summary>
    public static class SpriteFactory
    {
        public const string Folder = "Assets/_Project/UI/Sprites";

        public static Sprite Panel => Load("UI_Panel");
        public static Sprite Button => Load("UI_Button");
        public static Sprite Round => Load("UI_Round");
        public static Sprite Star => Load("UI_Star");
        public static Sprite Heart => Load("UI_Heart");
        public static Sprite Arrow => Load("UI_Arrow");
        public static Sprite Bar => Load("UI_Bar");
        public static Sprite Reticle => Load("UI_Reticle");
        public static Sprite Target => Load("UI_TargetFace");

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        /// <summary>Crée (ou recrée) tous les sprites.</summary>
        public static void BuildAll()
        {
            BuildUtils.EnsureFolder(Folder);
            Create("UI_Panel", 64, 64, (x, y) => RoundedRect(x, y, 64, 64, 14, new Color(0.93f, 0.85f, 0.68f), new Color(0.42f, 0.29f, 0.16f), 3f), new Vector4(18, 18, 18, 18));
            Create("UI_Button", 64, 64, (x, y) => RoundedRect(x, y, 64, 64, 12, Color.white, new Color(0.25f, 0.17f, 0.08f), 2.5f), new Vector4(16, 16, 16, 16));
            Create("UI_Round", 64, 64, (x, y) => Circle(x, y, 64, 30f, Color.white), Vector4.zero);
            Create("UI_Star", 128, 128, StarPixel, Vector4.zero);
            Create("UI_Heart", 128, 128, HeartPixel, Vector4.zero);
            Create("UI_Arrow", 128, 128, ArrowPixel, Vector4.zero);
            Create("UI_Bar", 16, 16, (x, y) => Color.white, new Vector4(4, 4, 4, 4));
            Create("UI_Reticle", 64, 64, ReticlePixel, Vector4.zero);
            Create("UI_TargetFace", 128, 128, TargetPixel, Vector4.zero);
            AssetDatabase.Refresh();
        }

        private delegate Color PixelFunc(int x, int y);

        private static void Create(string name, int width, int height, PixelFunc pixel, Vector4 border)
        {
            string path = $"{Folder}/{name}.png";
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- formes

        private static Color RoundedRect(int x, int y, int w, int h, float radius, Color fill, Color border, float borderWidth)
        {
            // Distance au rectangle arrondi (signée) : < 0 à l'intérieur.
            float cx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - radius);
            float cy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - radius);
            float outside = new Vector2(Mathf.Max(cx, 0f), Mathf.Max(cy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(cx, cy), 0f);
            float d = outside + inside - radius;
            if (d > 0.5f) return Color.clear;
            float alpha = Mathf.Clamp01(0.5f - d);
            Color c = d > -borderWidth ? border : fill;
            c.a *= alpha;
            return c;
        }

        private static Color Circle(int x, int y, int size, float radius, Color color)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) - radius;
            color.a = Mathf.Clamp01(0.5f - d);
            return color;
        }

        private static Color StarPixel(int x, int y)
        {
            // Étoile à 5 branches : pour l'angle du pixel (dans son secteur), on calcule le rayon du bord
            // du polygone (segment entre une pointe et le creux voisin) et on compare au rayon du pixel.
            Vector2 p = new Vector2((x + 0.5f) / 128f - 0.5f, (y + 0.5f) / 128f - 0.5f) * 2f;
            p.y += 0.06f;
            float angle = Mathf.Atan2(p.x, p.y);          // 0 = pointe vers le haut
            float radius = p.magnitude;
            float sector = Mathf.PI * 2f / 5f;
            float a = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f);
            const float outer = 0.98f, inner = 0.40f;
            Vector2 v1 = new Vector2(outer, 0f);
            Vector2 v2 = new Vector2(inner * Mathf.Cos(sector * 0.5f), inner * Mathf.Sin(sector * 0.5f));
            Vector2 d = v2 - v1;
            Vector2 u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float denominator = u.x * d.y - u.y * d.x;
            float edge = Mathf.Abs(denominator) < 1e-5f ? outer : (v1.x * d.y - v1.y * d.x) / denominator;
            float dist = radius - edge;
            Color c = Color.white;
            c.a = Mathf.Clamp01(0.5f - dist * 64f);
            return c;
        }

        private static Color HeartPixel(int x, int y)
        {
            float px = ((x + 0.5f) / 128f - 0.5f) * 2.6f;
            float py = ((y + 0.5f) / 128f - 0.45f) * 2.6f;
            // Courbe du cœur : (x² + y² − 1)³ − x²y³ < 0
            float a = px * px + py * py - 1f;
            float f = a * a * a - px * px * py * py * py;
            Color c = Color.white;
            c.a = f < 0f ? 1f : Mathf.Clamp01(1f - f * 8f);
            return c;
        }

        private static Color ArrowPixel(int x, int y)
        {
            // Flèche vers le haut : triangle + tige.
            float px = (x + 0.5f) / 128f;
            float py = (y + 0.5f) / 128f;
            bool head = py > 0.5f && Mathf.Abs(px - 0.5f) < (1f - py) * 0.9f;
            bool shaft = py <= 0.55f && py > 0.08f && Mathf.Abs(px - 0.5f) < 0.09f;
            Color c = Color.white;
            c.a = head || shaft ? 1f : 0f;
            return c;
        }

        private static Color ReticlePixel(int x, int y)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(32f, 32f);
            float d = p.magnitude;
            bool dot = d < 3f;
            bool ring = Mathf.Abs(d - 14f) < 1.4f && (Mathf.Abs(p.x) > 6f || Mathf.Abs(p.y) > 6f);
            Color c = new Color(1f, 1f, 1f, dot || ring ? 0.95f : 0f);
            return c;
        }

        private static Color TargetPixel(int x, int y)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(64f, 64f);
            float d = p.magnitude / 62f;
            if (d > 1f) return Color.clear;
            Color[] rings =
            {
                new Color(1f, 0.9f, 0.2f), new Color(1f, 0.9f, 0.2f),
                new Color(0.85f, 0.2f, 0.2f), new Color(0.85f, 0.2f, 0.2f),
                new Color(0.25f, 0.6f, 0.85f), new Color(0.25f, 0.6f, 0.85f),
                new Color(0.15f, 0.15f, 0.15f), new Color(0.15f, 0.15f, 0.15f),
                new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.95f, 0.95f)
            };
            return rings[Mathf.Min(rings.Length - 1, Mathf.FloorToInt(d * rings.Length))];
        }
    }
}
