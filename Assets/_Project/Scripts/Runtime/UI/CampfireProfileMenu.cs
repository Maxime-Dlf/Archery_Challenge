// CampfireProfileMenu : choix du profil et de l'emplacement de sauvegarde, assis devant le feu de camp.
//
// OÙ LE POSER : sur le panneau du prefab « CampfireProfileMenu » (placé sur Camp/MenuAnchor, et instance
// globale dans UI_Global pour le mode clavier/souris).
// PAGES : PageProfiles, PageSlots, PageConfirm, PageDone.
// ÉLÉMENTS ATTENDUS :
//   PageProfiles  Content (liste), ProfileButtonTemplate (bouton modèle), NameInput (TMP_InputField, 20 car.),
//                 BtnCreate, Message
//   PageSlots     SlotsTitle, Slot1/Slot2/Slot3 (chacun : Label, BtnContinue, BtnNew), BtnBackProfiles,
//                 BtnDeleteProfile
//   PageConfirm   ConfirmText, BtnConfirmYes, BtnConfirmNo
//   PageDone      DoneText, BtnChangeProfile
//
// Un profil n'est pas protégé par mot de passe. Chaque profil a 3 emplacements ; « Nouvelle partie »
// sur un emplacement occupé demande une confirmation (écrasement).

using System;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Menu des profils et des emplacements.</summary>
    public class CampfireProfileMenu : UiPanel
    {
        private GameObject pageProfiles, pageSlots, pageConfirm, pageDone;
        private Transform listContent;
        private Button template;
        private TMP_InputField nameInput;
        private TMP_Text message, slotsTitle, confirmText, doneText;
        private readonly TMP_Text[] slotLabels = new TMP_Text[SaveSlotService.SlotCount];
        private readonly Button[] continueButtons = new Button[SaveSlotService.SlotCount];
        private readonly Button[] newButtons = new Button[SaveSlotService.SlotCount];
        private readonly List<GameObject> spawned = new List<GameObject>();
        private string selectedProfileId;
        private Action pendingConfirm;
        private bool bound;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            pageProfiles = UiBind.Find(transform, "PageProfiles")?.gameObject;
            pageSlots = UiBind.Find(transform, "PageSlots")?.gameObject;
            pageConfirm = UiBind.Find(transform, "PageConfirm")?.gameObject;
            pageDone = UiBind.Find(transform, "PageDone")?.gameObject;

            listContent = UiBind.Find(transform, "Content");
            template = UiBind.Button(transform, "ProfileButtonTemplate", false);
            if (template != null) template.gameObject.SetActive(false);
            nameInput = UiBind.Input(transform, "NameInput", false);
            if (nameInput != null) nameInput.characterLimit = NameValidator.MaxLength;
            message = UiBind.Text(transform, "Message", false);
            slotsTitle = UiBind.Text(transform, "SlotsTitle", false);
            confirmText = UiBind.Text(transform, "ConfirmText", false);
            doneText = UiBind.Text(transform, "DoneText", false);

            UiBind.Button(transform, "BtnCreate", false)?.onClick.AddListener(OnCreate);
            UiBind.Button(transform, "BtnBackProfiles", false)?.onClick.AddListener(() => Show(pageProfiles));
            UiBind.Button(transform, "BtnDeleteProfile", false)?.onClick.AddListener(OnDeleteProfile);
            UiBind.Button(transform, "BtnConfirmYes", false)?.onClick.AddListener(() => { var a = pendingConfirm; pendingConfirm = null; a?.Invoke(); });
            UiBind.Button(transform, "BtnConfirmNo", false)?.onClick.AddListener(() => { pendingConfirm = null; Show(selectedProfileId != null ? pageSlots : pageProfiles); });
            UiBind.Button(transform, "BtnChangeProfile", false)?.onClick.AddListener(() => { Refresh(); Show(pageProfiles); });
            UiBind.Button(transform, "BtnClose", false)?.onClick.AddListener(Close);

            for (int i = 0; i < SaveSlotService.SlotCount; i++)
            {
                Transform slot = UiBind.Find(transform, "Slot" + (i + 1));
                if (slot == null) continue;
                slotLabels[i] = UiBind.Text(slot, "Label", false);
                continueButtons[i] = UiBind.Button(slot, "BtnContinue", false);
                newButtons[i] = UiBind.Button(slot, "BtnNew", false);
                int index = i + 1;
                continueButtons[i]?.onClick.AddListener(() => OnContinue(index));
                newButtons[i]?.onClick.AddListener(() => OnNew(index));
            }
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        protected override void OnOpened()
        {
            Refresh();
            Show(pageProfiles);
        }

        private void Show(GameObject page)
        {
            foreach (GameObject p in new[] { pageProfiles, pageSlots, pageConfirm, pageDone })
                if (p != null)
                    p.SetActive(p == page);
        }

        /// <summary>Reconstruit la liste des profils.</summary>
        public void Refresh()
        {
            Bind();
            ProfileService profiles = Services.Get<ProfileService>();
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();
            if (profiles == null || listContent == null || template == null)
                return;

            foreach (ProfileData profile in profiles.Profiles)
            {
                Button button = Instantiate(template, listContent);
                button.gameObject.SetActive(true);
                button.name = "Profile_" + profile.name;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = profile.name;
                string id = profile.id;
                button.onClick.AddListener(() => OnProfileClicked(id));
                spawned.Add(button.gameObject);
            }
            UiBind.SetText(message, profiles.Profiles.Count == 0
                ? "Bienvenue ! Saisissez votre nom pour créer un profil."
                : "Choisissez votre profil ou créez-en un nouveau.");
        }

        private void OnCreate()
        {
            ProfileService profiles = Services.Get<ProfileService>();
            if (profiles == null || nameInput == null)
                return;
            ProfileData created = profiles.Create(nameInput.text);
            if (created == null)
            {
                UiBind.SetText(message, "Nom invalide : 1 à 20 caractères.");
                return;
            }
            nameInput.text = "";
            Refresh();
            OnProfileClicked(created.id);
        }

        private void OnProfileClicked(string profileId)
        {
            ProfileService profiles = Services.Get<ProfileService>();
            if (profiles == null || !profiles.Select(profileId))
                return;
            selectedProfileId = profileId;
            ShowSlots();
        }

        private void ShowSlots()
        {
            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (profiles == null || profiles.Current == null || slots == null)
                return;

            UiBind.SetText(slotsTitle, "Profil : " + profiles.Current.name);
            for (int i = 0; i < SaveSlotService.SlotCount; i++)
            {
                SlotSummary summary = slots.GetSummary(profiles.Current.id, i + 1);
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = summary.Exists
                        ? $"Emplacement {i + 1} : {summary.LevelsPassed}/5 arènes · {summary.TotalStars} ★ · {FormatPlayTime(summary.PlayTimeSeconds)}"
                        : $"Emplacement {i + 1} : vide";
                }
                if (continueButtons[i] != null) continueButtons[i].gameObject.SetActive(summary.Exists);
                if (newButtons[i] != null)
                {
                    TMP_Text label = newButtons[i].GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = summary.Exists ? "Écraser" : "Nouvelle partie";
                }
            }
            Show(pageSlots);
        }

        private static string FormatPlayTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00}";
        }

        private void OnContinue(int slotIndex)
        {
            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (profiles == null || slots == null || profiles.Current == null)
                return;
            if (slots.Load(profiles.Current.id, slotIndex))
                FinishSelection(slotIndex);
        }

        private void OnNew(int slotIndex)
        {
            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            if (profiles == null || slots == null || profiles.Current == null)
                return;

            SlotSummary summary = slots.GetSummary(profiles.Current.id, slotIndex);
            if (summary.Exists)
            {
                // Écrasement : confirmation obligatoire.
                UiBind.SetText(confirmText, $"Écraser l'emplacement {slotIndex} ? La progression sera perdue.");
                pendingConfirm = () => StartNew(slotIndex);
                Show(pageConfirm);
            }
            else
            {
                StartNew(slotIndex);
            }
        }

        private void StartNew(int slotIndex)
        {
            ProfileService profiles = Services.Get<ProfileService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();
            slots.NewGame(profiles.Current.id, slotIndex, Services.Get<GameCatalog>());
            FinishSelection(slotIndex);
        }

        private void FinishSelection(int slotIndex)
        {
            ProfileService profiles = Services.Get<ProfileService>();
            UiBind.SetText(doneText, $"Bonne partie, {profiles.Current.name} !\nEmplacement {slotIndex} chargé.\nRendez-vous au village des archers.");
            GameEvents.RaiseToast("Partie chargée : " + profiles.Current.name);
            if (hiddenUntilOpened)
                Close();
            else
                Show(pageDone);
        }

        private void OnDeleteProfile()
        {
            ProfileService profiles = Services.Get<ProfileService>();
            if (profiles == null || profiles.Current == null)
                return;
            string id = profiles.Current.id;
            UiBind.SetText(confirmText, $"Supprimer le profil « {profiles.Current.name} » et ses 3 emplacements ?");
            pendingConfirm = () =>
            {
                profiles.Delete(id);
                selectedProfileId = null;
                Refresh();
                Show(pageProfiles);
            };
            Show(pageConfirm);
        }
    }
}
