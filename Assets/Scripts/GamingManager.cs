using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class GamingManager : MonoBehaviour
{
    [Header("Core")]
    public GameObject MobpanelOfEnd;
    public GameObject DeskpanelOfEnd;

    [Header("Progress UI")]
    public Image Mflazhok;
    public Image Dflazhok;
    public Text Mpercent;
    public Text Dpercent;

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

    // Публичные данные
    public float perc = 0f;
    public float timer;
    public int AllValues;

    private bool timerGo = true;
    private bool once = true;

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
        YG2.saves.isGaming = true;
        Time.timeScale = 1f;
        timer = 0f;
        timerGo = true;

        VodovorotGameManager.Instance.SaveProgress();

        StartCoroutine(UpdateProgressRoutine());
        UpdateUI();
    }

    // Корутина вместо Update — обновляем прогресс реже (каждые 0.2 сек)
    private IEnumerator UpdateProgressRoutine()
    {
        while (true)
        {
            YG2.saves.score = HoleParent.totalScore;
            perc = AllValues > 15 ? (float)YG2.saves.score / (AllValues - 15) : 0f;

            // Обновляем UI прогресса
            UpdateProgressUI();

            // Проверка победы
            if (once && perc >= 1f)
            {
                once = false;
                EndOfGame();
            }

            yield return new WaitForSeconds(0.2f);   // достаточно 5 раз в секунду
        }
    }

    private void UpdateProgressUI()
    {
        float fill = Mathf.Clamp01(perc);

        if (YG2.envir.isMobile)
        {
            if (Mflazhok != null) Mflazhok.fillAmount = fill;
            if (Mpercent != null) Mpercent.text = $"{(int)(fill * 100)}%";
        }
        else
        {
            if (Dflazhok != null) Dflazhok.fillAmount = fill;
            if (Dpercent != null) Dpercent.text = $"{(int)(fill * 100)}%";
        }
    }

    void FixedUpdate()
    {
        if (timerGo)
            timer += Time.fixedDeltaTime;
    }

    public void HandleTimer(bool b) => timerGo = b;

    public void EndOfGame()
    {
        timerGo = false;
        once = false;
        Time.timeScale = 0;

        if (YG2.envir.isMobile)
            MobpanelOfEnd?.SetActive(true);
        else
            DeskpanelOfEnd?.SetActive(true);

        // Даём небольшую задержку перед полной остановкой времени
        Invoke(nameof(FullTimeStop), 7f);
    }

    private void FullTimeStop()
    {
        Time.timeScale = 0f;
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
    }
}
