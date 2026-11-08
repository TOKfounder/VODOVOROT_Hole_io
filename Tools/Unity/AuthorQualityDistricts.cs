using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

/// <summary>Authors saved map objectives, landmarks and their HUD; never builds them at runtime.</summary>
public static class AuthorQualityDistricts
{
    private const string PrefabPath = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
    private static readonly string[] Maps = { "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity", RoundExperienceLab.ScenePath };

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        RoundExperienceSettings settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>("Assets/GameData/RoundExperienceSettings.asset");
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform safe = prefab.transform.Find("RoundGuidanceCanvas/SafeArea");
            if (safe.Find("Optional route goal") == null)
            {
                RectTransform panel = Rect(safe, "Optional route goal", new Vector2(.14f, .23f), new Vector2(.86f, .33f));
                Image paper = panel.gameObject.AddComponent<Image>();
                paper.color = new Color(.05f, .12f, .16f, .9f);
                paper.raycastTarget = false;
                Text label = Rect(panel, "Label", new Vector2(.025f, .03f), new Vector2(.975f, .97f)).gameObject.AddComponent<Text>();
                label.font = RankingVisualStyle.Load().comicFont;
                label.fontSize = label.resizeTextMaxSize = 20;
                label.resizeTextMinSize = 12;
                label.resizeTextForBestFit = true;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.text = "Доп. цель · Автодвор  0/5\n↑ 25 м · нужен размер 3";
                panel.gameObject.SetActive(false);
            }
            Set(prefab.GetComponent<TooLargeTargetFeedback>(), "settings", settings);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var manifest = new JArray();
        try
        {
            for (int map = 0; map < Maps.Length; map++)
            {
                int theme = map == 3 ? 0 : map;
                Scene scene = EditorSceneManager.OpenScene(Maps[map]);
                if (scene.GetRootGameObjects().Any(root => root.name == "District routes"))
                {
                    foreach (RoundTargetGroup existing in All<RoundTargetGroup>(scene))
                        manifest.Add(Manifest(scene, existing));
                    continue;
                }
                GamingManager game = All<GamingManager>(scene).Single();
                BlackHoleController player = All<BlackHoleController>(scene).Single();
                Transform hud = All<Transform>(scene).Single(t => t.name == "Optional route goal");
                GameObject rootObject = new GameObject("District routes");
                LateRoundGoals goals = rootObject.AddComponent<LateRoundGoals>();
                var groups = new List<RoundTargetGroup>();
                var used = new HashSet<FallingObject>();
                for (int district = 0; district < 2; district++)
                {
                    string ru = theme == 0 ? (district == 0 ? "Автодвор" : "Хвойный сквер")
                        : theme == 1 ? (district == 0 ? "Сад отдыха" : "Цветочная аллея")
                        : (district == 0 ? "Квартал мастеров" : "Северные ворота");
                    string en = theme == 0 ? (district == 0 ? "Motor yard" : "Pine square")
                        : theme == 1 ? (district == 0 ? "Rest garden" : "Flower walk")
                        : (district == 0 ? "Craft quarter" : "Northern gates");
                    Vector3 seed = theme == 0 ? (district == 0 ? new Vector3(-25, 0, -40) : new Vector3(-24, 0, -27))
                        : theme == 1 ? (district == 0 ? new Vector3(-34, 0, -38) : new Vector3(-30, 0, -24))
                        : (district == 0 ? new Vector3(-50, 0, -38) : new Vector3(-76, 0, -27));
                    FallingObject[] targets = All<FallingObject>(scene)
                        .Where(item => !used.Contains(item) && item.GetComponent<Renderer>() != null && Eligible(item, theme, district))
                        .GroupBy(item => item.transform).Select(group => group.First())
                        .OrderBy(item => (item.transform.position - seed).sqrMagnitude).Take(5).ToArray();
                    if (targets.Length != 5)
                        throw new InvalidOperationException("Need five actual district targets for " + ru);
                    foreach (FallingObject item in targets) used.Add(item);
                    GameObject districtRoot = new GameObject(en);
                    districtRoot.transform.SetParent(rootObject.transform, false);
                    // The marker sits beside the district; it has no collider or score component.
                    Vector3 center = targets.Aggregate(Vector3.zero, (sum, item) => sum + item.transform.position) / targets.Length;
                    districtRoot.transform.position = FindClearMarker(scene, center);
                    RoundTargetGroup group = districtRoot.AddComponent<RoundTargetGroup>();
                    TextMesh label = Landmark(districtRoot.transform, theme, district, ru);
                    Set(group, "nameRu", ru);
                    Set(group, "nameEn", en);
                    Set(group, "districtLabel", label);
                    SetArray(group, "targets", targets);
                    groups.Add(group);
                    manifest.Add(Manifest(scene, group));
                }
                Set(goals, "game", game);
                Set(goals, "player", player);
                Set(goals, "viewCamera", player.GetComponentInChildren<Camera>(true));
                Set(goals, "settings", settings);
                Set(goals, "panel", hud.gameObject);
                Set(goals, "label", hud.GetComponentInChildren<Text>(true));
                SetArray(goals, "groups", groups.ToArray());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/QualityPass-20261108/Checks/district-authoring.json", manifest.ToString());
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved two existing-object route goals and themed landmarks per map, including isolated lab.";
    }

    public static string FinishLabelsAndSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        try
        {
            RoundExperienceSettings settings = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>("Assets/GameData/RoundExperienceSettings.asset");
            var config = new SerializedObject(settings);
            config.FindProperty("blockedTargetHintSeconds").floatValue = 1.2f;
            config.FindProperty("secondaryGoalRevealFraction").floatValue = .5f;
            config.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            for (int map = 0; map < Maps.Length; map++)
            {
                int theme = map == 3 ? 0 : map;
                Scene scene = EditorSceneManager.OpenScene(Maps[map]);
                foreach (RoundTargetGroup group in All<RoundTargetGroup>(scene))
                {
                    bool first = group.DisplayName(false) == "Motor yard" || group.DisplayName(false) == "Rest garden" || group.DisplayName(false) == "Craft quarter";
                    Set(group, "targetKindRu", theme == 0 ? (first ? "Машины" : "Хвойные деревья") : theme == 1 ? (first ? "Скамейки" : "Горшки") : (first ? "Двери домов" : "Входы крепости"));
                    Set(group, "targetKindEn", theme == 0 ? (first ? "Cars" : "Pine trees") : theme == 1 ? (first ? "Benches" : "Pots") : (first ? "House doors" : "Castle entrances"));
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved explicit objective target kinds and single-source hint/reveal defaults.";
    }

    private static JObject Manifest(Scene scene, RoundTargetGroup group) => JObject.FromObject(new
    {
        scene = scene.path, nameRu = group.DisplayName(true), nameEn = group.DisplayName(false),
        landmark = group.transform.position.ToString("F3"),
        targets = group.Targets.Select(item => new { path = Path(item.transform),
            globalId = GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), position = item.transform.position.ToString("F3") }).ToArray()
    });

    private static bool Eligible(FallingObject item, int theme, int district)
    {
        if (!item.gameObject.activeInHierarchy || item.isColon || item.transform.position.y > .7f)
            return false;
        string path = Path(item.transform);
        if (path.Contains("Stage2") || path.Contains("FastFarming"))
            return false;
        if (theme == 0)
            return district == 0 ? item.name.StartsWith("Vehicle_") : item.name.StartsWith("Natures_Fir Tree");
        if (theme == 1)
            return district == 0 ? item.name.ToLowerInvariant().StartsWith("bench") : item.name == "stone pot" || item.name == "Pot_tree";
        return district == 0 ? item.name == "Door" : item.name == "Simple Door Frame";
    }

    private static Vector3 FindClearMarker(Scene scene, Vector3 center)
    {
        Physics.SyncTransforms();
        for (float radius = 2.5f; radius <= 14f; radius += 1.1f)
            for (int step = 0; step < 16; step++)
            {
                float angle = step * Mathf.PI / 8;
                Vector3 point = new Vector3(center.x + Mathf.Cos(angle) * radius, .12f, center.z + Mathf.Sin(angle) * radius);
                // Actual mesh overlap avoids rejecting an entire courtyard merely
                // because the surrounding castle has one large collider AABB.
                Collider[] touching = Physics.OverlapBox(point + Vector3.up * 1.9f,
                    new Vector3(1.8f, 1.7f, .6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (!touching.Any(c => c.gameObject.scene == scene && c.enabled))
                    return point;
            }
        throw new InvalidOperationException("No clear district marker position near " + center);
    }

    private static TextMesh Landmark(Transform parent, int theme, int district, string caption)
    {
        Material wood = Material("DistrictWood", new Color(.22f, .15f, .10f));
        Material pale = Material("DistrictPale", new Color(.88f, .85f, .67f));
        Color accent = theme == 0 ? (district == 0 ? new Color(.94f, .37f, .10f) : new Color(.16f, .53f, .40f))
            : theme == 1 ? (district == 0 ? new Color(.22f, .55f, .35f) : new Color(.76f, .34f, .56f))
            : (district == 0 ? new Color(.64f, .20f, .16f) : new Color(.20f, .34f, .65f));
        Material color = Material("District" + theme + district, accent);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(parent, "Post", new Vector3(side * 1.45f, 1.5f, 0), new Vector3(.18f, 3, .18f), theme == 2 ? pale : wood);
            Box(parent, "Foot", new Vector3(side * 1.45f, .18f, 0), new Vector3(.55f, .36f, .6f), pale);
            if (theme == 2)
                for (int tooth = -1; tooth <= 1; tooth++)
                    Box(parent, "Battlement", new Vector3(side * 1.45f + tooth * .18f, 3.18f, 0), new Vector3(.13f, .3f, .32f), pale);
            if (theme == 1)
                Box(parent, "Trellis", new Vector3(side * 1.45f, 2, .12f), new Vector3(.6f, .1f, .35f), color);
        }
        Box(parent, "Sign frame", new Vector3(0, 2.7f, 0), new Vector3(3.25f, .85f, .18f), wood);
        Box(parent, "Sign face", new Vector3(0, 2.7f, -.12f), new Vector3(3.08f, .68f, .06f), color);
        if (theme == 0)
            for (int stripe = 0; stripe < 8; stripe++)
                Box(parent, "Awning stripe", new Vector3(-1.4f + stripe * .4f, 3.2f, -.24f), new Vector3(.4f, .15f, 1f), stripe % 2 == 0 ? color : pale);
        if (theme == 1)
        {
            Box(parent, "Garden crown", new Vector3(0, 3.22f, 0), new Vector3(3.5f, .18f, .45f), color);
            for (int rail = -2; rail <= 2; rail++)
                Box(parent, "Garden rail", new Vector3(rail * .5f, 3.42f, 0), new Vector3(.1f, .25f, .32f), wood);
        }
        var textRoot = new GameObject("District label", typeof(TextMesh));
        textRoot.transform.SetParent(parent, false);
        textRoot.transform.localPosition = new Vector3(0, 2.7f, -.16f);
        TextMesh text = textRoot.GetComponent<TextMesh>();
        text.font = RankingVisualStyle.Load().comicFont;
        text.fontSize = 64;
        text.characterSize = .045f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        text.text = caption;
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        return text;
    }

    private static Material Material(string name, Color color)
    {
        string path = "Assets/Art/MapPolish/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Vodovorot/Map surround"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_VertexColors", 0);
        material.SetFloat("_Pattern", 0);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        Renderer renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(cube, StaticEditorFlags.BatchingStatic);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object owner, string field, string value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).stringValue = value;
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
