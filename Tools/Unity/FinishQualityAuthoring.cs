using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

/// <summary>Completes Edit Mode previews and stores reusable landmark geometry in real prefabs.</summary>
public static class FinishQualityAuthoring
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        var manifest = new JArray();
        try
        {
            Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            SkinComparisonView view = All<SkinComparisonView>(menu).Single();
            var viewData = new SerializedObject(view);
            Reference<Text>(viewData, "title").text = "Сравнение скинов";
            Reference<Text>(viewData, "promise").text = SkinAppearanceText.CosmeticPromise(true);
            Reference<Text>(viewData, "equippedName").text = "Надето · Красный Тазик";
            Reference<Text>(viewData, "selectedName").text = "Выбрано · Белый Друг";
            Reference<Text>(viewData, "equippedDescription").text = SkinAppearanceText.Description(0, true);
            Reference<Text>(viewData, "selectedDescription").text = SkinAppearanceText.Description(1, true);
            // Representative authored examples make the sheet fully editable
            // before Play. Open() replaces them with the real current selection.
            Reference<RawImage>(viewData, "equippedImage").texture = (Texture)viewData.FindProperty("portraitsRu").GetArrayElementAtIndex(0).objectReferenceValue;
            Reference<RawImage>(viewData, "selectedImage").texture = (Texture)viewData.FindProperty("portraitsRu").GetArrayElementAtIndex(1).objectReferenceValue;
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);

            const string gamePrefab = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
            GameObject game = PrefabUtility.LoadPrefabContents(gamePrefab);
            try
            {
                game.transform.Find("RoundGuidanceCanvas/SafeArea/Optional route goal/Label").GetComponent<Text>().text =
                    "Доп. цель · Автодвор 0/5\nМашины · 25 м · нужен размер 3";
                PrefabUtility.SaveAsPrefabAsset(game, gamePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }

            const string folder = "Assets/Prefabs/MapPolish";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "MapPolish");
            string[] maps = { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity", RoundExperienceLab.ScenePath };
            for (int map = 0; map < maps.Length; map++)
            {
                int theme = map == 3 ? 0 : map;
                Scene scene = EditorSceneManager.OpenScene(maps[map]);
                foreach (RoundTargetGroup group in All<RoundTargetGroup>(scene))
                {
                    if (group.GetComponentsInChildren<FallingObject>(true).Length > 0 || group.GetComponentsInChildren<Collider>(true).Length > 0)
                        throw new InvalidOperationException("Landmark visual unexpectedly owns gameplay objects.");
                    bool first = group.DisplayName(false) == "Motor yard" || group.DisplayName(false) == "Rest garden" || group.DisplayName(false) == "Craft quarter";
                    string prefabPath = folder + "/" + new[] { "City", "Garden", "Castle" }[theme] + (first ? "Primary" : "Secondary") + "Landmark.prefab";
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Transform visual = group.transform.Find("Landmark visual");
                    if (visual == null)
                    {
                        Transform[] originalChildren = group.transform.Cast<Transform>().ToArray();
                        if (asset == null)
                        {
                            visual = new GameObject("Landmark visual").transform;
                            visual.SetParent(group.transform, false);
                            foreach (Transform child in originalChildren)
                                child.SetParent(visual, false);
                            TextMesh title = visual.GetComponentInChildren<TextMesh>();
                            title.color = theme < 2 ? RankingVisualStyle.Load().ink : Color.white;
                            PrefabUtility.SaveAsPrefabAssetAndConnect(visual.gameObject, prefabPath, InteractionMode.AutomatedAction);
                        }
                        else
                        {
                            if (asset.GetComponentInChildren<TextMesh>().text != group.DisplayName(true))
                                throw new InvalidOperationException("Existing landmark identity differs: " + prefabPath);
                            visual = ((GameObject)PrefabUtility.InstantiatePrefab(asset, group.transform)).transform;
                            visual.name = "Landmark visual";
                            visual.localPosition = Vector3.zero;
                            visual.localRotation = Quaternion.identity;
                            visual.localScale = Vector3.one;
                            foreach (Transform child in originalChildren)
                                UnityEngine.Object.DestroyImmediate(child.gameObject);
                        }
                    }
                    var data = new SerializedObject(group);
                    data.FindProperty("districtLabel").objectReferenceValue = visual.GetComponentInChildren<TextMesh>();
                    data.ApplyModifiedPropertiesWithoutUndo();
                    if (!PrefabUtility.IsPartOfPrefabInstance(visual.gameObject))
                        throw new InvalidOperationException("Landmark was not connected to a saved prefab.");
                    if (group.Targets.Select(item => item.transform).Distinct().Count() != 5)
                        throw new InvalidOperationException("A district must reference five separate visible objects.");
                    manifest.Add(JObject.FromObject(new { scene = scene.path, group = group.DisplayName(true), prefab = prefabPath,
                        reusedCityVisualInLab = map == 3, labelBound = true, targets = 5, colliders = 0 }));
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/QualityPass-20261108/Checks/landmark-prefabs.json", manifest.ToString());
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved complete Edit Mode UI preview and six reusable district visual prefabs (City reused in lab).";
    }
    private static T Reference<T>(SerializedObject data, string field) where T : UnityEngine.Object =>
        (T)data.FindProperty(field).objectReferenceValue;
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
