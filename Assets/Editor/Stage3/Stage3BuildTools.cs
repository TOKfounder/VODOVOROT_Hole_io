#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Stage3BuildTools
{
    [Serializable] private sealed class MusicAsset { public string path; public ulong packedBytes; }
    [Serializable] private sealed class Result
    {
        public string unityVersion, finishedUtc, result;
        public ulong totalBytes, musicPackedBytes;
        public double seconds;
        public int errors, warnings;
        public bool noEditorDemos, musicWithinBudget;
        public MusicAsset[] music;
    }
    [MenuItem("Tools/Vodovorot/Stage 3/Build WebGL review")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Build from idle Edit Mode.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Build does not discard or save an unsaved scene.");
        string target = Path.GetFullPath("Builds/Stage3-20261025/Yandex"); Directory.CreateDirectory(target);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            // Incremental builds can omit packedAssets; acceptance needs a fresh, measurable budget.
            locationPathName = target, target = BuildTarget.WebGL,
            options = BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache });
        var music = report.packedAssets.SelectMany(pack => pack.contents)
            .Where(asset => asset.sourceAssetPath.Contains("/Stage3Music/") && asset.sourceAssetPath.EndsWith(".wav"))
            .GroupBy(asset => asset.sourceAssetPath).Select(group => new MusicAsset { path = group.Key, packedBytes = (ulong)group.Sum(asset => (decimal)asset.packedSize) }).ToArray();
        ulong bytes = (ulong)music.Sum(asset => (decimal)asset.packedBytes);
        var result = new Result { unityVersion = Application.unityVersion, finishedUtc = DateTime.UtcNow.ToString("o"), result = report.summary.result.ToString(),
            totalBytes = report.summary.totalSize, seconds = report.summary.totalTime.TotalSeconds, errors = report.summary.totalErrors,
            warnings = report.summary.totalWarnings, music = music, musicPackedBytes = bytes,
            noEditorDemos = music.Length == 4 && music.All(asset => Path.GetFileName(asset.path).StartsWith("Full")),
            musicWithinBudget = bytes <= 6000000 };
        Directory.CreateDirectory("Builds/Stage3BuildQA");
        File.WriteAllText("Builds/Stage3BuildQA/build-report.json", JsonUtility.ToJson(result, true));
        if (report.summary.result != BuildResult.Succeeded || !result.noEditorDemos || !result.musicWithinBudget)
            throw new InvalidOperationException("Stage3 build/budget check failed; see Builds/Stage3BuildQA/build-report.json.");
    }
}
#endif
