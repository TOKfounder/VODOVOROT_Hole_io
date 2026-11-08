using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using Vodovorot.Stage2;

public static class PolishQualityIcons
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        try
        {
            Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            Sprite left = ShopArrow(menu, false);
            Sprite right = ShopArrow(menu, true);
            RankingVisualStyle style = RankingVisualStyle.Load();
            SkinComparisonView comparison = All<SkinComparisonView>(menu).Single();
            if (Reference<Button>(comparison, "previous").transform.Find("Authored arrow icon") != null)
                throw new InvalidOperationException("Icons already authored; use the focused repair method.");
            foreach (string field in new[] { "previous", "next" })
            {
                Button button = Reference<Button>(comparison, field);
                button.GetComponentInChildren<Text>(true).enabled = false;
                RectTransform rect = new GameObject("Authored arrow icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(button.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(38, 38);
                // Both original shop arrows use GUI_55; the left one reflects X.
                rect.localScale = new Vector3(field == "previous" ? -1 : 1, 1, 1);
                Image icon = rect.GetComponent<Image>();
                icon.sprite = field == "next" ? right : left;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            foreach (LayoutElement size in comparison.GetComponentsInChildren<LayoutElement>(true)
                .Where(element => element.name == "Comparison navigation" || element.GetComponent<Button>() != null))
                size.preferredHeight = style.minimumButtonHeight;
            foreach (Button button in comparison.GetComponentsInChildren<Button>(true))
                StyleButton(button, style);
            var comparisonData = new SerializedObject(comparison);
            SerializedProperty triggers = comparisonData.FindProperty("openButtons");
            for (int i = 0; i < triggers.arraySize; i++)
                StyleButton((Button)triggers.GetArrayElementAtIndex(i).objectReferenceValue, style);
            GameObject sheet = Reference<GameObject>(comparison, "sheet");
            sheet.GetComponent<Image>().sprite = style.filledShape;
            sheet.GetComponent<Image>().type = Image.Type.Sliced;
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            const string prefabPath = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform panel = prefab.transform.Find("RoundGuidanceCanvas/SafeArea/Optional route goal");
                if (panel.Find("Direction arrow") != null)
                    throw new InvalidOperationException("Icons already authored; inspect before reapplying.");
                RectTransform arrow = new GameObject("Direction arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                arrow.SetParent(panel, false);
                arrow.anchorMin = arrow.anchorMax = new Vector2(0, .5f);
                arrow.anchoredPosition = new Vector2(22, 0);
                arrow.sizeDelta = new Vector2(24, 24);
                Image icon = arrow.GetComponent<Image>();
                icon.sprite = right;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform label = panel.Find("Label") as RectTransform;
                label.anchorMin = new Vector2(.12f, .03f);
                label.anchorMax = new Vector2(.975f, .97f);
                foreach (Transform paper in prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Paper background"))
                {
                    Image background = paper.GetComponent<Image>();
                    background.sprite = style.filledShape;
                    background.type = Image.Type.Sliced;
                    Outline edge = background.GetComponent<Outline>() ?? background.gameObject.AddComponent<Outline>();
                    edge.effectColor = style.outline;
                    edge.effectDistance = new Vector2(2, -2);
                }
                foreach (Stage2RoundPresentation presentation in prefab.GetComponentsInChildren<Stage2RoundPresentation>(true))
                {
                    StyleButton(Reference<Button>(presentation, "rankingButton"), style);
                    StyleButton(Reference<Button>(presentation, "shareButton"), style);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string path in new[] { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity", RoundExperienceLab.ScenePath })
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                LateRoundGoals goals = All<LateRoundGoals>(scene).Single();
                Image arrow = All<Transform>(scene).Single(t => t.name == "Direction arrow").GetComponent<Image>();
                var data = new SerializedObject(goals);
                data.FindProperty("directionArrow").objectReferenceValue = arrow;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved actual shop arrow sprites, rounded canonical buttons and explicit objective direction images.";
    }
    public static string RepairLeftReflection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        try
        {
            Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            SkinComparisonView comparison = All<SkinComparisonView>(menu).Single();
            Reference<Button>(comparison, "previous").transform.Find("Authored arrow icon").localScale = new Vector3(-1, 1, 1);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved original shop's left-arrow X reflection.";
    }

    private static Sprite ShopArrow(Scene scene, bool right)
    {
        foreach (Button button in All<Button>(scene))
        {
            var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                if (button.onClick.GetPersistentTarget(index) is HorizontalLayout3D &&
                    button.onClick.GetPersistentMethodName(index) == "RotateOnDeg" &&
                    calls.GetArrayElementAtIndex(index).FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue == right &&
                    button.image != null && button.image.sprite != null)
                    return button.image.sprite;
        }
        throw new InvalidOperationException("Missing canonical shop arrow " + right);
    }
    private static void StyleButton(Button button, RankingVisualStyle style)
    {
        if (button == null) return;
        button.image.sprite = style.filledShape;
        button.image.type = Image.Type.Sliced;
        Outline edge = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        edge.effectColor = style.outline;
        edge.effectDistance = new Vector2(2, -2);
        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            Outline outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = style.outline;
            outline.effectDistance = new Vector2(1, -1);
        }
    }
    private static T Reference<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object =>
        (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
