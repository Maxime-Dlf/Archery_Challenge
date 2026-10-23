// NameEntryController : saisie du nom du joueur (1 à 20 caractères, accents et alphabets non latins acceptés).
//
// OÙ LE POSER : sur un champ TMP_Input Field (« NameInput » des panneaux de profil et de fin de manche).
//
// - Limite la saisie à 20 caractères et retire les caractères de contrôle.
// - Au clavier physique (PC) : on clique dans le champ et on tape.
// - En VR : le composant XR Keyboard Display (exemple « Spatial Keyboard » de XRI) ajouté sur le même champ
//   ouvre le clavier virtuel quand on le vise et qu'on appuie sur la gâchette. Il est ajouté
//   automatiquement par le générateur de prefabs quand l'exemple est installé.

using ArcheryChallenge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;

namespace ArcheryChallenge.UI
{
    /// <summary>Validation en direct d'un champ de nom.</summary>
    [RequireComponent(typeof(TMP_InputField))]
    [DisallowMultipleComponent]
    public class NameEntryController : MonoBehaviour
    {
        private TMP_InputField field;
        private XRKeyboardDisplay keyboardDisplay;
        private bool lastVr;
        private bool checkedMode;

        /// <summary>Texte nettoyé du champ (null si invalide).</summary>
        public string CleanedName
        {
            get
            {
                return Save.NameValidator.TryClean(field != null ? field.text : "", out string cleaned) ? cleaned : null;
            }
        }

        private void Awake()
        {
            field = GetComponent<TMP_InputField>();
            keyboardDisplay = GetComponent<XRKeyboardDisplay>();
            field.characterLimit = Save.NameValidator.MaxLength;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.lineType = TMP_InputField.LineType.SingleLine;
        }

        private void OnEnable()
        {
            if (field != null)
            {
                field.onValidateInput += ValidateChar;
                field.onEndEdit.AddListener(OnEndEdit);
            }
        }

        private void OnDisable()
        {
            if (field != null)
            {
                field.onValidateInput -= ValidateChar;
                field.onEndEdit.RemoveListener(OnEndEdit);
            }
        }

        // Le clavier virtuel n'a de sens qu'avec un vrai casque : au PC (et avec le rig VR simulé) on tape au clavier physique.
        private void Update()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (keyboardDisplay == null || mode == null)
                return;
            if (!checkedMode || mode.IsHeadset != lastVr)
            {
                checkedMode = true;
                lastVr = mode.IsHeadset;
                // Au PC, on détache le champ du clavier virtuel (sinon il s'ouvrirait au clic de souris).
                keyboardDisplay.inputField = lastVr ? field : null;
                keyboardDisplay.enabled = lastVr;
            }
        }

        private static char ValidateChar(string text, int index, char added)
        {
            // Les caractères de contrôle (retour chariot, tabulation) ne font pas partie d'un nom.
            return char.IsControl(added) ? '\0' : added;
        }

        private void OnEndEdit(string value)
        {
            if (Save.NameValidator.TryClean(value, out string cleaned))
                field.SetTextWithoutNotify(cleaned);
        }
    }
}
