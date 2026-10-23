// SceneService : chargement et déchargement de scènes en mode additif.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Boot reste toujours chargée ; World, puis les arènes, sont ajoutées par-dessus. Ce service évite les
// doubles chargements (une arène demandée deux fois) et attend la fin du chargement avant de rendre la main.
// Les scènes doivent être listées dans File → Build Profiles → Scene List (sinon « couldn't be loaded »).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.Core
{
    /// <summary>Gestion des scènes additives.</summary>
    [DisallowMultipleComponent]
    public class SceneService : MonoBehaviour
    {
        private readonly HashSet<string> loading = new HashSet<string>();
        private readonly HashSet<string> unloading = new HashSet<string>();

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Vrai si la scène est chargée (ou en cours de chargement).</summary>
        public bool IsLoaded(string sceneName)
        {
            if (loading.Contains(sceneName))
                return true;
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>Vrai si la scène est en cours de chargement ou de déchargement.</summary>
        public bool IsBusy(string sceneName) => loading.Contains(sceneName) || unloading.Contains(sceneName);

        /// <summary>Charge une scène en additif (sans effet si elle est déjà chargée).</summary>
        public Coroutine LoadAdditive(string sceneName) => StartCoroutine(LoadRoutine(sceneName));

        /// <summary>Décharge une scène (sans effet si elle n'est pas chargée).</summary>
        public Coroutine Unload(string sceneName) => StartCoroutine(UnloadRoutine(sceneName));

        /// <summary>Coroutine de chargement, à attendre avec <c>yield return</c>.</summary>
        public IEnumerator LoadRoutine(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || IsLoaded(sceneName) || unloading.Contains(sceneName))
            {
                // Si une autre routine charge déjà la scène, on attend simplement qu'elle ait terminé.
                while (loading.Contains(sceneName))
                    yield return null;
                yield break;
            }

            loading.Add(sceneName);
            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneService] Scène « {sceneName} » introuvable : ajoutez-la à la Scene List. {e.Message}");
            }

            if (operation != null)
            {
                while (!operation.isDone)
                    yield return null;
            }
            loading.Remove(sceneName);
        }

        /// <summary>Coroutine de déchargement.</summary>
        public IEnumerator UnloadRoutine(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded || unloading.Contains(sceneName))
                yield break;

            unloading.Add(sceneName);
            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                while (!operation.isDone)
                    yield return null;
            }
            unloading.Remove(sceneName);
        }
    }
}
