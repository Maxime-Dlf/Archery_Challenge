// ScorePopup : chiffre qui flotte au point d'impact (« X », « 9 », « −15 »…).
//
// OÙ LE POSER : n'a pas besoin d'être posé : ScorePopup.Spawn(...) crée (et réutilise) les objets.
// Le texte est un TextMeshPro 3D qui regarde la caméra, monte doucement puis disparaît.
//
// Les popups sont recyclés (petit pool) : aucune allocation pendant la partie une fois chauffé.

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Texte flottant temporaire en monde.</summary>
    public class ScorePopup : MonoBehaviour
    {
        private const float Lifetime = 1.1f;
        private const float RiseSpeed = 0.9f;
        private static readonly Stack<ScorePopup> Pool = new Stack<ScorePopup>();
        private static Transform poolRoot;

        private TextMeshPro text;
        private float age;
        private Color baseColor;
        private float baseSize;

        /// <summary>Affiche un texte flottant.</summary>
        /// <param name="position">Position monde.</param>
        /// <param name="content">Texte (court).</param>
        /// <param name="color">Couleur du texte.</param>
        /// <param name="size">Taille de police (0,5 ≈ lisible à 10 m).</param>
        public static void Spawn(Vector3 position, string content, Color color, float size = 0.5f)
        {
            ScorePopup popup = Pool.Count > 0 ? Pool.Pop() : Create();
            popup.Show(position, content, color, size);
        }

        private static ScorePopup Create()
        {
            if (poolRoot == null)
                poolRoot = new GameObject("ScorePopups").transform;

            GameObject go = new GameObject("ScorePopup");
            go.transform.SetParent(poolRoot, false);
            ScorePopup popup = go.AddComponent<ScorePopup>();
            popup.text = go.AddComponent<TextMeshPro>();
            popup.text.alignment = TextAlignmentOptions.Center;
            popup.text.fontStyle = FontStyles.Bold;
            popup.text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            popup.text.rectTransform.sizeDelta = new Vector2(4f, 1f);
            popup.text.outlineWidth = 0.2f;
            popup.text.outlineColor = new Color32(0, 0, 0, 255);
            return popup;
        }

        private void Show(Vector3 position, string content, Color color, float size)
        {
            transform.position = position;
            text.text = content;
            baseColor = color;
            baseSize = size;
            text.fontSize = size * 10f;
            text.color = color;
            age = 0f;
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                gameObject.SetActive(false);
                Pool.Push(this);
                return;
            }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            // Le texte regarde la caméra principale (il reste lisible quel que soit l'angle).
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

            // Petit « pop » au début puis fondu sur la dernière moitié.
            float t = age / Lifetime;
            float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
            text.fontSize = baseSize * 10f * pop;
            Color c = baseColor;
            c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            text.color = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPool()
        {
            Pool.Clear();
            poolRoot = null;
        }
    }
}
