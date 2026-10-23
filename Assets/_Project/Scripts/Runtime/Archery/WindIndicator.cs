// WindIndicator : bannière de vent qui flotte dans la direction du vent.
//
// OÙ LE POSER : sur la racine du prefab WindIndicator (mât + bannière), placé dans chaque arène.
// RÉGLAGES (tutoriel §P7.6) : « Pivot » ← l'objet qui porte la bannière (enfant à 3,7 m de haut) ;
// « Flutter Amplitude » = 12° ; « Flutter Speed » = 3.
//
// La bannière doit partir du mât vers +Z : le script tourne le pivot pour que son axe +Z pointe dans
// le sens du vent. L'inclinaison de la bannière augmente avec la force du vent, et un léger
// battement la fait onduler.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Bannière qui montre le vent.</summary>
    [DisallowMultipleComponent]
    public class WindIndicator : MonoBehaviour
    {
        [SerializeField] private Transform pivot;
        [SerializeField, Min(0f)] private float flutterAmplitude = 12f;
        [SerializeField, Min(0f)] private float flutterSpeed = 3f;

        private float yaw;
        private float sag; // 0 = horizontale, 80 = bannière qui pend (pas de vent)

        private void Update()
        {
            if (pivot == null)
                return;

            WindService wind = Services.Get<WindService>();
            Vector3 vector = wind != null ? wind.CurrentWind : Vector3.zero;
            float speed = vector.magnitude;

            if (speed > 0.05f)
                yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(vector.x, vector.z) * Mathf.Rad2Deg, Time.deltaTime * 3f);

            // Plus le vent est fort, plus la bannière se tend à l'horizontale.
            float targetSag = Mathf.Lerp(80f, 5f, Mathf.Clamp01(speed / 4f));
            sag = Mathf.Lerp(sag, targetSag, Time.deltaTime * 3f);

            float flutter = Mathf.Sin(Time.time * flutterSpeed) * flutterAmplitude * Mathf.Clamp01(speed / 2f + 0.15f);
            pivot.rotation = Quaternion.Euler(sag, yaw + flutter, 0f);
        }
    }
}
