// NameValidator : règles du nom du joueur (1 à 20 caractères Unicode).
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// Le nom est saisi au clavier virtuel (VR) ou physique (PC). Les accents et les alphabets non
// latins sont acceptés (UTF-8). On retire les espaces superflus et les caractères de contrôle.

using System.Globalization;
using System.Text;

namespace ArcheryChallenge.Save
{
    /// <summary>Validation et nettoyage des noms de joueur.</summary>
    public static class NameValidator
    {
        /// <summary>Nombre maximal de caractères d'un nom.</summary>
        public const int MaxLength = 20;

        /// <summary>
        /// Nettoie un nom (espaces au début/fin retirés, espaces multiples réduits, caractères de
        /// contrôle supprimés) puis vérifie qu'il contient 1 à 20 caractères visibles.
        /// </summary>
        /// <param name="raw">Texte saisi.</param>
        /// <param name="cleaned">Nom nettoyé (vide si invalide).</param>
        /// <returns>Vrai si le nom est utilisable.</returns>
        public static bool TryClean(string raw, out string cleaned)
        {
            cleaned = string.Empty;
            if (string.IsNullOrEmpty(raw))
                return false;

            StringBuilder sb = new StringBuilder(raw.Length);
            bool lastWasSpace = true; // vrai au départ : supprime les espaces de tête
            foreach (char c in raw)
            {
                if (char.IsControl(c))
                    continue;
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                        sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }

            string trimmed = sb.ToString().TrimEnd();
            if (trimmed.Length == 0)
                return false;

            // On compte des « éléments de texte » (un caractère accentué décomposé ou un emoji = 1).
            StringInfo info = new StringInfo(trimmed);
            if (info.LengthInTextElements > MaxLength)
                trimmed = info.SubstringByTextElements(0, MaxLength);

            cleaned = trimmed;
            return true;
        }

        /// <summary>Longueur en « caractères visibles » d'un texte.</summary>
        public static int Length(string text) => string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements;
    }
}
