// CaptureTool : prend une capture d'écran depuis un point de vue quelconque (outil de vérification).
//
// Utilisé par les tests visuels automatiques et pour illustrer la documentation : on crée une caméra
// temporaire, on la fait rendre dans une texture, et on enregistre un PNG. Fonctionne en Play comme hors Play.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Capture d'images depuis une caméra temporaire.</summary>
    public static class CaptureTool
    {
        /// <summary>Dossier des captures (hors projet, pour ne rien versionner).</summary>
        public static string Folder
        {
            get
            {
                string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "Captures");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>Capture une image depuis une position, en regardant un point.</summary>
        /// <returns>Chemin du PNG.</returns>
        public static string Capture(string fileName, Vector3 position, Vector3 lookAt, float fov = 60f, int width = 1280, int height = 720)
        {
            return Capture(fileName, position, Quaternion.LookRotation(lookAt - position, Vector3.up), fov, width, height);
        }

        /// <summary>Capture une image depuis une pose précise.</summary>
        public static string Capture(string fileName, Vector3 position, Quaternion rotation, float fov = 60f, int width = 1280, int height = 720)
        {
            GameObject go = new GameObject("CaptureCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            string path = Path.Combine(Folder, fileName.EndsWith(".png") ? fileName : fileName + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(go);
            return path;
        }

        /// <summary>
        /// Capture le Game view tel que le voit le joueur (interface en superposition comprise), après quelques
        /// images d'attente. Fonctionne en mode Play uniquement ; le PNG apparaît dans <see cref="Folder"/>.
        /// </summary>
        /// <returns>Chemin prévu du PNG (écrit à la fin de l'image en cours).</returns>
        public static string CaptureGameView(string fileName, int waitFrames = 3)
        {
            string path = Path.Combine(Folder, fileName.EndsWith(".png") ? fileName : fileName + ".png");
            int remaining = waitFrames;
            void Tick()
            {
                if (--remaining > 0)
                    return;
                EditorApplication.update -= Tick;
                if (EditorApplication.isPlaying)
                    ScreenCapture.CaptureScreenshot(path);
            }
            if (File.Exists(path))
                File.Delete(path);
            EditorApplication.update += Tick;
            return path;
        }

        /// <summary>Capture ce que voit une caméra existante.</summary>
        public static string CaptureCamera(string fileName, Camera source, int width = 1280, int height = 720)
        {
            return Capture(fileName, source.transform.position, source.transform.rotation, source.fieldOfView, width, height);
        }
    }
}
