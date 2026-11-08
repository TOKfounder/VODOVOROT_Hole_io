using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using YG;

/// <summary>Actual authored layout at physical GameView sizes; simulated DPR is labelled separately from WebGL.</summary>
public static class CheckQualityLayout
{
    private static readonly List<object> Checks = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Check(bool condition, string name, object evidence = null)
    {
        Checks.Add(new { name, passed = condition, evidence });
        if (!condition) throw new Exception(name);
    }
    private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    private static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static Rect Bounds(RectTransform rect)
    {
        Vector3[] points = new Vector3[4];
        rect.GetWorldCorners(points);
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    private static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;

    public static async Task<string> Run()
    {
        Checks.Clear();
        string failure = null;
        var view = new ViewScope();
        byte[] save = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        try
        {
            Check(Application.isPlaying && RankSimulationContext.Active && YG2.saves.musicValue == 0 && YG2.saves.soundValue == 0, "isolated silent Play profile");
            SceneManager.LoadScene(1);
            await Task.Delay(700);
            GamingManager game = One<GamingManager>();
            game.EndOfGame();
            await Task.Delay(500);
            foreach (Vector2Int css in new[] { new Vector2Int(1280,720), new Vector2Int(640,360), new Vector2Int(568,320), new Vector2Int(390,844) })
                foreach (int dpr in new[] { 1, 2 })
                {
                    view.Set(css.x * dpr, css.y * dpr);
                    await Task.Delay(250);
                    Check(Screen.width == css.x * dpr && Screen.height == css.y * dpr, "actual GameView pixel dimensions", new { css = css.ToString(), simulatedDpr = dpr, width = Screen.width, height = Screen.height });
                    foreach (bool mobile in new[] { false, true })
                    {
                        game.DeskpanelOfEnd.SetActive(!mobile);
                        game.MobpanelOfEnd.SetActive(mobile);
                        GameObject panel = mobile ? game.MobpanelOfEnd : game.DeskpanelOfEnd;
                        Canvas canvas = panel.GetComponentInParent<Canvas>();
                        canvas.GetComponent<CssCanvasScale>().enabled = false;
                        canvas.GetComponent<CanvasScaler>().scaleFactor = dpr;
                        foreach (bool ru in new[] { true, false })
                        {
                            YG2.saves.langRu = ru;
                            game.UpdateUI();
                            await Task.Delay(60);
                            Canvas.ForceUpdateCanvases();
                            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.transform.GetChild(0) as RectTransform);
                            Canvas.ForceUpdateCanvases();
                            string tag = css + " @" + dpr + (mobile ? " mobile" : " desktop") + (ru ? " RU" : " EN");
                            Rect paper = Bounds((RectTransform)panel.transform);
                            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
                            Check(Inside(screen, paper) && paper.width / dpr <= 801f, "results paper inside viewport with bounded width " + tag, new { paper = paper.ToString(), screen = screen.ToString() });
                            Transform body = panel.transform.Find("Wrapping result columns");
                            Rect header = Bounds(panel.transform.Find("Result heading") as RectTransform);
                            Rect goal = Bounds(panel.GetComponent<EndGameController>().AccountGoalText.rectTransform);
                            Rect bodyRect = Bounds(body as RectTransform);
                            Check(!header.Overlaps(goal) && !header.Overlaps(bodyRect) && !goal.Overlaps(bodyRect), "result heading, account goal and body separate " + tag);
                            Rect stats = Bounds(body.Find("Reward totals") as RectTransform);
                            Rect actions = Bounds(body.Find("Result actions") as RectTransform);
                            Check(!stats.Overlaps(actions), "reward totals separate from actions " + tag);
                            foreach (Transform row in body.Find("Reward totals"))
                            {
                                Rect[] texts = row.GetComponentsInChildren<Text>().Select(text => Bounds(text.rectTransform)).ToArray();
                                Rect icon = Bounds(row.GetComponentInChildren<Image>().rectTransform);
                                Check(texts.Length == 2 && !texts[0].Overlaps(texts[1]) && texts.All(text => !text.Overlaps(icon)), "caption, icon, value separate " + row.name + " " + tag);
                            }
                            Button[] buttons = panel.GetComponentsInChildren<Button>();
                            Check(buttons.Length == 4 && buttons.All(button => Inside(paper, Bounds(button.GetComponent<RectTransform>()))), "four original/new actions inside results paper " + tag);
                        }
                    }
                }
            SceneManager.LoadScene(0);
            Time.timeScale = 1;
            await Task.Delay(700);
            SkinComparisonView comparison = One<SkinComparisonView>();
            HorizontalLayout3D shop = One<MobileMenuLayout>().ActiveShop;
            Button opener = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .First(button => button.gameObject.activeInHierarchy && OpensShop(button, shop));
            opener.onClick.Invoke();
            await Task.Delay(100);
            int coins = YG2.saves.goldCoins;
            int diamonds = YG2.saves.diamonds;
            int equipped = YG2.saves.equipedMaterial;
            int[] owned = (int[])YG2.saves.massiveOfObtaining.Clone();
            comparison.Open();
            Check(comparison.IsOpen && comparison.SelectedIndex == shop.ChosenLegacyIndex && comparison.EquippedIndex == equipped, "comparison shows actual selected/equipped catalog identities");
            foreach (string field in new[] { "previous", "next" })
            {
                Button navigation = Field<Button>(comparison, field);
                Check(navigation.GetComponentsInChildren<Image>().Any(image => image.gameObject != navigation.gameObject && image.sprite != null),
                    "comparison navigation uses real graphic icon " + field);
                Transform icon = navigation.transform.Find("Authored arrow icon");
                Check(icon != null && (field == "previous" ? icon.localScale.x < 0 : icon.localScale.x > 0),
                    "comparison arrow preserves original shop direction " + field);
            }
            for (int index = 0; index < 5; index++)
            {
                Check(Field<RawImage>(comparison, "selectedImage").texture != null && comparison.SelectedIndex == shop.ChosenLegacyIndex && comparison.EquippedIndex == equipped,
                    "catalogue portrait follows actual selection " + comparison.SelectedIndex);
                Field<Button>(comparison, "next").onClick.Invoke();
                await Task.Delay(1100);
            }
            foreach (Vector2Int css in new[] { new Vector2Int(640,360), new Vector2Int(390,844) })
            {
                view.Set(css.x, css.y);
                await Task.Delay(250);
                Canvas canvas = comparison.GetComponent<Canvas>();
                canvas.GetComponent<CssCanvasScale>().enabled = false;
                canvas.GetComponent<CanvasScaler>().scaleFactor = 1;
                Canvas.ForceUpdateCanvases();
                Rect screen = new Rect(0, 0, Screen.width, Screen.height);
                GameObject sheet = Field<GameObject>(comparison, "sheet");
                Check(Inside(screen, Bounds(sheet.GetComponent<RectTransform>())), "comparison sheet inside viewport " + css);
                Rect left = Bounds(Field<RawImage>(comparison, "equippedImage").rectTransform);
                Rect right = Bounds(Field<RawImage>(comparison, "selectedImage").rectTransform);
                Check(!left.Overlaps(right) && left.width > 60 && right.width > 60, "two legible separate real portraits " + css, new { left = left.ToString(), right = right.ToString() });
            }
            comparison.Close();
            Check(!comparison.IsOpen && shop.gameObject.activeInHierarchy && shop.ActivePurchaseId == shop.ChosenSkinId, "close returns to current shop selection and original purchase SKU");
            Check(coins == YG2.saves.goldCoins && diamonds == YG2.saves.diamonds && equipped == YG2.saves.equipedMaterial && owned.SequenceEqual(YG2.saves.massiveOfObtaining), "comparison does not purchase, equip or change economy");
            Check(save.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "personal save byte-identical");
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            view.Dispose();
            File.WriteAllText("Docs/QualityPass-20261108/Checks/layout-checks.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { scope = "Editor actual layout; DPR simulated, WebGL still required", checks = Checks, failure }, Newtonsoft.Json.Formatting.Indented));
        }
        if (failure != null) throw new Exception(failure);
        return "PASS " + Checks.Count + " actual authored layout checks.";
    }

    private static bool OpensShop(Button button, HorizontalLayout3D shop)
    {
        var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            if (button.onClick.GetPersistentTarget(index) == shop.transform.parent.gameObject &&
                button.onClick.GetPersistentMethodName(index) == "SetActive" &&
                calls.GetArrayElementAtIndex(index).FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue)
                return true;
        return false;
    }

    private sealed class ViewScope : IDisposable
    {
        private readonly EditorWindow window;
        private readonly object group;
        private readonly int originalIndex, originalTotal;
        private readonly Rect originalPosition;
        private readonly PropertyInfo selected;
        public ViewScope()
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            window = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));
            originalPosition = window.position;
            selected = window.GetType().GetProperty("selectedSizeIndex", Private);
            originalIndex = (int)selected.GetValue(window);
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            originalTotal = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        }
        public void Set(int width, int height)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), new object[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "Quality QA " + width + "x" + height });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            selected.SetValue(window, index);
            window.Focus();
        }
        public void Dispose()
        {
            selected.SetValue(window, originalIndex);
            // Unity's API takes the total index including built-ins, not the custom-list index.
            while ((int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) > originalTotal)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { originalTotal });
            window.position = originalPosition;
        }
    }
}
