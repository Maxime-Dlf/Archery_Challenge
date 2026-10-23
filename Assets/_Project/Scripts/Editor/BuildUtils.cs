// BuildUtils : outils communs aux générateurs d'éditeur (données, prefabs, scènes).
//
// Ces générateurs automatisent des étapes du tutoriel qui seraient très longues à la main :
// remplir les ScriptableObjects, assembler les prefabs d'arcs et de flèches, monter les scènes.
// Ils sont IDEMPOTENTS : on peut les relancer sans créer de doublons (ils mettent à jour l'existant).
// Menu : Tools → Archery Challenge → Build …

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Fonctions utilitaires pour les générateurs.</summary>
    public static class BuildUtils
    {
        public const string DataRoot = "Assets/_Project/Data";
        public const string PrefabRoot = "Assets/_Project/Prefabs";
        public const string MaterialRoot = "Assets/_Project/Art/Materials";
        public const string SceneRoot = "Assets/_Project/Scenes";

        /// <summary>Vrai pour exécuter les générateurs sans boîte de dialogue (automatisation, tests).</summary>
        public static bool SilentMode;

        /// <summary>
        /// Demande confirmation avant de régénérer des SCÈNES : un générateur repart de zéro et écrase donc tout ce qui a été
        /// posé ou déplacé à la main dans ces scènes (plantes, cibles, décors, panneaux).
        /// </summary>
        /// <returns>Vrai si l'on peut continuer.</returns>
        public static bool ConfirmSceneRegeneration(string what)
        {
            if (SilentMode || Application.isBatchMode)
                return true;
            return EditorUtility.DisplayDialog("Régénérer " + what + " ?",
                "Cette commande RECRÉE " + what + " à partir des générateurs et ÉCRASE tout ce qui a été ajouté, déplacé ou supprimé " +
                "à la main dans ces scènes (plantes, cibles, décors, panneaux…).\n\n" +
                "Si l'équipe a retouché ces scènes, faites une copie (ou un commit Git) avant de continuer.",
                "Régénérer", "Annuler");
        }

        /// <summary>Crée les dossiers manquants d'un chemin d'asset.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>Charge un asset ou le crée (ScriptableObject) s'il n'existe pas.</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        /// <summary>Enregistre un objet modifié (ScriptableObject ou prefab).</summary>
        public static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
        }

        /// <summary>Layer par nom (0 si introuvable, avec avertissement).</summary>
        public static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                Debug.LogWarning($"[BuildUtils] Layer « {name} » absent (Tags and Layers).");
                return 0;
            }
            return layer;
        }

        /// <summary>Crée un objet vide enfant.</summary>
        public static GameObject Child(Transform parent, string name, Vector3 localPosition = default, Quaternion? localRotation = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            return go;
        }

        // ---------------------------------------------------------------- SerializedObject

        /// <summary>Affecte un champ sérialisé (même privé) d'un composant.</summary>
        public static void Set(UnityEngine.Object target, string field, object value)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target), "Set(" + field + ")");
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[BuildUtils] Champ « {field} » introuvable sur {target.GetType().Name}.");
                return;
            }
            Assign(p, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Affecte un tableau d'objets référencés.</summary>
        public static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[BuildUtils] Tableau « {field} » introuvable sur {target.GetType().Name}.");
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty p, object value)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.ObjectReference: p.objectReferenceValue = value as UnityEngine.Object; break;
                case SerializedPropertyType.Float: p.floatValue = Convert.ToSingle(value); break;
                case SerializedPropertyType.Integer: p.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Boolean: p.boolValue = Convert.ToBoolean(value); break;
                case SerializedPropertyType.String: p.stringValue = (string)value; break;
                case SerializedPropertyType.Enum: p.enumValueIndex = Convert.ToInt32(value); break;
                case SerializedPropertyType.Color: p.colorValue = (Color)value; break;
                case SerializedPropertyType.Vector3: p.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector2: p.vector2Value = (Vector2)value; break;
                default:
                    Debug.LogWarning($"[BuildUtils] Type de champ non géré : {p.propertyType} ({p.propertyPath})");
                    break;
            }
        }

        /// <summary>Ajoute un composant par nom de type (ignoré avec un avertissement si le type n'existe pas).</summary>
        public static Component AddByName(GameObject go, string fullTypeName)
        {
            Type type = FindType(fullTypeName);
            if (type == null)
            {
                Debug.LogWarning($"[BuildUtils] Type introuvable (ignoré) : {fullTypeName}");
                return null;
            }
            Component existing = go.GetComponent(type);
            return existing != null ? existing : go.AddComponent(type);
        }

        /// <summary>Retrouve un type du jeu par son nom complet.</summary>
        public static Type FindType(string fullTypeName)
        {
            return Type.GetType(fullTypeName + ", ArcheryChallenge.Runtime");
        }

        // ---------------------------------------------------------------- matériaux

        /// <summary>Matériau URP Unlit (ou Lit) créé ou mis à jour.</summary>
        public static Material EnsureMaterial(string name, string shaderName, Color color, bool transparent = false)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            EnsureFolder(MaterialRoot);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[BuildUtils] Shader introuvable : {shaderName}");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);

            if (transparent)
            {
                // Mode transparent URP : alpha blending, pas d'écriture de profondeur.
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Enregistre un GameObject de la scène comme prefab (écrase l'existant) et le détruit de la scène.</summary>
        public static GameObject SaveAsPrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
