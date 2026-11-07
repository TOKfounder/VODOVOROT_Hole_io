#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vodovorot.Stage2;
using YG;

/// <summary>Editor-only, isolated-profile gameplay QA. Start explicitly through the local Editor bridge.</summary>
[InitializeOnLoad]
public sealed class Stage2PlayChecks : MonoBehaviour
{
    private const string ActiveKey = "Vodovorot.Stage2QA.Active";
    private const string ExistsKey = "Vodovorot.Stage2QA.SaveExisted";
    private const string MetaKey = "Vodovorot.Stage2QA.MetaExisted";
    private const string ProfileKey = "Vodovorot.Stage2QA.ProfileCaptured";
    private const string StartedKey = "Vodovorot.Stage2QA.EditorStarted";
    private const string MenuPath = "Assets/Scenes/MainMenu.unity";
    private static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string BackupDirectory => Path.Combine(Project, "Library/Stage2QA");
    private static string OutputDirectory => Path.Combine(Project, "Builds/Stage2QA");
    private static string SavePath => Path.Combine(Project, "Assets/PluginYourGames/Editor/SavesEditorYG2.json");
    private static string BackupPath => Path.Combine(BackupDirectory, "editor-saves-original.bin");
    private static string ProfilePath => Path.Combine(BackupDirectory, "runtime-profile-original.json");

    [Serializable] private sealed class CheckResult { public string name; public bool passed; public string detail; }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, unityVersion;
        public bool passed, completed;
        public string scope = "Editor runtime; offline SDK simulation, no production account calls; real City physics. Player teleports, 55% reward progress and timer advancement are QA fixtures.";
        public List<CheckResult> checks = new List<CheckResult>();
        public List<string> errors = new List<string>();
    }

    [Serializable] private sealed class CameraDiagnostic
    {
        public string stage, scene, player, body, mainCamera;
        public Vector3 playerPosition, playerScale, bodyPosition, siteCenter;
        public float timeScale;
        public int worldRenderers, enabledWorldRenderers, mainFrustumRenderers;
        public string forwardRay;
        public List<CameraState> cameras = new List<CameraState>();
        public List<string> compositionRenderers = new List<string>();
    }
    [Serializable] private sealed class CameraState
    {
        public string name, parent, targetTexture, clearFlags;
        public Vector3 position, localPosition, rotation, scale, siteViewport;
        public Color background;
        public int cullingMask, targetDisplay;
        public bool enabled, active;
        public float nearClip, farClip, depth;
    }

    private Report report;
    private bool finished;
    private BlackHoleController player;
    private GamingManager game;

    static Stage2PlayChecks()
    {
        EditorApplication.playModeStateChanged += PlayStateChanged;
        EditorApplication.quitting += RestoreFiles;
        EditorApplication.update += Watchdog;
        // Also recover the local save after an interrupted Editor run/domain reload.
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(ActiveKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                RestoreAfterStop();
        };
    }

    [MenuItem("Tools/Vodovorot/Stage 2/Run isolated runtime checks")]
    public static void StartChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run QA from Edit Mode.");
        if (SessionState.GetBool(ActiveKey, false)) RestoreAfterStop();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("QA will not discard an unsaved scene. Save/revert it before running QA.");
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(OutputDirectory);
        bool existed = File.Exists(SavePath), metaExisted = File.Exists(SavePath + ".meta");
        if (existed) File.WriteAllBytes(BackupPath, File.ReadAllBytes(SavePath));
        else if (File.Exists(BackupPath)) File.Delete(BackupPath);
        if (metaExisted) File.WriteAllBytes(BackupPath + ".meta", File.ReadAllBytes(SavePath + ".meta"));
        SessionState.SetBool(ExistsKey, existed);
        SessionState.SetBool(MetaKey, metaExisted);
        SessionState.SetBool(ProfileKey, false);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
        if (SceneManager.GetActiveScene().path != MenuPath) EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        FocusGame();
        EditorApplication.isPlaying = true;
    }

    private static void Watchdog()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, (float)EditorApplication.timeSinceStartup) < 240) return;
        Debug.LogError("[Stage2QA] Runtime watchdog stopped a stalled check after 240 seconds; the original local save will be restored.");
        EditorApplication.isPlaying = false;
    }

    private static void FocusGame()
    {
        Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (type == null) return;
        EditorWindow window = EditorWindow.GetWindow(type);
        window.Show(); window.Focus();
    }

    private static void PlayStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (FindAnyObjectByType<Stage2PlayChecks>() == null)
            {
                var root = new GameObject("Stage2 isolated runtime QA");
                DontDestroyOnLoad(root);
                root.AddComponent<Stage2PlayChecks>();
            }
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            RestoreRuntimeProfile();
            RestoreFiles();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreAfterStop();
            // The SDK may save during teardown after ExitingPlayMode. Restore again last.
            EditorApplication.delayCall += RestoreAfterStop;
        }
    }

    private static void RestoreRuntimeProfile()
    {
        if (SessionState.GetBool(ProfileKey, false) && File.Exists(ProfilePath))
            YG2.saves = JsonUtility.FromJson<SavesYG>(File.ReadAllText(ProfilePath));
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private static void RestoreFiles()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (SessionState.GetBool(ExistsKey, false) && File.Exists(BackupPath))
            File.WriteAllBytes(SavePath, File.ReadAllBytes(BackupPath));
        else if (!SessionState.GetBool(ExistsKey, false) && File.Exists(SavePath)) File.Delete(SavePath);
        if (SessionState.GetBool(MetaKey, false) && File.Exists(BackupPath + ".meta"))
            File.WriteAllBytes(SavePath + ".meta", File.ReadAllBytes(BackupPath + ".meta"));
        else if (!SessionState.GetBool(MetaKey, false) && File.Exists(SavePath + ".meta")) File.Delete(SavePath + ".meta");
    }

    private static void RestoreAfterStop()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        RestoreRuntimeProfile(); RestoreFiles();
        bool restored = !SessionState.GetBool(ExistsKey, false)
            ? !File.Exists(SavePath)
            : File.Exists(SavePath) && File.ReadAllBytes(SavePath).SequenceEqual(File.ReadAllBytes(BackupPath));
        Directory.CreateDirectory(BackupDirectory);
        File.WriteAllText(Path.Combine(BackupDirectory, "restoration.json"),
            "{\"editorSaveByteExact\":" + (restored ? "true" : "false") + ",\"productionCalls\":false}");
        // Keep the guard through a delayed restoration to cover SDK teardown.
        EditorApplication.delayCall += () =>
        {
            RestoreFiles();
            SessionState.SetBool(ActiveKey, false);
            if (!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().path != MenuPath &&
                !Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        };
    }

    private void Start()
    {
        report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion };
        Application.logMessageReceived += CaptureError;
        StartCoroutine(RunSafely());
    }

    private void CaptureError(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.errors.Add(message + "\n" + trace);
    }

    private IEnumerator RunSafely()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(RunChecks());
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { Check("runner.exception", false, error.ToString()); break; }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested);
            else yield return next;
        }
        // Even failed assertions return through the real main menu before restoring data.
        Time.timeScale = 1f;
        AsyncOperation menu = SceneManager.LoadSceneAsync("MainMenu");
        while (menu != null && !menu.isDone) yield return null;
        yield return Frames(6);
        RestoreRuntimeProfile();
        VodovorotGameManager.Instance?.GameController?.UpdateAllUI();
        Check("runtime.no-errors", report.errors.Count == 0, string.Join("\n", report.errors));
        report.completed = true; finished = true; WriteReport();
        Application.logMessageReceived -= CaptureError;
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    private IEnumerator RunChecks()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!YG2.isSDKEnabled && Time.realtimeSinceStartup < deadline) yield return null;
        Require("sdk.ready", YG2.isSDKEnabled && YG2.saves != null, "SDK initialized before profile fixture.");
        File.WriteAllText(ProfilePath, JsonUtility.ToJson(YG2.saves, true));
        SessionState.SetBool(ProfileKey, true);
        // A disposable local profile. No scores, identities, purchases or history from the
        // developer's profile are used as test fixtures, and Editor SDK cannot submit online.
        YG2.saves = new SavesYG { isFirst = false, tutorialDone = true, langRu = true,
            selectedMapID = 0, chosenMode = 0, equipedMaterial = 2, exp = 100, goldCoins = 17, diamonds = 3,
            massiveOfObtaining = new[] { 1, 1, 1, 0, 0 }, musicValue = 0, soundValue = 0 };
        AudioManager audioSettings = FindAnyObjectByType<AudioManager>();
        if (audioSettings != null)
        {
            audioSettings.MusicSlider.value = 0;
            audioSettings.SoundSlider.value = 0;
        }
        Check("profile.isolated-offline", !LeaderboardRoundService.CanUsePlatform, "Editor platform requests disabled; disposable QA profile.");
        AsyncOperation city = SceneManager.LoadSceneAsync(CascadeCityLayout.CitySceneName);
        while (!city.isDone) yield return null;
        yield return Frames(10);
        game = VodovorotGameManager.Instance != null ? VodovorotGameManager.Instance.GamingManager : null;
        player = FindAnyObjectByType<BlackHoleController>();
        Require("city.live", game != null && player != null && game.IsTotalCleaningMode,
            "Real City scene, player and total-cleaning controller.");
        game.HandleTimer(false);
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour.GetType().Name == "MovementScript") behaviour.enabled = false;
        var layout = FindAnyObjectByType<CascadeCityLayout>();
        Check("city.compositions", layout != null && layout.Applied && layout.ArrangedCount == 16,
            layout != null ? "Applied=" + layout.Applied + "; count=" + layout.ArrangedCount + "; site=" + layout.SiteCenter : "No layout component.");
        string invariant = "Layout not applied";
        Check("city.value-and-geometry", layout != null && layout.ValidateInitialState(out invariant), invariant ?? "Original map total/value/fit geometry preserved.");
        bool isolatedPhysics = CascadePhysics.ValidateConfiguration(out string physicsError);
        CascadeProp[] arranged = FindObjectsByType<CascadeProp>();
        Check("city.isolated-cascade-collisions", isolatedPhysics && arranged.Length == 16 &&
            arranged.All(member => member.gameObject.layer == CascadePhysics.Layer &&
                member.GetComponentsInChildren<Collider>(true).Where(collider => collider.GetComponentInParent<FallingObject>() == member.GetComponent<FallingObject>())
                .All(collider => collider.gameObject.layer == CascadePhysics.Layer)),
            physicsError ?? "Only the 16 arranged props use CityCascade; its self collision is on and original layer 7 self collision remains off.");
        Check("city.cascade-query-masks", (CascadePhysics.IncludeInFallableMask(1 << 7) & (1 << CascadePhysics.Layer)) != 0 &&
            CascadePhysics.IncludeInFallableMask(1 << 6) == (1 << 6), "AI masks that include original Fallable also include CityCascade; other masks remain unchanged.");
        Check("city.fit-boundary", !Tool.CanFit2D(new Vector3(.6f, .6f, .6f), new Vector3(.599f, 1, .599f)) &&
            Tool.CanFit2D(new Vector3(.6f, .6f, .6f), new Vector3(.6f, 1, .6f)), "Exact original 2D fit boundary remains inclusive; smaller hole rejects.");
        DiagnoseCameras("city-before-teleport", layout);
        yield return Capture("city-before-teleport.png", true);
        if (layout != null && layout.Applied) MovePlayer(layout.SiteCenter + new Vector3(0, 0, -4f));
        DiagnoseCameras("city-immediately-after-teleport", layout);
        // Do not freeze a just-teleported Rigidbody/camera before its next physics/render
        // update. The production camera is untouched; only the QA capture waits.
        yield return new WaitForFixedUpdate();
        yield return Frames(2);
        DiagnoseCameras("city-after-two-live-frames", layout);
        yield return Capture("city-compositions.png", true);

        yield return CheckActualAbsorption();
        yield return CheckTooLarge();
        yield return CheckSupportFall();

        // After the actual-physics checks, use a nonzero reward fixture to verify settlement
        // and duplicate callbacks. This is deliberately separate from physical absorption QA.
        HoleParent.totalScore = Mathf.CeilToInt(game.AllValues * .55f);
        int xp = YG2.saves.exp, coins = YG2.saves.goldCoins, diamonds = YG2.saves.diamonds;
        game.timer = game.totalCleaningDuration - .005f; game.HandleTimer(true);
        Time.timeScale = 1f;
        yield return Frames(12);
        var settled = game.SettledReward;
        Check("round.natural-end", game.Stage2NaturalCompletion && game.HasRewardBeenApplied && game.IsStage2RankRewardEligible,
            "Timer-driven completion=" + game.Stage2NaturalCompletion + "; useful absorbed value=" + game.Stage2PlayerAbsorbedValue);
        Check("round.base-reward", settled.exp == 25 && settled.coins == 15 && settled.diamonds == 3 &&
            YG2.saves.exp - xp == settled.exp && YG2.saves.goldCoins - coins == settled.coins &&
            YG2.saves.diamonds - diamonds == settled.diamonds, "Actual deltas XP/coins/diamonds=" + (YG2.saves.exp - xp) + "/" +
            (YG2.saves.goldCoins - coins) + "/" + (YG2.saves.diamonds - diamonds) + "; settled=" + JsonUtility.ToJson(settled));
        int paidXp = YG2.saves.exp, paidCoins = YG2.saves.goldCoins, paidDiamonds = YG2.saves.diamonds;
        game.EndOfGame(); game.ApplyCurrentTotalCleaningRewardIfNeeded(); game.FinalizeInterruptedMatchIfNeeded();
        yield return Frames(3);
        Check("round.reward-once", YG2.saves.exp == paidXp && YG2.saves.goldCoins == paidCoins && YG2.saves.diamonds == paidDiamonds,
            "Repeated result/exit lifecycle did not pay the base reward again.");
        LeaderboardRoundPanel.Show(game.Stage2RoundId, game.IsStage2RankRewardEligible);
        yield return Frames(3);
        LeaderboardRoundSnapshot rank = LeaderboardRoundService.Current;
        Check("ranking.offline-no-fake-position", rank != null && rank.status == LeaderboardRoundStatus.Unavailable && rank.rank == 0 &&
            rank.bonusCoins == 0 && !LeaderboardRoundService.TryGetConfirmedRank(out _),
            rank != null ? "Status=" + rank.status + "; rank=" + rank.rank + "; bonus=" + rank.bonusCoins : "No offline snapshot.");
        yield return Capture("result-ranking-offline.png", false);
        yield return Stage2LeaderboardViewChecks.Run(game);
        LeaderboardRoundPanel.Close();
        yield return Capture("result-base.png", false);
        AchievementCard.CardData data = AchievementCard.BuildData(game);
        File.WriteAllText(Path.Combine(OutputDirectory, "achievement-card-data.json"), JsonUtility.ToJson(data, true));
        Check("card.data", data.rank == 0 && data.xp == YG2.saves.exp && data.percent.EndsWith("%") &&
            !string.IsNullOrEmpty(data.skin) && data.url.StartsWith("https://yandex.ru/games/app/"), JsonUtility.ToJson(data));
        Texture2D portrait = AchievementCard.RenderEquippedSkin();
        bool visible = false;
        if (portrait != null)
        {
            Color32[] pixels = portrait.GetPixels32();
            int opaque = pixels.Count(pixel => pixel.a > 16);
            visible = portrait.width == 512 && portrait.height == 512 && opaque > 100 && opaque < pixels.Length;
            File.WriteAllBytes(Path.Combine(OutputDirectory, "equipped-skin-portrait.png"), portrait.EncodeToPNG());
            Destroy(portrait);
        }
        Check("card.equipped-mesh-portrait", visible, "512x512 rendered native mesh, nonempty alpha silhouette; PNG saved.");
        for (int skin = 0; skin < 5; skin++)
        {
            Texture2D renderedSkin = AchievementCard.RenderEquippedSkin(skin);
            int covered = renderedSkin != null ? renderedSkin.GetPixels32().Count(pixel => pixel.a > 16) : 0;
            Check("card.skin-" + skin + "-portrait", renderedSkin != null && covered > 1000 && covered < 512 * 512,
                "Actual equipped mesh/material; visible alpha pixels=" + covered);
            if (renderedSkin != null)
            {
                File.WriteAllBytes(Path.Combine(OutputDirectory, "portrait-skin-" + skin + ".png"), renderedSkin.EncodeToPNG());
                Destroy(renderedSkin);
            }
        }
        AchievementCard.Show(game);
        yield return Frames(4);
        Check("card.native-overlay", FindAnyObjectByType<AchievementCard>() != null,
            "Native Unity card opened with settled data; no external browser/share request.");
        yield return Capture("achievement-card.png", false);
    }

    private IEnumerator CheckActualAbsorption()
    {
        player.InitializeScore(0, true);
        FallingObject target = FindObjectsByType<FallingObject>().Where(item =>
            item.value > 0 && !item.isColon && item.GetComponent<CascadeProp>() == null &&
            item.GetComponent<Rigidbody>() != null && item.GetComponent<Rigidbody>().isKinematic &&
            item.GetComponent<Collider>() != null && item.GetComponent<Collider>().bounds.min.y < .2f &&
            Tool.CanFit2D(item.size, player.GetFitSize())).OrderBy(item => (item.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        Require("absorption.target", target != null, "Existing ground-level prop fits the actual starting hole.");
        int value = target.value, before = HoleParent.totalScore;
        Vector3 center = target.GetComponent<Collider>().bounds.center;
        MovePlayer(center);
        float end = Time.realtimeSinceStartup + 7f;
        while (target.value > 0 && Time.realtimeSinceStartup < end) yield return null;
        Check("absorption.real-physics", target.value == 0 && HoleParent.totalScore >= before + value && game.Stage2PlayerAbsorbedValue >= value,
            "Target=" + target.name + "; original value=" + value + "; remaining=" + target.value + "; score delta=" + (HoleParent.totalScore - before));
    }

    private IEnumerator CheckTooLarge()
    {
        FallingObject large = FindObjectsByType<FallingObject>().Where(item =>
            item.value > 0 && !item.isColon && item.GetComponent<CascadeProp>() == null &&
            item.GetComponent<Rigidbody>() != null && item.GetComponent<Rigidbody>().isKinematic &&
            item.GetComponent<Collider>() != null && item.GetComponent<Collider>().bounds.min.y < .3f &&
            !Tool.CanFit2D(item.size, player.GetFitSize()) && item.size.x > player.GetFitSize().x * 3f &&
            item.size.y > Mathf.Max(player.GetFitSize().x, player.GetFitSize().z) * 3f && item.size.z > player.GetFitSize().z * 3f)
            .OrderBy(item => (item.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        Require("fit.too-large-fixture", large != null, "Existing prop is too large under the original cached dimensions.");
        Vector3 position = large.transform.position; int value = large.value;
        MovePlayer(large.GetComponent<Collider>().bounds.center);
        float until = Time.realtimeSinceStartup + .8f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Check("fit.too-large-immovable", large.value == value && large.GetComponent<Rigidbody>().isKinematic &&
            Vector3.Distance(position, large.transform.position) < .002f,
            "Target=" + large.name + "; retained value=" + large.value + "; kinematic=" + large.GetComponent<Rigidbody>().isKinematic);
    }

    private IEnumerator CheckSupportFall()
    {
        FieldInfo supportField = typeof(CascadeProp).GetField("supports", BindingFlags.NonPublic | BindingFlags.Instance);
        CascadeProp upper = FindObjectsByType<CascadeProp>().FirstOrDefault(member =>
        {
            var supports = (FallingObject[])supportField.GetValue(member);
            return supports != null && supports.Length == 1 && member.GetComponent<FallingObject>().value > 0 && supports[0].value > 0;
        });
        if (upper == null) { Check("cascade.support-fixture", false, "No intact arranged two-item support pair."); yield break; }
        FallingObject top = upper.GetComponent<FallingObject>(), bottom = ((FallingObject[])supportField.GetValue(upper))[0];
        player.InitializeScore(795, true);
        bool fits = Tool.CanFit2D(top.size, player.GetFitSize()) && Tool.CanFit2D(bottom.size, player.GetFitSize());
        Require("cascade.original-fit", fits, "Support and upper prop fit after the normal level-3 growth fixture; dimensions untouched.");
        float bottomY = bottom.transform.position.y, topY = top.transform.position.y;
        float firstBottom = -1, firstTop = -1, start = Time.realtimeSinceStartup;
        MovePlayer(bottom.GetComponent<Collider>().bounds.center);
        while (Time.realtimeSinceStartup - start < 7f && (bottom.value > 0 || top.value > 0))
        {
            if (firstBottom < 0 && (bottom.value <= 0 || bottom.transform.position.y < bottomY - .015f)) firstBottom = Time.realtimeSinceStartup - start;
            if (firstTop < 0 && (top.value <= 0 || top.transform.position.y < topY - .015f)) firstTop = Time.realtimeSinceStartup - start;
            yield return null;
        }
        Check("cascade.sequential-support-fall", firstBottom >= 0 && firstTop >= 0 && firstTop + .05f >= firstBottom && top.value == 0 && bottom.value == 0,
            "Real physics timestamps bottom/top=" + firstBottom.ToString("0.000") + "/" + firstTop.ToString("0.000") +
            " seconds; remaining values=" + bottom.value + "/" + top.value + ". Same physics-step fall is allowed; no direct score call.");
    }

    private void MovePlayer(Vector3 target)
    {
        target.y = player.transform.position.y;
        Rigidbody body = player.GetComponentInChildren<Rigidbody>();
        if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.position = target; }
        player.transform.position = target;
        Physics.SyncTransforms();
    }

    private void DiagnoseCameras(string stage, CascadeCityLayout layout)
    {
        Camera main = Camera.main;
        Rigidbody body = player != null ? player.GetComponentInChildren<Rigidbody>() : null;
        var diagnostic = new CameraDiagnostic { stage = stage, scene = SceneManager.GetActiveScene().name,
            player = player != null ? player.name : "null", body = body != null ? body.name : "null",
            playerPosition = player != null ? player.transform.position : Vector3.zero,
            playerScale = player != null ? player.transform.lossyScale : Vector3.zero,
            bodyPosition = body != null ? body.position : Vector3.zero,
            siteCenter = layout != null ? layout.SiteCenter : Vector3.zero,
            mainCamera = main != null ? main.name : "null", timeScale = Time.timeScale };
        foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (!camera.gameObject.scene.IsValid()) continue;
            diagnostic.cameras.Add(new CameraState { name = camera.name,
                parent = camera.transform.parent != null ? camera.transform.parent.name : "ROOT",
                position = camera.transform.position, localPosition = camera.transform.localPosition,
                rotation = camera.transform.eulerAngles, scale = camera.transform.lossyScale,
                enabled = camera.enabled, active = camera.gameObject.activeInHierarchy,
                cullingMask = camera.cullingMask, targetTexture = camera.targetTexture != null ? camera.targetTexture.name : "null",
                targetDisplay = camera.targetDisplay, clearFlags = camera.clearFlags.ToString(), background = camera.backgroundColor,
                nearClip = camera.nearClipPlane, farClip = camera.farClipPlane, depth = camera.depth,
                siteViewport = camera.WorldToViewportPoint(diagnostic.siteCenter) });
        }
        Plane[] frustum = main != null ? GeometryUtility.CalculateFrustumPlanes(main) : null;
        foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>())
        {
            if (renderer.gameObject.scene != SceneManager.GetActiveScene() || renderer.GetComponentInParent<HoleParent>() != null) continue;
            diagnostic.worldRenderers++;
            if (!renderer.enabled) continue;
            diagnostic.enabledWorldRenderers++;
            if (frustum != null && GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) diagnostic.mainFrustumRenderers++;
            if (renderer.GetComponent<CascadeProp>() != null)
                diagnostic.compositionRenderers.Add(renderer.name + " pos=" + renderer.bounds.center + " size=" + renderer.bounds.size +
                    " layer=" + renderer.gameObject.layer + " visible=" + renderer.isVisible + " viewport=" +
                    (main != null ? main.WorldToViewportPoint(renderer.bounds.center).ToString() : "no camera") + " materials=" +
                    string.Join(";", renderer.sharedMaterials.Select(material => material == null ? "null" : material.name + "/" +
                        (material.shader != null ? material.shader.name + "/supported=" + material.shader.isSupported : "null shader") + "/queue=" + material.renderQueue)));
        }
        if (main != null && Physics.Raycast(main.transform.position, main.transform.forward, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            diagnostic.forwardRay = hit.collider.name + "; layer=" + hit.collider.gameObject.layer + "; hit=" + hit.point + "; distance=" + hit.distance;
        string json = JsonUtility.ToJson(diagnostic, true);
        File.WriteAllText(Path.Combine(OutputDirectory, "camera-" + stage + ".json"), json);
        Debug.Log("[Stage2QA Camera] " + json);
        Check("camera." + stage, main != null && main.isActiveAndEnabled && main.targetTexture == null && diagnostic.enabledWorldRenderers > 0,
            "main=" + diagnostic.mainCamera + "; player=" + diagnostic.playerPosition + "; body=" + diagnostic.bodyPosition +
            "; site=" + diagnostic.siteCenter + "; enabled world=" + diagnostic.enabledWorldRenderers + "; in frustum=" + diagnostic.mainFrustumRenderers + "; details in camera-" + stage + ".json");
    }

    private IEnumerator Capture(string name, bool pause)
    {
        FocusGame();
        string path = Path.Combine(OutputDirectory, name);
        if (File.Exists(path)) File.Delete(path);
        float speed = Time.timeScale;
        if (pause) Time.timeScale = 0f;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        yield return Frames(6);
        Check("screenshot." + name, File.Exists(path) && new FileInfo(path).Length > 1000, path);
        if (pause) Time.timeScale = speed;
    }

    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private void Check(string name, bool passed, string detail)
    {
        report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail });
        Debug.Log("[Stage2QA] " + (passed ? "PASS " : "FAIL ") + name + ": " + detail);
        WriteReport();
    }
    private void Require(string name, bool passed, string detail)
    { Check(name, passed, detail); if (!passed) throw new InvalidOperationException(name + ": " + detail); }
    private void WriteReport()
    {
        report.finishedUtc = DateTime.UtcNow.ToString("o");
        report.passed = report.completed && report.checks.All(check => check.passed) && report.errors.Count == 0;
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "runtime-report.json"), JsonUtility.ToJson(report, true));
    }
    private void OnDestroy()
    {
        Application.logMessageReceived -= CaptureError;
        if (!finished && report != null) { report.checks.Add(new CheckResult { name = "runner.interrupted", passed = false, detail = "Play Mode stopped before the checks finished." }); WriteReport(); }
    }
}
#endif
