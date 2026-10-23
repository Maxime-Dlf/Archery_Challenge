// MonsterHealth : points de vie d'un monstre et réaction aux flèches.
//
// OÙ LE POSER : sur la racine d'un prefab de monstre (Monster_Slime, Monster_Turtle), Layer « Monster ».
// RÉGLAGE : « Definition » ← Monster_Slime ou Monster_Turtle.
//
// - Le slime meurt en 1 flèche, la tortue en 3.
// - La tortue (Can Defend) bloque les flèches venant de FACE quand elle est en défense : la flèche
//   rebondit. Elle est vulnérable de dos et de flanc.
// - Éliminer un monstre rend des PV au joueur (+2 slime, +4 tortue).

using System;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Player;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Monsters
{
    /// <summary>Santé d'un monstre.</summary>
    [DisallowMultipleComponent]
    public class MonsterHealth : MonoBehaviour, IArrowReceiver
    {
        [SerializeField] private MonsterDefinition definition;

        private int health;
        private MonsterBrain brain;

        /// <summary>Annoncé à la mort du monstre.</summary>
        public event Action<MonsterHealth> Died;

        /// <summary>Annoncé à chaque coup reçu (non bloqué).</summary>
        public event Action<MonsterHealth> Damaged;

        public MonsterDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public int Health => health;
        public bool IsDead => health <= 0;

        private void Awake()
        {
            brain = GetComponent<MonsterBrain>();
            ResetHealth();
        }

        /// <summary>Remet la santé au maximum (réapparition).</summary>
        public void ResetHealth() => health = definition != null ? definition.maxHealth : 1;

        /// <inheritdoc />
        public ArrowHitOutcome OnArrowHit(Arrow arrow, Vector3 point, Vector3 direction, Collider collider)
        {
            if (IsDead)
                return ArrowHitOutcome.PassThrough;

            // La tortue en défense bloque les tirs de face.
            if (definition != null && definition.canDefend && brain != null && brain.IsDefending)
            {
                float facing = Vector3.Dot(direction.normalized, transform.forward);
                if (facing < -0.3f)
                {
                    GameEvents.RaiseMonsterEvent(point, "block");
                    return ArrowHitOutcome.Deflect;
                }
            }

            ApplyDamage(1, point);
            return ArrowHitOutcome.Stick;
        }

        /// <summary>Inflige des dégâts (flèche ou autre source).</summary>
        public void ApplyDamage(int amount, Vector3 point)
        {
            if (IsDead)
                return;

            health = Mathf.Max(0, health - amount);
            Damaged?.Invoke(this);
            GameEvents.RaiseMonsterEvent(point, "hit");
            ScorePopup.Spawn(point + Vector3.up * 0.3f, "-" + amount, new Color(0.6f, 1f, 0.5f));

            if (health <= 0)
                Die(point);
            else if (brain != null)
                brain.OnHit();
        }

        private void Die(Vector3 point)
        {
            // Les flèches plantées dans le monstre sont rendues au pool (il va disparaître).
            foreach (Arrow arrow in GetComponentsInChildren<Arrow>(true))
                arrow.Despawn();

            PlayerHealth player = Services.Get<PlayerHealth>();
            if (player != null && definition != null)
                player.Heal(definition.healOnKill);

            var slots = Services.Get<Save.SaveSlotService>();
            if (slots != null)
                slots.Current.stats.monstersKilled++;

            GameEvents.RaiseMonsterEvent(point, "die");
            GameEvents.RaiseMonsterKilled(this);
            GameEvents.RaiseToast(definition != null ? $"{definition.displayName} éliminé ! +{definition.healOnKill} PV" : "Monstre éliminé !");
            Died?.Invoke(this);
            if (brain != null)
                brain.Die();
            else
                gameObject.SetActive(false);
        }
    }
}
