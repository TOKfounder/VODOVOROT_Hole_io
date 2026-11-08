using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

public static class AuthorSkinComparison
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        try
        {
            if (scene.GetRootGameObjects().Any(root => root.name == "SkinComparisonCanvas"))
                throw new InvalidOperationException("Comparison already authored; inspect before rebuilding.");
            const string art = "Assets/Art/SkinComparison";
            if (!AssetDatabase.IsValidFolder(art)) AssetDatabase.CreateFolder("Assets/Art", "SkinComparison");
            SkinVisualCatalog catalog = SkinVisualCatalog.Load();
            var ru = new Texture2D[5];
            var en = new Texture2D[5];
            for (int index = 0; index < 5; index++)
            {
                ru[index] = Portrait(catalog.GetByLegacyIndex(index), true, art);
                en[index] = Portrait(catalog.GetByLegacyIndex(index), false, art);
            }
            RankingVisualStyle style = RankingVisualStyle.Load();
            RectTransform canvasRoot = Rect(null, "SkinComparisonCanvas");
            Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11000;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            canvasRoot.gameObject.AddComponent<CanvasScaler>();
            canvasRoot.gameObject.AddComponent<CssCanvasScale>();
            RectTransform safe = Rect(canvasRoot, "SafeArea");
            Set(canvasRoot.gameObject.AddComponent<SettingsSafeArea>(), "content", safe);
            RectTransform sheet = Rect(safe, "Comparison sheet");
            sheet.offsetMin = new Vector2(8, 8);
            sheet.offsetMax = new Vector2(-8, -8);
            sheet.gameObject.AddComponent<Image>().color = style.paper;
            VerticalLayoutGroup stack = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(10, 10, 8, 8);
            stack.spacing = 6;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            Text title = Label(sheet, "Title", 26, 32, style.ink);
            Text promise = Label(sheet, "Cosmetic promise", 17, 28, style.ink);
            RectTransform cards = Rect(sheet, "Two catalogue skins");
            Element(cards.gameObject, -1, 0, 1);
            HorizontalLayoutGroup columns = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.spacing = 10;
            columns.childControlWidth = columns.childControlHeight = true;
            columns.childForceExpandWidth = columns.childForceExpandHeight = true;
            Card(cards, "Equipped", style, out Text equippedName, out RawImage equippedImage, out Text equippedDescription);
            Card(cards, "Selected", style, out Text selectedName, out RawImage selectedImage, out Text selectedDescription);
            RectTransform footer = Rect(sheet, "Comparison navigation");
            Element(footer.gameObject, -1, 44);
            HorizontalLayoutGroup navigation = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            navigation.spacing = 10;
            navigation.childControlWidth = navigation.childControlHeight = true;
            navigation.childForceExpandWidth = navigation.childForceExpandHeight = true;
            Button previous = Button(footer, "Previous skin", "←", style.blue, 24);
            Element(previous.gameObject, 64, 44);
            Button close = Button(footer, "Back to shop", "В магазин", style.green, 20);
            Element(close.gameObject, 220, 44, 1);
            Button next = Button(footer, "Next skin", "→", style.blue, 24);
            Element(next.gameObject, 64, 44);
            MainMenuController menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            HorizontalLayout3D[] shops = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HorizontalLayout3D>(true)).ToArray();
            var open = new Button[2];
            Text[] oldHeadings = { menu.PanelOfSkins[6], menu.DPanelOfSkins[6] };
            for (int index = 0; index < oldHeadings.Length; index++)
            {
                Text heading = oldHeadings[index];
                RectTransform old = heading.rectTransform;
                Button trigger = Button(old.parent, "Compare with equipped", "Сравнить с надетым", style.blue, 18);
                RectTransform rect = trigger.GetComponent<RectTransform>();
                rect.anchorMin = old.anchorMin;
                rect.anchorMax = old.anchorMax;
                rect.pivot = old.pivot;
                rect.anchoredPosition = old.anchoredPosition;
                rect.sizeDelta = old.sizeDelta + new Vector2(0, 14);
                rect.localScale = old.localScale;
                rect.localRotation = old.localRotation;
                heading.enabled = false;
                open[index] = trigger;
            }
            SkinComparisonView view = canvasRoot.gameObject.AddComponent<SkinComparisonView>();
            SetArray(view, "shops", shops);
            SetArray(view, "openButtons", open);
            SetArray(view, "portraitsRu", ru);
            SetArray(view, "portraitsEn", en);
            Set(view, "sheet", sheet.gameObject);
            Set(view, "previous", previous);
            Set(view, "next", next);
            Set(view, "close", close);
            Set(view, "title", title);
            Set(view, "promise", promise);
            Set(view, "equippedName", equippedName);
            Set(view, "selectedName", selectedName);
            Set(view, "equippedImage", equippedImage);
            Set(view, "selectedImage", selectedImage);
            Set(view, "equippedDescription", equippedDescription);
            Set(view, "selectedDescription", selectedDescription);
            sheet.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved comparison sheet, two shop entry buttons and10 actual catalogue portraits; no new purchase route.";
    }

    private static Texture2D Portrait(SkinVisualCatalog.Entry entry, bool ru, string folder)
    {
        string path = folder + "/" + entry.id + (ru ? "-ru" : "-en") + ".png";
        Texture2D generated = SkinPortraitRenderer.Render(entry, ru, 512);
        try { File.WriteAllBytes(path, generated.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(generated); }
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void Card(Transform parent, string name, RankingVisualStyle style, out Text title, out RawImage image, out Text description)
    {
        RectTransform card = Rect(parent, name);
        card.gameObject.AddComponent<Image>().color = new Color(.91f, .94f, .89f);
        Element(card.gameObject, 0, 0, 1).flexibleWidth = 1;
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 4, 4);
        layout.spacing = 3;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        title = Label(card, "Skin name", 20, 28, style.ink);
        RectTransform frame = Rect(card, "Portrait frame");
        Element(frame.gameObject, 0, 0, 1);
        RectTransform picture = Rect(frame, "Actual catalogue portrait");
        image = picture.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        AspectRatioFitter ratio = picture.gameObject.AddComponent<AspectRatioFitter>();
        ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        ratio.aspectRatio = 1;
        description = Label(card, "Visible cosmetic differences", 17, 48, style.ink);
    }

    private static Button Button(Transform parent, string name, string caption, Color color, int size)
    {
        RectTransform rect = Rect(parent, name);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        Text label = Label(rect, "Label", size, -1, Color.white);
        label.text = caption;
        label.rectTransform.offsetMin = new Vector2(4, 2);
        label.rectTransform.offsetMax = new Vector2(-4, -2);
        return button;
    }
    private static Text Label(Transform parent, string name, int maximum, float height, Color color)
    {
        Text text = Rect(parent, name).gameObject.AddComponent<Text>();
        text.font = RankingVisualStyle.Load().comicFont;
        text.fontSize = text.resizeTextMaxSize = maximum;
        text.resizeTextMinSize = 12;
        text.resizeTextForBestFit = true;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        if (height >= 0) Element(text.gameObject, -1, height);
        return text;
    }
    private static LayoutElement Element(GameObject root, float width, float height, float flex = 0)
    {
        LayoutElement element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
        element.minWidth = element.minHeight = 0;
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleHeight = flex;
        return element;
    }
    private static RectTransform Rect(Transform parent, string name)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
    {
        var data = new SerializedObject(owner);
        var array = data.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
