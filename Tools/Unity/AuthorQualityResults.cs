using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Vodovorot.RoundExperience;
using Vodovorot.Stage2;
using Vodovorot.Stage3;

/// <summary>Saved native layout groups own the results structure at every DPR.</summary>
public static class AuthorQualityResults
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use clean Edit Mode.");
        const string path = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("ResultsCanvas") != null)
                throw new InvalidOperationException("Results already authored; inspect before rebuilding.");
            GamingManager game = root.GetComponentInChildren<GamingManager>(true);
            RankingVisualStyle style = RankingVisualStyle.Load();
            RectTransform canvasRoot = Rect(root.transform, "ResultsCanvas");
            Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            canvasRoot.gameObject.AddComponent<CssCanvasScale>();
            CanvasGroup group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            Image dim = canvasRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(.04f, .08f, .12f, .45f);
            RectTransform safe = Rect(canvasRoot, "SafeArea");
            SettingsSafeArea adapter = canvasRoot.gameObject.AddComponent<SettingsSafeArea>();
            SetReference(adapter, "content", safe);
            VerticalLayoutGroup stack = safe.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(12, 12, 12, 12);
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            ConfigurePanel(game.MobpanelOfEnd, game, true, safe, style);
            ConfigurePanel(game.DeskpanelOfEnd, game, false, safe, style);
            ResultsCanvasVisibility visibility = canvasRoot.gameObject.AddComponent<ResultsCanvasVisibility>();
            SetReference(visibility, "group", group);
            SetReference(visibility, "mobilePanel", game.MobpanelOfEnd);
            SetReference(visibility, "desktopPanel", game.DeskpanelOfEnd);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        RepairSizingAndOrder();
        return "Saved native wrapping result columns, original reward controls and authored ranking/share actions.";
    }

    public static string RepairSizingAndOrder()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use Edit Mode.");
        const string path = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GamingManager game = root.GetComponentInChildren<GamingManager>(true);
            foreach (GameObject panel in new[] { game.MobpanelOfEnd, game.DeskpanelOfEnd })
            {
                Image original = panel.GetComponent<Image>();
                Transform existing = panel.transform.Find("Paper background");
                RectTransform background = existing != null ? (RectTransform)existing : Rect(panel.transform, "Paper background");
                Image image = background.GetComponent<Image>();
                if (image == null)
                    image = background.gameObject.AddComponent<Image>();
                image.sprite = original.sprite;
                image.type = original.type;
                image.color = original.color;
                image.pixelsPerUnitMultiplier = original.pixelsPerUnitMultiplier;
                image.fillCenter = original.fillCenter;
                image.preserveAspect = false;
                Element(background.gameObject, -1, -1).ignoreLayout = true;
                background.SetAsFirstSibling();
                // An Image also supplies a preferred layout size from its sprite.
                // Keep the decorative graphic off the layout root so its source
                // pixel dimensions do not override the native content dimensions.
                original.enabled = false;
                LayoutElement paperSize = panel.GetComponent<LayoutElement>();
                paperSize.minWidth = paperSize.minHeight = 0;
                paperSize.preferredWidth = 800;
                paperSize.flexibleWidth = paperSize.flexibleHeight = 0;
                paperSize.preferredHeight = -1;
                panel.transform.Find("Result heading").SetSiblingIndex(1);
                panel.GetComponent<EndGameController>().AccountGoalText.transform.SetSiblingIndex(2);
                panel.transform.Find("Wrapping result columns").SetSiblingIndex(3);
                Transform body = panel.transform.Find("Wrapping result columns");
                body.GetComponent<GridLayoutGroup>().cellSize = new Vector2(236, 174);
                Transform actions = body.Find("Result actions");
                foreach (Transform child in actions)
                    child.GetComponent<LayoutElement>().preferredHeight = child.name == "Ranking and share" ? 42 : 54;

            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "Saved content-sized paper background and explicit heading/goal/body order.";
    }

    private static void ConfigurePanel(GameObject panel, GamingManager game, bool mobile, Transform safe, RankingVisualStyle style)
    {
        EndGameController result = panel.GetComponent<EndGameController>();
        Text[] labels = mobile ? game.PanelOfEnd : game.DPanelOfEnd;
        Text title = labels[mobile ? 0 : 1];
        Text[] captions = { labels[mobile ? 1 : 0], labels[2], labels[3] };
        Text[] values = mobile ? new[] { result.MexpText, result.McoinText, result.MbrillText }
            : new[] { result.expText, result.coinText, result.brillText };
        Button next = labels[4].GetComponentInParent<Button>(true);
        Button ad = labels[5].GetComponentInParent<Button>(true);
        panel.transform.SetParent(safe, false);
        Reset(panel.GetComponent<RectTransform>());
        LayoutElement paperSize = Element(panel, -1, -1);
        paperSize.preferredWidth = 960;
        VerticalLayoutGroup paper = panel.AddComponent<VerticalLayoutGroup>();
        paper.padding = new RectOffset(12, 12, 12, 12);
        paper.spacing = 8;
        paper.childAlignment = TextAnchor.MiddleCenter;
        paper.childControlWidth = paper.childControlHeight = true;
        paper.childForceExpandWidth = paper.childForceExpandHeight = false;
        panel.GetComponent<Image>().color = style.paper;

        RectTransform header = Rect(panel.transform, "Result heading");
        Element(header.gameObject, 248, 36);
        result.resultImage.transform.SetParent(header, false);
        Anchor(result.resultImage.rectTransform, .02f, .05f, .17f, .95f);
        result.resultImage.preserveAspect = true;
        title.transform.SetParent(header, false);
        Anchor(title.rectTransform, .22f, 0, .98f, 1);
        TextStyle(title, style, 28, 20);
        result.AccountGoalText.transform.SetParent(panel.transform, false);
        Reset(result.AccountGoalText.rectTransform);
        Element(result.AccountGoalText.gameObject, 700, 22);
        TextStyle(result.AccountGoalText, style, 17, 13);

        RectTransform body = Rect(panel.transform, "Wrapping result columns");
        LayoutElement bodySize = Element(body.gameObject, -1, -1);
        bodySize.flexibleWidth = 1;
        GridLayoutGroup grid = body.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(236, 196);
        grid.spacing = new Vector2(16, 16);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        RectTransform stats = Rect(body, "Reward totals");
        VerticalLayoutGroup rows = stats.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.spacing = 6;
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandWidth = rows.childForceExpandHeight = true;
        for (int index = 0; index < 3; index++)
        {
            if (!values[index].transform.IsChildOf(panel.transform))
                throw new InvalidOperationException("Numeric reference belongs to another results panel.");
            RectTransform row = Rect(stats, "Reward row " + index);
            Element(row.gameObject, -1, 64);
            Transform icon = values[index].transform.Find(index == 0 ? "Image (1)" : "Image");
            if (icon == null)
                throw new InvalidOperationException("Missing authored reward icon.");
            icon.SetParent(row, false);
            captions[index].transform.SetParent(row, false);
            values[index].transform.SetParent(row, false);
            Anchor(captions[index].rectTransform, 0, 0, .59f, 1);
            Anchor((RectTransform)icon, .62f, .22f, .76f, .78f);
            Anchor(values[index].rectTransform, .80f, 0, 1, 1);
            TextStyle(captions[index], style, 23, 15);
            captions[index].alignment = TextAnchor.MiddleRight;
            TextStyle(values[index], style, 25, 15);
            values[index].alignment = TextAnchor.MiddleLeft;
            icon.GetComponent<Image>().preserveAspect = true;
        }
        RectTransform actions = Rect(body, "Result actions");
        VerticalLayoutGroup buttons = actions.gameObject.AddComponent<VerticalLayoutGroup>();
        buttons.spacing = 8;
        buttons.childAlignment = TextAnchor.MiddleCenter;
        buttons.childControlWidth = buttons.childControlHeight = true;
        buttons.childForceExpandWidth = true;
        buttons.childForceExpandHeight = false;
        next.transform.SetParent(actions, false);
        ad.transform.SetParent(actions, false);
        Reset(next.GetComponent<RectTransform>());
        Reset(ad.GetComponent<RectTransform>());
        Element(next.gameObject, 236, 64);
        Element(ad.gameObject, 236, 64);
        Anchor(labels[4].rectTransform, .04f, .06f, .96f, .94f);
        Anchor(labels[5].rectTransform, .04f, .06f, .96f, .94f);
        ButtonLabel(labels[4], style, 23);
        ButtonLabel(labels[5], style, 17);
        RectTransform footer = Rect(actions, "Ranking and share");
        Element(footer.gameObject, 236, 44);
        HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 10;
        footerLayout.childControlWidth = footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = true;
        Button ranking = Action(footer, "Ranking", "Рейтинг", style.blue, style);
        Button share = Action(footer, "Share", "Поделиться", style.amber, style);
        Stage2RoundPresentation presentation = panel.GetComponent<Stage2RoundPresentation>();
        if (presentation == null)
            presentation = panel.AddComponent<Stage2RoundPresentation>();
        SetReference(presentation, "actionBar", footer);
        SetReference(presentation, "rankingButton", ranking);
        SetReference(presentation, "shareButton", share);
        panel.SetActive(false);
    }

    private static Button Action(Transform parent, string name, string text, Color color, RankingVisualStyle style)
    {
        RectTransform rect = Rect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Text label = Rect(rect, "Label").gameObject.AddComponent<Text>();
        label.text = text;
        ButtonLabel(label, style, 16);
        return button;
    }

    private static void ButtonLabel(Text text, RankingVisualStyle style, int maximum)
    {
        TextStyle(text, style, maximum, 12);
        text.color = Color.white;
        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = style.outline;
        outline.effectDistance = new Vector2(1, -1);
    }

    private static void TextStyle(Text text, RankingVisualStyle style, int maximum, int minimum)
    {
        text.font = style.comicFont;
        text.color = style.ink;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minimum;
        text.resizeTextMaxSize = text.fontSize = maximum;
        text.raycastTarget = false;
    }

    private static LayoutElement Element(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        return element;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Anchor(rect, 0, 0, 1, 1);
        return rect;
    }

    private static void Reset(RectTransform rect) => Anchor(rect, 0, 0, 1, 1);

    private static void Anchor(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
