// GameViewViewport : (éditeur) lit la partie de l'image réellement VISIBLE dans la fenêtre Game.
//
// POURQUOI : la fenêtre Game peut être zoomée (curseur « Scale ») ou plus petite que la résolution choisie : seule la partie
// centrale de l'image est alors visible, et l'interface collée aux coins de l'écran (HUD, légende des touches) est coupée.
// Ce lecteur donne le rectangle visible à ScreenViewport (runtime), qui replie l'interface dans cette zone.

#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using ArcheryChallenge.Core;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Zone visible de la fenêtre Game de l'éditeur.</summary>
    public static class GameViewViewport
    {
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static PropertyInfo targetInViewProperty;
        private static PropertyInfo viewInWindowProperty;
        private static EditorWindow cached;

        [InitializeOnLoadMethod]
        private static void Register() => ScreenViewport.EditorProvider = TryGetVisible;

        private static EditorWindow FindGameView()
        {
            if (cached != null)
                return cached;
            if (GameViewType == null)
                return null;
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(GameViewType);
            cached = all.Length > 0 ? (EditorWindow)all[0] : null;
            return cached;
        }

        /// <summary>Rectangle visible, normalisé (0..1, origine en bas à gauche comme Screen), ou faux si la vue n'est pas zoomée.</summary>
        public static bool TryGetVisible(out Rect visible)
        {
            visible = new Rect(0f, 0f, 1f, 1f);
            try
            {
                EditorWindow view = FindGameView();
                if (view == null)
                    return false;
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                targetInViewProperty ??= GameViewType.GetProperty("targetInView", flags);
                viewInWindowProperty ??= GameViewType.GetProperty("viewInWindow", flags);
                if (targetInViewProperty == null || viewInWindowProperty == null)
                    return false;
                // « targetInView » : où se place l'image rendue dans la zone d'affichage (origine en haut à gauche, elle peut déborder
                // si la vue est zoomée) ; « viewInWindow » : taille de la zone d'affichage.
                Rect target = (Rect)targetInViewProperty.GetValue(view);
                Rect shownView = (Rect)viewInWindowProperty.GetValue(view);
                if (target.width < 1f || target.height < 1f || shownView.width < 1f || shownView.height < 1f)
                    return false;
                float xMin = Mathf.Clamp01(-target.x / target.width);
                float xMax = Mathf.Clamp01((shownView.width - target.x) / target.width);
                float topMin = Mathf.Clamp01(-target.y / target.height);
                float topMax = Mathf.Clamp01((shownView.height - target.y) / target.height);
                if (xMax - xMin < 0.05f || topMax - topMin < 0.05f)
                    return false;
                // Passage de l'origine en haut à gauche (vue) à l'origine en bas à gauche (Screen).
                visible = Rect.MinMaxRect(xMin, 1f - topMax, xMax, 1f - topMin);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Diagnostic : liste les champs utiles de la fenêtre Game.</summary>
        public static string Dump()
        {
            StringBuilder sb = new StringBuilder();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            sb.AppendLine("GameView : " + (GameViewType != null));
            if (GameViewType == null)
                return sb.ToString();
            foreach (FieldInfo f in GameViewType.GetFields(flags))
            {
                string n = f.Name.ToLowerInvariant();
                if (n.Contains("zoom") || n.Contains("scale") || n.Contains("target") || n.Contains("size") || n.Contains("area"))
                    sb.AppendLine($"champ {f.Name} : {f.FieldType.Name}");
            }
            foreach (PropertyInfo p in GameViewType.GetProperties(flags))
            {
                string n = p.Name.ToLowerInvariant();
                if (n.Contains("zoom") || n.Contains("scale") || n.Contains("target") || n.Contains("size") || n.Contains("area") || n.Contains("viewin"))
                    sb.AppendLine($"propriété {p.Name} : {p.PropertyType.Name}");
            }
            Type za = typeof(EditorWindow).Assembly.GetType("UnityEditor.ZoomableArea");
            if (za != null)
                foreach (PropertyInfo p in za.GetProperties(flags))
                {
                    string n = p.Name.ToLowerInvariant();
                    if (n.Contains("shown") || n.Contains("scale") || n.Contains("rect") || n.Contains("draw"))
                        sb.AppendLine($"ZoomableArea.{p.Name} : {p.PropertyType.Name}");
                }
            EditorWindow view = FindGameView();
            if (view != null)
            {
                foreach (string name in new[] { "targetInView", "viewInWindow", "targetInContent", "targetRenderSize", "targetSize", "zoomAreaScale", "viewInParent", "targetInParent", "currentGameViewSize" })
                {
                    PropertyInfo prop = GameViewType.GetProperty(name, flags);
                    sb.AppendLine($"valeur {name} = {prop?.GetValue(view)}");
                }
                object zoom = GameViewType.GetField("m_ZoomArea", flags)?.GetValue(view);
                if (zoom != null)
                    foreach (string name in new[] { "shownArea", "shownAreaInsideMargins", "rect", "drawRect", "scale" })
                        sb.AppendLine($"valeur zoom.{name} = {zoom.GetType().GetProperty(name, flags)?.GetValue(zoom)}");
                sb.AppendLine($"fenêtre : position {view.position}");
            }
            bool ok = TryGetVisible(out Rect visible);
            sb.AppendLine($"zone visible : {ok} {visible}");
            return sb.ToString();
        }
    }
}
#endif
