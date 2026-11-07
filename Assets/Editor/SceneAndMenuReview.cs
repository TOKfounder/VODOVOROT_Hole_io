#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vodovorot.Stage2;
using Vodovorot.Stage3;
using YG;

/// <summary>Opt-in, isolated actual Editor menu/scene captures. Does not buy/equip or save scenes/profile.</summary>
[InitializeOnLoad]
public static class SceneAndMenuReview
{
    public const string ReportPath = "Builds/Stage3MenuQA/scene-menu-report.json";
    public const string ScreenshotDirectory = "Builds/Stage3MenuQA/Screens";
    private const string Key = "Vodovorot.SceneAndMenuReview";
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string ProfilePath = "Assets/PluginYourGames/Editor/SavesEditorYG2.json";
    private const string SettingsPath = "Assets/PluginYourGames/Resources/SettingsYG2.asset";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly string[] Panels = { "main", "settings", "skins", "maps", "modes", "progress", "currency", "leaders" };
    private static readonly string[] PanelPaths = { "", "", "PanelOfSkins", "PanelOfMaps", "PanelOfModes", "PanelOfProgress", "PanelOfValute", "PanelOfLeaders" };
    private static readonly int[] Levels = { 0, 1, 4, 7, 10 }, Coins = { 0, 20, 270, 800, 2400 }, Donate = { 0, 10000, 10, 40, 100 };
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(844, 390) };
    private static readonly string[] FormNames = { "desktop", "mobile-portrait", "mobile-landscape" };
    private static readonly HashSet<string> OfflineUrls = new HashSet<string>();
    private static readonly Stack<IEnumerator> Work = new Stack<IEnumerator>();
    private static Report report;
    private static bool finishing, runtimeInitialized;
    private static double entryDeadline, runDeadline;

    [Serializable] private sealed class Check { public string name, detail; public bool passed; }
    [Serializable] private sealed class CaptureRecord { public string path, scene, form, language, panel; public int width, height; }
    [Serializable] private sealed class SceneRecord { public string path, sha256; public bool loaded, active; }
    [Serializable] private sealed class EditorState
    {
        public List<SceneRecord> scenes = new List<SceneRecord>();
        public int viewIndex;
        public string sizeGroup, runToken, profileSha, settingsSha, environmentJson;
        public bool profileExisted;
        public bool editorPaused, runInBackground;
        public Rect viewRect;
    }
    [Serializable] private sealed class AudioRecord
    {
        public string label, scene, theme, clip;
        public ulong playerId, sourceId;
        public int voices, playingVoices;
        public float seconds, clipLength;
        public double realtime;
    }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, unityVersion;
        public string scope = "Actual Editor Game View and scene AudioSources. Explicit RankSimulationContext profile/service isolation; no real purchases/equips, no device/browser claim. Remote SDK demo images use a local gray runtime-cache texture for offline operation.";
        public bool completed, passed, restoredEditorState;
        public List<Check> checks = new List<Check>();
        public List<CaptureRecord> captures = new List<CaptureRecord>();
        public List<AudioRecord> audio = new List<AudioRecord>();
        public List<string> errors = new List<string>();
        public List<string> offlineImageUrls = new List<string>();
    }
    private static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string StatePath => Path.Combine(Project, "Library/Stage3MenuQA/editor-state.json");

    static SceneAndMenuReview()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        if (SessionState.GetBool(Key, false))
        {
            if (SessionState.GetBool(Key + ".Finishing", false))
            { finishing = true; EditorApplication.delayCall += RestoreEditorState; return; }
            // Runs after domain reload, before ordinary scene Awake/Start image loaders.
            try { PrimeOfflineImageCache(); }
            catch (Exception error)
            {
                EditorApplication.delayCall += () => { EnsureReport(); Add("offline-cache.initialization", false, error.ToString()); Finish(); };
            }
            entryDeadline = EditorApplication.timeSinceStartup + 50;
        }
    }

    [MenuItem("Tools/Vodovorot/Stage 3/Run isolated scene and menu review")]
    public static void StartChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key, false))
            throw new InvalidOperationException("Start scene/menu review from idle Edit Mode.");
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        if (scenes.Any(scene => scene.isDirty) || setup.Any(scene => string.IsNullOrEmpty(scene.path)))
            throw new InvalidOperationException("Save or close untitled/dirty scenes before review; no scene is discarded or saved by the runner.");
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
        Directory.CreateDirectory(Path.Combine(Project, ScreenshotDirectory));
        var view = GameView();
        var state = new EditorState
        {
            scenes = setup.Select(scene => new SceneRecord { path = scene.path, loaded = scene.isLoaded,
                active = scene.isActive, sha256 = Hash(scene.path) }).ToList(),
            viewIndex = SelectedSize(view), viewRect = view.position, sizeGroup = CurrentSizeGroup(),
            runToken = "SceneMenuQA-" + DateTime.UtcNow.Ticks,
            profileExisted = File.Exists(ProfilePath), profileSha = Hash(ProfilePath), settingsSha = Hash(SettingsPath),
            environmentJson = JsonUtility.ToJson(YG2.envir)
            , editorPaused = EditorApplication.isPaused, runInBackground = Application.runInBackground
        };
        File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
        report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion };
        Write();
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Finishing", false);
        runtimeInitialized = false;
        entryDeadline = EditorApplication.timeSinceStartup + 50;
        try
        {
            PrimeOfflineImageCache();
            EditorApplication.isPaused = false;
            if (SceneManager.GetActiveScene().path != MenuScene || SceneManager.sceneCount != 1)
                EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            // Arms storage isolation before PluginYG/gameplay Awake. No ranking overlay is requested.
            RankSimulationContext.Start(RankSimulationScenario.NoImprovement);
        }
        catch
        {
            if (report != null) Add("entry.failed", false, "Entry failed before runtime checks.");
            Finish(); throw;
        }
    }

    public static void StopChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EnsureReport(); Add("runner.interrupted", false, "Review stopped explicitly before completion."); Finish();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            InitializeRuntime();
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Work.Clear(); Application.logMessageReceived -= OnLog;
            if (!SessionState.GetBool(Key + ".Finishing", false))
            { EnsureReport(); Add("runner.interrupted", false, "Play Mode ended before review completed."); Write(); }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += RestoreEditorState;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || finishing) return;
        if (!Application.isPlaying)
        {
            if (entryDeadline > 0 && EditorApplication.timeSinceStartup > entryDeadline)
            { EnsureReport(); Add("entry.timeout", false, "Play/SDK entry exceeded 50 seconds."); Finish(); }
            return;
        }
        // Some Editor reload orders install this callback after EnteredPlayMode.
        // The first live update must initialize the work queue in that case too.
        if (!runtimeInitialized) InitializeRuntime();
        if (finishing) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            GameView().Repaint();
            if (runDeadline > 0 && EditorApplication.timeSinceStartup > runDeadline)
                throw new TimeoutException("Scene/menu review exceeded the five-minute runtime limit.");
            // An Editor iterator avoids adding a component from an Editor-only assembly.
            for (int steps = 0; steps < 32 && Work.Count > 0; steps++)
            {
                IEnumerator current = Work.Peek();
                if (!current.MoveNext()) { Work.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) Work.Push(nested);
                else return;
            }
            if (Work.Count == 0 && report != null && !report.completed) { report.completed = true; Finish(); }
        }
        catch (Exception error) { Add("runner.exception", false, error.ToString()); Finish(); }
    }

    private static void InitializeRuntime()
    {
        if (runtimeInitialized || SessionState.GetBool(Key + ".Finishing", false)) return;
        runtimeInitialized = true;
        try
        {
            EnsureReport(); finishing = false; PrimeOfflineImageCache();
            Application.runInBackground = true;
            runDeadline = EditorApplication.timeSinceStartup + 300;
            Application.logMessageReceived += OnLog;
            Work.Clear(); Work.Push(Run()); GameView().Focus();
        }
        catch (Exception error) { Add("entry.runtime-initialization", false, error.ToString()); Finish(); }
    }

    private static IEnumerator Run()
    {
        double deadline = EditorApplication.timeSinceStartup + 40;
        while ((!YG2.isSDKEnabled || !RankSimulationContext.Active) && EditorApplication.timeSinceStartup < deadline) yield return null;
        Add("profile.isolated-before-play", YG2.isSDKEnabled && RankSimulationContext.Active && RankSimulationContext.BlockEditorSave,
            "RankSimulationContext was armed before entry; all ordinary SDK saves are suppressed.");
        if (!RankSimulationContext.Active) yield break;
        string runtimeEnvironment = JsonUtility.ToJson(YG2.envir);
        var originalProfile = typeof(RankSimulationContext).GetField("originalProfile", Static)?.GetValue(null);
        Add("profile.distinct-runtime-object", originalProfile != null && !ReferenceEquals(originalProfile, YG2.saves), "The fixture is a deep clone.");
        YG2.saves.isFirst = false; YG2.saves.done = true; YG2.saves.tutorialDone = true;
        YG2.saves.isGaming = false; YG2.saves.chosenMode = 0; YG2.saves.equipedMaterial = 0;
        YG2.saves.massiveOfObtaining = new[] { 1, 0, 0, 0, 0 };
        YG2.saves.soundValue = .5f; YG2.saves.musicValue = .5f;
        BackgroundMusicPlayer.StopPreview(); BackgroundMusicPlayer.SetAutomaticPlaybackForReview(true);
        LeaderboardRoundPanel.Close();
        yield return Frames(10);
        GameController controller = Find<GameController>(); MainMenuController menu = Find<MainMenuController>();
        if (controller == null || menu == null) throw new InvalidOperationException("MainMenu controllers are missing.");
        ValidateMenuReferences(menu, controller);
        // Start each authored carousel while its real panel/canvas is active.
        MobileMenuLayout layout = MobileMenuLayout.Install(controller);
        layout.enabled = false;
        foreach (bool mobile in new[] { false, true })
        {
            SetCanvas(controller, mobile, true); ShowPanel(controller.currentCanvas.transform, "skins", mobile);
            yield return Frames(4);
        }
        layout.enabled = true;
        var shops = UnityEngine.Object.FindObjectsByType<HorizontalLayout3D>(FindObjectsInactive.Include)
            .Where(shop => shop.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
        Add("shop.two-authored-carousels", shops.Length == 2, "Expected desktop and mobile carousels; actual=" + shops.Length);
        foreach (HorizontalLayout3D shop in shops) ValidateShopFoundation(shop, menu);
        yield return CheckMenuRotation(controller, layout);
        yield return WaitForMusic("menu");
        int wallet = YG2.saves.goldCoins, diamonds = YG2.saves.diamonds;
        for (int form = 0; form < Sizes.Length; form++)
        {
            bool mobile = form != 0; SetViewSize(Sizes[form]); SetCanvas(controller, mobile);
            yield return Frames(6);
            Add("view." + FormNames[form], SelectedViewSizeMatches(Sizes[form]) &&
                controller.currentCanvas.gameObject.activeInHierarchy, "Actual Game View " + Screen.width + "x" + Screen.height);
            HorizontalLayout3D shop = controller.currentCanvas.GetComponentInChildren<HorizontalLayout3D>(true);
            foreach (bool russian in new[] { true, false })
            {
                YG2.saves.langRu = russian;
                menu.UpdateMainMenu(); // Existing language/price UI path; never changes YG2.envir.
                var language = Find<LanguageManager>();
                if (language != null)
                { if (language.Mimage != null) language.Mimage.sprite = russian ? language.isRus : language.isEng;
                  if (language.Dimage != null) language.Dimage.sprite = russian ? language.isRus : language.isEng; }
                foreach (HorizontalLayout3D carousel in shops) carousel.UpdateForChosen();
                yield return Frames(3);
                string locale = russian ? "ru" : "en";
                AudioRecord before = Music("menu panels baseline " + FormNames[form] + "/" + locale);
                foreach (string panel in Panels)
                {
                    ShowPanel(controller.currentCanvas.transform, panel, IsMobileCanvas(controller)); yield return Frames(3);
                    yield return Capture(FormNames[form], locale, panel);
                    AudioRecord after = Music("menu panel " + panel);
                    Add("music.panel-persistence." + FormNames[form] + "." + locale + "." + panel,
                        MusicContinues(before, after),
                        "Same FullMenu source/time through panel swap: " + before.seconds.ToString("F2") + "→" + after.seconds.ToString("F2"));
                    before = after;
                    if (panel == "skins" && shop != null)
                    {
                        for (int choice = 0; choice < 5; choice++)
                        {
                            yield return SelectSkin(shop, choice);
                            ValidateSelectedShop(shop, menu, russian, FormNames[form] + "." + locale);
                            yield return Capture(FormNames[form], locale, "skin-" + shop.ChosenSkinId);
                        }
                    }
                }
            }
        }
        Add("economy.no-purchases-or-equips", YG2.saves.goldCoins == wallet && YG2.saves.diamonds == diamonds &&
            YG2.saves.equipedMaterial == 0 && YG2.saves.massiveOfObtaining.SequenceEqual(new[] { 1, 0, 0, 0, 0 }),
            "Only carousel selection changed; wallet, ownership and equipped identity stayed fixed.");
        ulong playerId = Music("menu before scene traversal").playerId;
        string[] scenes = { "Low Poly City", "Garden", "MedievalCastle", "MainMenu" };
        for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
        {
            YG2.saves.chosenMode = 0; YG2.saves.selectedMapID = sceneIndex == 3 ? 0 : sceneIndex;
            YG2.saves.isGaming = sceneIndex != 3; YG2.saves.tutorialDone = true;
            Time.timeScale = 1f;
            AsyncOperation operation = SceneManager.LoadSceneAsync(scenes[sceneIndex], LoadSceneMode.Single);
            deadline = EditorApplication.timeSinceStartup + 35;
            while (!operation.isDone && EditorApplication.timeSinceStartup < deadline) yield return null;
            if (!operation.isDone) throw new TimeoutException("Scene did not load: " + scenes[sceneIndex]);
            yield return Frames(8); yield return WaitForMusic(BackgroundMusicPlayer.ThemeForScene(scenes[sceneIndex]));
            AudioRecord music = Music("scene " + scenes[sceneIndex]);
            Add("music.scene-auto." + scenes[sceneIndex], music.playerId == playerId && music.theme == BackgroundMusicPlayer.ThemeForScene(scenes[sceneIndex]) &&
                music.clip == "Full" + char.ToUpperInvariant(music.theme[0]) + music.theme.Substring(1) && music.playingVoices == 1 && music.voices == 2,
                "Persistent player, selected looping Full clip and exactly two allocated music voices.");
            if (sceneIndex == 3) continue;
            controller = Find<GameController>(); var gaming = Find<GamingManager>();
            if (gaming != null) { gaming.totalCleaningDuration = 3600; gaming.timer = 0; }
            Add("scene.normal-hud." + scenes[sceneIndex], gaming != null && gaming.IsTotalCleaningMode && gaming.Stage2Mode == 0 && !gaming.HasRewardBeenApplied,
                "Ordinary scene Awake/Start entered mode 0; no forced end/results replace the HUD capture.");
            for (int form = 0; form < Sizes.Length; form++)
            {
                SetViewSize(Sizes[form]); SetCanvas(controller, form != 0); yield return Frames(5);
                foreach (bool russian in new[] { true, false })
                {
                    YG2.saves.langRu = russian; gaming?.UpdateUI(); yield return Frames(2);
                    yield return Capture(FormNames[form], russian ? "ru" : "en", "hud-" + scenes[sceneIndex].Replace(" ", "-"));
                }
            }
        }
        Add("runtime.no-errors", report.errors.Count == 0, string.Join("\n", report.errors));
        Add("profile.disk-byte-exact", ProfileUnchanged(), "Source save-file existence/hash equals entry snapshot.");
        Add("profile.environment-unchanged", JsonUtility.ToJson(YG2.envir) == runtimeEnvironment, "No YG2.envir/device/language environment mutation after normal SDK initialization.");
        Add("settings.asset-unchanged", Hash(SettingsPath) == State().settingsSha, "No SDK settings asset mutation.");
        Add("profile.save-suppression-active", RankSimulationContext.SuppressedSaveCount > 0, "Suppressed ordinary saves=" + RankSimulationContext.SuppressedSaveCount);
    }

    private static IEnumerator SelectSkin(HorizontalLayout3D shop, int desired)
    {
        Time.timeScale = 1f;
        for (int attempt = 0; shop.ChosenLegacyIndex != desired && attempt < 6; attempt++)
        {
            shop.RotateOnDeg(true);
            yield return Wait(1.12);
        }
        Add("shop.rotation." + shop.name + "." + desired, shop.ChosenLegacyIndex == desired, "Used actual RotateOnDeg; no Buy/Equip invocation.");
    }

    private static void ValidateShopFoundation(HorizontalLayout3D shop, MainMenuController menu)
    {
        string label = shop.transform.parent.parent.name;
        Add("shop." + label + ".text-bindings", shop.HasCompleteShopTextBindings, "Four actual description/price/level Text references.");
        Add("shop." + label + ".unchanged-economic-arrays", Read<int[]>(shop, "necessaryLevels").SequenceEqual(Levels) &&
            Read<int[]>(shop, "costsForCoins").SequenceEqual(Coins) && Read<int[]>(shop, "costsForDonate").SequenceEqual(Donate),
            "Historical required levels 0/1/4/7/10, coin prices 0/20/270/800/2400 and fallback SDK prices unchanged.");
        foreach (SkinVisualCatalog.Entry entry in SkinVisualCatalog.Load().Entries)
        {
            bool found = shop.TryGetVisualById(entry.id, out Transform root);
            Transform body = found ? Relative(root, entry.bodyPath, entry.modelRootPath) : null;
            Transform water = found ? Relative(root, entry.waterPath, entry.modelRootPath) : null;
            Text caption = found ? Relative(root, entry.captionPath, entry.modelRootPath)?.GetComponent<Text>() : null;
            bool references = found && root.gameObject.activeSelf && body != null && body.GetComponent<Renderer>() != null &&
                water != null && water.gameObject.activeSelf && water.GetComponent<Renderer>() != null && caption != null && caption.gameObject.activeSelf;
            Add("shop." + label + ".full-model." + entry.id, references, "Complete model root/body/water/world-space caption preserved.");
            Add("shop." + label + ".caption-remap." + entry.id, caption != null && shop.captions.Contains(caption.gameObject) &&
                (label == "CanvasForDesktop" ? menu.DPanelOfSkins : menu.PanelOfSkins).Contains(caption), "Live controller localization arrays point to replacement caption.");
        }
    }

    private static void ValidateSelectedShop(HorizontalLayout3D shop, MainMenuController menu, bool ru, string form)
    {
        int index = shop.ChosenLegacyIndex; string id = SkinVisualCatalog.IdForLegacyIndex(index);
        int purchaseCalls = 0; PurchaseYG widget = null;
        for (int i = 0; i < shop.donateButton.onClick.GetPersistentEventCount(); i++)
            if (shop.donateButton.onClick.GetPersistentMethodName(i) == nameof(PurchaseYG.BuyPurchase) &&
                shop.donateButton.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off)
            { purchaseCalls++; widget = shop.donateButton.onClick.GetPersistentTarget(i) as PurchaseYG; }
        Add("shop." + form + "." + id + ".single-persistent-sku", purchaseCalls == 1 && shop.UsesPersistentPurchaseRoute &&
            widget != null && widget.id == id && shop.ActivePurchaseId == id && widget.textLegasy.priceValue == shop.costForDonateText,
            "One enabled persistent SDK purchase route; selected SKU=" + shop.ActivePurchaseId + "; never clicked.");
        string expectedDonate = index == 1 ? "" : Donate[index].ToString();
        var product = YG2.PurchaseByID(id);
        if (product != null && widget != null) expectedDonate = widget.showCurrencyCode ? product.price : product.priceValue;
        bool owned = YG2.saves.massiveOfObtaining[index] != 0;
        Add("shop." + form + "." + id + ".prices", owned || (shop.costForCoinsText.text == Coins[index].ToString() &&
            shop.necessaryLevelText.text == Levels[index] + (ru ? " уровень" : " level") && shop.costForDonateText.text == expectedDonate),
            owned ? "Owned basin hides purchase prices, as authored." : "Coins=" + shop.costForCoinsText.text + "; required=" + shop.necessaryLevelText.text +
                "; SDK displayed=" + shop.costForDonateText.text + "; expected=" + expectedDonate + " (SDK price when product exists, historical fallback otherwise).");
        SkinVisualCatalog.Entry entry = SkinVisualCatalog.Load().GetByLegacyIndex(index);
        Text caption = Relative(shop.SelectedVisualRoot, entry.captionPath, entry.modelRootPath)?.GetComponent<Text>();
        Add("shop." + form + "." + id + ".localized-selected-caption", caption != null && caption.text == (ru ? entry.nameRu : entry.nameEn),
            caption == null ? "Missing caption" : caption.text);
        Add("shop." + form + "." + id + ".localized-feature", shop.featureText != null &&
            shop.featureText.text == Read<string[]>(shop, ru ? "featuresRu" : "featuresEn")[index],
            "Existing description follows the chosen historical ID and isolated language.");
    }

    private static void ValidateMenuReferences(MainMenuController menu, GameController controller)
    {
        foreach (FieldInfo field in typeof(MainMenuController).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.FieldType == typeof(Text[]))
            { var texts = (Text[])field.GetValue(menu); Add("refs.menu." + field.Name, texts != null && texts.Length > 0 && texts.All(text => text != null), "Existing localization array contains live Texts."); }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                Add("refs.menu." + field.Name, (UnityEngine.Object)field.GetValue(menu) != null, "Existing serialized UI/audio reference.");
        }
        Add("refs.both-canvases", controller.CanvasForDesktop != null && controller.CanvasForMobile != null, "Both authored Canvas roots required.");
    }

    private static bool IsMobileCanvas(GameController controller) => controller.currentCanvas.gameObject == controller.CanvasForMobile;

    private static void SetCanvas(GameController controller, bool mobile, bool forceAuthored = false)
    {
        if (controller == null || controller.CanvasForDesktop == null || controller.CanvasForMobile == null)
            throw new InvalidOperationException("Scene has no complete desktop/mobile Canvas controller.");
        if (!forceAuthored && controller.gameObject.scene.name == "MainMenu")
        {
            MobileMenuLayout.Install(controller).SetMobileDeviceOverrideForReview(mobile);
            return; // The real LateUpdate resolves the active Game View's dimensions.
        }
        controller.CanvasForDesktop.SetActive(!mobile); controller.CanvasForMobile.SetActive(mobile);
        controller.currentCanvas = (mobile ? controller.CanvasForMobile : controller.CanvasForDesktop).GetComponent<Canvas>();
    }

    private static IEnumerator CheckMenuRotation(GameController controller, MobileMenuLayout layout)
    {
        string before = JsonUtility.ToJson(YG2.saves);
        SetViewSize(Sizes[1]); SetCanvas(controller, true); yield return Frames(6);
        ShowPanel(controller.currentCanvas.transform, "skins", IsMobileCanvas(controller));
        yield return SelectSkin(layout.ActiveShop, 3);
        InputField input = controller.currentCanvas.transform.Find("InputFieldForNick").GetComponent<InputField>();
        string originalDraft = input.text;
        input.SetTextWithoutNotify("QA draft");
        foreach (int form in new[] { 2, 1 })
        {
            SetViewSize(Sizes[form]); yield return Frames(6);
            Add("rotation.skins." + FormNames[form], layout.UsingDesktopMenu == (form == 2) &&
                controller.currentCanvas.transform.Find("PanelOfSkins").gameObject.activeInHierarchy &&
                layout.ActiveShop.ChosenLegacyIndex == 3 &&
                controller.currentCanvas.transform.Find("InputFieldForNick").GetComponent<InputField>().text == "QA draft" &&
                VodovorotGameManager.Instance.HorizontalLayout3D == layout.ActiveShop,
                "desktop=" + layout.UsingDesktopMenu + "; shopOpen=" + controller.currentCanvas.transform.Find("PanelOfSkins").gameObject.activeInHierarchy +
                "; chosen=" + layout.ActiveShop.ChosenLegacyIndex + "; draft=" + controller.currentCanvas.transform.Find("InputFieldForNick").GetComponent<InputField>().text +
                "; pointer=" + (VodovorotGameManager.Instance.HorizontalLayout3D == layout.ActiveShop));
        }
        ShowPanel(controller.currentCanvas.transform, "settings", IsMobileCanvas(controller));
        foreach (int form in new[] { 2, 1 })
        {
            SetViewSize(Sizes[form]); yield return Frames(6);
            string path = form == 2 ? "DesktopPanelOfSettings" : "MobilePanelOfSettings";
            Add("rotation.settings." + FormNames[form], layout.UsingDesktopMenu == (form == 2) &&
                controller.currentCanvas.transform.Find(path).gameObject.activeInHierarchy,
                "Keeps the settings form open on the corresponding authored Canvas.");
        }
        foreach (GameObject canvas in new[] { controller.CanvasForDesktop, controller.CanvasForMobile })
            canvas.transform.Find("InputFieldForNick").GetComponent<InputField>().SetTextWithoutNotify(originalDraft);
        Add("rotation.no-profile-write", JsonUtility.ToJson(YG2.saves) == before && ProfileUnchanged(),
            "Rotation and draft transfer do not equip, buy, commit the nickname or alter the isolated/persisted save.");
    }

    private static void ShowPanel(Transform canvas, string panel, bool mobile)
    {
        for (int i = 1; i < Panels.Length; i++)
        {
            string path = i == 1 ? (mobile ? "MobilePanelOfSettings" : "DesktopPanelOfSettings") : PanelPaths[i];
            Transform child = canvas.Find(path);
            if (child == null) { Add("refs.panel." + canvas.name + "." + path, false, "Missing actual panel."); continue; }
            child.gameObject.SetActive(Panels[i] == panel);
        }
        Canvas.ForceUpdateCanvases();
    }

    private static AudioRecord Music(string label)
    {
        var players = UnityEngine.Object.FindObjectsByType<BackgroundMusicPlayer>(FindObjectsInactive.Include);
        var player = players.FirstOrDefault(); var voices = player != null ? player.GetComponentsInChildren<AudioSource>(true) : Array.Empty<AudioSource>();
        AudioSource audible = voices.FirstOrDefault(voice => voice.isPlaying && !voice.mute && voice.volume > 0);
        var item = new AudioRecord { label = label, scene = SceneManager.GetActiveScene().name, theme = BackgroundMusicPlayer.CurrentThemeId,
            playerId = player != null ? EntityId.ToULong(player.GetEntityId()) : 0, sourceId = audible != null ? EntityId.ToULong(audible.GetEntityId()) : 0,
            voices = voices.Length, playingVoices = voices.Count(voice => voice.isPlaying), clip = audible != null && audible.clip != null ? audible.clip.name : "",
            seconds = audible != null ? audible.time : 0, clipLength = audible != null && audible.clip != null ? audible.clip.length : 0,
            realtime = EditorApplication.timeSinceStartup };
        report.audio.Add(item);
        Add("music.bounded." + label, players.Length == 1 && voices.Length == 2 && voices.Count(voice => voice.isPlaying) <= 2,
            "Player count=" + players.Length + "; allocated music voices=" + voices.Length);
        Add("music.no-legacy-duplication." + label, UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include)
            .All(manager => manager.musicSource == null || !manager.musicSource.isPlaying), "Legacy AudioManager music sources are stopped.");
        return item;
    }

    private static bool MusicContinues(AudioRecord before, AudioRecord after)
    {
        if (before.playerId == 0 || before.sourceId == 0 || before.playerId != after.playerId || before.sourceId != after.sourceId ||
            before.clip != "FullMenu" || after.clip != "FullMenu" || after.theme != "menu") return false;
        double elapsed = after.realtime - before.realtime;
        double progressed = after.seconds - before.seconds;
        if (progressed < -.025 && before.clipLength > 0) progressed += before.clipLength;
        return progressed >= -.025 && progressed <= elapsed + .5 && (elapsed <= .1 || progressed > .01);
    }

    private static IEnumerator WaitForMusic(string theme)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            var player = Find<BackgroundMusicPlayer>();
            if (BackgroundMusicPlayer.CurrentThemeId == theme && player != null &&
                player.GetComponentsInChildren<AudioSource>().Count(voice => voice.isPlaying && voice.clip != null && voice.loop) == 1)
            { yield return Wait(.85); yield break; }
            yield return null;
        }
        Add("music.load-timeout." + theme, false, "Theme failed to enter within 12 seconds.");
    }

    private static IEnumerator Capture(string form, string language, string panel)
    {
        GameView().Focus(); Canvas.ForceUpdateCanvases(); yield return Frames(2);
        string file = form + "-" + language + "-" + panel + ".png";
        string destination = Path.Combine(Project, ScreenshotDirectory, file);
        if (File.Exists(destination)) File.Delete(destination); // Only this runner's generated screenshot.
        ScreenCapture.CaptureScreenshot(destination);
        double deadline = EditorApplication.timeSinceStartup + 8;
        while (!File.Exists(destination) && EditorApplication.timeSinceStartup < deadline) yield return null;
        yield return Frames(2);
        int width = 0, height = 0;
        if (File.Exists(destination))
        {
            byte[] png = File.ReadAllBytes(destination);
            if (png.Length > 24 && png[0] == 137 && png[1] == 80)
            { width = BigEndianInt(png, 16); height = BigEndianInt(png, 20); }
        }
        int formIndex = Array.IndexOf(FormNames, form);
        Vector2Int expected = Sizes[Mathf.Max(0, formIndex)];
        Add("capture." + file, width == expected.x && height == expected.y, "Actual ScreenCapture PNG " + width + "x" + height + "; requested " + expected);
        report.captures.Add(new CaptureRecord { path = ScreenshotDirectory + "/" + file, scene = SceneManager.GetActiveScene().name,
            form = form, language = language, panel = panel, width = width, height = height }); Write();
    }

    private static void Finish()
    {
        if (finishing) return;
        finishing = true; SessionState.SetBool(Key + ".Finishing", true); Work.Clear();
        if (report != null)
        {
            Add("profile.disk-before-stop", ProfileUnchanged(), "Original profile bytes untouched.");
            Write();
        }
        try
        {
            RankSimulationContext.Stop();
            if (report != null) Add("profile.original-reference-restored", RankSimulationContext.OriginalProfileRestored && !RankSimulationContext.Active,
                "RankSimulationContext restored the original profile/service object.");
        }
        catch (Exception error) { Add("profile.stop-failed", false, error.ToString()); }
        finally
        {
            Application.logMessageReceived -= OnLog;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            Write();
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += RestoreEditorState;
    }

    private static void RestoreEditorState()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        { EditorApplication.delayCall += RestoreEditorState; return; }
        EnsureReport();
        try
        {
            EditorState state = State();
            EditorSceneManager.RestoreSceneManagerSetup(state.scenes.Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray());
            var view = GameView(); SetSelectedSize(view, state.viewIndex); view.position = state.viewRect;
            EditorApplication.isPaused = state.editorPaused; Application.runInBackground = state.runInBackground;
            RemoveReviewViewSizes(state);
            SceneSetup[] actual = EditorSceneManager.GetSceneManagerSetup();
            bool restored = actual.Length == state.scenes.Count && state.scenes.Select((item, index) =>
                actual[index].path == item.path && actual[index].isLoaded == item.loaded && actual[index].isActive == item.active).All(value => value) &&
                Enumerable.Range(0, SceneManager.sceneCount).All(i => !SceneManager.GetSceneAt(i).isDirty) && SelectedSize(view) == state.viewIndex;
            report.restoredEditorState = restored;
            Add("editor.scene-setup-and-gameview-restored", restored, "scenes=" + string.Join(",", actual.Select(item => item.path + ":" + item.isLoaded + ":" + item.isActive)) +
                "; dirty=" + string.Join(",", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).isDirty.ToString())) +
                "; view=" + SelectedSize(view) + "; expected=" + state.viewIndex);
            Add("source.scenes-byte-exact", state.scenes.All(scene => Hash(scene.path) == scene.sha256), "No scene source was saved by the runner.");
            Add("profile.disk-after-stop", ProfileUnchanged(), "Original persisted profile existence and SHA-256 unchanged.");
            Add("settings.asset-after-stop", Hash(SettingsPath) == state.settingsSha, "SDK settings were never saved or mutated.");
        }
        catch (Exception error) { Add("editor.restore-failed", false, error.ToString()); }
        finally
        {
            RemoveOfflineCacheEntries(); SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finishing", false);
            finishing = false; Work.Clear(); Write();
        }
    }

    private static void PrimeOfflineImageCache()
    {
        // Preserve cached real/local textures. Only absent remote demo URLs receive
        // a local placeholder, before ImageLoadYG can create UnityWebRequests.
        string json = JsonUtility.ToJson(YG2.infoYG) + JsonUtility.ToJson(YG.EditorScr.ServerInfo.saveInfo);
        foreach (Match match in Regex.Matches(json, "https?://[^\\\"\\s]+")) OfflineUrls.Add(match.Value.Replace("\\/", "/"));
        var cacheField = typeof(ImageLoadYG).GetField("saveTextures", Static);
        IList cache = cacheField?.GetValue(null) as IList;
        Type entryType = typeof(ImageLoadYG).GetNestedType("LoadTextures", BindingFlags.NonPublic);
        if (cache == null || entryType == null) throw new InvalidOperationException("ImageLoadYG offline cache contract changed; abort before Play.");
        FieldInfo link = entryType.GetField("link", Instance), texture = entryType.GetField("texture", Instance);
        foreach (string url in OfflineUrls)
        {
            bool exists = cache.Cast<object>().Any(item => (string)link.GetValue(item) == url);
            if (exists) continue;
            object entry = Activator.CreateInstance(entryType); link.SetValue(entry, url); texture.SetValue(entry, Texture2D.grayTexture); cache.Add(entry);
        }
        SessionState.SetString(Key + ".OfflineUrls", string.Join("\n", OfflineUrls));
        if (report != null) report.offlineImageUrls = OfflineUrls.ToList();
    }

    private static void RemoveOfflineCacheEntries()
    {
        IList cache = typeof(ImageLoadYG).GetField("saveTextures", Static)?.GetValue(null) as IList;
        Type entry = typeof(ImageLoadYG).GetNestedType("LoadTextures", BindingFlags.NonPublic);
        if (cache == null || entry == null) return;
        var urls = new HashSet<string>(SessionState.GetString(Key + ".OfflineUrls", "").Split('\n'));
        for (int index = cache.Count - 1; index >= 0; index--)
            if (urls.Contains((string)entry.GetField("link", Instance).GetValue(cache[index])) &&
                (Texture2D)entry.GetField("texture", Instance).GetValue(cache[index]) == Texture2D.grayTexture) cache.RemoveAt(index);
    }

    private static void BeforeReload()
    {
        if (!SessionState.GetBool(Key, false) || !Application.isPlaying) return;
        EnsureReport(); Add("runner.reload-interrupted", false, "Unexpected script reload during review; isolated session stopped."); Finish();
    }
    private static void OnLog(string message, string trace, LogType kind)
    { if (report != null && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) report.errors.Add(message + "\n" + trace); }
    private static void Add(string name, bool passed, string detail)
    { EnsureReport(); report.checks.Add(new Check { name = name, passed = passed, detail = detail }); }
    private static void EnsureReport()
    { if (report == null) report = File.Exists(Path.Combine(Project, ReportPath)) ? JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(Project, ReportPath))) : new Report(); }
    private static void Write()
    {
        if (report == null) return;
        report.finishedUtc = DateTime.UtcNow.ToString("o"); report.passed = report.completed && report.restoredEditorState && report.errors.Count == 0 && report.checks.All(check => check.passed);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Project, ReportPath)));
        File.WriteAllText(Path.Combine(Project, ReportPath), JsonUtility.ToJson(report, true));
    }
    private static EditorState State() => JsonUtility.FromJson<EditorState>(File.ReadAllText(StatePath));
    private static bool ProfileUnchanged() => File.Exists(ProfilePath) == State().profileExisted && Hash(ProfilePath) == State().profileSha;
    private static string Hash(string file)
    { if (!File.Exists(file)) return "<absent>"; using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant(); }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Instance).GetValue(target);
    private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
    private static Transform Relative(Transform root, string componentPath, string modelPath)
    { if (root == null || !componentPath.StartsWith(modelPath + "/", StringComparison.Ordinal)) return null; return root.Find(componentPath.Substring(modelPath.Length + 1)); }
    private static int BigEndianInt(byte[] bytes, int offset) => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    private static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count; double deadline = EditorApplication.timeSinceStartup + 12;
        while (Time.frameCount < target)
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Game View frames stalled; paused=" + EditorApplication.isPaused + "; focused=" + Application.isFocused);
            yield return null;
        }
    }
    private static IEnumerator Wait(double seconds)
    { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) yield return null; }

    private static EditorWindow GameView() => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
    private static int SelectedSize(EditorWindow window) => (int)window.GetType().GetProperty("selectedSizeIndex", Instance).GetValue(window);
    private static bool SelectedViewSizeMatches(Vector2Int expected)
    {
        var view = GameView();
        object size = view.GetType().GetProperty("currentGameViewSize", Instance).GetValue(view);
        return (int)size.GetType().GetProperty("width", Instance).GetValue(size) == expected.x &&
            (int)size.GetType().GetProperty("height", Instance).GetValue(size) == expected.y;
    }
    private static void SetSelectedSize(EditorWindow window, int index) => window.GetType().GetProperty("selectedSizeIndex", Instance).SetValue(window, index);
    private static object SizesInstance()
    {
        Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
        return typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
    }
    private static string CurrentSizeGroup()
    {
        object sizes = SizesInstance();
        return sizes.GetType().GetProperty("currentGroupType", Instance)?.GetValue(sizes)?.ToString() ?? "Standalone";
    }
    private static object SizeGroup(string name)
    { object sizes = SizesInstance(); Type group = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeGroupType"); return sizes.GetType().GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(group, name) }); }
    private static void SetViewSize(Vector2Int dimensions)
    {
        EditorState state = State(); object group = SizeGroup(state.sizeGroup);
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        string label = state.runToken + " " + dimensions.x + "x" + dimensions.y;
        int selected = -1;
        for (int index = 0; index < total; index++)
        {
            object existing = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
            if ((string)existing.GetType().GetProperty("baseText", Instance).GetValue(existing) == label) { selected = index; break; }
        }
        if (selected < 0)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), new object[] {
                Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), dimensions.x, dimensions.y, label });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); selected = total;
        }
        EditorWindow view = GameView(); SetSelectedSize(view, selected); view.Focus();
    }
    private static void RemoveReviewViewSizes(EditorState state)
    {
        object group = SizeGroup(state.sizeGroup);
        int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        for (int index = total - 1; index >= builtIn; index--)
        {
            object size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
            string text = (string)size.GetType().GetProperty("baseText", Instance).GetValue(size);
            // Unity's internal API takes the total index (including built-ins).
            if (text.StartsWith(state.runToken, StringComparison.Ordinal)) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
        }
    }
}
#endif
