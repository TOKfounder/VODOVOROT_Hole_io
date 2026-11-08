#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using YG;

[InitializeOnLoad]
[DefaultExecutionOrder(1000)]
public sealed class RoundExperienceLab : MonoBehaviour
{
    public const string ScenePath = "Assets/Tests/RoundExperienceLab.unity";
    private const string PendingKey = "Vodovorot.RoundExperience.LabPending";
    [SerializeField] private RoundExperienceSettings settings;
    [SerializeField] private GamingManager game;
    [SerializeField] private CameraCutawayView cutaway;
    [SerializeField] private EdibleTargetHints hints;
    [SerializeField] private Slider[] sliders;
    [SerializeField] private Text[] valueLabels;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Text status;
    private static RoundExperienceValues working;
    private static bool haveWorking;

    static RoundExperienceLab()
    {
        EditorApplication.update += EnterPendingLab;
        EditorApplication.playModeStateChanged += GuardDirectPlay;
    }

    [MenuItem("Tools/Vodovorot/Round experience/Open isolated tuning lab")]
    public static void OpenIsolated()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop the current Play session first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your scene before opening the lab.");
        haveWorking = false;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool("Vodovorot.RoundExperience.SilentFreshProfile", true);
        RankSimulationContext.UseTestRoundXp = false;
        RankSimulationContext.Start(RankSimulationScenario.Guest);
    }

    private static void GuardDirectPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            haveWorking = false;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool("Vodovorot.RoundExperience.SilentFreshProfile", false);
        }
        if (state != PlayModeStateChange.ExitingEditMode || SceneManager.GetActiveScene().path != ScenePath ||
            RankSimulationContext.BlockEditorSave) return;
        EditorApplication.isPlaying = false;
        Debug.LogWarning("Запустите стенд через Tools/Vodovorot/Round experience/Open isolated tuning lab: нужен отдельный профиль.");
    }

    private static void EnterPendingLab()
    {
        if (!SessionState.GetBool(PendingKey, false) || !Application.isPlaying || !RankSimulationContext.Active || !YG2.isSDKEnabled) return;
        SessionState.SetBool(PendingKey, false);
        ResetTestProfile();
        EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
    }

    private static void ResetTestProfile()
    {
        if (!RankSimulationContext.Active) throw new InvalidOperationException("No isolated profile.");
        var fresh = new SavesYG { isFirst = false, selectedMapID = 0, tutorialDone = true, soundValue = 0f, musicValue = 0f };
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), YG2.saves);
    }

    private void Start()
    {
        if (!RankSimulationContext.Active)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        if (!haveWorking)
        {
            working = settings.CreateRuntimeValues();
            haveWorking = true;
        }
        WriteSliders();
        foreach (Slider slider in sliders) slider.onValueChanged.AddListener(ApplySliders);
        restartButton.onClick.AddListener(Restart);
        resetButton.onClick.AddListener(ResetValues);
        saveButton.onClick.AddListener(SaveValues);
        stopButton.onClick.AddListener(RankSimulationContext.Stop);
        ApplySliders(0);
        status.text = "ТЕСТ · профиль в памяти · звук выключен\nКороткий раунд — диагностика, не плейтест.";
    }

    private void WriteSliders()
    {
        sliders[0].SetValueWithoutNotify(working.RoundSeconds);
        sliders[1].SetValueWithoutNotify(working.MinimumCompletedExperience);
        sliders[2].SetValueWithoutNotify(working.MinimumCompletedCoins);
        sliders[3].SetValueWithoutNotify(working.CutawayReleaseSeconds);
        sliders[4].SetValueWithoutNotify(working.CutawayFadeOutSeconds);
        sliders[5].SetValueWithoutNotify(working.CutawayFadeInSeconds);
        sliders[6].SetValueWithoutNotify(working.TargetHintRadius);
        sliders[7].SetValueWithoutNotify(working.BlockedTargetHintSeconds);
        sliders[8].SetValueWithoutNotify(working.SecondaryGoalRevealFraction);
    }

    private void ApplySliders(float unused)
    {
        working.RoundSeconds = sliders[0].value;
        working.MinimumCompletedExperience = Mathf.RoundToInt(sliders[1].value);
        working.MinimumCompletedCoins = Mathf.RoundToInt(sliders[2].value);
        working.CutawayReleaseSeconds = sliders[3].value;
        working.CutawayFadeOutSeconds = sliders[4].value;
        working.CutawayFadeInSeconds = sliders[5].value;
        working.TargetHintRadius = sliders[6].value;
        working.BlockedTargetHintSeconds = sliders[7].value;
        working.SecondaryGoalRevealFraction = sliders[8].value;
        foreach (TooLargeTargetFeedback feedback in FindObjectsByType<TooLargeTargetFeedback>(FindObjectsInactive.Include))
            feedback.VisibleSeconds = working.BlockedTargetHintSeconds;
        foreach (LateRoundGoals goals in FindObjectsByType<LateRoundGoals>(FindObjectsInactive.Include))
            goals.RevealFraction = working.SecondaryGoalRevealFraction;
        game.ConfigureExperienceForTesting(working);
        cutaway.ReleaseSeconds = working.CutawayReleaseSeconds;
        cutaway.FadeOutSeconds = working.CutawayFadeOutSeconds;
        cutaway.FadeInSeconds = working.CutawayFadeInSeconds;
        hints.Radius = working.TargetHintRadius;
        valueLabels[0].text = "Раунд: " + working.RoundSeconds.ToString("0") + " с";
        valueLabels[1].text = "Мин. опыт: " + working.MinimumCompletedExperience;
        valueLabels[2].text = "Мин. монеты: " + working.MinimumCompletedCoins;
        valueLabels[3].text = "Возврат объектов: " + working.CutawayReleaseSeconds.ToString("0.00") + " с";
        valueLabels[4].text = "Скрытие: " + working.CutawayFadeOutSeconds.ToString("0.00") + " с";
        valueLabels[5].text = "Появление: " + working.CutawayFadeInSeconds.ToString("0.00") + " с";
        valueLabels[6].text = "Подсветка: " + working.TargetHintRadius.ToString("0.0") + " м";
        valueLabels[7].text = "Подсказка: " + working.BlockedTargetHintSeconds.ToString("0.0") + " с";
        valueLabels[8].text = "Доп. цели: " + (working.SecondaryGoalRevealFraction * 100).ToString("0") + "% раунда";
    }

    private void Restart()
    {
        ResetTestProfile();
        EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
    }

    private void ResetValues()
    {
        working = settings.CreateRuntimeValues();
        WriteSliders();
        ApplySliders(0);
        status.text = "Возвращены сохранённые параметры. Профиль не записывается.";
    }

    private void SaveValues()
    {
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("roundSeconds").floatValue = working.RoundSeconds;
        serialized.FindProperty("minimumCompletedExperience").intValue = working.MinimumCompletedExperience;
        serialized.FindProperty("minimumCompletedCoins").intValue = working.MinimumCompletedCoins;
        serialized.FindProperty("cutawayReleaseSeconds").floatValue = working.CutawayReleaseSeconds;
        serialized.FindProperty("cutawayFadeOutSeconds").floatValue = working.CutawayFadeOutSeconds;
        serialized.FindProperty("cutawayFadeInSeconds").floatValue = working.CutawayFadeInSeconds;
        serialized.FindProperty("targetHintRadius").floatValue = working.TargetHintRadius;
        serialized.FindProperty("blockedTargetHintSeconds").floatValue = working.BlockedTargetHintSeconds;
        serialized.FindProperty("secondaryGoalRevealFraction").floatValue = working.SecondaryGoalRevealFraction;
        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(settings);
        status.text = "Параметры сохранены в RoundExperienceSettings.asset.\nНовые раунды используют выбранные значения.";
    }
}
#endif
