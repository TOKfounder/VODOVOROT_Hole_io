using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YG;

public class GamingManager : MonoBehaviour
{
    private const string TotalCleaningTimerName = "TimerText";

    public struct MatchRewardData
    {
        public int exp;
        public int coins;
        public int diamonds;
        public int resultSpriteIndex;
    }

    [Header("Core")]
    public GameObject MobpanelOfEnd;
    public GameObject DeskpanelOfEnd;

    [Header("Progress UI")]
    public Image Mflazhok;
    public Image Dflazhok;
    public Text Mpercent;
    public Text Dpercent;
    public Text totalCleaningTimerText;

    [Header("Mobile UI")]
    public Text BoostText;
    public Text[] MobilePanelOfSettings;
    public Text[] PanelOfEnd;

    [Header("Desktop UI")]
    public Text DBoostText;
    public Text[] DesktopPanelOfSettings;
    public Text[] DPanelOfEnd;

    [Header("World Bounds")]
    public GameObject[] walls;
    public float minX, maxX, minZ, maxZ;

    [Header("Total Cleaning Mode")]
    public float totalCleaningDuration = 180f;

    // Публичные данные
    public float perc = 0f;
    public float timer;
    public int AllValues;

    private bool isTotalCleaningMode = false;
    private bool timerGo = true;
    private bool once = true;
    private bool rewardApplied = false;
    private bool endSequenceStarted = false;

    public bool IsTotalCleaningMode => isTotalCleaningMode;
    public bool HasRewardBeenApplied => rewardApplied;
    public float RemainingTime => Mathf.Max(0f, totalCleaningDuration - timer);

    private void Awake()
    {
        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.GamingManager = this;

        // Расчёт границ карты
        if (walls.Length >= 4)
        {
            maxX = walls[0].GetComponent<Collider>().bounds.min.x;
            minX = walls[1].GetComponent<Collider>().bounds.max.x;
            minZ = walls[2].GetComponent<Collider>().bounds.max.z;
            maxZ = walls[3].GetComponent<Collider>().bounds.min.z;
        }
    }

    void Start()
    {
        // Отключаем тени на всех мешах (оптимизация)
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        once = true;
        rewardApplied = false;
        endSequenceStarted = false;
        YG2.saves.isGaming = true;
        Time.timeScale = 1f;
        timer = 0f;
        timerGo = true;

        isTotalCleaningMode = VodovorotGameManager.Instance?.ModeManager != null &&
            VodovorotGameManager.Instance.ModeManager.currentMode == ModeManager.Mode.TotalCleaning;
        if (!isTotalCleaningMode)
            isTotalCleaningMode = YG2.saves.chosenMode == (int)ModeManager.Mode.TotalCleaning;

        if (totalCleaningTimerText != null)
        {
            totalCleaningTimerText.gameObject.SetActive(isTotalCleaningMode);
            if (isTotalCleaningMode)
                totalCleaningTimerText.text = FormatTime(RemainingTime);
        }

        VodovorotGameManager.Instance.SaveProgress();

        StartCoroutine(UpdateProgressRoutine());
        UpdateUI();
        UpdateProgressUI();
    }

    // Корутина вместо Update — обновляем прогресс реже (каждые 0.2 сек)
    private IEnumerator UpdateProgressRoutine()
    {
        while (true)
        {
            YG2.saves.score = HoleParent.totalScore;
            perc = GetCapturePercent();

            // Обновляем UI прогресса
            UpdateProgressUI();

            // Проверка победы
            if (once)
            {
                if (isTotalCleaningMode && (perc >= 1f || timer >= totalCleaningDuration - 0.01f))
                {
                    once = false;
                    EndOfGame();
                }
                else if (!isTotalCleaningMode && perc >= 1f)
                {
                    once = false;
                    EndOfGame();
                }
            }

            yield return new WaitForSeconds(0.2f);   // достаточно 5 раз в секунду
        }
    }

    private void UpdateProgressUI()
    {
        float fill = Mathf.Clamp01(perc);
        string percentText = $"{(int)(fill * 100)}%";

        if (isTotalCleaningMode)
        {
            ResolveTotalCleaningTimerText();
            UpdateTotalCleaningTimerUI();
        }

        if (YG2.envir.isMobile)
        {
            if (Mflazhok != null) Mflazhok.fillAmount = fill;
            if (Mpercent != null)
            {
                Mpercent.text = percentText;
            }
        }
        else
        {
            if (Dflazhok != null) Dflazhok.fillAmount = fill;
            if (Dpercent != null)
            {
                Dpercent.text = percentText;
            }
        }
    }

    private void UpdateTotalCleaningTimerUI()
    {
        if (totalCleaningTimerText == null)
            return;

        totalCleaningTimerText.text = FormatTime(RemainingTime);
    }

    private void ResolveTotalCleaningTimerText()
    {
        Canvas currentCanvas = VodovorotGameManager.Instance?.GameController != null
            ? VodovorotGameManager.Instance.GameController.currentCanvas
            : FindAnyObjectByType<Canvas>();

        if (currentCanvas == null)
            return;

        if (totalCleaningTimerText != null && totalCleaningTimerText.transform.IsChildOf(currentCanvas.transform))
        {
            totalCleaningTimerText.gameObject.SetActive(isTotalCleaningMode);
            return;
        }

        Text[] texts = currentCanvas.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.name != TotalCleaningTimerName)
                continue;

            totalCleaningTimerText = text;
            totalCleaningTimerText.gameObject.SetActive(isTotalCleaningMode);
            if (isTotalCleaningMode)
                totalCleaningTimerText.text = FormatTime(RemainingTime);
            break;
        }
    }

    void FixedUpdate()
    {
        if (timerGo)
            timer += Time.fixedDeltaTime;

        if (isTotalCleaningMode && once && timer >= totalCleaningDuration - 0.01f)
        {
            once = false;
            EndOfGame();
        }
    }

    public void HandleTimer(bool b) => timerGo = b;

    public void EndOfGame()
    {
        if (endSequenceStarted)
            return;

        endSequenceStarted = true;
        timerGo = false;
        once = false;
        Time.timeScale = 0;

        if (YG2.envir.isMobile)
            MobpanelOfEnd?.SetActive(true);
        else
            DeskpanelOfEnd?.SetActive(true);
    }

    public float GetCapturePercent()
    {
        if (isTotalCleaningMode)
        {
            if (AllValues <= 0)
                return 0f;

            return Mathf.Clamp01((float)HoleParent.totalScore / AllValues);
        }

        return AllValues > 15 ? (float)YG2.saves.score / (AllValues - 15) : 0f;
    }

    public MatchRewardData GetTotalCleaningReward(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (progress < 0.5f)
        {
            int coins = Mathf.RoundToInt(15f * (progress / 0.5f));
            int exp = Mathf.RoundToInt(25f * (progress / 0.5f));
            int diamonds = coins / 5;

            return new MatchRewardData
            {
                exp = exp,
                coins = coins,
                diamonds = diamonds,
                resultSpriteIndex = 2
            };
        }

        if (progress < 0.6f)
        {
            return new MatchRewardData
            {
                exp = 25,
                coins = 15,
                diamonds = 3,
                resultSpriteIndex = 2
            };
        }

        if (progress < 0.7f)
        {
            return new MatchRewardData
            {
                exp = 35,
                coins = 20,
                diamonds = 4,
                resultSpriteIndex = 1
            };
        }

        return new MatchRewardData
        {
            exp = 50,
            coins = 25,
            diamonds = 5,
            resultSpriteIndex = 0
        };
    }

    public MatchRewardData GetCurrentTotalCleaningReward() => GetTotalCleaningReward(GetCapturePercent());

    public void ApplyMatchReward(MatchRewardData reward)
    {
        YG2.saves.score = HoleParent.totalScore;
        YG2.saves.exp += reward.exp;
        YG2.saves.goldCoins += reward.coins;
        YG2.saves.diamonds += reward.diamonds;

        YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
        VodovorotGameManager.Instance?.SaveProgress();
        rewardApplied = true;
    }

    public void ApplyCurrentTotalCleaningRewardIfNeeded()
    {
        if (!isTotalCleaningMode || rewardApplied)
            return;

        ApplyMatchReward(GetCurrentTotalCleaningReward());
    }

    public void FinalizeInterruptedMatchIfNeeded()
    {
        ApplyCurrentTotalCleaningRewardIfNeeded();
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return $"{minutes:00}:{secs:00}";
    }

    // ====================== ЛОКАЛИЗАЦИЯ ======================
    public void UpdateUI()
    {
        bool ru = YG2.saves.langRu;

        // Boost
        BoostText.text = DBoostText.text = ru ? "Буст Скорости" : "Speed Boost";

        // Settings in game
        SetPair(MobilePanelOfSettings[0], DesktopPanelOfSettings[0], ru, "Настройки", "Settings");
        SetPair(MobilePanelOfSettings[1], DesktopPanelOfSettings[1], ru, "Язык", "Language");
        SetPair(MobilePanelOfSettings[2], DesktopPanelOfSettings[2], ru, "Звуки", "Sounds");
        SetPair(MobilePanelOfSettings[3], DesktopPanelOfSettings[3], ru, "Музыка", "Music");
        SetPair(MobilePanelOfSettings[4], DesktopPanelOfSettings[4], ru, "Завершить игру", "End the game");

        // End panel
        SetPair(PanelOfEnd[0], DPanelOfEnd[0], ru, "Опыт:", "Experience:");
        SetPair(PanelOfEnd[1], DPanelOfEnd[1], ru, "Итог", "Result");
        SetPair(PanelOfEnd[2], DPanelOfEnd[2], ru, "Монеты:", "Coins:");
        SetPair(PanelOfEnd[3], DPanelOfEnd[3], ru, "Бриллианты:", "Brilliants:");
        SetPair(PanelOfEnd[4], DPanelOfEnd[4], ru, "Продолжить", "Continue");
        SetPair(PanelOfEnd[5], DPanelOfEnd[5], ru, "x3 Монеты\n(короткая реклама)", "x3 Coins\n(short ad)");
    }

    private void SetPair(Text mobile, Text desktop, bool ru, string russian, string english)
    {
        if (mobile != null) mobile.text = ru ? russian : english;
        if (desktop != null) desktop.text = ru ? russian : english;
    }

    // ====================== Вспомогательные методы ======================
    public void StartGameplay()
    {
        // Можно добавить сюда дополнительную логику запуска уровня
        Debug.Log("[GamingManager] Gameplay started");
    }

    public void ResetForNewGame()
    {
        perc = 0f;
        timer = 0f;
        timerGo = true;
        once = true;
        AllValues = 0;
        HoleParent.totalScore = 0;
        EnemyController.count = 0;
        rewardApplied = false;
        endSequenceStarted = false;
        isTotalCleaningMode = false;
    }
}
