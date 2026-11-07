#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

/// <summary>Explicit, project-local migration checks. Never submits platform requests.</summary>
[InitializeOnLoad]
public static class Unity66MigrationTools
{
    private const string Root = "Migration/Unity66";
    private const string Save = "Assets/PluginYourGames/Editor/SavesEditorYG2.json";
    private const string Guard = "Vodovorot.Unity66.ProfileGuard";
    private static double nextPoll;
    private static int idleFrames;
    private static IEnumerator sdkChecks;

    [Serializable] private sealed class Request { public string id, action; }
    [Serializable] private sealed class Response { public string id, action, detail; public bool success; }

    static Unity66MigrationTools()
    {
        EditorApplication.update += Tick;
        EditorApplication.quitting += Restore;
        if (SessionState.GetBool("Vodovorot.Unity66.McpReload", false))
            EditorApplication.delayCall += VerifyMcpReload;
    }

    private static void Tick()
    {
        if (sdkChecks != null)
        {
            try { if (!sdkChecks.MoveNext()) sdkChecks = null; }
            catch (Exception error) { sdkChecks = null; Debug.LogException(error); }
        }
        if (SessionState.GetBool(Guard, false))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) idleFrames = 0;
            else if (++idleFrames > 30) Restore();
        }
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        string command = Root + "/command.json";
        if (!File.Exists(command)) return;
        Request request = JsonUtility.FromJson<Request>(File.ReadAllText(command));
        if (request == null || string.IsNullOrEmpty(request.id)) return;
        File.Move(command, Root + "/consumed-" + request.id + ".json");
        var result = new Response { id = request.id, action = request.action, success = true };
        try
        {
            switch (request.action)
            {
                case "state": result.detail = State(); break;
                case "validate": Validate(); result.detail = "Validation completed"; break;
                case "mcp-reload": PrepareMcpReload(); result.detail = "MCP round trips passed; domain reload requested"; break;
                case "mcp-verify": VerifyMcpReload(); result.detail = "MCP reload result written"; break;
                case "sdk":
                    if (!EditorApplication.isPlaying || !SessionState.GetBool(Guard, false))
                        throw new InvalidOperationException("Start the isolated migration Play session first");
                    sdkChecks = SdkChecks();
                    result.detail = "Stock PluginYG simulator checks started";
                    break;
                case "qa":
                    PrepareSilentProfile();
                    Stage2PlayChecks.StartChecks();
                    result.detail = "Isolated silent runtime checks queued";
                    break;
                case "play":
                    PrepareSilentProfile();
                    EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                    EditorApplication.isPlaying = true;
                    result.detail = "Silent disposable profile queued";
                    break;
                case "stop": EditorApplication.isPlaying = false; result.detail = "Stopping; profile restoration follows teardown"; break;
                case "build": Build(); result.detail = "Release WebGL build completed"; break;
                default: throw new ArgumentException("Unsupported migration command");
            }
        }
        catch (Exception error) { result.success = false; result.detail = error.ToString(); Debug.LogException(error); }
        File.WriteAllText(Root + "/result-" + request.id + ".json", JsonUtility.ToJson(result, true));
    }

    public static string State()
    {
        return "Unity=" + Application.unityVersion + "; scene=" + SceneManager.GetActiveScene().path +
            "; playing=" + EditorApplication.isPlaying + "; dirty=" + SceneManager.GetActiveScene().isDirty +
            "; profileGuard=" + SessionState.GetBool(Guard, false) +
            "; SDK=" + YG2.isSDKEnabled + "; music=" + YG2.saves.musicValue + "; sfx=" + YG2.saves.soundValue;
    }

    private static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Requires idle Edit Mode");
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Unsaved scene must be preserved");
    }

    private static void PrepareSilentProfile()
    {
        RequireIdle();
        if (SessionState.GetBool(Guard, false)) throw new InvalidOperationException("A test profile is already active");
        Directory.CreateDirectory(Root);
        File.Copy(Save, Root + "/profile-before-play.json", true);
        SessionState.SetBool(Guard, true);
        idleFrames = 0;
        // A separate temporary Editor profile is silent before the first Awake/Start.
        // The game then applies these values through its ordinary AudioManager settings.
        var fixture = new SavesYG { isFirst = false, tutorialDone = true, done = true,
            selectedMapID = 0, chosenMode = 0, musicValue = 0, soundValue = 0 };
        File.WriteAllText(Save, JsonUtility.ToJson(fixture, true));
    }

    private static void Restore()
    {
        if (!SessionState.GetBool(Guard, false)) return;
        File.Copy(Root + "/profile-before-play.json", Save, true);
        YG2.saves = JsonUtility.FromJson<SavesYG>(File.ReadAllText(Save));
        SessionState.SetBool(Guard, false);
        File.WriteAllText(Root + "/profile-restoration.json", new JObject {
            ["unity"] = Application.unityVersion,
            ["byteExact"] = File.ReadAllBytes(Save).SequenceEqual(File.ReadAllBytes(Root + "/profile-before-play.json")),
            ["music"] = YG2.saves.musicValue, ["sfx"] = YG2.saves.soundValue,
            ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
    }

    public static void Validate()
    {
        RequireIdle();
        Directory.CreateDirectory(Root);
        var problems = new JArray();
        var scenes = new JArray();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                Scene scene = EditorSceneManager.OpenScene(entry.path);
                int missing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
                var brokenMaterials = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                    .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null &&
                        (material.shader == null || material.shader.name == "Hidden/InternalErrorShader" || !material.shader.isSupported))
                    .Select(material => material.name).Distinct().ToArray();
                scenes.Add(new JObject { ["path"] = entry.path, ["roots"] = scene.rootCount,
                    ["missingScripts"] = missing, ["brokenMaterials"] = JArray.FromObject(brokenMaterials) });
                if (missing != 0 || brokenMaterials.Length != 0) problems.Add(entry.path);
            }
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive) &&
                setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        }
        string before = "/Users/ruslanrassulov/Desktop/Games/_MigrationBackups/VODOVOROT_Hole_io-20261106-pre-unity66/Project/" + Save;
        JObject oldSave = JObject.Parse(File.ReadAllText(before));
        JObject roundTrip = JObject.Parse(JsonUtility.ToJson(JsonUtility.FromJson<SavesYG>(oldSave.ToString())));
        string[] lost = oldSave.Properties().Where(property => !JToken.DeepEquals(property.Value, roundTrip[property.Name]))
            .Select(property => property.Name).ToArray();
        if (lost.Length != 0) problems.Add("Old save fields changed: " + string.Join(",", lost));
        Stage2EditorTools.Validate();
        File.WriteAllText(Root + "/validation-" + Application.unityVersion + ".json", new JObject {
            ["unity"] = Application.unityVersion, ["scenes"] = scenes, ["oldSaveChangedFields"] = JArray.FromObject(lost),
            ["renderPipeline"] = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null ? "Built-in" : "SRP",
            ["problems"] = problems, ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
        if (problems.Count != 0) throw new InvalidOperationException(problems.ToString());
    }

    private static void PrepareMcpReload()
    {
        RequireIdle();
        var handles = new System.Collections.Generic.HashSet<int>();
        var temporary = new System.Collections.Generic.List<GameObject>();
        try
        {
            for (int index = 0; index < 256; index++)
            {
                var item = new GameObject("MCP identity QA " + index) { hideFlags = HideFlags.HideAndDontSave };
                temporary.Add(item);
                int handle = MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.GetInstanceIDCompat(item);
                if (handle == 0 || !handles.Add(handle) ||
                    MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.InstanceIDToObjectCompat(handle) != item)
                    throw new InvalidOperationException("MCP lost an object identity");
            }
        }
        finally { foreach (GameObject item in temporary) UnityEngine.Object.DestroyImmediate(item); }
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        SessionState.SetInt("Vodovorot.Unity66.McpHandle", MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.GetInstanceIDCompat(asset));
        File.WriteAllText(Root + "/mcp-before-reload.json", new JObject {
            ["handle"] = SessionState.GetInt("Vodovorot.Unity66.McpHandle", 0),
            ["entityId"] = EntityId.ToULong(asset.GetEntityId()).ToString(),
            ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
        SessionState.SetBool("Vodovorot.Unity66.McpReload", true);
        EditorUtility.RequestScriptReload();
    }

    private static void VerifyMcpReload()
    {
        int handle = (int)JObject.Parse(File.ReadAllText(Root + "/mcp-before-reload.json"))["handle"];
        UnityEngine.Object resolved = MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.InstanceIDToObjectCompat(handle);
        bool passed = resolved != null && AssetDatabase.GetAssetPath(resolved) == "Assets/Scenes/MainMenu.unity" &&
            MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.GetInstanceIDCompat(resolved) == handle;
        File.WriteAllText(Root + "/mcp-domain-reload.json", new JObject {
            ["unity"] = Application.unityVersion, ["passed"] = passed, ["uniqueObjectRoundTrips"] = 256,
            ["handle"] = handle, ["resolvedPath"] = resolved == null ? "" : AssetDatabase.GetAssetPath(resolved),
            ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
        SessionState.SetBool("Vodovorot.Unity66.McpReload", false);
        if (!passed) Debug.LogError("MCP handle failed after domain reload");
    }

    private static IEnumerator SdkChecks()
    {
        var checks = new JArray();
        double until = EditorApplication.timeSinceStartup + 20;
        while (!YG2.isSDKEnabled && EditorApplication.timeSinceStartup < until) yield return null;
        void Check(string name, bool passed) { checks.Add(new JObject { ["name"] = name, ["passed"] = passed }); }
        Check("sdk.initialized", YG2.isSDKEnabled);
        Check("profile.silent", YG2.saves.musicValue == 0 && YG2.saves.soundValue == 0);
        bool oldAdFailure = YG2.infoYG.Simulation.testFailAds;
        bool oldPurchaseFailure = YG2.infoYG.Payments.testBuyFail;
        int opened = 0, closed = 0, rewarded = 0, adErrors = 0, purchased = 0, purchaseErrors = 0;
        Action onOpen = () => opened++;
        Action onClose = () => closed++;
        Action<string> onReward = id => rewarded++;
        Action onAdError = () => adErrors++;
        Action<string> onPurchase = id => purchased++;
        Action<string> onPurchaseError = id => purchaseErrors++;
        YG2.onOpenRewardedAdv += onOpen;
        YG2.onCloseRewardedAdv += onClose;
        YG2.onRewardAdv += onReward;
        YG2.onErrorRewardedAdv += onAdError;
        YG2.onPurchaseSuccess += onPurchase;
        YG2.onPurchaseFailed += onPurchaseError;
        try
        {
            int diamonds = YG2.saves.diamonds;
            YG2.infoYG.Simulation.testFailAds = false;
            YG2.RewardedAdvShow("couple");
            until = EditorApplication.timeSinceStartup + YG2.infoYG.Simulation.loadAdv + YG2.infoYG.Simulation.durationAdv + 1;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check("rewarded.success.once", opened == 1 && closed == 1 && rewarded == 1 && YG2.saves.diamonds == diamonds + 5);
            Check("rewarded.resume", !YG2.nowAdsShow && !YG2.isPauseGame && Time.timeScale == 1);

            diamonds = YG2.saves.diamonds;
            YG2.infoYG.Simulation.testFailAds = true;
            YG2.RewardedAdvShow("couple");
            until = EditorApplication.timeSinceStartup + YG2.infoYG.Simulation.loadAdv + YG2.infoYG.Simulation.durationAdv + 1;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check("rewarded.failure.no-award", adErrors == 1 && rewarded == 1 && YG2.saves.diamonds == diamonds);
            Check("rewarded.failure.resume", !YG2.nowAdsShow && !YG2.isPauseGame && closed == 2);

            YG2.infoYG.Payments.testBuyFail = false;
            YG2.BuyPayments("hand");
            until = EditorApplication.timeSinceStartup + YG2.infoYG.Simulation.loadAdv + YG2.infoYG.Payments.durationPayPanel + 1;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check("purchase.simulated-success", purchased == 1 && YG2.saves.diamonds == diamonds + 20 && !YG2.isPauseGame);

            diamonds = YG2.saves.diamonds;
            YG2.infoYG.Payments.testBuyFail = true;
            YG2.BuyPayments("hand");
            until = EditorApplication.timeSinceStartup + YG2.infoYG.Simulation.loadAdv + YG2.infoYG.Payments.durationPayPanel + 1;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check("purchase.simulated-failure", purchaseErrors == 1 && purchased == 1 && YG2.saves.diamonds == diamonds && !YG2.isPauseGame);

            YG2.SetFocusWindowGame(false);
            Check("focus.handler.hide", !YG2.isFocusWindowGame);
            YG2.SetFocusWindowGame(true);
            Check("focus.handler.show", YG2.isFocusWindowGame);
            YG2.PauseGame(true);
            Check("sdk.pause.callback", YG2.isPauseGame && Time.timeScale == 0 && AudioListener.pause);
            YG2.PauseGame(false);
            Check("sdk.resume.callback", !YG2.isPauseGame && Time.timeScale == 1 && !AudioListener.pause);

            YG2.SaveProgress();
            int savedId = YG2.saves.idSave;
            YG.Insides.YGInsides.LoadEditor();
            Check("storage.save-reload", YG2.saves.idSave == savedId && YG2.saves.diamonds == diamonds &&
                YG2.saves.musicValue == 0 && YG2.saves.soundValue == 0);
        }
        finally
        {
            YG2.infoYG.Simulation.testFailAds = oldAdFailure;
            YG2.infoYG.Payments.testBuyFail = oldPurchaseFailure;
            YG2.onOpenRewardedAdv -= onOpen;
            YG2.onCloseRewardedAdv -= onClose;
            YG2.onRewardAdv -= onReward;
            YG2.onErrorRewardedAdv -= onAdError;
            YG2.onPurchaseSuccess -= onPurchase;
            YG2.onPurchaseFailed -= onPurchaseError;
            File.WriteAllText(Root + "/sdk-simulator.json", new JObject {
                ["unity"] = Application.unityVersion, ["scope"] = "Stock PluginYG Editor simulator and actual Unity callback handlers; isolated local profile; no live platform or charges",
                ["checks"] = checks, ["passed"] = checks.Count == 13 && checks.All(check => (bool)check["passed"]),
                ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
        }
    }

    public static void Build()
    {
        RequireIdle();
        string target = Path.GetFullPath("Builds/Unity66/Yandex");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = target, target = BuildTarget.WebGL, options = BuildOptions.DetailedBuildReport });
        File.WriteAllText(Root + "/webgl-build.json", new JObject {
            ["unity"] = Application.unityVersion, ["result"] = report.summary.result.ToString(),
            ["errors"] = report.summary.totalErrors, ["warnings"] = report.summary.totalWarnings,
            ["bytes"] = report.summary.totalSize, ["seconds"] = report.summary.totalTime.TotalSeconds,
            ["path"] = target, ["finishedUtc"] = DateTime.UtcNow.ToString("o") }.ToString());
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed");
    }

    public static void CheckMcpCompiler()
    {
        RequireIdle();
        object response = MCPForUnity.Editor.Tools.ExecuteCode.HandleCommand(new JObject {
            ["action"] = "execute", ["code"] = "return UnityEngine.Application.unityVersion;" });
        File.WriteAllText(Root + "/mcp-code-execution.json", Newtonsoft.Json.JsonConvert.SerializeObject(response,
            Newtonsoft.Json.Formatting.Indented));
    }
}
#endif
