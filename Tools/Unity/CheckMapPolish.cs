using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

public static class CheckMapPolish
{
    private static readonly List<object> checks = new List<object>();
    private static readonly List<string> errors = new List<string>();
    private static void Check(bool condition, string name)
    {
        checks.Add(new
        {
            name,
            passed = condition
        });
        if (!condition)
            throw new Exception(name);
    }
    private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message);
    }

    public static async Task<string> Run()
    {
        checks.Clear();
        errors.Clear();
        string failure = null;
        byte[] save = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        Application.logMessageReceived += Log;
        try
        {
            Check(Application.isPlaying && RankSimulationContext.Active, "isolated Play profile");
            Check(YG.YG2.saves.musicValue == 0 && YG.YG2.saves.soundValue == 0, "Music and SFX zero through isolated game profile");
            RoundExperienceLab lab = One<RoundExperienceLab>();
            var labData = new SerializedObject(lab);
            Check(labData.FindProperty("sliders").arraySize == 9, "saved existing lab has nine live controls");
            var hide = (UnityEngine.UI.Slider)labData.FindProperty("sliders").GetArrayElementAtIndex(4).objectReferenceValue;
            var show = (UnityEngine.UI.Slider)labData.FindProperty("sliders").GetArrayElementAtIndex(5).objectReferenceValue;
            var reach = (UnityEngine.UI.Slider)labData.FindProperty("sliders").GetArrayElementAtIndex(6).objectReferenceValue;
            hide.value = .4f;
            show.value = .6f;
            reach.value = 8f;
            Check(Mathf.Abs(One<CameraCutawayView>().FadeOutSeconds - .4f) < .01f &&
                Mathf.Abs(One<CameraCutawayView>().FadeInSeconds - .6f) < .01f && One<EdibleTargetHints>().Radius == 8,
                "live controls change the real fade and hint systems");
            ((UnityEngine.UI.Button)labData.FindProperty("resetButton").objectReferenceValue).onClick.Invoke();
            Check(Mathf.Abs(One<CameraCutawayView>().FadeInSeconds - .35f) < .01f &&
                Mathf.Abs(One<EdibleTargetHints>().Radius - 5.5f) < .01f, "Reset restores saved camera/hint tuning");
            foreach (string shader in new[] { "Vodovorot/Cutaway dither", "Vodovorot/Map surround", "Vodovorot/Edible target bracket" })
            {
                Shader asset = Shader.Find(shader);
                Check(asset != null && asset.isSupported && !ShaderUtil.GetShaderMessages(asset).Any(x => x.severity.ToString() == "Error"),
                    "Editor shader compiled: " + shader);
            }
            var moves = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("Docs/MapPolish-20261108/Checks/opening-authoring.json"));
            int[] originalCounts = { 11896, 6057, 7604 };
            for (int map = 1; map <= 3; map++)
            {
                // Match the menu's selected-map state: Garden previously replaced
                // its authored spawn only when this state was set correctly.
                YG.YG2.saves.selectedMapID = map - 1;
                SceneManager.LoadScene(map);
                await Task.Delay(600);
                BlackHoleController player = One<BlackHoleController>();
                Vector3 spawn = map == 3 ? new Vector3(-66.1f, .164f, -50f) : new Vector3(-31.7f, .164f, -38.1f);
                Check(Vector3.Distance(player.transform.position, spawn) < .05f,
                    "menu-selected map retains its authored opening spawn on map " + map);
                EdibleTargetHints hints = One<EdibleTargetHints>();
                if (map == 2)
                {
                    var cameraData = new SerializedObject(One<CameraCutawayView>());
                    SerializedProperty list = cameraData.FindProperty("occluders");
                    var obstacles = new HashSet<Renderer>(Enumerable.Range(0, list.arraySize)
                        .Select(index => list.GetArrayElementAtIndex(index).objectReferenceValue as Renderer));
                    Renderer[] hedges = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                        .Where(renderer => renderer.bounds.size.y >= 1f && renderer.sharedMaterials.Any(material =>
                            material != null && material.name == "hedge")).ToArray();
                    Check(hedges.Length > 0 && hedges.All(obstacles.Contains),
                        "Garden segmented hedges participate in the real camera cutaway");
                }
                FallingObject[] items = UnityEngine.Object.FindObjectsByType<FallingObject>(FindObjectsInactive.Exclude);
                Check(items.Length == originalCounts[map - 1], "original edible object count retained on map " + map);
                var lookup = items.GroupBy(x => PathOf(x.transform)).ToDictionary(x => x.Key, x => x.First());
                var wrongValues = new List<string>();
                foreach (var edit in moves[map - 1]["edits"])
                {
                    string path = (string)edit["name"];
                    if (!lookup.TryGetValue(path, out FallingObject item) || item.value != (int)edit["value"])
                        wrongValues.Add(path + " expected=" + edit["value"] + " actual=" + (item == null ? "missing" : item.value + " size=" + item.size));
                }
                Check(wrongValues.Count == 0, "moved props preserve actual runtime value on map " + map + ": " + string.Join(",", wrongValues.Take(3)));
                Check(items.Count(x => x.IsAvailableTarget && x.CanBeEatenBy(player) &&
                    Vector3.Distance(x.transform.position, player.transform.position) < 3) >= 4,
                    "at least four real starting targets within3m on map " + map);
                GameObject surround = SceneManager.GetActiveScene().GetRootGameObjects().Single(x => x.name.StartsWith("Map surround"));
                Check(!surround.GetComponentsInChildren<Collider>().Any() && !surround.GetComponentsInChildren<FallingObject>().Any(),
                    "saved surround changes neither collisions nor map scoring on map " + map);
                string theme = new[] { "City", "Garden", "Castle" }[map - 1];
                Check(surround.GetComponentsInChildren<MeshFilter>().All(x => x.sharedMesh != null) &&
                    AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/MapPolish/" + theme + "Ground.asset") != null &&
                    AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/MapPolish/" + theme + "Decor.asset") != null,
                    "surround geometry is saved in editable assets on map " + map);
                var hintData = new SerializedObject(hints);
                Check(hintData.FindProperty("markers").arraySize == 8, "eight authored pooled target markers on map " + map);
                await Task.Delay(250);
                Check(hints.VisibleCount > 0, "eligible starting targets visibly marked on map " + map);
                var field = typeof(EdibleTargetHints).GetField("targets", BindingFlags.Instance | BindingFlags.NonPublic);
                FallingObject[] marked = (FallingObject[])field.GetValue(hints);
                Check(marked.Where(x => x != null).All(x => x.IsAvailableTarget && x.CanBeEatenBy(player)),
                    "hint selection uses the real fit and availability rules on map " + map);
                Time.timeScale = 0;
                await Task.Delay(120);
                Check(hints.VisibleCount == 0, "target hints hidden under pause on map " + map);
                Time.timeScale = 1;
                await Task.Delay(250);
                FallingObject consumed = ((FallingObject[])field.GetValue(hints)).First(x => x != null);
                consumed.OnScored(player);
                await Task.Delay(250);
                Check(!((FallingObject[])field.GetValue(hints)).Contains(consumed), "consumed target marker removed on map " + map);
                FallingObject nextSize = items.FirstOrDefault(x => x.IsAvailableTarget && !x.CanBeEatenBy(player) &&
                    Tool.CanFit2D(x.size, player.GetFitSize() * 1.95f));
                Check(nextSize != null, "next-size targets exist on map " + map);
                player.InitializeScore(26, true);
                // The fit cache follows the renderer in the next player Update.
                await Task.Delay(150);
                Check(nextSize.CanBeEatenBy(player), "growth unlocks the same target in physics and hint rule on map " + map +
                    " (target=" + nextSize.size + ", fit=" + player.GetFitSize() + ")");
            }
            await CheckFade();
            Check(save.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "personal save byte-identical");
            Check(errors.Count == 0, "no runtime errors during focused component run");
        }
        catch (Exception error)
        {
            failure = error.ToString();
            throw;
        }
        finally
        {
            Application.logMessageReceived -= Log;
            File.WriteAllText("Docs/QualityPass-20261108/Checks/map-regression.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new
                {
                    environment = "Editor controlled actual-component fixtures, not gameplay",
                    finishedUtc = DateTime.UtcNow,
                    checks,
                    errors,
                    failure
                }, Newtonsoft.Json.Formatting.Indented));
            RankSimulationContext.Stop();
        }
        return "PASS: " + checks.Count + " map, fit, marker, fade and restoration assertions.";
    }

    private static async Task CheckFade()
    {
        CameraCutawayView cutaway = One<CameraCutawayView>();
        var data = new SerializedObject(cutaway);
        Camera camera = (Camera)data.FindProperty("viewCamera").objectReferenceValue;
        Vector3 originalPosition = camera.transform.position;
        var list = data.FindProperty("occluders");
        Renderer tower = Enumerable.Range(0, list.arraySize).Select(i => (Renderer)list.GetArrayElementAtIndex(i).objectReferenceValue)
            .First(x => x != null && x.name == "Tower_visual" && !x.forceRenderingOff && Vector3.Distance(x.bounds.center, originalPosition) > 30);
        int index = Enumerable.Range(0, list.arraySize).First(i => list.GetArrayElementAtIndex(i).objectReferenceValue == tower);
        Material[] originalMaterials = tower.sharedMaterials;
        var tint = new MaterialPropertyBlock();
        tint.SetFloat("_PolishFixture", 23f);
        tower.SetPropertyBlock(tint, 0);
        data.FindProperty("keepViewInsideMap").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        // Editor frame scheduling after loading a large map is variable. Use a
        // longer diagnostic transition and observe state instead of assuming 70ms
        // always contains a rendered frame. Production timing is checked in WebGL.
        cutaway.FadeOutSeconds = 1f;
        cutaway.FadeInSeconds = 1f;
        bool originalCulling = (bool)typeof(CameraCutawayView).GetField("originalOcclusionCulling",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cutaway);
        camera.transform.position = tower.bounds.center;
        var visibilityField = typeof(CameraCutawayView).GetField("visibleAmount", BindingFlags.NonPublic | BindingFlags.Instance);
        await ObserveIntermediate(visibilityField, cutaway, index);
        float visibility = ((float[])visibilityField.GetValue(cutaway))[index];
        Check(visibility > 0 && visibility < 1 && !tower.forceRenderingOff, "occluder has a real intermediate fade-out state");
        Check(!tower.sharedMaterials.SequenceEqual(originalMaterials), "temporary fade materials installed without editing sources");
        await Task.Delay(1300);
        Check(tower.forceRenderingOff && !camera.useOcclusionCulling, "fully hidden obstruction no longer participates in baked culling");
        camera.transform.position = originalPosition;
        await ObserveIntermediate(visibilityField, cutaway, index);
        visibility = ((float[])visibilityField.GetValue(cutaway))[index];
        Check(visibility > 0 && visibility < 1 && !tower.forceRenderingOff, "occluder returns progressively after release delay");
        camera.transform.position = tower.bounds.center;
        await Task.Delay(1300);
        Check(tower.forceRenderingOff, "direction reversal finishes hidden without stale transition");
        cutaway.enabled = false;
        Check(!tower.forceRenderingOff && tower.sharedMaterials.SequenceEqual(originalMaterials), "disable restores original visibility and exact shared materials");
        tower.GetPropertyBlock(tint, 0);
        Check(tint.GetFloat("_PolishFixture") == 23f, "disable restores original per-material property block");
        Check(camera.useOcclusionCulling == originalCulling, "disable restores original camera culling flag");
        tower.SetPropertyBlock(null, 0);
        camera.transform.position = originalPosition;
    }

    private static async Task ObserveIntermediate(FieldInfo field, CameraCutawayView cutaway, int index)
    {
        for (int sample = 0; sample < 40; sample++)
        {
            await Task.Delay(40);
            float amount = ((float[])field.GetValue(cutaway))[index];
            if (amount > 0f && amount < 1f)
                return;
        }
    }
}
