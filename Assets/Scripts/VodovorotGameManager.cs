using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using YG;

public class VodovorotGameManager : MonoBehaviour
{
    public static VodovorotGameManager Instance { get; private set; }

    public static List<Collider> allPlatforms = new List<Collider>();

    [Header("Core Controllers")]
    public GameController GameController;
    public AudioManager AudioManager;
    public LanguageManager LanguageManager;

    [Header("Menu Controllers")]
    public MainMenuController MainMenuController;

    [Header("Gameplay Controllers")]
    public GamingManager GamingManager;
    public ModeManager ModeManager;
    public HorizontalLayout3D HorizontalLayout3D;

    public bool IsInGame => SceneManager.GetActiveScene().buildIndex != 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAllSystems();
    }

    private void InitializeAllSystems()
    {
        // Используем FindAnyObjectByType как ты просил
        if (GameController == null)
            GameController = FindAnyObjectByType<GameController>();

        if (AudioManager == null)
            AudioManager = FindAnyObjectByType<AudioManager>();

        if (LanguageManager == null)
            LanguageManager = FindAnyObjectByType<LanguageManager>();

        if (MainMenuController == null)
            MainMenuController = FindAnyObjectByType<MainMenuController>();

        if (GamingManager == null)
            GamingManager = FindAnyObjectByType<GamingManager>();

        if (ModeManager == null)
            ModeManager = FindAnyObjectByType<ModeManager>();

        if (HorizontalLayout3D == null)
            HorizontalLayout3D = FindAnyObjectByType<HorizontalLayout3D>();

        allPlatforms.Clear();
    }

    // Методы для сохранения
    public void SaveProgress() => YG2.SaveProgress();

    public void AddExp(int amount)
    {
        YG2.saves.exp += amount;
        SaveProgress();
    }

    public void AddGoldCoins(int amount)
    {
        YG2.saves.goldCoins += amount;
        SaveProgress();
    }

    public void AddDiamonds(int amount)
    {
        YG2.saves.diamonds += amount;
        SaveProgress();
    }

    public void StartGame(int mapID = -1)
    {
        if (mapID >= 0) YG2.saves.selectedMapID = mapID;
        YG2.saves.isGaming = true;
        SaveProgress();
        SceneManager.LoadScene(YG2.saves.selectedMapID + 1);
    }

    public void ReturnToMenu()
    {
        YG2.saves.isGaming = false;
        SaveProgress();
        SceneManager.LoadScene(0);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeAllSystems();

        if (IsInGame)
            ResetGameplayRuntimeState();

        // Даём YG2 один кадр на инициализацию
        StartCoroutine(DelayedMenuUpdate());
    }

    private void ResetGameplayRuntimeState()
    {
        HoleParent.totalScore = 0;
        HoleParent.holeList.Clear();
        EnemyController.count = 0;
        GamingManager?.ResetForNewGame();
    }

    private IEnumerator DelayedMenuUpdate()
    {
        yield return null;        // ждём один кадр

        if (IsInGame)
        {
            if (GamingManager != null)
                GamingManager.UpdateUI();
        }
        else
        {
            if (MainMenuController != null)
                MainMenuController.UpdateMainMenu();
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
}
