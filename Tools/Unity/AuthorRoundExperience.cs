using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using PinePie.SimpleJoystick;
using PinePie.SimpleJoystick.Examples.DemoScript;

public static class AuthorRoundExperience
{
    public const string SettingsPath = "Assets/GameData/RoundExperienceSettings.asset";
    public const string GamePrefab = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save your work before authoring.");
        string originalScene = SceneManager.GetActiveScene().path;
        if (!AssetDatabase.IsValidFolder("Assets/GameData")) AssetDatabase.CreateFolder("Assets", "GameData");
        var settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RoundExperienceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            ConfigureGame(prefab, settings);
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        string[] scenes = { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity" };
        int[] obstacleCounts = new int[scenes.Length];
        for (int index = 0; index < scenes.Length; index++)
        {
            Scene scene = EditorSceneManager.OpenScene(scenes[index], OpenSceneMode.Single);
            CameraCutawayView cutaway = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CameraCutawayView>(true)).Single();
            GameObject gameplay = PrefabUtility.GetOutermostPrefabInstanceRoot(cutaway.gameObject);
            Renderer[] obstacles = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
                .Where(r => !r.transform.IsChildOf(gameplay.transform) && IsLargeOccluder(r)).ToArray();
            var serialized = new SerializedObject(cutaway);
            SerializedProperty list = serialized.FindProperty("occluders");
            list.arraySize = obstacles.Length;
            for (int i = 0; i < obstacles.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = obstacles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(cutaway);
            obstacleCounts[index] = obstacles.Length;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Scene menuScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>(SettingsPath);
        MainMenuController menu = menuScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuController>(true)).Single();
        SetReference(menu, "experienceSettings", settings);
        foreach (Canvas canvas in menuScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
            .Where(c => c.name == "CanvasForDesktop" || c.name == "CanvasForMobile"))
        {
            bool mobile = canvas.name == "CanvasForMobile";
            Text level = mobile ? menu.levelText : menu.DlevelText;
            Text mainGoal = TextAt(level.transform.parent, "AccountGoal", new Vector2(mobile ? 50 : 0, -40),
                new Vector2(mobile ? 300 : 430, mobile ? 60 : 40), 22, Color.white);
            BindGoal(mainGoal, settings, menu, null);
            Transform progress = canvas.transform.Find("PanelOfProgress");
            if (progress != null)
            {
                Text detail = TextAt(progress, "AccountGoal", new Vector2(0, 220), new Vector2(680, 90), 36, Color.white);
                BindGoal(detail, settings, menu, null);
            }
        }
        menu.rank.text = menu.Drank.text = "—";
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);

        UnityEngine.Object sdkSettings = AssetDatabase.LoadMainAssetAtPath("Assets/PluginYourGames/Resources/SettingsYG2.asset");
        var sdk = new SerializedObject(sdkSettings);
        sdk.FindProperty("InterstitialAdv.interAdvInterval").intValue = 180;
        sdk.FindProperty("Simulation.advIntervalSimulation").intValue = 180;
        sdk.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(sdkSettings);
        AssetDatabase.SaveAssetIfDirty(settings);
        if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        return "Authored shared gameplay/menu UI and references. Cutaway renderers: " + string.Join(",", obstacleCounts);
    }

    public static string RepairMenuGoalBindings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your scene first.");
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>(SettingsPath);
        var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuController>(true)).Single();
        SetReference(menu, "experienceSettings", settings);
        int count = 0;
        foreach (AccountProgressView view in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AccountProgressView>(true)))
        {
            SetReference(view, "settings", settings);
            if (view.transform.parent.name == "Progress")
            {
                bool mobile = view.GetComponentInParent<Canvas>(true).name == "CanvasForMobile";
                TextAt(view.transform.parent, "AccountGoal", new Vector2(mobile ? 50 : 0, -40),
                    new Vector2(mobile ? 300 : 430, mobile ? 60 : 40), 22, Color.white);
            }
            count++;
        }
        menu.scoreText.text = menu.DscoreText.text = "0";
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Rebound " + count + " goal views to a freshly loaded settings asset; removed authored XP placeholder.";
    }

    public static string RepairResultGoalSpacing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            foreach (EndGameController end in prefab.GetComponentsInChildren<EndGameController>(true))
            {
                Text goal = TextAt(end.transform, "AccountGoal", new Vector2(0, 200), new Vector2(820, 44), 26, RankingVisualStyle.Load().ink);
                SetReference(end, "accountGoalText", goal);
            }
            SetReference(prefab.GetComponent<CameraCutawayView>(), "game", prefab.GetComponentInChildren<GamingManager>(true));
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        return "Saved a separate authored goal row below the results title in both panels.";
    }

    public static string RepairHudLayering()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            foreach (RoundHudView hud in prefab.GetComponentsInChildren<RoundHudView>(true))
                hud.transform.SetAsFirstSibling();
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        return "Saved growth readouts behind the authored modal panels in both canvases.";
    }

    public static string RepairCameraOccluders()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        string[] paths = { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity",
            "Assets/Scenes/MedievalCastle.unity", RoundExperienceLab.ScenePath };
        var counts = new System.Collections.Generic.List<string>();
        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            CameraCutawayView cutaway = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CameraCutawayView>(true)).Single();
            GameObject gameplay = PrefabUtility.GetOutermostPrefabInstanceRoot(cutaway.gameObject);
            Renderer[] obstacles = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
                .Where(r => !r.transform.IsChildOf(gameplay.transform) && IsLargeOccluder(r)).ToArray();
            var data = new SerializedObject(cutaway);
            SerializedProperty list = data.FindProperty("occluders");
            list.arraySize = obstacles.Length;
            for (int i = 0; i < obstacles.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = obstacles[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(cutaway);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            counts.Add(path + ": " + obstacles.Length);
        }
        EditorSceneManager.OpenScene(original);
        return string.Join("\n", counts);
    }

    public static string RepairCameraBodyBounds()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            GameController controller = prefab.GetComponentInChildren<GameController>(true);
            BlackHoleController player = prefab.GetComponentInChildren<BlackHoleController>(true);
            Renderer[] visuals = BindCameraBodyVisuals(prefab.GetComponent<CameraCutawayView>(), controller, player);
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
            return "Saved player visual bounds: " + string.Join(", ", visuals.Select(renderer => renderer.name + " " + renderer.bounds.size));
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static Renderer[] BindCameraBodyVisuals(CameraCutawayView cutaway, GameController controller, BlackHoleController player)
    {
        Renderer[] visuals = new[] { controller.mainToilet, controller.mainObodok }
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Concat(new[] { player.hole.GetComponent<Renderer>() }).Where(renderer => renderer != null).Distinct().ToArray();
        var data = new SerializedObject(cutaway);
        SerializedProperty list = data.FindProperty("playerVisuals");
        list.arraySize = visuals.Length;
        for (int i = 0; i < visuals.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        return visuals;
    }

    private static bool IsLargeOccluder(Renderer renderer)
    {
        Bounds bounds = renderer.bounds;
        float span = Mathf.Max(bounds.size.x, bounds.size.z);
        // Include narrow towers/walls and elevated roofs, while leaving the
        // flat ground and small edible props outside the cutaway list.
        return (span >= 3f && bounds.max.y >= 1f) || (bounds.size.y >= 3f && span >= 1f);
    }

    private static void ConfigureGame(GameObject root, RoundExperienceSettings settings)
    {
        GamingManager game = root.GetComponentInChildren<GamingManager>(true);
        GameController controller = root.GetComponentInChildren<GameController>(true);
        BlackHoleController player = root.GetComponentInChildren<BlackHoleController>(true);
        SetReference(game, "experienceSettings", settings);

        JoystickController mobileJoystick = controller.CanvasForMobile.GetComponentInChildren<JoystickController>(true);
        JoystickController desktopJoystick = controller.CanvasForDesktop.GetComponentInChildren<JoystickController>(true);
        foreach (MovementScript movement in root.GetComponentsInChildren<MovementScript>(true))
        {
            SetReference(movement, "mobileJoystick", mobileJoystick);
            SetReference(movement, "desktopJoystick", desktopJoystick);
            SetReference(movement, "gamingManager", game);
        }

        CameraCutawayView cutaway = root.GetComponent<CameraCutawayView>() ?? root.AddComponent<CameraCutawayView>();
        SetReference(cutaway, "viewCamera", player.GetComponentInChildren<Camera>(true));
        SetReference(cutaway, "target", player.transform);
        SetReference(cutaway, "settings", settings);
        SetReference(cutaway, "game", game);
        BindCameraBodyVisuals(cutaway, controller, player);

        foreach (GameObject canvas in new[] { controller.CanvasForMobile, controller.CanvasForDesktop })
        {
            Text growth = TextAt(canvas.transform, "GrowthReadout", new Vector2(0, -120), new Vector2(600, 62), 30, Color.white);
            growth.transform.SetAsFirstSibling();
            growth.rectTransform.anchorMin = growth.rectTransform.anchorMax = new Vector2(.5f, 1f);
            growth.rectTransform.anchoredPosition = new Vector2(0, -120);
            RoundHudView hud = growth.GetComponent<RoundHudView>() ?? growth.gameObject.AddComponent<RoundHudView>();
            SetReference(hud, "player", player);
            SetReference(hud, "game", game);
            SetReference(hud, "growthText", growth);
            growth.text = "Размер 1 · До роста: 26";
        }

        foreach (EndGameController end in root.GetComponentsInChildren<EndGameController>(true))
        {
            SetReference(end, "coinBonusButton", end.transform.Find("ShowRewardedAdv").GetComponent<Button>());
            Text goal = TextAt(end.transform, "AccountGoal", new Vector2(0, 200), new Vector2(820, 44), 26, RankingVisualStyle.Load().ink);
            BindGoal(goal, settings, null, game);
            SetReference(end, "accountGoalText", goal);
        }
    }

    public static string BuildLab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your scene first.");
        string originalScene = SceneManager.GetActiveScene().path;
        if (!AssetDatabase.IsValidFolder("Assets/Tests")) AssetDatabase.CreateFolder("Assets", "Tests");
        if (!System.IO.File.Exists(RoundExperienceLab.ScenePath) &&
            !AssetDatabase.CopyAsset("Assets/Scenes/Low Poly City.unity", RoundExperienceLab.ScenePath))
            throw new InvalidOperationException("Could not copy the actual City scene for the lab.");
        Scene scene = EditorSceneManager.OpenScene(RoundExperienceLab.ScenePath);
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "RoundExperienceLab");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        GameObject canvasObject = new GameObject("RoundExperienceLab", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RoundExperienceLab));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        GameObject panel = new GameObject("Parameters", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(12, -12);
        rect.sizeDelta = new Vector2(350, 410);
        panel.GetComponent<Image>().color = new Color(.03f, .12f, .18f, .96f);
        Text heading = TextAt(panel.transform, "Title", new Vector2(175, -24), new Vector2(326, 38), 24, Color.white);
        heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0, 1);
        heading.text = "ТЕСТ · параметры раунда";
        var sliders = new Slider[4];
        var labels = new Text[4];
        float[] minimum = { 15, 0, 0, 0 };
        float[] maximum = { 180, 25, 10, 1 };
        string[] captions = { "Раунд: 180 с", "Мин. опыт: 5", "Мин. монеты: 1", "Возврат объектов: 0.15 с" };
        for (int i = 0; i < 4; i++)
        {
            float y = -62 - i * 62;
            labels[i] = TextAt(panel.transform, "Value" + i, new Vector2(175, y), new Vector2(326, 30), 20, Color.white);
            labels[i].rectTransform.anchorMin = labels[i].rectTransform.anchorMax = new Vector2(0, 1);
            labels[i].text = captions[i];
            GameObject control = new GameObject("Slider" + i, typeof(RectTransform), typeof(Image), typeof(Slider));
            control.transform.SetParent(panel.transform, false);
            RectTransform sliderRect = control.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0, 1);
            sliderRect.anchoredPosition = new Vector2(175, y - 24);
            sliderRect.sizeDelta = new Vector2(302, 18);
            control.GetComponent<Image>().color = new Color(.22f, .33f, .4f, 1f);
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(control.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(22, 30);
            handle.GetComponent<Image>().color = RankingVisualStyle.Load().green;
            sliders[i] = control.GetComponent<Slider>();
            sliders[i].handleRect = handleRect;
            sliders[i].targetGraphic = handle.GetComponent<Image>();
            sliders[i].minValue = minimum[i];
            sliders[i].maxValue = maximum[i];
            sliders[i].wholeNumbers = i < 3;
        }
        Button restart = LabButton(panel.transform, "Restart", "Заново", 90, -308);
        Button reset = LabButton(panel.transform, "Reset", "Сброс", 260, -308);
        Button save = LabButton(panel.transform, "Save", "Сохранить", 90, -350);
        Button stop = LabButton(panel.transform, "Stop", "Стоп", 260, -350);
        Text status = TextAt(panel.transform, "Status", new Vector2(175, -390), new Vector2(328, 40), 15, Color.white);
        status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0, 1);
        status.text = "Отдельный профиль · не входит в релиз";
        RoundExperienceLab lab = canvasObject.GetComponent<RoundExperienceLab>();
        var serialized = new SerializedObject(lab);
        serialized.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>(SettingsPath);
        serialized.FindProperty("game").objectReferenceValue = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GamingManager>(true)).Single();
        serialized.FindProperty("cutaway").objectReferenceValue = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CameraCutawayView>(true)).Single();
        serialized.FindProperty("sliders").arraySize = 4;
        serialized.FindProperty("valueLabels").arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            serialized.FindProperty("sliders").GetArrayElementAtIndex(i).objectReferenceValue = sliders[i];
            serialized.FindProperty("valueLabels").GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }
        serialized.FindProperty("restartButton").objectReferenceValue = restart;
        serialized.FindProperty("resetButton").objectReferenceValue = reset;
        serialized.FindProperty("saveButton").objectReferenceValue = save;
        serialized.FindProperty("stopButton").objectReferenceValue = stop;
        serialized.FindProperty("status").objectReferenceValue = status;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(originalScene);
        return "Saved editable isolated tuning scene: " + RoundExperienceLab.ScenePath;
    }

    private static Button LabButton(Transform parent, string name, string caption, float x, float y)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(155, 36);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = RankingVisualStyle.Load().filledShape;
        image.type = Image.Type.Sliced;
        image.color = RankingVisualStyle.Load().blue;
        Text text = TextAt(buttonObject.transform, "Caption", Vector2.zero, new Vector2(145, 32), 21, Color.white);
        text.text = caption;
        return buttonObject.GetComponent<Button>();
    }

    private static void BindGoal(Text text, RoundExperienceSettings settings, MainMenuController menu, GamingManager game)
    {
        AccountProgressView view = text.GetComponent<AccountProgressView>() ?? text.gameObject.AddComponent<AccountProgressView>();
        SetReference(view, "settings", settings);
        SetReference(view, "menu", menu);
        SetReference(view, "round", game);
        SetReference(view, "goalText", text);
        text.text = "До уровня 1: 100 опыта";
    }

    private static Text TextAt(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject label = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        label.transform.SetParent(parent, false);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = label.GetComponent<Text>();
        text.font = RankingVisualStyle.Load().comicFont;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, fontSize - 8);
        text.resizeTextMaxSize = fontSize;
        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = color == Color.white ? Color.black : Color.clear;
        outline.effectDistance = new Vector2(1.3f, -1.3f);
        return text;
    }

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.name + "/" + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
