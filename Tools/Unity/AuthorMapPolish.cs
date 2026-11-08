using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

/// <summary>Editor-only authoring. The shipped scenes contain the complete saved result.</summary>
public static class AuthorMapPolish
{
    private const string Art = "Assets/Art/MapPolish";
    private const string GamePrefab = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
    private static RoundExperienceSettings settings;
    private static readonly string[] Maps = { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity" };
    private static readonly string[] Themes = { "City", "Garden", "Castle" };

    public static string Run()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        Folder("Assets/Art");
        Folder(Art);
        settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>("Assets/GameData/RoundExperienceSettings.asset");
        var config = new SerializedObject(settings);
        config.FindProperty("cutawayFadeOutSeconds").floatValue = .14f;
        config.FindProperty("cutawayFadeInSeconds").floatValue = .35f;
        config.FindProperty("targetHintRadius").floatValue = 5.5f;
        config.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(settings);
        GameObject marker = BuildMarker();
        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            ConfigureGameplay(prefab, marker);
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var report = new List<object>();
        for (int i = 0; i < Maps.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(Maps[i]);
            GamingManager game = All<GamingManager>(scene).Single();
            BlackHoleController player = All<BlackHoleController>(scene).Single();
            float maxX = game.walls[0].GetComponent<Collider>().bounds.min.x;
            float minX = game.walls[1].GetComponent<Collider>().bounds.max.x;
            float minZ = game.walls[2].GetComponent<Collider>().bounds.max.z;
            float maxZ = game.walls[3].GetComponent<Collider>().bounds.min.z;
            BuildSurround(scene, i, minX, maxX, minZ, maxZ);
            report.Add(ArrangeOpening(scene, player, i));
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.backgroundColor = ThemeGround(i);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        ConfigureSettingsCanvas(null, All<GameController>(menu).Single());
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        // The existing real-City lab keeps its GUID and UI, and receives the new saved world.
        Scene lab = EditorSceneManager.OpenScene(RoundExperienceLab.ScenePath);
        GamingManager labGame = All<GamingManager>(lab).Single();
        BuildSurround(lab, 0, labGame.walls[1].GetComponent<Collider>().bounds.max.x,
            labGame.walls[0].GetComponent<Collider>().bounds.min.x, labGame.walls[2].GetComponent<Collider>().bounds.max.z,
            labGame.walls[3].GetComponent<Collider>().bounds.min.z);
        ArrangeOpening(lab, All<BlackHoleController>(lab).Single(), 0);
        All<BlackHoleController>(lab).Single().GetComponentInChildren<Camera>(true).backgroundColor = ThemeGround(0);
        EditorSceneManager.MarkSceneDirty(lab);
        EditorSceneManager.SaveScene(lab);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(original);
        RepairBorderFraming();
        IncludeGardenHedges();
        File.WriteAllText("Docs/MapPolish-20261108/Checks/authoring-run.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return "Saved three themed surrounds, responsive existing settings,8 pooled target markers, fade shader and opening prop routes. Original scene restored.";
    }

    public static string RepairSettings()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        GameObject root = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            ConfigureSettingsCanvas(root, root.GetComponentInChildren<GameController>(true));
            PrefabUtility.SaveAsPrefabAsset(root, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        ConfigureSettingsCanvas(null, All<GameController>(menu).Single());
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        EditorSceneManager.OpenScene(original);
        return "Saved responsive settings in both canvases of gameplay prefab and MainMenu.";
    }

    public static string IncludeGardenHedges()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(Maps[1]);
        CameraCutawayView cutaway = All<CameraCutawayView>(scene).Single();
        var data = new SerializedObject(cutaway);
        SerializedProperty list = data.FindProperty("occluders");
        // The garden fence consists of two-metre segments, below the building
        // threshold. They still hide the rim on the authored opening route.
        Renderer[] hedges = All<Renderer>(scene).Where(renderer => renderer.bounds.size.y >= 1f &&
            renderer.sharedMaterials.Any(material => material != null && material.name == "hedge")).ToArray();
        Renderer[] obstacles = Enumerable.Range(0, list.arraySize)
            .Select(index => list.GetArrayElementAtIndex(index).objectReferenceValue as Renderer)
            .Where(renderer => renderer != null).Concat(hedges).Distinct().ToArray();
        list.arraySize = obstacles.Length;
        for (int index = 0; index < obstacles.Length; index++)
            list.GetArrayElementAtIndex(index).objectReferenceValue = obstacles[index];
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(cutaway);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(original);
        return "Included " + hedges.Length + " saved Garden hedge segments in the existing cutaway list.";
    }

    public static string RepairSurrounds()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        for (int i = 0; i < 4; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(i == 3 ? RoundExperienceLab.ScenePath : Maps[i]);
            GamingManager game = All<GamingManager>(scene).Single();
            BuildSurround(scene, i == 3 ? 0 : i, game.walls[1].GetComponent<Collider>().bounds.max.x,
                game.walls[0].GetComponent<Collider>().bounds.min.x, game.walls[2].GetComponent<Collider>().bounds.max.z,
                game.walls[3].GetComponent<Collider>().bounds.min.z);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(original);
        AssetDatabase.SaveAssets();
        return "Saved outward-facing perimeter meshes; exact wall-derived bounds retained.";
    }

    public static string PolishCastleSurround()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(Maps[2]);
        GamingManager game = All<GamingManager>(scene).Single();
        BuildSurround(scene, 2, game.walls[1].GetComponent<Collider>().bounds.max.x,
            game.walls[0].GetComponent<Collider>().bounds.min.x, game.walls[2].GetComponent<Collider>().bounds.max.z,
            game.walls[3].GetComponent<Collider>().bounds.min.z);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(original);
        return "Saved castle perimeter masonry, banners and planted accents outside the unchanged playable walls.";
    }

    public static string RepairBorderFraming()
    {
        RequireEdit();
        string original = SceneManager.GetActiveScene().path;
        GameObject prefab = PrefabUtility.LoadPrefabContents(GamePrefab);
        try
        {
            var cameraData = new SerializedObject(prefab.GetComponent<CameraCutawayView>());
            cameraData.FindProperty("surroundingViewMargin").floatValue = 16f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, GamePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var patched = new List<object>();
        for (int map = 0; map < 4; map++)
        {
            int theme = map == 3 ? 0 : map;
            Scene scene = EditorSceneManager.OpenScene(map == 3 ? RoundExperienceLab.ScenePath : Maps[map]);
            if (theme == 1)
                continue;
            Material paving = MaterialAsset(Themes[theme] + "CourtyardPaving", "Vodovorot/Map surround",
                theme == 2 ? new Color(.47f, .49f, .44f) : new Color(.58f, .60f, .57f));
            paving.SetFloat("_VertexColors", 0);
            paving.SetFloat("_Pattern", 1);
            paving.SetFloat("_TileSize", 1.2f);
            foreach (Renderer surface in All<Renderer>(scene).Where(x =>
                theme == 0 ? x.name == "Road Concrete Tile (6)" : theme == 2 && PathOf(x.transform) == "Medievel City/Plane"))
            {
                surface.sharedMaterial = paving;
                EditorUtility.SetDirty(surface);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
                patched.Add(new
                {
                    scene = scene.path,
                    path = PathOf(surface.transform),
                    material = AssetDatabase.GetAssetPath(paving)
                });
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(original);
        File.WriteAllText("Docs/MapPolish-20261108/Checks/border-surface-patches.json", Newtonsoft.Json.JsonConvert.SerializeObject(patched, Newtonsoft.Json.Formatting.Indented));
        return "Saved16m scenery context and explicit perimeter/courtyard paving materials; collisions untouched.";
    }

    private static void ConfigureGameplay(GameObject root, GameObject marker)
    {
        GameController controller = root.GetComponentInChildren<GameController>(true);
        BlackHoleController player = root.GetComponentInChildren<BlackHoleController>(true);
        GamingManager game = root.GetComponentInChildren<GamingManager>(true);
        ConfigureSettingsCanvas(root, controller);
        var fade = new SerializedObject(root.GetComponent<CameraCutawayView>());
        fade.FindProperty("fadeShader").objectReferenceValue = Shader.Find("Vodovorot/Cutaway dither");
        fade.ApplyModifiedPropertiesWithoutUndo();
        Transform pool = root.transform.Find("Edible target markers");
        if (pool != null)
            UnityEngine.Object.DestroyImmediate(pool.gameObject);
        pool = new GameObject("Edible target markers").transform;
        pool.SetParent(root.transform, false);
        EdibleTargetHints hints = root.GetComponent<EdibleTargetHints>() ?? root.AddComponent<EdibleTargetHints>();
        var data = new SerializedObject(hints);
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("game").objectReferenceValue = game;
        data.FindProperty("viewCamera").objectReferenceValue = player.GetComponentInChildren<Camera>(true);
        data.FindProperty("settings").objectReferenceValue = settings;
        data.FindProperty("targetLayers").intValue = (1 << 7) | (1 << 30);
        data.FindProperty("markers").arraySize = 8;
        for (int i = 0; i < 8; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(marker, pool);
            instance.name = "Target bracket " + (i + 1);
            instance.SetActive(false);
            data.FindProperty("markers").GetArrayElementAtIndex(i).objectReferenceValue = instance.transform;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject BuildMarker()
    {
        Material material = MaterialAsset("TargetBracket", "Vodovorot/Edible target bracket", new Color(.45f, 1f, .16f));
        var mesh = new Mesh { name = "Target bracket plane" };
        mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh = SaveMesh(mesh, "TargetBracket");
        GameObject source = new GameObject("EdibleTargetBracket", typeof(MeshFilter), typeof(MeshRenderer));
        source.GetComponent<MeshFilter>().sharedMesh = mesh;
        source.GetComponent<MeshRenderer>().sharedMaterial = material;
        source.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, Art + "/EdibleTargetBracket.prefab");
        UnityEngine.Object.DestroyImmediate(source);
        return prefab;
    }

    private static void ConfigureSettings(RectTransform panel)
    {
        Rect(panel, .045f, .055f, .955f, .945f);
        panel.localScale = Vector3.one;
        panel.localPosition = new Vector3(panel.localPosition.x, panel.localPosition.y, 0);
        Image surface = panel.GetComponent<Image>();
        if (surface != null)
        {
            surface.sprite = RankingVisualStyle.Load().filledShape;
            surface.type = Image.Type.Sliced;
            surface.color = new Color(.15f, .22f, .25f, .98f);
        }
        Rect((RectTransform)panel.Find("RaycastTarget"), -1, -1, 2, 2);
        Image blocker = panel.Find("RaycastTarget").GetComponent<Image>();
        if (blocker != null)
            blocker.color = Color.clear;
        Rect((RectTransform)panel.Find("Caption"), .15f, .82f, .85f, .96f);
        FitText(panel.Find("Caption").GetComponent<Text>(), 60);
        Rect((RectTransform)panel.Find("Return"), .86f, .82f, .97f, .98f);
        PreserveImages(panel.Find("Return"));
        Transform language = panel.Find("LanguageButton");
        ReparentLabel(language, panel, "LanguageLabel", .14f, .64f, .5f, .78f);
        Rect((RectTransform)language, .55f, .63f, .72f, .79f);
        PreserveImages(language);
        ConfigureAudio(panel, "SoundButton", "SoundLabel", "SoundSlider", .43f, .57f);
        ConfigureAudio(panel, "MelodyButton", "MusicLabel", "MusicSlider", .23f, .37f);
        Transform exit = panel.Find("ReturningToHome");
        if (exit != null)
        {
            ReparentLabel(exit, panel, "ExitLabel", .2f, .04f, .68f, .17f);
            Rect((RectTransform)exit, .71f, .035f, .84f, .175f);
            PreserveImages(exit);
        }
        foreach (RectTransform child in panel.GetComponentsInChildren<RectTransform>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
    }

    private static void ConfigureSettingsCanvas(GameObject root, GameController controller)
    {
        GameObject[] roots = root != null ? new[] { root } : controller.gameObject.scene.GetRootGameObjects();
        RectTransform[] panels = roots.SelectMany(x => x.GetComponentsInChildren<RectTransform>(true))
            .Where(x => x.name == "MobilePanelOfSettings" || x.name == "DesktopPanelOfSettings").ToArray();
        Transform existing = roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(x => x.name == "SettingsCanvas")?.transform;
        GameObject canvasObject = existing != null ? existing.gameObject :
            new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (root != null)
            canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        Transform safe = canvasObject.transform.Find("SafeArea");
        if (safe == null)
        {
            safe = new GameObject("SafeArea", typeof(RectTransform)).transform;
            safe.SetParent(canvasObject.transform, false);
        }
        Rect((RectTransform)safe, 0, 0, 1, 1);
        SettingsSafeArea adapter = canvasObject.GetComponent<SettingsSafeArea>() ?? canvasObject.AddComponent<SettingsSafeArea>();
        var adapterData = new SerializedObject(adapter);
        adapterData.FindProperty("content").objectReferenceValue = safe;
        adapterData.ApplyModifiedPropertiesWithoutUndo();
        foreach (RectTransform panel in panels)
        {
            panel.SetParent(safe, false);
            ConfigureSettings(panel);
            if (panel.name == "MobilePanelOfSettings")
                controller.MobileSettingsPanel = panel.gameObject;
            else
                controller.DesktopSettingsPanel = panel.gameObject;
        }
        EditorUtility.SetDirty(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
    }

    private static void ConfigureAudio(Transform panel, string name, string labelName, string sliderName, float low, float high)
    {
        Transform icon = panel.Find(name);
        ReparentLabel(icon, panel, labelName, .08f, low, .32f, high);
        Transform slider = icon.Find("Slider") ?? panel.Find(sliderName);
        slider.SetParent(panel, false);
        slider.name = sliderName;
        Rect((RectTransform)slider, .35f, low + .02f, .75f, high - .02f);
        Rect((RectTransform)slider.Find("Background"), 0, .35f, 1, .65f);
        Rect((RectTransform)slider.Find("Fill Area"), 0, .35f, 1, .65f);
        Rect((RectTransform)slider.Find("Handle Slide Area"), 0, .2f, 1, .8f);
        RectTransform handle = (RectTransform)slider.Find("Handle Slide Area/Handle");
        handle.sizeDelta = new Vector2(20, 0);
        Rect((RectTransform)icon, .79f, low - .01f, .92f, high + .01f);
        PreserveImages(icon);
        foreach (Text text in slider.GetComponentsInChildren<Text>(true))
            text.raycastTarget = false;
    }

    private static void ReparentLabel(Transform parent, Transform panel, string name, float x0, float y0, float x1, float y1)
    {
        Transform label = panel.Find(name) ?? parent.GetComponentsInChildren<Text>(true).First().transform;
        label.SetParent(panel, false);
        label.name = name;
        Rect((RectTransform)label, x0, y0, x1, y1);
        FitText(label.GetComponent<Text>(), 52);
    }
    private static void FitText(Text text, int size)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 18;
        text.resizeTextMaxSize = size;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }
    private static void PreserveImages(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            image.preserveAspect = true;
    }
    private static void Rect(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static Color ThemeGround(int theme) => theme == 0 ? new Color(.29f, .38f, .32f) : theme == 1 ? new Color(.30f, .43f, .22f) : new Color(.37f, .39f, .34f);

    private static void BuildSurround(Scene scene, int theme, float minX, float maxX, float minZ, float maxZ)
    {
        string name = "Map surround — " + Themes[theme];
        GameObject previous = scene.GetRootGameObjects().FirstOrDefault(x => x.name == name);
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);
        GameObject root = new GameObject(name);
        Color ground = ThemeGround(theme), paving = theme == 2 ? new Color(.48f, .49f, .43f) : new Color(.64f, .65f, .57f);
        var floor = new Geometry();
        // Ground infill sits below the original roads/courtyard. It has no collider.
        for (float x = minX - 24; x < maxX + 24; x += 4)
            for (float z = minZ - 24; z < maxZ + 24; z += 4)
            {
                bool inside = x + 2 > minX && x + 2 < maxX && z + 2 > minZ && z + 2 < maxZ;
                float edge = Mathf.Min(Mathf.Abs(x + 2 - minX), Mathf.Abs(x + 2 - maxX), Mathf.Abs(z + 2 - minZ), Mathf.Abs(z + 2 - maxZ));
                Color color = !inside && edge < 4 ? paving : ground;
                float variation = ((Mathf.FloorToInt(x / 4) * 17 + Mathf.FloorToInt(z / 4) * 31) & 3) * .018f;
                // Adjacent tiles share one height. A small apron step left a
                // side-on crack through which the old water backdrop was visible.
                floor.Ground(x, z, Mathf.Min(x + 4, maxX + 24), Mathf.Min(z + 4, maxZ + 24), -.24f, color * (.96f + variation));
            }
        // The distant ground closes the horizon without a blue clear-color strip.
        floor.Ground(minX - 1200, minZ - 1200, maxX + 1200, minZ - 24, -.24f, ground);
        floor.Ground(minX - 1200, maxZ + 24, maxX + 1200, maxZ + 1200, -.24f, ground);
        floor.Ground(minX - 1200, minZ - 24, minX - 24, maxZ + 24, -.24f, ground);
        floor.Ground(maxX + 24, minZ - 24, maxX + 1200, maxZ + 24, -.24f, ground);
        Material floorMaterial = MaterialAsset(Themes[theme] + "Ground", "Vodovorot/Map surround", Color.white);
        floorMaterial.SetFloat("_Pattern", theme == 1 ? .25f : .7f);
        floorMaterial.SetFloat("_TileSize", theme == 2 ? 1.2f : 2f);
        SaveObject(root.transform, "Ground and perimeter paths", floor, Themes[theme] + "Ground", floorMaterial);
        var decor = new Geometry();
        var random = new System.Random(471 + theme);
        for (int side = 0; side < 4; side++)
        {
            float length = side < 2 ? maxX - minX : maxZ - minZ;
            for (float along = 2; along < length; along += 8)
            {
                float x = side < 2 ? minX + along : side == 2 ? minX - 7 : maxX + 7;
                float z = side < 2 ? side == 0 ? minZ - 7 : maxZ + 7 : minZ + along;
                // Small forms frame the edge while staying outside the playable wall plane.
                if (theme == 2)
                {
                    Color stone = new Color(.47f, .46f, .40f);
                    decor.Box(new Vector3(x, .35f, z), new Vector3(2.1f, .7f, 2.1f), stone);
                    decor.Box(new Vector3(x, 1.15f, z), new Vector3(1.45f, .9f, 1.45f), stone * .92f);
                    decor.Box(new Vector3(x, 1.68f, z), new Vector3(1.8f, .18f, 1.8f), stone * 1.08f);
                    decor.Box(new Vector3(x, 2.5f, z), new Vector3(.12f, 1.6f, .12f), new Color(.28f, .21f, .13f));
                    decor.Box(new Vector3(x + .45f, 2.8f, z), new Vector3(.8f, .75f, .06f), new Color(.75f, .28f, .16f));
                    decor.Pyramid(new Vector3(x - 2.5f, .05f, z + .6f), 1.5f, 1.05f, new Color(.28f, .42f, .24f));
                }
                else
                {
                    decor.Box(new Vector3(x, .18f, z), new Vector3(2.2f, .36f, 2.2f), paving * .7f);
                    decor.Box(new Vector3(x, .8f, z), new Vector3(.3f, 1.6f, .3f), new Color(.35f, .25f, .15f));
                    decor.Pyramid(new Vector3(x, 1.1f, z), 2.8f, 2.4f, new Color(.34f + (float)random.NextDouble() * .14f, .57f, .22f));
                    decor.Pyramid(new Vector3(x, 2f, z), 2.1f, 1.6f, new Color(.48f, .68f, .28f));
                }
                if (theme == 0)
                {
                    float bx = side < 2 ? x : x + (side == 2 ? -12 : 12);
                    float bz = side < 2 ? z + (side == 0 ? -12 : 12) : z;
                    float height = 3 + (float)random.NextDouble() * 4;
                    Color wall = new Color(.48f + (float)random.NextDouble() * .22f, .53f + (float)random.NextDouble() * .12f, .49f);
                    decor.Box(new Vector3(bx, height * .5f, bz), new Vector3(5, height, 4), wall);
                    decor.Box(new Vector3(bx, height + .15f, bz), new Vector3(5.2f, .3f, 4.2f), wall * .7f);
                    for (int level = 0; level < 3; level++)
                        decor.Box(new Vector3(bx, height * .2f + level * height * .27f, bz - 2.02f), new Vector3(3.8f, .45f, .04f), new Color(.37f, .59f, .63f));
                }
            }
        }
        // Low curbs make the collision boundary legible, never taller than the player's rim.
        Color curb = paving * .8f;
        if (theme == 2)
        {
            // Small individual blocks read as masonry without a texture atlas
            // or extra colliders, and leave the whole original map accessible.
            for (int side = 0; side < 4; side++)
            {
                float length = side < 2 ? maxX - minX : maxZ - minZ;
                for (float along = .55f; along < length; along += 1.2f)
                {
                    float x = side < 2 ? minX + along : side == 2 ? minX - 1.2f : maxX + 1.2f;
                    float z = side < 2 ? side == 0 ? minZ - 1.2f : maxZ + 1.2f : minZ + along;
                    Vector3 size = side < 2 ? new Vector3(1.12f, .72f, .55f) : new Vector3(.55f, .72f, 1.12f);
                    Color stone = new Color(.47f, .46f, .40f) * (Mathf.FloorToInt(along) % 2 == 0 ? 1f : .94f);
                    decor.Box(new Vector3(x, .36f, z), size, stone);
                    if (Mathf.FloorToInt(along / 1.2f) % 2 == 0)
                        decor.Box(new Vector3(x, .88f, z), size * .65f, stone * 1.08f);
                }
            }
        }
        decor.Box(new Vector3((minX + maxX) * .5f, .1f, minZ - .35f), new Vector3(maxX - minX, .2f, .4f), curb);
        decor.Box(new Vector3((minX + maxX) * .5f, .1f, maxZ + .35f), new Vector3(maxX - minX, .2f, .4f), curb);
        decor.Box(new Vector3(minX - .35f, .1f, (minZ + maxZ) * .5f), new Vector3(.4f, .2f, maxZ - minZ), curb);
        decor.Box(new Vector3(maxX + .35f, .1f, (minZ + maxZ) * .5f), new Vector3(.4f, .2f, maxZ - minZ), curb);
        SaveObject(root.transform, "Planters and distant silhouettes", decor, Themes[theme] + "Decor", MaterialAsset("SurroundDecor", "Vodovorot/Map surround", Color.white));
    }

    private static object ArrangeOpening(Scene scene, BlackHoleController player, int theme)
    {
        // Re-running other repairs must not repeatedly pull more objects from the map.
        string stamp = "Opening route authored";
        if (scene.GetRootGameObjects().Any(x => x.name == stamp))
            return new
            {
                scene = scene.path,
                unchanged = true
            };
        Vector3 start = player.transform.position;
        Vector3 fit = player.GetFitSize();
        var candidates = All<FallingObject>(scene).Where(x => !x.isColon && x.GetComponent<Renderer>() != null && IsLooseProp(x.name) &&
            !x.name.StartsWith("Props_Traffic cone") && !x.name.StartsWith("Natures_Pot Bush_small") && !x.name.StartsWith("Natures_Rock_Big"))
            .Where(x => { Bounds b = BoundsOf(x); return x.gameObject.activeInHierarchy && b.min.y > -.3f && b.min.y < .5f && b.size.y < 1.6f && Mathf.Max(b.size.x, b.size.z) < 1.3f; }).ToArray();
        var small = candidates.Where(x => Tool.CanFit2D(SizeOf(x), fit) && Mathf.Max(BoundsOf(x).size.x, BoundsOf(x).size.z) < .48f)
            .OrderBy(x => (x.transform.position - start).sqrMagnitude).Take(40).ToArray();
        var chosen = new HashSet<FallingObject>(small);
        var medium = new List<FallingObject>();
        int secondValue = 0;
        foreach (FallingObject item in candidates.Where(x => !chosen.Contains(x) && Tool.CanFit2D(SizeOf(x), fit * (.81f / .41f)))
            .OrderByDescending(x => Value(SizeOf(x))).ThenBy(x => (x.transform.position - start).sqrMagnitude))
        {
            medium.Add(item);
            secondValue += Value(SizeOf(item));
            if (secondValue >= 170 || medium.Count >= 170)
                break;
        }
        foreach (var item in medium)
            chosen.Add(item);
        var placed = new List<Bounds>();
        Bounds[] ground = All<Renderer>(scene).Select(x => x.bounds).Where(x => x.size.y < .25f &&
            x.size.x * x.size.z > 4 && x.max.y >= -.5f && x.max.y < .6f).ToArray();
        var edits = new List<object>();
        int index = 0;
        foreach (FallingObject item in small.Concat(medium))
        {
            bool first = index < small.Length;
            int slot = first ? index : index - small.Length;
            float forward = first ? 1.4f + (slot / 2) * .55f : 13f + (slot / 4) * .85f;
            float lateral = first ? (slot % 2 == 0 ? -.19f : .19f) : (slot % 4 - 1.5f) * .65f + Mathf.Sin(slot / 4 * .18f) * 1.1f;
            Vector3 right;
            Vector3 point = RoutePoint(theme, start, forward, lateral, out right);
            Bounds bounds = BoundsOf(item);
            Vector3 scoreSize = SizeOf(item);
            Vector3 before = item.transform.position;
            bool clear = false;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float dx = attempt == 0 ? 0 : ((attempt + 1) / 2) * (attempt % 2 == 0 ? -.5f : .5f);
                Vector3 candidate = point + right * dx;
                float floor = theme == 2 ? -.15f : 0f;
                foreach (Bounds surface in ground)
                    if (candidate.x >= surface.min.x && candidate.x <= surface.max.x &&
                        candidate.z >= surface.min.z && candidate.z <= surface.max.z)
                        floor = Mathf.Max(floor, surface.max.y);
                candidate.y = floor + .015f;
                Bounds proposed = new Bounds(new Vector3(candidate.x, candidate.y + bounds.extents.y, candidate.z), bounds.size);
                if (placed.Any(x => x.Intersects(proposed)))
                    continue;
                Collider[] nearby = Physics.OverlapBox(proposed.center, Vector3.Max(proposed.extents - Vector3.one * .015f, Vector3.one * .01f));
                bool blocked = nearby.Any(c => !c.isTrigger && !c.transform.IsChildOf(player.transform) &&
                    !(c.bounds.size.y < .25f && c.bounds.size.x * c.bounds.size.z > 5) &&
                    !(c.GetComponentInParent<FallingObject>() is FallingObject owner && chosen.Contains(owner)));
                if (blocked)
                    continue;
                Vector3 delta = proposed.center - bounds.center;
                // Author the same default components Awake would otherwise add,
                // so the touched prop's fit/physics remains inspectable before Play.
                if (item.GetComponent<Collider>() == null)
                    item.gameObject.AddComponent<BoxCollider>();
                if (item.GetComponent<Rigidbody>() == null)
                    item.gameObject.AddComponent<Rigidbody>().isKinematic = true;
                item.transform.position += delta;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                placed.Add(proposed);
                Physics.SyncTransforms();
                clear = true;
                edits.Add(new
                {
                    name = PathOf(item.transform),
                    before = before.ToString(),
                    after = item.transform.position.ToString(),
                    size = scoreSize.ToString(),
                    value = Value(scoreSize),
                    wave = first ? 1 : 2
                });
                break;
            }
            index++;
            if (!clear)
                Debug.LogWarning("Opening route kept blocked prop in place: " + item.name);
        }
        new GameObject(stamp);
        return new
        {
            scene = scene.path,
            initialCandidates = small.Length,
            secondCandidates = medium.Count,
            moved = edits.Count,
            edits
        };
    }

    private static bool IsLooseProp(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("props_") || lower.Contains("pot") || lower.Contains("bush") || lower.Contains("stone") ||
            lower.Contains("rock") || lower.Contains("barrel") || lower.Contains("crate") || lower == "lamp 7";
    }
    private static Vector3 RoutePoint(int theme, Vector3 start, float distance, float lateral, out Vector3 right)
    {
        if (theme != 1)
        {
            right = Vector3.right;
            return start + new Vector3(lateral, 0, distance);
        }
        Vector3[] route = { Vector3.zero,new Vector3(0,0,5),new Vector3(7.2f,0,5),new Vector3(7.2f,0,20),
            new Vector3(-3,0,20),new Vector3(-3,0,32) };
        for (int i = 1; i < route.Length; i++)
        {
            Vector3 direction = (route[i] - route[i - 1]).normalized;
            float length = Vector3.Distance(route[i], route[i - 1]);
            if (distance <= length || i == route.Length - 1)
            {
                right = Vector3.Cross(Vector3.up, direction);
                return start + route[i - 1] + direction * distance + right * lateral;
            }
            distance -= length;
        }
        right = Vector3.right;
        return start;
    }
    private static Bounds BoundsOf(FallingObject item) => item.GetComponent<Collider>() != null
        ? item.GetComponent<Collider>().bounds : item.GetComponent<Renderer>().bounds;
    private static Vector3 SizeOf(FallingObject item)
    {
        var bounds = new Bounds(item.transform.position, Vector3.zero);
        bounds.Encapsulate(BoundsOf(item));
        return bounds.size;
    }

    private static int Value(Vector3 size)
    {
        // Authoring estimate only; CheckMapPolish compares every moved prop
        // against the real FallingObject.Start result before accepting the layout.
        float volume = size.x * size.y * size.z;
        if (volume <= .087f)
            return 1;
        if (volume <= .51f)
            return 2;
        if (volume <= 10.63f)
            return 3;
        if (volume <= 20f)
            return 5;
        if (volume <= 60f)
            return 10;
        if (volume <= 100f)
            return 25;
        if (volume <= 250f)
            return 40;
        if (volume <= 860f)
            return 60;
        return 100;
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true));
    private static void RequireEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
    }
    private static void Folder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static Material MaterialAsset(string name, string shader, Color color)
    {
        string path = Art + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }
    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Art + "/" + name + ".asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, saved);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(saved);
        return saved;
    }
    private static void SaveObject(Transform root, string name, Geometry geometry, string meshName, Material material)
    {
        GameObject child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(root, false);
        child.GetComponent<MeshFilter>().sharedMesh = SaveMesh(geometry.Mesh(meshName), meshName);
        child.GetComponent<MeshRenderer>().sharedMaterial = material;
        child.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        child.GetComponent<MeshRenderer>().receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
    }
    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int n = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            colors.AddRange(new[] { color, color, color, color });
            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        public void Ground(float x0, float z0, float x1, float z1, float y, Color color) => Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), color);
        public void Box(Vector3 p, Vector3 size, Color color)
        {
            Vector3 e = size * .5f;
            float x = p.x, y = p.y, z = p.z, a = e.x, b = e.y, c = e.z;
            Ground(x - a, z - c, x + a, z + c, y + b, color);
            Side(new Vector3(x - a, y - b, z - c), new Vector3(x + a, y - b, z - c), new Vector3(x + a, y + b, z - c), new Vector3(x - a, y + b, z - c), color);
            Side(new Vector3(x + a, y - b, z + c), new Vector3(x - a, y - b, z + c), new Vector3(x - a, y + b, z + c), new Vector3(x + a, y + b, z + c), color);
            Side(new Vector3(x - a, y - b, z + c), new Vector3(x - a, y - b, z - c), new Vector3(x - a, y + b, z - c), new Vector3(x - a, y + b, z + c), color);
            Side(new Vector3(x + a, y - b, z - c), new Vector3(x + a, y - b, z + c), new Vector3(x + a, y + b, z + c), new Vector3(x + a, y + b, z - c), color);
        }
        private void Side(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) => Quad(a, d, c, b, color);
        public void Pyramid(Vector3 p, float width, float height, Color color)
        {
            float e = width * .5f;
            Vector3 top = p + Vector3.up * height;
            Vector3[] ring = { p + new Vector3(-e, 0, -e), p + new Vector3(-e, 0, e), p + new Vector3(e, 0, e), p + new Vector3(e, 0, -e) };
            for (int i = 0; i < 4; i++)
                Quad(ring[i], ring[(i + 1) % 4], top, top, color);
        }
        public Mesh Mesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(vertices);
            m.SetColors(colors);
            m.SetTriangles(triangles, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
