#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vodovorot.Stage2;
using Vodovorot.Stage3;
using YG;

/// <summary>Actual Editor runtime integration checks for the explicit review milestone.</summary>
[InitializeOnLoad]
public sealed class Stage3ReviewChecks : MonoBehaviour
{
    private const string Key = "Vodovorot.Stage3QA.Active";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string DirectoryPath => Path.Combine(Project, "Builds/Stage3QA");
    private static string StatePath => Path.Combine(Project, "Library/Stage3QA");
    private static string SavePath => Path.Combine(Project, "Assets/PluginYourGames/Editor/SavesEditorYG2.json");
    [Serializable] private sealed class Check { public string name, detail; public bool passed; }
    [Serializable] private sealed class FrameTimes { public List<float> seconds = new List<float>(); }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, unityVersion;
        public string scope = "Editor runtime. Explicit PluginYG fixture data and in-memory XP/coins. No real Yandex rank or physical-device claim.";
        public bool completed, passed, editorSaveByteExact;
        public List<Check> checks = new List<Check>();
        public List<string> errors = new List<string>();
    }
    private Report report;

    static Stage3ReviewChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            Directory.CreateDirectory(StatePath);
            File.AppendAllText(Path.Combine(StatePath, "qa-transition.log"), DateTime.UtcNow.ToString("o") + " " + state + "\n");
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var root = new GameObject("Stage 3 isolated review checks");
                DontDestroyOnLoad(root); root.AddComponent<Stage3ReviewChecks>();
                FocusGame();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreViewSize();
                SessionState.SetBool(Key, false);
                EditorApplication.delayCall += VerifyAfterStop;
            }
        };
    }

    public static void StartChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run from Edit Mode.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Review checks will not discard an unsaved scene.");
        Directory.CreateDirectory(StatePath); Directory.CreateDirectory(DirectoryPath);
        File.WriteAllBytes(Path.Combine(StatePath, "qa-original-save.bin"), File.ReadAllBytes(SavePath));
        SessionState.SetBool(Key, true);
        RememberViewSize();
        RankSimulationContext.Start(RankSimulationScenario.Up5);
        RankSimulationContext.ShowImmediate();
    }

    private void Start()
    {
        report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion };
        Application.logMessageReceived += OnLog;
        StartCoroutine(RunSafely());
    }

    private void OnLog(string text, string trace, LogType kind)
    {
        if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) report.errors.Add(text + "\n" + trace);
    }

    private IEnumerator RunSafely()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { Add("runner.exception", false, error.ToString()); break; }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        BackgroundMusicPlayer.StopPreview();
        Add("runtime.no-errors", report.errors.Count == 0, string.Join("\n", report.errors));
        report.editorSaveByteExact = OriginalSaveIsExact();
        Add("profile.disk-untouched-during-play", report.editorSaveByteExact, "Source Editor save equals the bytes captured before SDK initialization.");
        report.completed = true; Write();
        Application.logMessageReceived -= OnLog;
        var original = typeof(RankSimulationContext).GetField("originalProfile", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
        RankSimulationContext.Stop();
        Add("profile.original-reference-restored", original != null && ReferenceEquals(original, YG2.saves) &&
            RankSimulationContext.OriginalProfileRestored && !RankSimulationContext.Active,
            "Stopping the simulation restores the original profile object, not merely equivalent JSON.");
        Write();
    }

    private IEnumerator Run()
    {
        double deadline = EditorApplication.timeSinceStartup + 30;
        while ((!YG2.isSDKEnabled || !RankSimulationContext.Active) && EditorApplication.timeSinceStartup < deadline) yield return null;
        Add("sdk.profile-isolated", YG2.isSDKEnabled && RankSimulationContext.Active && RankSimulationContext.BlockEditorSave,
            "Explicit simulation was armed before entering Play Mode.");
        if (!RankSimulationContext.Active) yield break;
        yield return Frames(8);
        SetViewSize(1280, 720); yield return Frames(6);
        foreach (string theme in new[] { "menu", "city", "garden", "medieval" })
        {
            AudioClip clip = Resources.Load<AudioClip>(BackgroundMusicPlayer.ResourceForTheme(theme));
            Add("audio.demo-" + theme, clip != null && clip.length >= 20 && clip.length <= 30 && clip.channels == 2,
                clip == null ? "Missing demo" : clip.length.ToString("0.00") + " s; " + clip.channels + " channels");
            AudioClip full = Resources.Load<AudioClip>(BackgroundMusicPlayer.ProductionResourceForTheme(theme));
            Add("audio.full-" + theme, full != null && full.length >= 90 && full.length <= 120 && full.channels == 2,
                full == null ? "Missing full theme" : full.length.ToString("0.00") + " s; looping production resource");
        }
        foreach (RankSimulationScenario scenario in Enum.GetValues(typeof(RankSimulationScenario)))
        {
            RankSimulationContext.Start(scenario);
            YG2.saves.soundValue = .6f; YG2.saves.musicValue = .4f;
            RankSimulationContext.ShowImmediate();
            if (scenario == RankSimulationScenario.Delay)
            {
                yield return new WaitForSecondsRealtime(.35f);
                Add("ranking.delay-loading", LeaderboardRoundService.Current != null && LeaderboardRoundService.Current.status == LeaderboardRoundStatus.Loading,
                    "Delayed fixture stays loading before its response.");
            }
            yield return AwaitReply();
            var snapshot = LeaderboardRoundService.Current;
            bool confirmed = scenario != RankSimulationScenario.Guest && scenario != RankSimulationScenario.Error;
            bool expectedBonus = scenario == RankSimulationScenario.Up1 || scenario == RankSimulationScenario.Up5 ||
                scenario == RankSimulationScenario.Up20 || scenario == RankSimulationScenario.Top3 ||
                scenario == RankSimulationScenario.RepeatResult || scenario == RankSimulationScenario.Delay;
            Add("ranking." + scenario + ".status", snapshot != null && snapshot.isSimulation &&
                snapshot.status == (confirmed ? LeaderboardRoundStatus.Confirmed : scenario == RankSimulationScenario.Guest ? LeaderboardRoundStatus.Guest : LeaderboardRoundStatus.Unavailable),
                snapshot == null ? "Missing reply" : snapshot.status + "; simulation=" + snapshot.isSimulation);
            Add("ranking." + scenario + ".bonus", RankSimulationContext.TestBonusCoins == (expectedBonus ? 5 : 0) && RankSimulationContext.AwardCount == (expectedBonus ? 1 : 0),
                "Test bonus=" + RankSimulationContext.TestBonusCoins + "; count=" + RankSimulationContext.AwardCount);
            if (confirmed)
                Add("ranking." + scenario + ".rank", snapshot.rank == RankSimulationPlan.Create(scenario).TargetRank,
                    "Confirmed fixture rank=" + snapshot.rank);
            if (scenario == RankSimulationScenario.Up5)
            {
                BackgroundMusicPlayer.PlayPreviewTheme("menu");
                yield return CaptureMovementAndFrames();
                yield return Capture("ranking-landscape.png");
                yield return AudioChecks();
            }
            else yield return new WaitForSecondsRealtime(2.1f);
            int coins = YG2.saves.goldCoins, awardCount = RankSimulationContext.AwardCount;
            RankSimulationContext.RepeatResult(); yield return Frames(3);
            Add("ranking." + scenario + ".repeat-once", YG2.saves.goldCoins == coins && RankSimulationContext.AwardCount == awardCount,
                "Reopening same round did not increase test wallet or award count.");
            Add("ranking." + scenario + ".profile-safe", OriginalSaveIsExact(), "No source save-file write.");
        }
        SetViewSize(390, 844); yield return Frames(7);
        RankSimulationContext.Start(RankSimulationScenario.Up20); RankSimulationContext.ShowImmediate();
        yield return AwaitReply(); yield return new WaitForSecondsRealtime(2.2f);
        yield return GeometryChecks("portrait"); yield return Capture("ranking-portrait.png");
        YG2.saves.langRu = false;
        LeaderboardRoundPanel.Show(RankSimulationContext.CurrentRoundId, true);
        yield return Frames(3);
        var englishPanel = UnityEngine.Object.FindAnyObjectByType<LeaderboardRoundPanel>();
        Text englishTitle = englishPanel == null ? null : (Text)englishPanel.GetType().GetField("title", Private).GetValue(englishPanel);
        Add("layout.english-localization", englishTitle != null && englishTitle.text.Contains("Rank"),
            englishTitle == null ? "No title" : englishTitle.text);
        yield return Capture("ranking-portrait-en.png");
        YG2.saves.langRu = true; LeaderboardRoundPanel.Show(RankSimulationContext.CurrentRoundId, true); yield return Frames(2);
        SetViewSize(844, 390); yield return Frames(7);
        yield return GeometryChecks("short-landscape"); yield return Capture("ranking-short-landscape.png");
        SetViewSize(1280, 720); yield return Frames(5);
        RankSimulationContext.Start(RankSimulationScenario.Up5);
        RankSimulationContext.UseTestRoundXp = true;
        RankSimulationContext.StartCityRound(5f);
        deadline = EditorApplication.timeSinceStartup + 30;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            var rank = LeaderboardRoundService.Current;
            if (SceneManager.GetActiveScene().name == "Low Poly City" && rank != null && rank.isSimulation && rank.status == LeaderboardRoundStatus.Confirmed) break;
            yield return null;
        }
        GamingManager game = UnityEngine.Object.FindAnyObjectByType<GamingManager>();
        Add("city.real-timer-settlement", game != null && game.Stage2NaturalCompletion && game.HasRewardBeenApplied,
            "Real City scene and normal timer result; XP/eligibility are explicitly labeled fixture data.");
        Add("city.scenario-after-round", LeaderboardRoundService.Current != null && LeaderboardRoundService.Current.isSimulation &&
            LeaderboardRoundService.Current.status == LeaderboardRoundStatus.Confirmed && RankSimulationContext.TestBonusCoins == 5,
            RankSimulationContext.Detail);
        yield return new WaitForSecondsRealtime(2.2f);
        yield return Capture("ranking-after-short-city.png");
        yield return ResultsChecks(game);
        yield return CardChecks(game);
        Add("profile.saves-suppressed", RankSimulationContext.SuppressedSaveCount > 0, "Blocked disk writes=" + RankSimulationContext.SuppressedSaveCount);
    }

    private IEnumerator AudioChecks()
    {
        BackgroundMusicPlayer.SetAutomaticPlaybackForReview(false);
        var player = UnityEngine.Object.FindAnyObjectByType<BackgroundMusicPlayer>();
        Add("audio.ranking-background-at-timescale-zero", player != null && Time.timeScale == 0 && player.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying),
            "Background audition remains audible in the result UI, whose time scale is zero.");
        YG2.saves.musicValue = 0; yield return Frames(2);
        Add("audio.music-zero-mute", player != null && player.GetComponentsInChildren<AudioSource>().All(source => source.mute || source.volume <= 0),
            "Saved Music=0 truly silences both music voices.");
        yield return new WaitForSecondsRealtime(2.6f);
        Stage2Audio.PlayConfirmedRankImprovement(9, 8, "stage3-qa-sfx-with-music-muted"); yield return Frames(2);
        AudioSource accent = UnityEngine.Object.FindObjectsByType<AudioSource>().FirstOrDefault(source => source.name == "Confirmed rank accent");
        Add("audio.rank-on-sfx-when-music-muted", accent != null && accent.isPlaying && accent.volume > 0 &&
            (accent.outputAudioMixerGroup == null || accent.outputAudioMixerGroup.name == "SFX"), "Victory follows SFX, independent of background music.");
        YG2.saves.soundValue = 0; yield return Frames(2);
        Add("audio.sfx-zero-stops-rank", accent != null && !accent.isPlaying, "Sound=0 stops the victory effect.");
        YG2.saves.soundValue = .6f; YG2.saves.musicValue = .4f;
        BackgroundMusicPlayer.PlayPreviewTheme("city"); yield return Frames(2);
        BackgroundMusicPlayer.PlayPreviewTheme("garden"); yield return Frames(2);
        Add("audio.bounded-crossfade", player != null && player.GetComponentsInChildren<AudioSource>().Length == 2,
            "Rapid theme requests reuse exactly two sources.");
        BackgroundMusicPlayer.StopPreview();
        BackgroundMusicPlayer.SetAutomaticPlaybackForReview(true);
        yield return new WaitForSecondsRealtime(.9f);
        Add("audio.preview-stop-restores-production", player != null && BackgroundMusicPlayer.PreviewThemeId == null &&
            BackgroundMusicPlayer.AutomaticPlaybackEnabled && BackgroundMusicPlayer.CurrentThemeId == BackgroundMusicPlayer.ProductionThemeId &&
            player.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying && source.loop),
            "Stopping the Demo audition restores the looping Full theme for the active scene.");
        Type audition = AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
            assembly.GetType("Vodovorot.Stage3.Editor.Stage3MusicAudition")).First(type => type != null);
        MethodInfo previewAccent = audition.GetMethod("PlayRankAccent", BindingFlags.Public | BindingFlags.Static);
        bool accepted = previewAccent != null && (bool)previewAccent.Invoke(null, new object[] { false });
        yield return AwaitAccentAudition(audition);
        Add("audio.audition-accent-separate", accepted && accent != null && accent.isPlaying && BackgroundMusicPlayer.PreviewThemeId == null &&
            !BackgroundMusicPlayer.AutomaticPlaybackEnabled && !BackgroundMusicPlayer.HasActiveMusicVoice,
            "Accent-alone suspends automatic production playback and waits until both music voices actually stop.");
        accepted = previewAccent != null && (bool)previewAccent.Invoke(null, new object[] { true });
        yield return AwaitAccentAudition(audition);
        player = UnityEngine.Object.FindAnyObjectByType<BackgroundMusicPlayer>();
        float duck = player == null ? 1 : (float)player.GetType().GetField("duck", Private).GetValue(player);
        Add("audio.audition-accent-with-background", accepted && accent != null && accent.isPlaying &&
            BackgroundMusicPlayer.PreviewThemeId != null && duck < .99f,
            "Explicit combined audition starts the real SFX path and ducks the live background; duck=" + duck);
        audition.GetMethod("Stop", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        yield return new WaitForSecondsRealtime(.9f);
        Add("audio.audition-stop-restores-auto", BackgroundMusicPlayer.AutomaticPlaybackEnabled &&
            BackgroundMusicPlayer.PreviewThemeId == null && BackgroundMusicPlayer.CurrentThemeId == BackgroundMusicPlayer.ProductionThemeId,
            "Normal audition Stop returns to the active scene soundtrack.");
        BackgroundMusicPlayer.SetAutomaticPlaybackForReview(true);
    }

    private IEnumerator ResultsChecks(GamingManager match)
    {
        if (match == null) { Add("results.match-present", false, "No settled match."); yield break; }
        GameController controller = UnityEngine.Object.FindAnyObjectByType<GameController>();
        var style = RankingVisualStyle.Load();
        foreach (bool ru in new[] { true, false })
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(844, 390) })
        {
            bool mobile = size.x != 1280;
            YG2.saves.langRu = ru; SetViewSize(size.x, size.y);
            controller.CanvasForDesktop.SetActive(!mobile); controller.CanvasForMobile.SetActive(mobile);
            controller.currentCanvas = (mobile ? controller.CanvasForMobile : controller.CanvasForDesktop).GetComponent<Canvas>();
            GameObject panel = mobile ? match.MobpanelOfEnd : match.DeskpanelOfEnd;
            panel.SetActive(true); match.UpdateUI(); yield return Frames(5);
            LeaderboardRoundPanel.Close(); yield return Frames(2);
            Text[] labels = mobile ? match.PanelOfEnd : match.DPanelOfEnd;
            EndGameController owner = panel.GetComponent<EndGameController>();
            Text[] values = mobile ? new[] { owner.MexpText, owner.McoinText, owner.MbrillText } : new[] { owner.expText, owner.coinText, owner.brillText };
            string key = size.x + "x" + size.y + (ru ? "-ru" : "-en");
            Add("results." + key + ".shared-style", panel.GetComponent<Image>().color == style.paper &&
                labels.Take(4).Concat(values).All(text => text != null && text.font == style.comicFont && text.color == style.ink),
                "Original result panel and settled reward values use the approved light/ink palette and game font.");
            Add("results." + key + ".original-actions", labels.Skip(4).Take(2).All(text => text.font == style.comicFont &&
                text.GetComponent<Outline>() != null && text.GetComponent<Outline>().enabled),
                "Original Continue and rewarded-ad actions retain their forms/listeners with legible outlined text.");
            Rect panelBounds = ProjectedBounds((RectTransform)panel.transform);
            Add("results." + key + ".panel-visible", panelBounds.xMin >= -.5f && panelBounds.xMax <= Screen.width + .5f &&
                panelBounds.yMin >= -.5f && panelBounds.yMax <= Screen.height + .5f,
                "The whole result panel fits the real viewport: " + panelBounds + "; screen=" + Screen.width + "x" + Screen.height);
            Text title = labels.Take(4).FirstOrDefault(text => text.text == (ru ? "Итог" : "Result"));
            Text experience = labels.Take(4).FirstOrDefault(text => text.text == (ru ? "Опыт:" : "Experience:"));
            Add("results." + key + ".localized-roles", title != null && experience != null &&
                ProjectedBounds(title.rectTransform).center.y > ProjectedBounds(experience.rectTransform).center.y &&
                Mathf.Abs(ProjectedBounds(experience.rectTransform).center.y - ProjectedBounds(values[0].rectTransform).center.y) < 22,
                "The localized result heading sits above the XP row; Experience remains aligned with the actual XP value.");
            Transform footer = panel.transform.Find("Stage2ResultActions");
            Button[] footerButtons = footer == null ? Array.Empty<Button>() : footer.GetComponentsInChildren<Button>();
            Rect[] originalButtons = labels.Skip(4).Take(2).Select(text => ProjectedBounds((RectTransform)text.GetComponentInParent<Button>().transform)).ToArray();
            Add("results." + key + ".footer-clear", footerButtons.Length == 2 && footerButtons.All(button =>
                originalButtons.All(originalButton => !ProjectedBounds((RectTransform)button.transform).Overlaps(originalButton))),
                "Ranking/share never cover the original Continue/rewarded-ad buttons.");
            string[] footerWords = footer == null ? Array.Empty<string>() : footer.GetComponentsInChildren<Text>().Select(text => text.text).ToArray();
            Add("results." + key + ".footer-localized", footerWords.Contains(ru ? "Рейтинг" : "Ranking") &&
                footerWords.Contains(ru ? "Поделиться" : "Share"), "Footer follows the same current language as the result.");
            yield return Capture("results-" + key + ".png");
        }
        controller.CanvasForMobile.SetActive(false); controller.CanvasForDesktop.SetActive(true);
        controller.currentCanvas = controller.CanvasForDesktop.GetComponent<Canvas>();
        YG2.saves.langRu = true; match.UpdateUI();
    }

    private IEnumerator CardChecks(GamingManager match)
    {
        if (match == null) { Add("card.match-present", false, "No completed match for actual card integration."); yield break; }
        FieldInfo captured = typeof(GamingManager).GetField("<Stage2EquippedSkinId>k__BackingField", Private);
        string original = match.Stage2EquippedSkinId;
        int originalEquipped = YG2.saves.equipedMaterial;
        foreach (SkinVisualCatalog.Entry entry in SkinVisualCatalog.Load().Entries)
        {
            // Explicit isolated fixture: emulate a different skin captured at round start,
            // then choose another skin later. The card must keep the captured identity.
            captured.SetValue(match, entry.id);
            YG2.saves.equipedMaterial = (entry.legacyIndex + 1) % 5;
            AchievementCard.CardData data = AchievementCard.BuildData(match);
            Add("card." + entry.id + ".round-snapshot", data.skinId == entry.id && data.skin.Replace(" ", "") == entry.nameRu.Replace("\n", "").Replace(" ", ""),
                "Current shop choice differs; card uses the completed round's stable skin ID and name.");
            AchievementCard.Show(match); yield return Frames(3);
            yield return Capture("card-" + entry.id + ".png");
        }
        captured.SetValue(match, original); YG2.saves.equipedMaterial = originalEquipped;
        foreach (bool ru in new[] { true, false })
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(844, 390) })
        {
            YG2.saves.langRu = ru; SetViewSize(size.x, size.y); yield return Frames(5);
            AchievementCard.Show(match); yield return Frames(5); Canvas.ForceUpdateCanvases();
            AchievementCard view = UnityEngine.Object.FindAnyObjectByType<AchievementCard>();
            bool fit = view != null;
            foreach (Button button in view.GetComponentsInChildren<Button>())
            {
                Rect bounds = Bounds((RectTransform)button.transform);
                fit &= bounds.height >= 51.5f && bounds.xMin >= -.5f && bounds.xMax <= Screen.width + .5f &&
                    bounds.yMin >= -.5f && bounds.yMax <= Screen.height + .5f;
                fit &= button.GetComponentInChildren<Text>().font == RankingVisualStyle.Load().comicFont;
            }
            string key = size.x + "x" + size.y + (ru ? "-ru" : "-en");
            Add("card." + key + ".actions", fit, "Immediate close, save and copy are on screen, at least52px high and use the game font.");
            yield return Capture("card-" + key + ".png");
            Destroy(view.gameObject); yield return Frames(2);
        }
        YG2.saves.langRu = true;
        SetViewSize(1280, 720); yield return Frames(3);
    }

    private static IEnumerator AwaitAccentAudition(Type audition)
    {
        double deadline = EditorApplication.timeSinceStartup + 5;
        PropertyInfo pending = audition.GetProperty("RankAccentPending", BindingFlags.Public | BindingFlags.Static);
        while ((bool)pending.GetValue(null) && EditorApplication.timeSinceStartup < deadline) yield return null;
        yield return Frames(3);
    }

    private IEnumerator AwaitReply()
    {
        double deadline = EditorApplication.timeSinceStartup + 8;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            var snapshot = LeaderboardRoundService.Current;
            if (snapshot != null && snapshot.roundId == RankSimulationContext.CurrentRoundId &&
                (snapshot.status == LeaderboardRoundStatus.Confirmed || snapshot.status == LeaderboardRoundStatus.Guest || snapshot.status == LeaderboardRoundStatus.Unavailable)) yield break;
            yield return null;
        }
        Add("ranking.reply-timeout", false, RankSimulationContext.ScenarioLabel);
    }

    private IEnumerator CaptureMovementAndFrames()
    {
        var panel = UnityEngine.Object.FindAnyObjectByType<LeaderboardRoundPanel>();
        var rows = panel == null ? null : panel.GetType().GetField("rows", Private)?.GetValue(panel) as IList;
        RectTransform own = null; float start = 0, target = 0;
        if (rows != null)
            foreach (object row in rows)
            {
                Text label = (Text)row.GetType().GetField("label").GetValue(row);
                if (!label.text.Contains("Вы")) continue;
                own = (RectTransform)row.GetType().GetField("rect").GetValue(row);
                start = own.anchoredPosition.y; target = (float)row.GetType().GetField("targetY").GetValue(row); break;
            }
        var times = new FrameTimes(); float started = Time.realtimeSinceStartup;
        for (int frame = 0; frame < 20; frame++)
        {
            yield return new WaitForEndOfFrame();
            times.seconds.Add(Time.realtimeSinceStartup - started);
            SaveScaledFrame(Path.Combine(DirectoryPath, "ranking-up5-" + frame.ToString("D2") + ".png"));
            yield return new WaitForSecondsRealtime(.1f);
        }
        File.WriteAllText(Path.Combine(DirectoryPath, "ranking-up5-frame-times.json"), JsonUtility.ToJson(times, true));
        Add("ranking.actual-own-row-moves", own != null && own.anchoredPosition.y > start && Mathf.Abs(own.anchoredPosition.y - target) < .2f,
            "Own row start/target/end=" + start + "/" + target + "/" + (own == null ? 0 : own.anchoredPosition.y));
        yield return GeometryChecks("landscape");
    }

    private IEnumerator GeometryChecks(string label)
    {
        Canvas.ForceUpdateCanvases();
        var panel = UnityEngine.Object.FindAnyObjectByType<LeaderboardRoundPanel>();
        int expectedWidth = label == "portrait" ? 390 : label == "short-landscape" ? 844 : 1280;
        int expectedHeight = label == "portrait" ? 844 : label == "short-landscape" ? 390 : 720;
        Add("layout." + label + ".actual-size", panel != null && Screen.width == expectedWidth && Screen.height == expectedHeight,
            "Actual GameView=" + Screen.width + "x" + Screen.height + "; requested=" + expectedWidth + "x" + expectedHeight);
        if (panel == null) yield break;
        var viewport = (RectTransform)panel.GetType().GetField("viewport", Private).GetValue(panel);
        var share = (Button)panel.GetType().GetField("shareButton", Private).GetValue(panel);
        var close = (Button)panel.GetType().GetField("closeButton", Private).GetValue(panel);
        Rect body = Bounds(viewport), a = Bounds((RectTransform)share.transform), b = Bounds((RectTransform)close.transform);
        Add("layout." + label + ".footer-clear", !body.Overlaps(a) && !body.Overlaps(b) && a.height >= 47.5f && b.height >= 47.5f,
            "Footer outside scroll viewport; touch heights=" + a.height + "/" + b.height);
        Add("layout." + label + ".on-screen", a.xMin >= -.5f && b.xMax <= Screen.width + .5f && a.yMin >= -.5f && b.yMax <= Screen.height + .5f,
            "Both actions remain within actual screen bounds.");
        var topClose = (Button)panel.GetType().GetField("topCloseButton", Private).GetValue(panel);
        Text closeLabel = topClose.GetComponentInChildren<Text>();
        Add("layout." + label + ".close-glyph-visible", closeLabel != null && closeLabel.text == "X" && closeLabel.cachedTextGenerator.vertexCount >= 4,
            "Immediate close label has generated visible glyph geometry.");
        if (label == "portrait")
        {
            Text bonus = (Text)panel.GetType().GetField("bonusText", Private).GetValue(panel);
            Rect reward = Bounds(bonus.rectTransform);
            Add("layout.portrait.reward-fully-visible", reward.yMin >= body.yMin - .5f && reward.yMax <= body.yMax + .5f,
                "Reward bounds=" + reward + "; viewport=" + body);
            var neighbors = (IList)panel.GetType().GetField("rows", Private).GetValue(panel);
            foreach (object row in neighbors)
            {
                Type rowType = row.GetType();
                if ((bool)rowType.GetField("isSelf").GetValue(row)) continue;
                var rect = (RectTransform)rowType.GetField("rect").GetValue(row);
                if (!rect.gameObject.activeSelf) continue;
                var entry = (LeaderboardNeighbor)rowType.GetField("entry").GetValue(row);
                var nameLabel = (Text)rowType.GetField("label").GetValue(row);
                var scoreLabel = (Text)rowType.GetField("score").GetValue(row);
                string originalName = entry.name, originalScore = scoreLabel.text;
                entry.name = "Очень длинное имя игрока — A really long player name for a narrow screen";
                MethodInfo setRow = panel.GetType().GetMethod("SetRowLabel", Private);
                setRow.Invoke(panel, new object[] { row, entry.rank });
                Canvas.ForceUpdateCanvases();
                float nameWidth = nameLabel.cachedTextGeneratorForLayout.GetPreferredWidth(nameLabel.text,
                    nameLabel.GetGenerationSettings(new Vector2(10000, 100))) / Mathf.Max(.01f, nameLabel.pixelsPerUnit);
                Add("layout.portrait.long-name", nameLabel.text.EndsWith("…") && nameWidth <= nameLabel.rectTransform.rect.width + .5f &&
                    scoreLabel.text == originalScore,
                    "Long neighbor name is visibly abbreviated while rank and XP retain separate space: " + nameLabel.text);
                entry.name = originalName; setRow.Invoke(panel, new object[] { row, entry.rank });
                break;
            }
        }
        if (label == "short-landscape")
        {
            var rows = (IList)panel.GetType().GetField("rows", Private).GetValue(panel);
            int active = 0; bool fit = true;
            foreach (object row in rows)
            {
                var rect = (RectTransform)row.GetType().GetField("rect").GetValue(row);
                if (!rect.gameObject.activeSelf) continue;
                active++; Rect bounds = Bounds(rect);
                fit &= bounds.yMin >= body.yMin - .5f && bounds.yMax <= body.yMax + .5f;
            }
            Add("layout.short-landscape.neighbors-fully-visible", active >= 3 && fit,
                "All " + active + " visible neighbor rows fit inside the initial viewport.");
        }
        yield return null;
    }

    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(DirectoryPath, name), texture.EncodeToPNG()); Destroy(texture);
        yield return null;
    }
    private static void SaveScaledFrame(string path)
    {
        Texture2D source = ScreenCapture.CaptureScreenshotAsTexture();
        RenderTexture target = RenderTexture.GetTemporary(960, 540, 0);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, target); RenderTexture.active = target;
            var output = new Texture2D(960, 540, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG()); Destroy(output);
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(source); }
    }
    private void Add(string name, bool passed, string detail)
    {
        report.checks.Add(new Check { name = name, passed = passed, detail = detail });
        Debug.Log("[Stage3 QA] " + (passed ? "PASS " : "FAIL ") + name + ": " + detail);
    }
    private void Write()
    {
        report.finishedUtc = DateTime.UtcNow.ToString("o");
        report.passed = report.completed && report.checks.All(check => check.passed);
        File.WriteAllText(Path.Combine(DirectoryPath, "runtime-report.json"), JsonUtility.ToJson(report, true));
    }
    private static bool OriginalSaveIsExact() => File.ReadAllBytes(SavePath).SequenceEqual(File.ReadAllBytes(Path.Combine(StatePath, "qa-original-save.bin")));
    private static void VerifyAfterStop()
    {
        File.WriteAllText(Path.Combine(DirectoryPath, "restoration.json"), "{\"editorSaveByteExact\":" + (OriginalSaveIsExact() ? "true" : "false") + ",\"productionCalls\":false}");
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static Rect Bounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(item => item.x), corners.Min(item => item.y), corners.Max(item => item.x), corners.Max(item => item.y));
    }
    private static Rect ProjectedBounds(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2[] pixels = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner)).ToArray();
        return Rect.MinMaxRect(pixels.Min(point => point.x), pixels.Min(point => point.y), pixels.Max(point => point.x), pixels.Max(point => point.y));
    }
    private static EditorWindow GameView()
    {
        Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        return EditorWindow.GetWindow(type);
    }
    private static void FocusGame() { EditorWindow window = GameView(); window.Show(); window.Focus(); }
    private static void RememberViewSize()
    {
        var property = GameView().GetType().GetProperty("selectedSizeIndex", Private | BindingFlags.Public);
        if (property != null) SessionState.SetInt("Vodovorot.Stage3QA.OriginalView", (int)property.GetValue(GameView()));
    }
    private static void RestoreViewSize()
    {
        var property = GameView().GetType().GetProperty("selectedSizeIndex", Private | BindingFlags.Public);
        property?.SetValue(GameView(), SessionState.GetInt("Vodovorot.Stage3QA.OriginalView", 0));
    }
    private static void SetViewSize(int width, int height)
    {
        Assembly editor = typeof(EditorWindow).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance").GetValue(null);
        Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        Type sizeType = editor.GetType("UnityEditor.GameViewSize");
        Type sizeEnum = editor.GetType("UnityEditor.GameViewSizeType");
        object size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(sizeEnum, "FixedResolution"), width, height, "Stage3 QA " + width + "x" + height });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var window = GameView();
        window.GetType().GetProperty("selectedSizeIndex", Private | BindingFlags.Public).SetValue(window, total);
        window.Focus();
    }
}
#endif
