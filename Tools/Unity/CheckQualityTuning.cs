using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

public static class CheckQualityTuning
{
    private const string SettingsPath = "Assets/GameData/RoundExperienceSettings.asset";
    private static SerializedObject Lab()
    {
        if (!Application.isPlaying || !RankSimulationContext.Active || YG.YG2.saves.musicValue != 0 || YG.YG2.saves.soundValue != 0)
            throw new InvalidOperationException("Use silent isolated lab.");
        return new SerializedObject(UnityEngine.Object.FindAnyObjectByType<RoundExperienceLab>());
    }
    private static void Choose(SerializedObject lab, float seconds, float fraction)
    {
        var sliders = lab.FindProperty("sliders");
        ((Slider)sliders.GetArrayElementAtIndex(7).objectReferenceValue).value = seconds;
        ((Slider)sliders.GetArrayElementAtIndex(8).objectReferenceValue).value = fraction;
        ((Button)lab.FindProperty("saveButton").objectReferenceValue).onClick.Invoke();
        var values = AssetDatabase.LoadAssetAtPath<RoundExperienceSettings>(SettingsPath).CreateRuntimeValues();
        if (Mathf.Abs(values.BlockedTargetHintSeconds - seconds) > .001f || Mathf.Abs(values.SecondaryGoalRevealFraction - fraction) > .001f)
            throw new Exception("Save button did not write actual settings.");
    }
    public static async Task<string> SaveAndRestart()
    {
        var lab = Lab();
        File.WriteAllBytes("/tmp/quality-settings-before-tuning.asset", File.ReadAllBytes(SettingsPath));
        Choose(lab, 2.1f, .65f);
        ((Button)lab.FindProperty("restartButton").objectReferenceValue).onClick.Invoke();
        await Task.Delay(500);
        if (Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<TooLargeTargetFeedback>().VisibleSeconds - 2.1f) > .001f ||
            Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<LateRoundGoals>().RevealFraction - .65f) > .001f)
            throw new Exception("Restart lost live chosen values.");
        File.WriteAllText("Docs/QualityPass-20261108/Checks/tuning-save-phase1.json", "{\"savedHintSeconds\":2.1,\"savedRevealFraction\":0.65,\"actualRestartUsedChosenValues\":true}");
        return "Saved2.1s/65% through actual lab Save button; Restart kept both actual systems. Stop and reopen isolated lab next.";
    }
    public static string VerifyFreshAndRestore()
    {
        var lab = Lab();
        if (Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<TooLargeTargetFeedback>().VisibleSeconds - 2.1f) > .001f ||
            Mathf.Abs(UnityEngine.Object.FindAnyObjectByType<LateRoundGoals>().RevealFraction - .65f) > .001f)
            throw new Exception("Fresh Play did not read saved values.");
        Choose(lab, 1.2f, .5f);
        File.WriteAllText("Docs/QualityPass-20261108/Checks/tuning-save-restoration.json", "{\"freshIsolatedPlayReadSavedSettings\":true,\"restoredThroughSaveButton\":true,\"hintSeconds\":1.2,\"revealFraction\":0.5}");
        return "Fresh isolated Play read2.1s/65% from saved asset; restored1.2s/50% through actual Save button.";
    }
}
