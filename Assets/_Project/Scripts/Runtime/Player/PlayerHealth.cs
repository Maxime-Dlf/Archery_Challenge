// PlayerHealth : points de vie du joueur (10), dégâts, soin et réapparition.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// RÈGLES (§P10.1 du tutoriel)
// - 10 PV au départ. Un coup de monstre retire 2 (slime) ou 4 (tortue) PV : flash rouge, vibration.
// - Éliminer un monstre rend des PV (sans dépasser 10).
// - À 0 PV : fondu au noir, le joueur réapparaît au hub avec 10 PV, sans autre pénalité.
// - Une courte invulnérabilité (1 s) suit chaque coup pour éviter d'enchaîner les dégâts.

using System.Collections;
using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Player
{
    /// <summary>Santé du joueur.</summary>
    [DisallowMultipleComponent]
    public class PlayerHealth : MonoBehaviour
    {
        public const int MaxHealth = 10;

        [SerializeField, Min(0f)] private float invulnerabilitySeconds = 1f;

        private int health = MaxHealth;
        private float invulnerableUntil;
        private bool dying;

        public int Health => health;
        public bool IsDead => health <= 0;

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        private void OnEnable()
        {
            GameEvents.ProgressionChanged += SyncFromSlot;
            SyncFromSlot();
        }

        private void OnDisable() => GameEvents.ProgressionChanged -= SyncFromSlot;

        // Nouvel emplacement de sauvegarde : on reprend les PV enregistrés.
        private SlotData lastSlot;

        private void SyncFromSlot()
        {
            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (slots == null)
                return;
            SlotData slot = slots.Current;
            if (slot == lastSlot)
                return;
            lastSlot = slot;
            health = Mathf.Clamp(slot.health <= 0 ? MaxHealth : slot.health, 1, MaxHealth);
            GameEvents.RaisePlayerHealthChanged(health, MaxHealth);
        }

        /// <summary>Inflige des dégâts (ignorés pendant l'invulnérabilité).</summary>
        /// <param name="amount">Points de vie retirés.</param>
        /// <param name="source">Position de l'attaquant (recul, direction du flash).</param>
        public void Damage(int amount, Vector3 source)
        {
            if (dying || amount <= 0 || Time.time < invulnerableUntil)
                return;

            invulnerableUntil = Time.time + invulnerabilitySeconds;
            health = Mathf.Max(0, health - amount);
            Persist();
            GameEvents.RaisePlayerHurt(source);
            GameEvents.RaisePlayerHealthChanged(health, MaxHealth);

            if (health <= 0)
                StartCoroutine(DieRoutine());
        }

        /// <summary>Rend des points de vie (sans dépasser 10).</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || health <= 0)
                return;
            health = Mathf.Min(MaxHealth, health + amount);
            Persist();
            GameEvents.RaisePlayerHealthChanged(health, MaxHealth);
        }

        /// <summary>Remet la santé au maximum.</summary>
        public void FullHeal()
        {
            health = MaxHealth;
            Persist();
            GameEvents.RaisePlayerHealthChanged(health, MaxHealth);
        }

        private void Persist()
        {
            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (slots != null)
                slots.Current.health = health;
        }

        private IEnumerator DieRoutine()
        {
            dying = true;
            GameEvents.RaisePlayerDied();

            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (slots != null)
                slots.Current.stats.deaths++;

            FadeService fade = Services.Get<FadeService>();
            PlayerModeService mode = Services.Get<PlayerModeService>();

            // Abandon d'une éventuelle manche en cours.
            Levels.LevelController active = Levels.LevelController.Active;
            if (active != null)
                active.Abort();

            if (fade != null)
                yield return fade.FadeOut(0.5f);
            if (mode != null)
            {
                if (!mode.TeleportToSpawn("hub"))
                    mode.TeleportToSpawn("camp");
            }
            FullHeal();
            yield return new WaitForSeconds(0.3f);
            if (fade != null)
                yield return fade.FadeIn(0.7f);
            GameEvents.RaiseToast("Vous avez été vaincu : retour au village.");
            dying = false;
        }
    }
}
