using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.LB;

public class MainMenuController : MonoBehaviour
{
    [Header("Input Fields")]
    public InputField nameInput;
    public InputField DnameInput;

    [Header("Level & Points")]
    public Image levelImage;
    public Text levelText;
    public Text pointText;
    public Image DlevelImage;
    public Text DlevelText;
    public Text DpointText;

    [Header("Maps")]
    public Sprite[] maps;
    public Image mapField;

    [Header("Diamonds & Exchange")]
    public Text cntOfDiamonds;
    public Button exchangeBut;
    public Text DcntOfDiamonds;
    public Button DexchangeBut;

    [Header("Audio")]
    public AudioSource dzyn;
    public AudioSource fart;

    [Header("Mobile UI")]
    public Text rank;
    public GameObject triggerForDaimonds;
    public GameObject triggerForNewSkin;

    public Button couple, hand, bag, box;
    public Text Tcouple, Thand, Tbag, Tbox;
    public Text scoreText;

    [Header("Desktop UI")]
    public Text Drank;
    public GameObject DtriggerForDaimonds;
    public GameObject DtriggerForNewSkin;

    public Button Dcouple, Dhand, Dbag, Dbox;
    public Text DTcouple, DThand, DTbag, DTbox;
    public Text DscoreText;

    [Header("Text Arrays for Localization")]
    public Text[] MainMenu;
    public Text[] PanelOfSkins;
    public Text PanelOfLeaders;
    public Text[] MobilePanelOfSettings;
    public Text[] PanelOfMaps;
    public Text[] PanelOfModes;
    public Text[] PanelOfProgress;
    public Text[] PanelOfValute;

    public Text[] DMainMenu;
    public Text[] DPanelOfSkins;
    public Text DPanelOfLeaders;
    public Text[] DesktopPanelOfSettings;
    public Text[] DPanelOfMaps;
    public Text[] DPanelOfModes;
    public Text[] DPanelOfProgress;
    public Text[] DPanelOfValute;

    // Счётчики для rewarded рекламы
    private int CntHand = 2;
    private int CntBag = 5;
    private int CntBox = 10;

    private void Awake()
    {
        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.MainMenuController = this;

        YG2.saves.isGaming = false;
    }

    private void OnEnable()
    {
        YG2.onPurchaseSuccess += SuccessPurchased;
        YG2.onPurchaseFailed += FailedPurchased;
        YG2.onRewardAdv += UpgradeForAdv;
        YG2.onGetLeaderboard += onUpdateLB;
        YG2.GetLeaderboard("BestPlayers");
    }

    private void OnDisable()
    {
        YG2.onPurchaseSuccess -= SuccessPurchased;
        YG2.onPurchaseFailed -= FailedPurchased;
        YG2.onRewardAdv -= UpgradeForAdv;
        YG2.onGetLeaderboard -= onUpdateLB;
    }

    void Start()
    {
        nameInput.onEndEdit.AddListener(SaveNick);
        DnameInput.onEndEdit.AddListener(SaveNick);

        exchangeBut.onClick.AddListener(ExchangeButton);
        DexchangeBut.onClick.AddListener(ExchangeButton);

        couple.onClick.AddListener(() => ShowRewardedAdv("couple"));
        hand.onClick.AddListener(() => ShowRewardedAdv("hand"));
        bag.onClick.AddListener(() => ShowRewardedAdv("bag"));
        box.onClick.AddListener(() => ShowRewardedAdv("box"));

        Dcouple.onClick.AddListener(() => ShowRewardedAdv("couple"));
        Dhand.onClick.AddListener(() => ShowRewardedAdv("hand"));
        Dbag.onClick.AddListener(() => ShowRewardedAdv("bag"));
        Dbox.onClick.AddListener(() => ShowRewardedAdv("box"));

        UpdateMainMenu();
        UpdateTriggers();
    }

    // ====================== ЛОКАЛИЗАЦИЯ ======================
    private void UpdateAllTexts()
    {
        bool ru = YG2.saves.langRu;

        // Main Menu
        SetPair(MainMenu[0], DMainMenu[0], ru, "ВОДОВОРОТ Дыра.ио", "WHIRLPOOL Hole");
        SetPair(MainMenu[1], DMainMenu[1], ru, "Введите ваш ник", "Enter your nickname");
        SetPair(MainMenu[2], DMainMenu[2], ru, "Уровень", "Level");
        SetPair(MainMenu[3], DMainMenu[3], ru, "Магазин\nСкинов", "Skin\nStore");
        SetPair(MainMenu[4], DMainMenu[4], ru, "КАРТЫ", "MAPS");
        SetPair(MainMenu[5], DMainMenu[5], ru, "РЕЖИМЫ", "MODES");
        SetPair(MainMenu[6], DMainMenu[8], ru, "ИГРАТЬ", "PLAY");     // порядок в массивах немного отличается
        SetPair(MainMenu[7], DMainMenu[6], ru, "ЛИДЕРЫ", "LEADERS");
        SetPair(MainMenu[8], DMainMenu[7], ru, "НАСТРОЙКИ", "SETTINGS");

        // Skins
        SetPair(PanelOfSkins[0], DPanelOfSkins[0], ru, "Описание", "Description");
        SetPair(PanelOfSkins[1], DPanelOfSkins[1], ru, "Белый\nДруг", "White\nFriend");
        SetPair(PanelOfSkins[2], DPanelOfSkins[2], ru, "Золотой\nунитаз", "Golden\nbowl");
        SetPair(PanelOfSkins[3], DPanelOfSkins[3], ru, "Трон\nКощея", "Scrag's\nThrone");
        SetPair(PanelOfSkins[4], DPanelOfSkins[4], ru, "Туалет\nБога", "God's\nToilet");
        SetPair(PanelOfSkins[5], DPanelOfSkins[5], ru, "Красный\nТазик", "Red\nbasin");
        SetPair(PanelOfSkins[6], DPanelOfSkins[6], ru, "Особенности", "Features");

        // Leaders
        PanelOfLeaders.text = DPanelOfLeaders.text = ru ? "Легенды" : "Legends";

        // Settings
        SetPair(MobilePanelOfSettings[0], DesktopPanelOfSettings[0], ru, "Настройки", "Settings");
        SetPair(MobilePanelOfSettings[1], DesktopPanelOfSettings[1], ru, "Язык", "Language");
        SetPair(MobilePanelOfSettings[2], DesktopPanelOfSettings[2], ru, "Звуки", "Sounds");
        SetPair(MobilePanelOfSettings[3], DesktopPanelOfSettings[3], ru, "Музыка", "Music");

        // Maps
        SetPair(PanelOfMaps[0], DPanelOfMaps[0], ru, "Доступные Локации", "Available Locations");
        SetPair(PanelOfMaps[1], DPanelOfMaps[1], ru, "Городской Вайб", "City Vibe");
        SetPair(PanelOfMaps[2], DPanelOfMaps[2], ru, "Садовый Парк", "Garden Park");
        SetPair(PanelOfMaps[3], DPanelOfMaps[3], ru, "Новые карты скоро...", "New maps are coming soon...");

        // Modes
        SetPair(PanelOfModes[0], DPanelOfModes[0], ru, "Доступные Режимы", "Available Modes");
        SetPair(PanelOfModes[1], DPanelOfModes[1], ru, "Тотальная Зачистка", "Total Cleaning");
        SetPair(PanelOfModes[2], DPanelOfModes[2], ru,
            "Задача поглотить абсолютно все объекты на карте на 100%",
            "The task is to absorb absolutely all objects on the map by 100%");
        SetPair(PanelOfModes[3], DPanelOfModes[3], ru, "Босс Туалетов", "The Toilet Boss");
        SetPair(PanelOfModes[4], DPanelOfModes[4], ru,
            "Задача перегнать Босса по уровню и победить поглотив его",
            "The task is to overtake the Boss by level and defeat him by absorbing him");
        SetPair(PanelOfModes[5], DPanelOfModes[5], ru, "Охота", "Hunting");
        SetPair(PanelOfModes[6], DPanelOfModes[6], ru,
            "Появляется 6 врагов-туалетов. Твоя задача - поглотить всех",
            "6 toilet enemies appear. Your task is to consume everyone");
        SetPair(PanelOfModes[7], DPanelOfModes[7], ru, "Командный", "Teamwork");
        SetPair(PanelOfModes[8], DPanelOfModes[8], ru,
            "3 Красных Vs 3 Синих. Задача задавить вражескую команду",
            "3 Red Vs 3 Blue. The task is to crush the enemy team");
        SetPair(PanelOfModes[9], DPanelOfModes[9], ru, "Скоро в игре", "Coming soon in the game");
        SetPair(PanelOfModes[10], DPanelOfModes[10], ru, "Скоро в игре", "Coming soon in the game");
        SetPair(PanelOfModes[11], DPanelOfModes[11], ru, "Скоро в игре", "Coming soon in the game");

        // Progress
        SetPair(PanelOfProgress[0], DPanelOfProgress[0], ru, "Прогресс", "Progress");
        SetPair(PanelOfProgress[1], DPanelOfProgress[1], ru, "Красный\nТазик", "Red\nbasin");
        SetPair(PanelOfProgress[2], DPanelOfProgress[2], ru, "Белый\nДруг", "White\nFriend");
        SetPair(PanelOfProgress[3], DPanelOfProgress[3], ru, "Золотой\nунитаз", "Golden\nbowl");
        SetPair(PanelOfProgress[4], DPanelOfProgress[4], ru, "Трон\nКощея", "Scrag's\nThrone");
        SetPair(PanelOfProgress[5], DPanelOfProgress[5], ru, "Туалет\nБога", "God's\nToilet");

        // Valute
        SetPair(PanelOfValute[0], DPanelOfValute[0], ru, "Магазин Валюты", "Currency Store");
        SetPair(PanelOfValute[1], DPanelOfValute[1], ru, "Баланс:", "Balance:");
        SetPair(PanelOfValute[2], DPanelOfValute[2], ru, "пара\nбриллиантов", "couple\ndiamonds");
        SetPair(PanelOfValute[3], DPanelOfValute[3], ru, "Горсть\nбриллиантов", "Bunch\ndiamonds");
        SetPair(PanelOfValute[4], DPanelOfValute[4], ru, "Мешок\nбриллиантов", "Bag\ndiamonds");
        SetPair(PanelOfValute[5], DPanelOfValute[5], ru, "Бочка\nбриллиантов", "Barrel\ndiamonds");
        SetPair(PanelOfValute[6], DPanelOfValute[6], ru, "Сундук\nбриллиантов", "Chest\ndiamonds");
        SetPair(PanelOfValute[7], DPanelOfValute[7], ru, "Обменять", "Exchange");
    }

    private void SetPair(Text mobile, Text desktop, bool ru, string russian, string english)
    {
        if (mobile != null) mobile.text = ru ? russian : english;
        if (desktop != null) desktop.text = ru ? russian : english;
    }

    // ====================== ОБНОВЛЕНИЕ UI ======================
    public void UpdateMainMenu()
    {
        UpdateMapOnBackground(YG2.saves.selectedMapID);

        YG2.saves.levelOfProgress = (int)(YG2.saves.exp / 100f);

        levelText.text = YG2.saves.levelOfProgress.ToString();
        pointText.text = Tool.ConvertText(YG2.saves.goldCoins);
        levelImage.fillAmount = (YG2.saves.exp % 100f) / 100f;

        DlevelText.text = YG2.saves.levelOfProgress.ToString();
        DpointText.text = Tool.ConvertText(YG2.saves.goldCoins);
        DlevelImage.fillAmount = (YG2.saves.exp % 100f) / 100f;

        if (YG2.saves.isNickGiven)
        {
            nameInput.text = YG2.saves.nickName;
            DnameInput.text = YG2.saves.nickName;
        }

        UpdateAllTexts();
        UpdatePanelOfValute();
        UpdateTriggers();
        OnOpenLeaderboard();
    }

    public void UpdatePanelOfValute()
    {
        bool ru = YG2.saves.langRu;

        cntOfDiamonds.text = DcntOfDiamonds.text = YG2.saves.diamonds.ToString();

        Tcouple.text = DTcouple.text = ru ? "1 рекл" : "1 ad";
        Thand.text = DThand.text = ru ? $"{CntHand} рекл" : $"{CntHand} ad";
        Tbag.text = DTbag.text = ru ? $"{CntBag} рекл" : $"{CntBag} ad";
        Tbox.text = DTbox.text = ru ? $"{CntBox} рекл" : $"{CntBox} ad";
    }

    public void UpdateTriggers()
    {
        bool hasNew = CheckForNewSkin();
        bool hasDia = CheckForDaimonds();

        triggerForNewSkin.SetActive(hasNew);
        triggerForDaimonds.SetActive(hasDia);
        DtriggerForNewSkin.SetActive(hasNew);
        DtriggerForDaimonds.SetActive(hasDia);
    }

    public bool CheckForNewSkin()
    {
        int[] levels = { 0, 1, 4, 7, 10 };
        int[] costs = { 0, 20, 270, 800, 2400 };

        for (int i = 0; i < YG2.saves.massiveOfObtaining.Length; i++)
        {
            if (YG2.saves.massiveOfObtaining[i] == 1) continue;
            if (levels[i] <= YG2.saves.levelOfProgress || costs[i] <= YG2.saves.goldCoins)
                return true;
        }
        return false;
    }

    public bool CheckForDaimonds() => YG2.saves.diamonds != 0;

    private void ShowRewardedAdv(string id) => YG2.RewardedAdvShow(id);

    private void UpgradeForAdv(string id)
    {
        if (id == "couple") YG2.saves.diamonds += 5;
        else if (id == "hand") { CntHand = Mathf.Max(0, CntHand - 1); if (CntHand == 0) { YG2.saves.diamonds += 20; CntHand = 2; } }
        else if (id == "bag") { CntBag = Mathf.Max(0, CntBag - 1); if (CntBag == 0) { YG2.saves.diamonds += 100; CntBag = 5; } }
        else if (id == "box") { CntBox = Mathf.Max(0, CntBox - 1); if (CntBox == 0) { YG2.saves.diamonds += 300; CntBox = 10; } }

        VodovorotGameManager.Instance.SaveProgress();
        UpdatePanelOfValute();
    }

    private void SuccessPurchased(string id)
    {
        switch (id)
        {
            case "hand": YG2.saves.diamonds += 20; break;
            case "bag": YG2.saves.diamonds += 100; break;
            case "box": YG2.saves.diamonds += 300; break;
            case "chest": YG2.saves.diamonds += 600; break;
            case "gold": YG2.saves.massiveOfObtaining[2] = 1; break;
            case "scrag": YG2.saves.massiveOfObtaining[3] = 1; break;
            case "lord": YG2.saves.massiveOfObtaining[4] = 1; break;
        }

        VodovorotGameManager.Instance.SaveProgress();
        YG2.ConsumePurchaseByID(id);

        VodovorotGameManager.Instance.HorizontalLayout3D?.UpdateForChosen();
        UpdatePanelOfValute();
        UpdateTriggers();
    }

    private void FailedPurchased(string id) => Debug.Log($"Покупка {id} была совершена");

    private void SaveNick(string name)
    {
        YG2.saves.isNickGiven = true;
        YG2.saves.nickName = name;
        VodovorotGameManager.Instance.SaveProgress();
    }

    private void ExchangeButton()
    {
        if (YG2.saves.diamonds == 0)
        {
            fart.Play();
            return;
        }

        YG2.saves.goldCoins += YG2.saves.diamonds * 5;
        YG2.saves.diamonds = 0;

        VodovorotGameManager.Instance.SaveProgress();
        UpdatePanelOfValute();
        UpdateTriggers();
    }

    private void onUpdateLB(LBData data)
    {
        if (data.technoName == "BestPlayers")
        {
            rank.text = Drank.text = data.currentPlayer.rank.ToString();
        }
    }

    public void UpdateMapOnBackground(int id)
    {
        if (id < 0 || id >= maps.Length) return;
        mapField.sprite = maps[id];
        YG2.saves.selectedMapID = id;
        VodovorotGameManager.Instance.SaveProgress();
    }

    public void OnOpenLeaderboard() => YG2.GetLeaderboard("BestPlayers");
}