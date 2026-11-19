using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LiarsBar.Editor
{
    /// <summary>
    /// Запуск EditMode-тестов из меню. Результат пишется в консоль и в Temp/liarsbar_tests.txt,
    /// чтобы его могли прочитать внешние инструменты (CI, MCP).
    /// </summary>
    public static class TestMenu
    {
        private const string ResultPath = "Temp/liarsbar_tests.txt";

        [MenuItem("LiarsBar/Tests/Run EditMode Tests")]
        public static void RunEditMode()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{result.TestStatus} pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount} time={result.Duration:0.00}s");
                CollectFailures(result, sb);

                var text = sb.ToString();
                File.WriteAllText(ResultPath, text);
                if (result.FailCount > 0) Debug.LogError("[Tests] " + text);
                else Debug.Log("[Tests] " + text);
            }

            private static void CollectFailures(ITestResultAdaptor result, StringBuilder sb)
            {
                if (result.HasChildren)
                {
                    foreach (var child in result.Children) CollectFailures(child, sb);
                }
                else if (result.TestStatus == TestStatus.Failed)
                {
                    sb.AppendLine($"FAIL {result.FullName}: {result.Message}");
                }
            }
        }
    }
}
