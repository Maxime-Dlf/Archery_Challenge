// FacePlayer : un objet (texte flottant, panneau d'interface) qui se tourne toujours vers le joueur.
//
// OÙ LE POSER : sur un texte 3D ou un panneau flottant (menu du feu de camp, étiquettes du râtelier, cartels).
// RÉGLAGES : « Yaw Only » (coché : le panneau reste vertical et ne pivote qu'autour de l'axe vertical, plus
// confortable) ; « Max Distance » (au-delà, on ne calcule plus rien).
//
// Convention : un TextMeshPro 3D ou un Canvas en espace monde se lit depuis son côté −Z ; l'objet est donc
// orienté pour que son axe +Z s'éloigne du joueur. Le joueur suivi est la tête active (casque ou caméra PC).

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Oriente un objet vers la tête du joueur.</summary>
    [DisallowMultipleComponent]
    public class FacePlayer : MonoBehaviour
    {
        [SerializeField] private bool yawOnly = true;
        [SerializeField, Min(1f)] private float maxDistance = 30f;

        private void LateUpdate()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (head == null)
                return;

            Vector3 away = transform.position - head.position;
            if (yawOnly)
                away.y = 0f;
            float sqr = away.sqrMagnitude;
            if (sqr < 0.0001f || sqr > maxDistance * maxDistance)
                return;
            transform.rotation = Quaternion.LookRotation(away / Mathf.Sqrt(sqr), Vector3.up);
        }
    }
}
