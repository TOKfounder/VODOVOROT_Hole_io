using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using PinePie.SimpleJoystick.Examples.DemoScript;
using YG;

public static class CheckQualityPass
{
    private static readonly List<object> Checks = new List<object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string name, object evidence = null)
    {
        Checks.Add(new { name, passed = condition, evidence });
        if (!condition) throw new Exception(name);
    }
    private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    private static T Field<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

    public static async Task<string> Run()
    {
        Checks.Clear();
        string failure = null;
        byte[] original = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        float originalScale = Time.timeScale;
        try
        {
            Check(Application.isPlaying && RankSimulationContext.Active, "isolated actual Play profile");
            Check(YG2.saves.musicValue == 0 && YG2.saves.soundValue == 0, "Music and SFX zero in game profile");
            var state = new RoundGoalProgress(new[] { 3, 3, 7, 9 });
            Check(state.Total == 3 && state.Collect(3) && !state.Collect(3) && !state.Collect(99) && state.Gathered == 1,
                "objective IDs unique, repeated/foreign notification ignored");
            state.MarkUnavailable(7);
            Check(!state.Collect(7) && !state.Resolved && !state.Complete && state.Lost == 1, "rival's target never counts as player completion");
            state.Collect(9);
            Check(state.Resolved && !state.Complete, "unavailable route resolves without fabricated success");
            var complete = new RoundGoalProgress(new[] { 0, 1 });
            complete.Collect(1); complete.Collect(0);
            Check(complete.Complete && complete.Resolved, "early real collection can complete a route before reveal");
            RoundExperienceLab lab = One<RoundExperienceLab>();
            var data = new SerializedObject(lab);
            Check(data.FindProperty("sliders").arraySize == 9, "saved lab has nine live controls");
            Slider hintTime = (Slider)data.FindProperty("sliders").GetArrayElementAtIndex(7).objectReferenceValue;
            Slider reveal = (Slider)data.FindProperty("sliders").GetArrayElementAtIndex(8).objectReferenceValue;
            hintTime.value = 2.4f;
            reveal.value = .7f;
            Check(Mathf.Abs(One<TooLargeTargetFeedback>().VisibleSeconds - 2.4f) < .001f &&
                Mathf.Abs(One<LateRoundGoals>().RevealFraction - .7f) < .001f, "live controls change actual blocked-target and objective systems");
            ((Button)data.FindProperty("resetButton").objectReferenceValue).onClick.Invoke();
            Check(Mathf.Abs(One<TooLargeTargetFeedback>().VisibleSeconds - 1.2f) < .001f &&
                Mathf.Abs(One<LateRoundGoals>().RevealFraction - .5f) < .001f, "Reset restores real saved values");
            for (int map = 1; map <= 3; map++)
            {
                YG2.saves.selectedMapID = map - 1;
                SceneManager.LoadScene(map);
                await Task.Delay(650);
                GamingManager game = One<GamingManager>();
                game.HandleTimer(false);
                BlackHoleController player = One<BlackHoleController>();
                LateRoundGoals goals = One<LateRoundGoals>();
                SerializedProperty savedGroups = new SerializedObject(goals).FindProperty("groups");
                RoundTargetGroup[] groups = Enumerable.Range(0, savedGroups.arraySize).Select(index => (RoundTargetGroup)savedGroups.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
                Check(groups.Length == 2 && groups.All(group => group.Targets.Length == 5 && group.Targets.All(item => item != null)), "map " + map + " two saved groups of five actual targets");
                Check(groups.SelectMany(group => group.Targets).Distinct().Count() == 10, "map " + map + " no duplicate objective target");
                Image direction = Field<Image>(goals, "directionArrow");
                Check(direction != null && direction.sprite != null, "map " + map + " objective direction uses saved graphic icon");
                Check(groups.All(group => group.GetComponentsInChildren<Collider>().Length == 0 && group.GetComponentsInChildren<FallingObject>().Length == 0), "map " + map + " landmarks do not alter collisions or map score");
                foreach (RoundTargetGroup group in groups)
                {
                    TextMesh text = group.GetComponentInChildren<TextMesh>();
                    Check(text != null && text.GetComponent<Renderer>().bounds.size.x < 3.05f, "map " + map + " district label fits authored sign: " + group.DisplayName(true), text.GetComponent<Renderer>().bounds.size.ToString());
                }
                GameObject panel = Field<GameObject>(goals, "panel");
                game.timer = game.totalCleaningDuration * .2f;
                await Task.Delay(60);
                Check(!panel.activeSelf, "map " + map + " late goal hidden in first half");
                int valueBefore = game.Stage2PlayerAbsorbedValue;
                FallingObject target = groups[0].Targets[0];
                int amount = target.value;
                target.OnScored(player);
                target.OnScored(player);
                Check(game.Stage2PlayerAbsorbedValue == valueBefore + amount && goals.ProgressAt(0).Gathered + goals.ProgressAt(1).Gathered == 1,
                    "map " + map + " actual scored event counts early target once, economic value unchanged");
                game.timer = game.totalCleaningDuration * .6f;
                await Task.Delay(250);
                Check(panel.activeSelf && Field<Text>(goals, "label").text.Contains("1/5"), "map " + map + " late HUD reveals existing progress");
                Time.timeScale = 0;
                await Task.Delay(50);
                Check(!panel.activeSelf, "map " + map + " late goal hidden while paused");
                Time.timeScale = 1;
                await CheckRequiredSize(player, game, map);
            }
            Check(original.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "personal save byte-identical after actual-component tests");
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            Time.timeScale = originalScale;
            File.WriteAllText("Docs/QualityPass-20261108/Checks/component-checks.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { checks = Checks, failure }, Newtonsoft.Json.Formatting.Indented));
        }
        if (failure != null) throw new Exception(failure);
        return "PASS " + Checks.Count + " focused actual-component checks.";
    }

    private static async Task CheckRequiredSize(BlackHoleController player, GamingManager game, int map)
    {
        MovementScript movement = player.GetComponentInChildren<MovementScript>();
        movement.enabled = false;
        Vector3 position = player.transform.position;
        // Move away from props before the11 growth endpoints so the fixture itself
        // cannot accidentally absorb targets or complete the map.
        player.transform.position = new Vector3(500, .16f, 500);
        SpiralRotation[] rotations = player.GetComponentsInChildren<SpiralRotation>();
        foreach (SpiralRotation rotation in rotations) rotation.enabled = false;
        player.InitializeScore(0, true);
        await Task.Delay(100);
        int[] scores = { 0, 26, 163, 795, 1779, 3703, 5127, 8751, 11375, 13000, 15000 };
        for (int level = 0; level < scores.Length; level++)
        {
            Vector3 predicted = player.GetFitSizeAtLevel(level);
            player.InitializeScore(scores[level], true);
            await Task.Delay(45);
            Vector3 actual = player.GetFitSize();
            Check(Vector3.Distance(predicted, actual) < .005f, "map " + map + " required-size prediction equals actual endpoint " + (level + 1), new { predicted = predicted.ToString("F4"), actual = actual.ToString("F4") });
        }
        player.InitializeScore(0, true);
        player.transform.position = position;
        foreach (SpiralRotation rotation in rotations) rotation.enabled = true;
        await Task.Delay(80);
        FallingObject target = UnityEngine.Object.FindObjectsByType<FallingObject>(FindObjectsInactive.Exclude)
            .Where(item => item.IsAvailableTarget && !item.CanBeEatenBy(player) && item.rend.bounds.size.y > .1f && player.RequiredSizeFor(item.size) > 1)
            .OrderBy(item => (item.transform.position - player.transform.position).sqrMagnitude).First();
        TooLargeTargetFeedback feedback = One<TooLargeTargetFeedback>();
        GameObject hint = Field<GameObject>(feedback, "panel");
        Bounds bounds = target.rend.bounds;
        player.transform.position = new Vector3(bounds.center.x, position.y, bounds.min.z - .15f);
        typeof(MovementScript).GetField("<IntentDirection>k__BackingField", Private).SetValue(movement, Vector3.zero);
        typeof(HoleParent).GetMethod("ReportRejectedTarget", Private).Invoke(player, new object[] { target });
        Check(!hint.activeSelf, "map " + map + " standing near large target does not show attempted-action hint");
        typeof(MovementScript).GetField("<IntentDirection>k__BackingField", Private).SetValue(movement, Vector3.forward);
        int score = player.score;
        typeof(HoleParent).GetMethod("ReportRejectedTarget", Private).Invoke(player, new object[] { target });
        Check(hint.activeSelf && Field<Text>(feedback, "label").text.Contains(player.RequiredSizeFor(target.size).ToString()) && player.score == score,
            "map " + map + " blocked attempted action shows actual required size without score change");
        Time.timeScale = 0;
        await Task.Delay(50);
        Check(!hint.activeSelf, "map " + map + " blocked-target hint hidden under pause");
        Time.timeScale = 1;
        player.transform.position = position;
        typeof(MovementScript).GetField("<IntentDirection>k__BackingField", Private).SetValue(movement, Vector3.zero);
        movement.enabled = true;
    }
}
