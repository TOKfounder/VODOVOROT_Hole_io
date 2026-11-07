#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fixed local commands for the review milestone in this project's Editor.</summary>
[InitializeOnLoad]
public static class Stage3ReviewTools
{
    [Serializable] private sealed class Request
    {
        public string id, action, scenario, theme;
        public int width, height;
    }
    [Serializable] private sealed class Result { public string id, action, detail; public bool success; }
    [Serializable] private sealed class MusicClipSpec { public string themeId, file; public float seconds; }
    [Serializable] private sealed class MusicManifest { public int revision; public MusicClipSpec[] clips; }
    private static double nextPoll;
    private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Stage3QA"));

    static Stage3ReviewTools() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .4;
        string command = Path.Combine(DirectoryPath, "command.json");
        if (!File.Exists(command)) return;
        Request request;
        try { request = JsonUtility.FromJson<Request>(File.ReadAllText(command)); }
        catch { return; }
        if (request == null || string.IsNullOrEmpty(request.id)) return;
        File.Move(command, Path.Combine(DirectoryPath, "consumed-" + request.id + ".json"));
        var result = new Result { id = request.id, action = request.action, success = true };
        try
        {
            switch (request.action)
            {
                case "inspect":
                    result.detail = "Project=" + Application.dataPath + "; Unity=" + Application.unityVersion +
                        "; Scene=" + SceneManager.GetActiveScene().path + "; Playing=" + EditorApplication.isPlaying +
                        "; PendingPlay=" + EditorApplication.isPlayingOrWillChangePlaymode +
                        "; Paused=" + EditorApplication.isPaused +
                        "; SaveGuard=" + FindType("Vodovorot.Stage3.RankSimulationContext").GetProperty("BlockEditorSave").GetValue(null);
                    break;
                case "console":
                    Type entries = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
                    Type logEntry = typeof(Editor).Assembly.GetType("UnityEditor.LogEntry");
                    int count = (int)entries.GetMethod("GetCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    var getEntry = entries.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    var logs = new System.Text.StringBuilder();
                    for (int index = Mathf.Max(0, count - 30); index < count; index++)
                    {
                        object entry = Activator.CreateInstance(logEntry);
                        getEntry.Invoke(null, new[] { (object)index, entry });
                        foreach (FieldInfo field in logEntry.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            if (field.Name == "message" || field.Name == "condition" || field.Name == "mode") logs.Append(field.Name).Append('=').Append(field.GetValue(entry)).Append('\n');
                    }
                    result.detail = logs.ToString();
                    break;
                case "menu-bindings":
                    Invoke("Vodovorot.Stage3.Editor.Stage3MenuStyleBindings", "Apply");
                    result.detail = "Bound existing shop text/price/font references; see Builds/Stage3MenuQA/bindings-report.json.";
                    break;
                case "build":
                    Invoke("Stage3BuildTools", "Build");
                    result.detail = "Built local WebGL review, without publishing; see Builds/Stage3BuildQA/build-report.json.";
                    break;
                case "scene-menu-qa":
                    Invoke("SceneAndMenuReview", "StartChecks");
                    result.detail = "Isolated real menu/scene/music review queued; see Builds/Stage3MenuQA/scene-menu-report.json.";
                    break;
                case "scene-menu-stop":
                    Invoke("SceneAndMenuReview", "StopChecks");
                    result.detail = "Stopped scene/menu review and scheduled restoration.";
                    break;
                case "reload-menu":
                    if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                        throw new InvalidOperationException("Reload requires idle Editor and saved scenes.");
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                    result.detail = "Reloaded saved MainMenu for actual binding verification.";
                    break;
                case "cancel-pending":
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Cancel-entry check requires Edit Mode.");
                    Type cancelScenario = FindType("Vodovorot.Stage3.RankSimulationScenario");
                    Invoke("Vodovorot.Stage3.RankSimulationContext", "Start", Enum.Parse(cancelScenario, "Up5"));
                    Invoke("Vodovorot.Stage3.RankSimulationContext", "Stop");
                    result.detail = "Queued simulation entry and cancelled it in the same Editor update; inspect the settled Editor state next.";
                    break;
                case "prepare":
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Prepare from Edit Mode.");
                    Invoke("Vodovorot.Stage3.Editor.RankingVisualStyleBuilder", "Build");
                    Invoke("Vodovorot.Stage3.Editor.RankSimulationSetup", "SeedVisualTestTable");
                    result.detail = "Prepared explicit game-style resources and PluginYG visual-test table.";
                    break;
                case "skin-build":
                    Invoke("Vodovorot.Stage3.Editor.SkinVisualCatalogBuilder", "Build");
                    result.detail = "Built complete shop visual catalogue; existing consumers unchanged.";
                    break;
                case "skin-check":
                    Invoke("Vodovorot.Stage3.Editor.SkinVisualCatalogReview", "Run");
                    result.detail = "Catalogue foundation check complete; see Builds/Stage3SkinQA/catalog-report.json.";
                    break;
                case "card-check":
                    Invoke("AchievementVisualReview", "Run");
                    result.detail = "Rendered all five complete skins in both localized achievement cards; see Builds/Stage3CardQA.";
                    break;
                case "music-check":
                    MusicManifest music = JsonUtility.FromJson<MusicManifest>(File.ReadAllText("Assets/Audio/Stage3/Stage3MusicDemoManifest.json"));
                    if (music.clips == null || music.clips.Length != 4) throw new InvalidOperationException("Expected four current music demos.");
                    result.detail = "Unity import, music revision " + music.revision + ": ";
                    foreach (MusicClipSpec spec in music.clips)
                    {
                        AudioClip clip = Resources.Load<AudioClip>("Stage3Music/" + Path.GetFileNameWithoutExtension(spec.file));
                        var importer = AssetImporter.GetAtPath("Assets/Editor/Stage3/MusicAuditions/Resources/Stage3Music/" + spec.file) as AudioImporter;
                        if (clip == null || Mathf.Abs(clip.length - spec.seconds) > .002f || clip.channels != 2 || clip.frequency != 44100 ||
                            importer == null || importer.defaultSampleSettings.compressionFormat != AudioCompressionFormat.Vorbis)
                            throw new InvalidOperationException("Current demo failed actual Unity import: " + spec.themeId);
                        result.detail += spec.themeId + "=" + clip.length.ToString("0.00") + "s stereo Vorbis; ";
                    }
                    break;
                case "qa":
                    Invoke("Stage3ReviewChecks", "StartChecks");
                    result.detail = "Isolated integration checks queued; see Builds/Stage3QA/runtime-report.json for completion.";
                    break;
                case "show":
                    Type scenario = FindType("Vodovorot.Stage3.RankSimulationScenario");
                    Invoke("Vodovorot.Stage3.RankSimulationContext", "Start", Enum.Parse(scenario, request.scenario ?? "Up5"));
                    Invoke("Vodovorot.Stage3.RankSimulationContext", "ShowImmediate");
                    result.detail = "Opened explicitly labeled ranking prototype: " + request.scenario;
                    break;
                case "window":
                    result.success = EditorApplication.ExecuteMenuItem("Tools/Vodovorot/Проверка рейтинга");
                    result.detail = "Ranking review window opened.";
                    break;
                case "theme":
                    Invoke("Vodovorot.Stage3.BackgroundMusicPlayer", "PlayPreviewTheme", request.theme ?? "menu");
                    result.detail = "Explicit demo audition: " + request.theme;
                    break;
                case "stop":
                    Invoke("Vodovorot.Stage3.RankSimulationContext", "Stop");
                    Invoke("Vodovorot.Stage3.BackgroundMusicPlayer", "StopPreview");
                    result.detail = "Review simulation stopped and originals restored.";
                    break;
                default: throw new InvalidOperationException("Unsupported local review command.");
            }
        }
        catch (Exception error)
        {
            result.success = false; result.detail = error.ToString(); Debug.LogException(error);
        }
        File.WriteAllText(Path.Combine(DirectoryPath, "result-" + request.id + ".json"), JsonUtility.ToJson(result, true));
    }

    public static Type FindType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name))
            .FirstOrDefault(type => type != null) ?? throw new InvalidOperationException("Type unavailable: " + name);
    }

    private static object Invoke(string name, string method, params object[] args)
    {
        Type type = FindType(name);
        MethodInfo target = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(item => item.Name == method && item.GetParameters().Length == args.Length);
        if (target == null) throw new MissingMethodException(name, method);
        return target.Invoke(null, args);
    }
}
#endif
