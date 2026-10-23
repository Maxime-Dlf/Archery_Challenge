// MonsterDefinition : caractéristiques d'un monstre (ScriptableObject), annexe C.5 du tutoriel.
//
// OÙ LE CRÉER : Assets/_Project/Data/Monsters/ → Create → Archery Challenge → Monster Definition.

using UnityEngine;

namespace ArcheryChallenge.Monsters
{
    /// <summary>Données d'équilibrage d'un type de monstre (slime, tortue).</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Monster Definition", fileName = "Monster_New")]
    public class MonsterDefinition : ScriptableObject
    {
        public string id = "slime";
        public string displayName = "Slime";

        [Header("Santé et dégâts")]
        [Min(1)] public int maxHealth = 1;
        [Min(0)] public int damage = 2;
        [Tooltip("Points de vie rendus au joueur quand il élimine ce monstre.")]
        [Min(0)] public int healOnKill = 2;

        [Header("Déplacement")]
        public float walkSpeed = 1f;
        public float runSpeed = 2.5f;

        [Header("Comportement")]
        [Tooltip("Distance de repérage du joueur (m).")]
        public float senseRadius = 15f;
        [Tooltip("Distance d'attaque (m).")]
        public float attackRange = 1.6f;
        [Tooltip("Délai entre deux attaques (s).")]
        public float attackCooldown = 1.5f;
        [Tooltip("Vrai pour la tortue : bloque les flèches de face quand elle est visée.")]
        public bool canDefend;
        [Tooltip("Durée de l'étourdissement après un coup reçu (s).")]
        public float stunDuration = 0.8f;
    }
}
