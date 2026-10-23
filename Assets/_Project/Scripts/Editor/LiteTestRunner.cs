// LiteTestRunner : exécute les tests NUnit du projet SANS la fenêtre Test Runner (utile en automatisation).
//
// Menu : Tools → Archery Challenge → Run EditMode Tests (Console). Le résultat s'écrit dans la Console et
// dans Temp/test_results.txt. Les tests PlayMode (UnityTest) s'exécutent en mode Play via PlayModeHost.
// Ce n'est qu'un exécuteur minimal (SetUp, TearDown, Test, TestCase, UnityTest) : la fenêtre officielle
// Window → General → Test Runner reste la référence pour l'équipe.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Exécuteur de tests minimal.</summary>
    public static class LiteTestRunner
    {
        public static string LastReport { get; private set; } = "";

        [MenuItem("Tools/Archery Challenge/Run EditMode Tests (Console)")]
        public static void RunEditModeMenu() => Debug.Log(RunEditMode("ArcheryChallenge.Tests.EditMode"));

        /// <summary>Exécute toutes les méthodes [Test] / [TestCase] d'un assembly de tests.</summary>
        public static string RunEditMode(string assemblyName)
        {
            StringBuilder report = new StringBuilder();
            int passed = 0, failed = 0;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
                return "Assembly introuvable : " + assemblyName;

            foreach (Type type in assembly.GetTypes().Where(t => t.IsClass && t.IsPublic && !t.IsAbstract))
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setUp = methods.FirstOrDefault(m => HasAttribute(m, "SetUpAttribute"));
                MethodInfo tearDown = methods.FirstOrDefault(m => HasAttribute(m, "TearDownAttribute"));

                foreach (MethodInfo method in methods)
                {
                    bool isTest = HasAttribute(method, "TestAttribute");
                    object[] cases = method.GetCustomAttributes(true).Where(a => a.GetType().Name == "TestCaseAttribute").ToArray();
                    if (!isTest && cases.Length == 0)
                        continue;

                    object[][] argumentSets = cases.Length > 0
                        ? cases.Select(c => (object[])c.GetType().GetProperty("Arguments").GetValue(c)).ToArray()
                        : new[] { new object[0] };

                    foreach (object[] args in argumentSets)
                    {
                        object instance = Activator.CreateInstance(type);
                        string name = $"{type.Name}.{method.Name}" + (args.Length > 0 ? "(" + string.Join(", ", args.Select(a => a?.ToString())) + ")" : "");
                        try
                        {
                            setUp?.Invoke(instance, null);
                            try { method.Invoke(instance, args); }
                            finally { tearDown?.Invoke(instance, null); }
                            passed++;
                            report.AppendLine("PASS " + name);
                        }
                        catch (Exception e)
                        {
                            failed++;
                            Exception inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                            report.AppendLine("FAIL " + name + " :: " + inner.Message);
                        }
                    }
                }
            }

            report.AppendLine($"=== EditMode : {passed} réussis, {failed} échoués ===");
            LastReport = report.ToString();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "test_results.txt"), LastReport, Encoding.UTF8);
            return LastReport;
        }

        private static bool HasAttribute(MethodInfo method, string attributeName) =>
            method.GetCustomAttributes(true).Any(a => a.GetType().Name == attributeName);

        // ---------------------------------------------------------------- PlayMode

        /// <summary>Lance les tests [UnityTest] d'un assembly en mode Play (coroutines).</summary>
        public static void RunPlayMode(string assemblyName)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LiteTestRunner] Entrez en mode Play avant de lancer les tests PlayMode.");
                return;
            }
            GameObject host = new GameObject("PlayModeTestHost");
            host.AddComponent<PlayModeHost>().Begin(assemblyName);
        }
    }

    /// <summary>Hôte de coroutines pour les tests PlayMode.</summary>
    public class PlayModeHost : MonoBehaviour
    {
        public static string Report { get; private set; } = "";
        public static bool Done { get; private set; }

        public void Begin(string assemblyName)
        {
            Done = false;
            Report = "";
            StartCoroutine(RunAll(assemblyName));
        }

        private IEnumerator RunAll(string assemblyName)
        {
            StringBuilder report = new StringBuilder();
            int passed = 0, failed = 0;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
            {
                Report = "Assembly introuvable : " + assemblyName;
                Done = true;
                yield break;
            }

            foreach (Type type in assembly.GetTypes().Where(t => t.IsClass && t.IsPublic && !t.IsAbstract))
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setUp = methods.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "SetUpAttribute"));
                MethodInfo tearDown = methods.FirstOrDefault(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "TearDownAttribute"));

                foreach (MethodInfo method in methods.Where(m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "UnityTestAttribute")))
                {
                    object instance = Activator.CreateInstance(type);
                    string name = $"{type.Name}.{method.Name}";
                    string failure = null;
                    try { setUp?.Invoke(instance, null); }
                    catch (Exception e) { failure = Unwrap(e); }

                    if (failure == null)
                    {
                        IEnumerator routine = null;
                        try { routine = (IEnumerator)method.Invoke(instance, null); }
                        catch (Exception e) { failure = Unwrap(e); }

                        while (failure == null && routine != null)
                        {
                            bool hasNext;
                            try { hasNext = routine.MoveNext(); }
                            catch (Exception e) { failure = Unwrap(e); break; }
                            if (!hasNext)
                                break;
                            yield return routine.Current;
                        }
                    }

                    try { tearDown?.Invoke(instance, null); } catch (Exception e) { failure = failure ?? Unwrap(e); }
                    if (failure == null) { passed++; report.AppendLine("PASS " + name); }
                    else { failed++; report.AppendLine("FAIL " + name + " :: " + failure); }
                    yield return null;
                }
            }

            report.AppendLine($"=== PlayMode : {passed} réussis, {failed} échoués ===");
            Report = report.ToString();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "test_results_playmode.txt"), Report, Encoding.UTF8);
            Done = true;
            Destroy(gameObject);
        }

        private static string Unwrap(Exception e) => (e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e).Message;
    }
}
