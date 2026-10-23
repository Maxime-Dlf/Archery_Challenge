// NockZone : zone d'encochage permissive autour de la corde.
//
// OÙ LE POSER : sur l'objet enfant « NockZone » d'un arc (même position que StringRest), avec un
// Sphere Collider « Is Trigger » de rayon 0,25 m. Layer « ArrowZone » (qui ne détecte que les flèches).
// RÉGLAGE : « Bow » ← la racine de l'arc.
//
// Garde la liste des flèches TENUES (état Held) qui se trouvent dans la zone. Le pilote VR (BowString)
// s'en sert pour encocher dès qu'une flèche tenue approche de la corde. Le rayon est agrandi de 30 %
// en Facile (voir DifficultyApplier).

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Détecte les flèches proches de la corde.</summary>
    [RequireComponent(typeof(SphereCollider))]
    [DisallowMultipleComponent]
    public class NockZone : MonoBehaviour
    {
        [SerializeField] private Bow bow;

        private readonly List<Arrow> arrowsInside = new List<Arrow>();
        private SphereCollider sphere;
        private float baseRadius;

        public Bow Bow => bow;

        /// <summary>Première flèche tenue dans la zone (null si aucune).</summary>
        public Arrow GetHeldArrow()
        {
            for (int i = arrowsInside.Count - 1; i >= 0; i--)
            {
                Arrow a = arrowsInside[i];
                if (a == null || !a.gameObject.activeInHierarchy)
                {
                    arrowsInside.RemoveAt(i);
                    continue;
                }
                if (a.State == ArrowState.Held)
                    return a;
            }
            return null;
        }

        private void Awake()
        {
            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            baseRadius = sphere.radius;
            if (bow == null)
                bow = GetComponentInParent<Bow>();
        }

        private void OnEnable() => GameEvents.DifficultyChanged += OnDifficultyChanged;
        private void OnDisable() => GameEvents.DifficultyChanged -= OnDifficultyChanged;

        private void Start() => ApplyDifficulty();

        private void OnDifficultyChanged(GameDifficulty difficulty) => ApplyDifficulty();

        private void ApplyDifficulty()
        {
            if (sphere == null)
                return;
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            float mult = applier != null && applier.Current != null ? applier.Current.nockRadiusMultiplier : 1f;
            sphere.radius = baseRadius * mult;
        }

        private void OnTriggerEnter(Collider other)
        {
            Arrow arrow = other.GetComponentInParent<Arrow>();
            if (arrow != null && !arrowsInside.Contains(arrow))
                arrowsInside.Add(arrow);
        }

        private void OnTriggerExit(Collider other)
        {
            Arrow arrow = other.GetComponentInParent<Arrow>();
            if (arrow != null)
                arrowsInside.Remove(arrow);
        }
    }
}
