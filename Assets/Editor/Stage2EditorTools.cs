#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Local, fixed QA/build commands for the already open Editor. No network or production account writes.</summary>
[InitializeOnLoad]
public static class Stage2EditorTools
{
    [Serializable] private sealed class Request { public string id; public string action; public string method; }
    [Serializable] private sealed class Result { public string id; public string action; public bool success; public string detail; }
    private static double nextPoll;
    private static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string CommandPath => Path.Combine(Project, "Library/Stage2QA/command.json");

    static Stage2EditorTools() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        if (!File.Exists(CommandPath)) return;
        Request request;
        try { request = JsonUtility.FromJson<Request>(File.ReadAllText(CommandPath)); }
        catch (Exception) { return; }
        if (request == null || string.IsNullOrEmpty(request.id)) return;
        File.Move(CommandPath, Path.Combine(Path.GetDirectoryName(CommandPath), "consumed-" + request.id + ".json"));
        bool success = true; string detail;
        try
        {
            switch (request.action)
            {
                case "validate": detail = Validate(); break;
                case "play": EditorApplication.isPlaying = true; detail = "Entering Play Mode"; break;
                case "stop": EditorApplication.isPlaying = false; detail = "Leaving Play Mode"; break;
                case "qa": Stage2PlayChecks.StartChecks(); detail = "Runtime checks queued"; break;
                case "screenshot":
                    string image = Path.Combine(Project, "Builds/Stage2QA/editor-game.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(image));
                    ScreenCapture.CaptureScreenshot(image); detail = image; break;
                case "build": detail = Build(); break;
                default: throw new InvalidOperationException("Unsupported local QA command");
            }
        }
        catch (Exception exception) { success = false; detail = exception.ToString(); Debug.LogException(exception); }
        string destination = Path.Combine(Project, "Library/Stage2QA/result-" + request.id + ".json");
        File.WriteAllText(destination, JsonUtility.ToJson(new Result { id=request.id, action=request.action, success=success, detail=detail }, true));
    }

    [MenuItem("Tools/Vodovorot/Stage 2/Validate")]
    public static void ValidateMenu() { Debug.Log(Validate()); }

    public static string Validate()
    {
        string[] required = { "Vodovorot.Stage2.AchievementCard", "Vodovorot.Stage2.Stage2Audio",
            "Vodovorot.Stage2.LeaderboardRoundService", "Vodovorot.Stage2.LeaderboardRoundPanel" };
        foreach (string name in required)
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(name) != null))
                throw new Exception("Missing runtime implementation: " + name);
        foreach (var script in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts/Stage2" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(script);
            if (!File.Exists(path + ".meta")) throw new Exception("Missing meta: " + path);
        }
        string[] checks = { "Stage2LeaderboardValidation", "Stage2AudioValidation", "CascadeValidation" };
        var completed = new System.Collections.Generic.List<string>();
        foreach (string suffix in checks)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly =>
            {
                try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException error) { return error.Types.Where(t=>t!=null).ToArray(); }
            }).FirstOrDefault(candidate => candidate.Name == suffix);
            if (type == null) continue;
            MethodInfo run = type.GetMethod("Run", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? type.GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (run != null && run.GetParameters().Length == 0) { run.Invoke(null,null); completed.Add(suffix); }
        }
        return "Compiled Stage 2 runtime, scripts imported. Invoked: " + string.Join(", ", completed);
    }

    [MenuItem("Tools/Vodovorot/Stage 2/Build WebGL")]
    public static void BuildMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop gameplay before building.");
        string target = Path.Combine(Project, "Builds/Stage2-20261024/Yandex");
        Directory.CreateDirectory(target);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = target,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        string summary = report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings="
            + report.summary.totalWarnings + "; bytes=" + report.summary.totalSize + "; seconds=" + report.summary.totalTime.TotalSeconds.ToString("0");
        Directory.CreateDirectory(Path.Combine(Project, "Builds/Stage2QA"));
        File.WriteAllText(Path.Combine(Project, "Builds/Stage2QA/build.txt"), summary);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception(summary);
        return summary;
    }
}
#endif
