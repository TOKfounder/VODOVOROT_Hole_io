using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using PinePie.SimpleJoystick.Examples.DemoScript;

public static class AuthorQualityGuidance
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use clean Edit Mode.");
        const string path = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("RoundGuidanceCanvas") != null)
                throw new InvalidOperationException("Guidance already authored; inspect before rebuilding.");
            RankingVisualStyle style = RankingVisualStyle.Load();
            RectTransform canvasRoot = Rect(root.transform, "RoundGuidanceCanvas", Vector2.zero, Vector2.one);
            Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasRoot.gameObject.AddComponent<CssCanvasScale>();
            RectTransform safe = Rect(canvasRoot, "SafeArea", Vector2.zero, Vector2.one);
            SettingsSafeArea adapter = canvasRoot.gameObject.AddComponent<SettingsSafeArea>();
            SetReference(adapter, "content", safe);
            RectTransform panel = Rect(safe, "Required size hint", new Vector2(.14f, .72f), new Vector2(.86f, .79f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.05f, .10f, .13f, .92f);
            background.raycastTarget = false;
            Text label = Rect(panel, "Label", new Vector2(.03f, .05f), new Vector2(.97f, .95f)).gameObject.AddComponent<Text>();
            label.font = style.comicFont;
            label.fontSize = label.resizeTextMaxSize = 21;
            label.resizeTextMinSize = 14;
            label.resizeTextForBestFit = true;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Нужен размер 3";
            label.raycastTarget = false;
            TooLargeTargetFeedback feedback = root.AddComponent<TooLargeTargetFeedback>();
            SetReference(feedback, "player", root.GetComponentInChildren<BlackHoleController>(true));
            SetReference(feedback, "movement", root.GetComponentInChildren<MovementScript>(true));
            SetReference(feedback, "game", root.GetComponentInChildren<GamingManager>(true));
            SetReference(feedback, "panel", panel.gameObject);
            SetReference(feedback, "label", label);
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Material target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/MapPolish/TargetBracket.mat");
            target.SetColor("_OutlineColor", new Color(.015f, .055f, .025f, 1f));
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "Saved required-size feedback and explicit dark target outline; original fit/score unchanged.";
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 minimum, Vector2 maximum)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
