// FontBuilder : crée les polices TextMeshPro « dynamiques » du jeu (accents et alphabets non latins).
//
// Menu : appelé par Tools → Archery Challenge → Build UI (§P12.3 du tutoriel).
// - Police principale : Inter (déjà dans VRTemplateAssets) en mode atlas DYNAMIQUE : les caractères
//   (é, è, ç, œ, Ñ, Ж…) sont ajoutés à la volée, ce qui est indispensable pour les noms libres.
// - Repli : Liberation Sans (licence OFL, fournie avec TextMesh Pro), couvre le grec et le cyrillique.
// - Les idéogrammes (李…) demanderaient en plus une police CJK (Noto Sans SC) : voir le document de reste à faire.

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur de polices TMP dynamiques.</summary>
    public static class FontBuilder
    {
        private const string Folder = "Assets/_Project/UI/Fonts";
        private const string InterTtf = "Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf";
        private const string LiberationTtf = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        /// <summary>Police principale du jeu.</summary>
        public static TMP_FontAsset MainFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Inter-Dynamic SDF.asset");

        /// <summary>Crée les polices (idempotent) et les définit par défaut.</summary>
        public static void Build()
        {
            BuildUtils.EnsureFolder(Folder);
            TMP_FontAsset fallback = Create(LiberationTtf, "LiberationSans-Dynamic SDF");
            TMP_FontAsset main = Create(InterTtf, "Inter-Dynamic SDF");
            if (main == null)
                return;

            if (fallback != null)
            {
                if (main.fallbackFontAssetTable == null)
                    main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(fallback))
                    main.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(main);
            }

            // Police par défaut de tous les textes TextMeshPro du projet.
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty p = so.FindProperty("m_defaultFontAsset");
                if (p != null)
                {
                    p.objectReferenceValue = main;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset Create(string ttfPath, string assetName)
        {
            string path = $"{Folder}/{assetName}.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[FontBuilder] Police introuvable : {ttfPath}");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogWarning($"[FontBuilder] Impossible de créer la police {assetName}.");
                return null;
            }

            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            if (asset.material != null)
            {
                asset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }
            // Pré-remplit les caractères français pour un premier affichage immédiat.
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?'’\"«»()-–—+×/%·°…éèêëàâäîïôöùûüçœÉÈÊËÀÂÄÎÏÔÖÙÛÜÇŒ€★☆");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
