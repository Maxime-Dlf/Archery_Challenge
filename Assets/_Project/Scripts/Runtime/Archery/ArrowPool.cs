// ArrowPool : réserve de flèches réutilisables.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Créer et détruire des objets pendant la partie provoque des saccades (ramasse-miettes). Le pool
// garde les flèches terminées désactivées et les redonne au tir suivant : aucune allocation une fois
// le pool « chauffé ». Chaque type de flèche (bois, frêne, carbone) a son propre tas.

using System.Collections.Generic;
using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.Archery
{
    /// <summary>Pool de flèches par type.</summary>
    [DisallowMultipleComponent]
    public class ArrowPool : MonoBehaviour
    {
        [Tooltip("Nombre de flèches préparées au démarrage pour chaque type.")]
        [SerializeField, Min(0)] private int prewarmPerType = 0;

        private readonly Dictionary<string, Stack<Arrow>> pools = new Dictionary<string, Stack<Arrow>>();
        private Transform container;

        private void Awake()
        {
            Services.Register(this);
            container = new GameObject("PooledArrows").transform;
            container.SetParent(transform, false);
        }

        private void OnDestroy() => Services.Unregister(this);

        /// <summary>
        /// Fournit une flèche du type demandé (active, au repos). Renvoie null si le prefab est absent.
        /// </summary>
        public Arrow Get(ArrowDefinition definition)
        {
            if (definition == null || definition.prefab == null)
                return null;

            if (!pools.TryGetValue(definition.id, out Stack<Arrow> stack))
            {
                stack = new Stack<Arrow>();
                pools[definition.id] = stack;
            }

            Arrow arrow = null;
            while (stack.Count > 0 && arrow == null)
                arrow = stack.Pop();

            if (arrow == null)
            {
                GameObject instance = Instantiate(definition.prefab);
                instance.name = definition.prefab.name;
                // Les flèches vivent dans la scène du pool (Boot, persistante) : une arène déchargée ne les emporte pas.
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                arrow = instance.GetComponent<Arrow>();
                if (arrow == null)
                {
                    Debug.LogError($"[ArrowPool] Le prefab « {definition.prefab.name} » n'a pas de composant Arrow.");
                    Destroy(instance);
                    return null;
                }
                arrow.Definition = definition;
            }

            arrow.transform.SetParent(null, true);
            arrow.gameObject.SetActive(true);
            return arrow;
        }

        /// <summary>Rend une flèche au pool.</summary>
        public void Release(Arrow arrow)
        {
            if (arrow == null)
                return;
            arrow.ResetForPool();
            arrow.transform.SetParent(container, true);

            string key = arrow.Definition != null ? arrow.Definition.id : "unknown";
            if (!pools.TryGetValue(key, out Stack<Arrow> stack))
            {
                stack = new Stack<Arrow>();
                pools[key] = stack;
            }
            stack.Push(arrow);
        }

        /// <summary>Rend toutes les flèches actives au pool (changement d'arène, réinitialisation).</summary>
        public void ReleaseAllActive()
        {
            Arrow[] arrows = FindObjectsByType<Arrow>(FindObjectsInactive.Exclude);
            foreach (Arrow a in arrows)
                Release(a);
        }
    }
}
