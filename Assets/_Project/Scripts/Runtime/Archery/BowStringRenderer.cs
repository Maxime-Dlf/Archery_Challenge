// BowStringRenderer : dessine la corde de l'arc avec un LineRenderer à 3 points.
//
// OÙ LE POSER : sur l'objet « String » d'un arc, avec un Line Renderer (Positions → Size = 3,
// Width 0,004, Use World Space coché, matériau M_BowString).
// RÉGLAGES : « Top » ← StringTop, « Bottom » ← StringBottom, « Nock » ← NockPoint.
//
// Les arcs du pack GanzSe n'ont pas de corde modélisée : la corde est dessinée par ce script.
// Au repos, les 3 points sont alignés (corde droite) ; quand la main tire, le point du milieu suit
// le point d'encoche et la corde forme un « V ».

using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Corde visible de l'arc.</summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public class BowStringRenderer : MonoBehaviour
    {
        [SerializeField] private Transform top;
        [SerializeField] private Transform bottom;
        [SerializeField] private Transform nock;

        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 3;
            line.useWorldSpace = true;
        }

        private void LateUpdate()
        {
            if (line == null || top == null || bottom == null || nock == null)
                return;
            line.SetPosition(0, top.position);
            line.SetPosition(1, nock.position);
            line.SetPosition(2, bottom.position);
        }
    }
}
