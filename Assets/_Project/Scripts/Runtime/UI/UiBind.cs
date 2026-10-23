// UiBind : outils pour retrouver les éléments d'interface d'un prefab par leur NOM.
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// Les prefabs d'interface sont « bruts » : un script de panneau retrouve ses éléments (« ScoreValue »,
// « BtnPlay »…) par leur nom d'objet, quelle que soit leur profondeur dans la hiérarchie. Les noms
// attendus sont listés dans l'en-tête de chaque script. Renommer un objet sans prévenir = bouton mort :
// un avertissement précis est écrit dans la Console quand un élément manque.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Recherche d'éléments d'interface par nom.</summary>
    public static class UiBind
    {
        /// <summary>Cherche un enfant (profondeur quelconque, objets inactifs compris) par son nom.</summary>
        public static Transform Find(Transform root, string name)
        {
            if (root == null)
                return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        /// <summary>Cherche un composant sur l'enfant du nom donné (avertit s'il manque).</summary>
        public static T Get<T>(Transform root, string name, bool warn = true) where T : Component
        {
            Transform t = Find(root, name);
            T component = t != null ? t.GetComponent<T>() : null;
            if (component == null && warn)
                Debug.LogWarning($"[UI] « {name} » ({typeof(T).Name}) introuvable sous « {(root != null ? root.name : "null")} ». Vérifiez le nom dans le prefab.", root);
            return component;
        }

        public static TMP_Text Text(Transform root, string name, bool warn = true) => Get<TMP_Text>(root, name, warn);
        public static Button Button(Transform root, string name, bool warn = true) => Get<Button>(root, name, warn);
        public static Slider Slider(Transform root, string name, bool warn = true) => Get<Slider>(root, name, warn);
        public static Toggle Toggle(Transform root, string name, bool warn = true) => Get<Toggle>(root, name, warn);
        public static TMP_InputField Input(Transform root, string name, bool warn = true) => Get<TMP_InputField>(root, name, warn);

        /// <summary>Écrit un texte si l'élément existe.</summary>
        public static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
                text.text = value;
        }

        /// <summary>Affiche ou masque un objet d'interface.</summary>
        public static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
                component.gameObject.SetActive(active);
        }

        /// <summary>Affiche les étoiles « ★★☆ » d'une note de 0 à 3.</summary>
        public static string Stars(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            return new string('★', stars) + new string('☆', 3 - stars);
        }

        /// <summary>Formate un temps en secondes : « 1:05 ».</summary>
        public static string Time(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        /// <summary>Texte du nom de la difficulté en français.</summary>
        public static string DifficultyName(Core.GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case Core.GameDifficulty.Easy: return "Facile";
                case Core.GameDifficulty.Hard: return "Difficile";
                default: return "Normal";
            }
        }

        /// <summary>Retire tous les enfants d'un conteneur sauf ceux indiqués.</summary>
        public static void ClearChildren(Transform container, params Transform[] keep)
        {
            var keepSet = new HashSet<Transform>(keep);
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (!keepSet.Contains(child))
                    Object.Destroy(child.gameObject);
            }
        }
    }
}
