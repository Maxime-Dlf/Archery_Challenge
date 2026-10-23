// ArrowAllowance : nombre de flèches que le joueur peut encore tirer.
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// Le LevelController règle ce compteur au début d'une manche (12 flèches, par exemple) et le
// carquois refuse de donner une flèche quand il tombe à zéro. En dehors d'une manche, et en
// entraînement, les flèches sont illimitées (Unlimited = vrai).

using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Compteur de flèches disponibles.</summary>
    public static class ArrowAllowance
    {
        /// <summary>Vrai = flèches illimitées (valeur par défaut).</summary>
        public static bool Unlimited { get; private set; } = true;

        /// <summary>Flèches restantes (sans signification si <see cref="Unlimited"/>).</summary>
        public static int Remaining { get; private set; }

        /// <summary>Vrai si le joueur peut prendre une nouvelle flèche.</summary>
        public static bool CanTake => Unlimited || Remaining > 0;

        /// <summary>Donne un nombre fixe de flèches.</summary>
        public static void Set(int count)
        {
            Unlimited = false;
            Remaining = Mathf.Max(0, count);
        }

        /// <summary>Flèches illimitées.</summary>
        public static void SetUnlimited()
        {
            Unlimited = true;
            Remaining = 0;
        }

        /// <summary>Consomme une flèche tirée.</summary>
        public static void Consume()
        {
            if (!Unlimited && Remaining > 0)
                Remaining--;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLaunch() => SetUnlimited();
    }
}
