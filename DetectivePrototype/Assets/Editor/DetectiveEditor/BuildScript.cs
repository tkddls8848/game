using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DetectiveEditor
{
    /// <summary>
    /// Windows 빌드를 스크립트로 만든다.
    ///
    /// 왜 스크립트인가: 이 프로젝트의 §18-2는 "손으로만 할 수 있는 일을 남기지 않는다"다.
    /// 지금까지 빌드는 사람이 에디터에서 눌러 만들었고, 그래서 셰이더가 벗겨져 화면이
    /// 자홍색으로 나온 사고를 재현하는 데 시간이 걸렸다. 빌드가 명령 한 줄이면 그 사고를
    /// batchmode에서 바로 잡을 수 있다.
    ///
    /// 빌드 전에 <see cref="RuntimeShaderSetup"/>을 돌린다. UI와 후처리는 실행 중에
    /// 머티리얼을 만들기 때문에, Always Included Shaders에 등록돼 있지 않으면 빌드에서
    /// 벗겨진다 — 에디터에서는 멀쩡하고 빌드에서만 깨지는 종류의 사고다.
    ///
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;proj&gt; \
    ///             -executeMethod DetectiveEditor.BuildScript.BuildWindows
    ///
    /// 종료 코드 0 = 성공. 실패하면 예외를 던져 batchmode가 0이 아닌 코드로 끝난다.
    /// </summary>
    public static class BuildScript
    {
        public const string OutputFolder = "Build";
        public const string ExecutableName = "DetectivePrototype.exe";
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Detective/Build Windows")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
                throw new Exception("씬이 없다: " + ScenePath + " — 먼저 RebuildMainScene을 돌려라.");

            // 실행 중에 붙는 머티리얼이 빌드에서 벗겨지지 않게 먼저 고친다.
            RuntimeShaderSetup.EnsureFromMenu();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, OutputFolder);
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outputDirectory, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log("[BuildScript] " + summary.result
                      + " · " + (summary.totalSize / (1024 * 1024)) + " MB"
                      + " · " + summary.totalTime
                      + " · 오류 " + summary.totalErrors + " · 경고 " + summary.totalWarnings);

            if (summary.result != BuildResult.Succeeded)
                throw new Exception("빌드 실패: " + summary.result + " (오류 " + summary.totalErrors + "건)");

            Debug.Log("[BuildScript] " + options.locationPathName);
        }
    }
}
