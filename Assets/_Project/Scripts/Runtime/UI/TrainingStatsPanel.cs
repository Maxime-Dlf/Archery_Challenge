// TrainingStatsPanel : panneau de statistiques et de réglages du stand d'entraînement.
//
// OÙ LE POSER : sur le Canvas du prefab « TrainingPanel » (sur Panels/BriefingAnchor d'Arena_Training).
// ÉLÉMENTS ATTENDUS : LastPoints, LastDistance, LastSpeed, LastTension, ShotCount, BowName, ArrowName,
//   WindLabel, BowPrev, BowNext, ArrowPrev, ArrowNext, BtnWind, BtnReset, ImpactMiniMap (avec ImpactMiniMap).
//
// Boutons : Arc ◄ ►, Flèches ◄ ► (tout l'équipement défile, même verrouillé), Vent (aucun → faible →
// moyen → fort), Réinitialiser (statistiques et mini-blason).

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Statistiques de tir en entraînement.</summary>
    public class TrainingStatsPanel : MonoBehaviour
    {
        private static readonly string[] WindNames = { "aucun", "faible", "moyen", "fort" };

        private TrainingMode training;
        private TMP_Text lastPoints, lastDistance, lastSpeed, lastTension, shotCount, bowName, arrowName, windLabel;
        private ImpactMiniMap miniMap;
        private bool bound;

        private void Awake()
        {
            training = GetComponentInParent<TrainingMode>();
            Bind();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            lastPoints = UiBind.Text(transform, "LastPoints", false);
            lastDistance = UiBind.Text(transform, "LastDistance", false);
            lastSpeed = UiBind.Text(transform, "LastSpeed", false);
            lastTension = UiBind.Text(transform, "LastTension", false);
            shotCount = UiBind.Text(transform, "ShotCount", false);
            bowName = UiBind.Text(transform, "BowName", false);
            arrowName = UiBind.Text(transform, "ArrowName", false);
            windLabel = UiBind.Text(transform, "WindLabel", false);
            Transform map = UiBind.Find(transform, "ImpactMiniMap");
            miniMap = map != null ? map.GetComponent<ImpactMiniMap>() : null;

            UiBind.Button(transform, "BowPrev", false)?.onClick.AddListener(() => training?.CycleBow(-1));
            UiBind.Button(transform, "BowNext", false)?.onClick.AddListener(() => training?.CycleBow(1));
            UiBind.Button(transform, "ArrowPrev", false)?.onClick.AddListener(() => training?.CycleArrow(-1));
            UiBind.Button(transform, "ArrowNext", false)?.onClick.AddListener(() => training?.CycleArrow(1));
            UiBind.Button(transform, "BtnWind", false)?.onClick.AddListener(() => training?.CycleWind());
            UiBind.Button(transform, "BtnReset", false)?.onClick.AddListener(() =>
            {
                training?.ResetStats();
                miniMap?.Clear();
            });
        }

        private void OnEnable()
        {
            Bind();
            if (training == null)
                training = GetComponentInParent<TrainingMode>();
            if (training != null)
            {
                training.Changed += Refresh;
                training.ImpactRecorded += OnImpact;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (training != null)
            {
                training.Changed -= Refresh;
                training.ImpactRecorded -= OnImpact;
            }
        }

        private void OnImpact(Vector2 normalized, int ring) => miniMap?.AddImpact(normalized, ring);

        /// <summary>Remet à jour les valeurs affichées.</summary>
        public void Refresh()
        {
            Bind();
            if (training == null)
                return;
            UiBind.SetText(lastPoints, training.ShotCount > 0 ? training.LastPoints.ToString() : "-");
            UiBind.SetText(lastDistance, training.ShotCount > 0 ? $"{training.LastDistance:0.0} m" : "-");
            UiBind.SetText(lastSpeed, training.LastSpeed > 0f ? $"{training.LastSpeed:0.0} m/s" : "-");
            UiBind.SetText(lastTension, training.LastSpeed > 0f ? $"{training.LastTension * 100f:0} %" : "-");
            UiBind.SetText(shotCount, $"{training.ShotCount} tirs · {training.TotalPoints} pts");

            EquipmentService equipment = Services.Get<EquipmentService>();
            if (equipment != null)
            {
                UiBind.SetText(bowName, equipment.SelectedBow != null ? equipment.SelectedBow.displayName : "-");
                UiBind.SetText(arrowName, equipment.SelectedArrow != null ? equipment.SelectedArrow.displayName : "-");
            }
            WindService wind = Services.Get<WindService>();
            if (wind != null)
                UiBind.SetText(windLabel, "Vent : " + WindNames[Mathf.Clamp(wind.IsManual ? wind.ManualLevel : 0, 0, 3)]);
        }
    }
}
