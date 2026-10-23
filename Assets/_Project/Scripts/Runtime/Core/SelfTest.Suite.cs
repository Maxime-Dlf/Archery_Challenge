// SelfTest (partie « série complète ») : enchaîne tous les scénarios qui peuvent partager une même session de jeu
// et rassemble les rapports. Les scénarios de tir (clavier/souris et VR) restent à lancer seuls, dans une session neuve.
//
// Lancé par SelfTest.RunSuite() en mode Play ; résultat dans SelfTest.SuiteReport quand SelfTest.SuiteRunning vaut faux.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    public partial class SelfTest
    {
        /// <summary>Vrai tant que la série complète tourne.</summary>
        public static bool SuiteRunning { get; private set; }

        /// <summary>Résumé de la série complète (une ligne par scénario, puis le détail des échecs).</summary>
        public static string SuiteReport { get; private set; } = "";

        /// <summary>Lance la série : interface, ravin, cycle et tremblement, manche complète, entraînement, monde, sauvegarde, rig simulé, panneaux d'arène.</summary>
        public static void RunSuite()
        {
            SelfTest host = new GameObject("SelfTestSuite").AddComponent<SelfTest>();
            host.StartCoroutine(host.Suite());
        }

        private IEnumerator Suite()
        {
            SuiteRunning = true;
            SuiteReport = "";
            StringBuilder summary = new StringBuilder();
            StringBuilder details = new StringBuilder();
            (string name, Func<SelfTest> run)[] scenarios =
            {
                ("Interface et décors", RunInterfaceScenario),
                ("Ravin", RunRavineScenario),
                ("Cycle des cibles et tremblement", RunCycleAndSwayScenario),
                ("Manche complète", RunCampaignRound),
                ("Entraînement", RunTrainingScenario),
                ("Monde", RunWorldScenario),
                ("Sauvegarde", RunSaveScenario),
                ("Rig VR simulé", RunSimulatedRigScenario),
                ("Panneaux d'arène", RunArenaBoardsScenario),
                ("Fenêtre Game zoomée", RunViewportScenario),
                ("Menus et arène 5", RunMenusScenario)
            };
            int totalFailures = 0;
            foreach ((string name, Func<SelfTest> run) in scenarios)
            {
                run();
                float wait = 0f;
                while (Running && wait < 900f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                bool ok = !Running && LastFailures == 0;
                totalFailures += Running ? 1 : Mathf.Max(0, LastFailures);
                summary.AppendLine($"{(ok ? "✔" : "✘")} {name} : {(Running ? "TEMPS DÉPASSÉ" : LastFailures == 0 ? "réussi" : LastFailures + " échec(s)")}");
                if (!ok)
                    details.AppendLine(LastReport);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            SuiteReport = $"=== Série complète : {(totalFailures == 0 ? "RÉUSSIE" : totalFailures + " échec(s)")} ===\n{summary}\n{details}";
            Debug.Log("[SelfTest] " + SuiteReport);
            SuiteRunning = false;
            Destroy(gameObject);
        }
    }
}
#endif
