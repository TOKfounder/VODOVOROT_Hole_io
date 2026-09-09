using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.LB;

public class MainMenuController : MonoBehaviour
{
	public static MainMenuController Instance;
	public InputField nameInput;
	public InputField DnameInput;
	public Image levelImage;
	public Text levelText;
	public Text pointText;
	public Image DlevelImage;
	public Text DlevelText;
	public Text DpointText;
	public Sprite[] maps;
	public Image mapField;

	public Text cntOfDiamonds;
	public Button exchangeBut;
	public Text DcntOfDiamonds;
	public Button DexchangeBut;

	
	public AudioSource dzyn;
	public AudioSource fart;


	[Header("Mobile UI")]
	public Text rank;

	public GameObject triggerForDaimonds;
	public GameObject triggerForNewSkin;

	public Button couple;
	public Button hand;
	public Button bag;
	public Button box;

	public Text Tcouple;
	public Text Thand;
	public Text Tbag;
	public Text Tbox;
	public Text scoreText;

	public Text[] MainMenu;
	public Text[] PanelOfSkins;
	public Text PanelOfLeaders;
	public Text[] MobilePanelOfSettings;
	public Text[] PanelOfMaps;
	public Text[] PanelOfModes;
	public Text[] PanelOfProgress;
	public Text[] PanelOfValute;

[Header("Desktop UI")]
	public Text Drank;

	public GameObject DtriggerForDaimonds;
	public GameObject DtriggerForNewSkin;

	public Button Dcouple;
	public Button Dhand;
	public Button Dbag;
	public Button Dbox;

	public Text DTcouple;
	public Text DThand;
	public Text DTbag;
	public Text DTbox;
	public Text DscoreText;

	public Text[] DMainMenu;
	public Text[] DPanelOfSkins;
	public Text DPanelOfLeaders;
	public Text[] DesktopPanelOfSettings;
	public Text[] DPanelOfMaps;
	public Text[] DPanelOfModes;
	public Text[] DPanelOfProgress;
	public Text[] DPanelOfValute;


	private int CntHand => YG2.saves.rewardedHandLeft;
	private int CntBag => YG2.saves.rewardedBagLeft;
	private int CntBox => YG2.saves.rewardedBoxLeft;




	void Awake()
	{
		Instance = this;
		YG2.saves.isGaming = false;
	}

	void OnEnable()
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
		// UpdateMainMenu();
		// LanguageManager.Instance.Onclick();
		// LanguageManager.Instance.Onclick();
		if (AudioManager.Instance != null)
			AudioManager.Instance.StartMusic();
		nameInput.onEndEdit.AddListener(SaveNick);
		DnameInput.onEndEdit.AddListener(SaveNick);
		exchangeBut.onClick.AddListener(ExchangeButton);
		DexchangeBut.onClick.AddListener(ExchangeButton);
		YG2.SaveProgress();
		couple.onClick.AddListener(() => ShowRewardedAdv("couple"));
		hand.onClick.AddListener(() => ShowRewardedAdv("hand"));
		bag.onClick.AddListener(() => ShowRewardedAdv("bag"));
		box.onClick.AddListener(() => ShowRewardedAdv("box"));
		Dcouple.onClick.AddListener(() => ShowRewardedAdv("couple"));
		Dhand.onClick.AddListener(() => ShowRewardedAdv("hand"));
		Dbag.onClick.AddListener(() => ShowRewardedAdv("bag"));
		Dbox.onClick.AddListener(() => ShowRewardedAdv("box"));
		UpdateTriggers();
		ActiveCanvas.ApplyUiFontEverywhere();
		if (GameController.Instance != null)
			GameController.Instance.RefreshModeSelectionUI();
		TutorialController.EnsureMenu();
	}

	public void UpdateTriggers()
	{
		triggerForNewSkin.SetActive(CheckForNewSkin());
		triggerForDaimonds.SetActive(CheckForDaimonds());
		DtriggerForNewSkin.SetActive(CheckForNewSkin());
		DtriggerForDaimonds.SetActive(CheckForDaimonds());
		
	}
	public bool CheckForNewSkin()
	{
		int[] necessaryLevels = {0, 1, 4, 7, 10 };
		int[] costsForCoins = {0, 20, 270, 800, 2400 };
		for (int i = 0; i < YG2.saves.massiveOfObtaining.Length; i++)
		{
			if (YG2.saves.massiveOfObtaining[i] == 1)
				continue;
			if (necessaryLevels[i] <= YG2.saves.levelOfProgress)
				return true;
			if (costsForCoins[i] <= YG2.saves.goldCoins)
				return true;
		}
		return false;
	}
	private void ShowRewardedAdv(string rewardID) => YG2.RewardedAdvShow(rewardID);

	private void UpgradeForAdv(string id)
	{
		if (id == "couple")
		{
			YG2.saves.diamonds += 5;
		}
		else if (id == "hand")
		{
			YG2.saves.rewardedHandLeft -= 1;
			if (YG2.saves.rewardedHandLeft <= 0)
			{
				YG2.saves.diamonds += 20;
				YG2.saves.rewardedHandLeft = 2;
			}
		}
		else if (id == "bag")
		{
			YG2.saves.rewardedBagLeft -= 1;
			if (YG2.saves.rewardedBagLeft <= 0)
			{
				YG2.saves.diamonds += 100;
				YG2.saves.rewardedBagLeft = 5;
			}
		}
		else if (id == "box")
		{
			YG2.saves.rewardedBoxLeft -= 1;
			if (YG2.saves.rewardedBoxLeft <= 0)
			{
				YG2.saves.diamonds += 300;
				YG2.saves.rewardedBoxLeft = 10;
			}
		}
		YG2.SaveProgress();
		UpdatePanelOfValute();
	}

	private void SuccessPurchased(string id)
	{
		if (id == "hand")
			YG2.saves.diamonds += 20;
		else if (id == "bag")
			YG2.saves.diamonds += 100;
		else if (id == "box")
			YG2.saves.diamonds += 300;
		else if (id == "chest")
			YG2.saves.diamonds += 600;
		else if (id == "gold")
			YG2.saves.massiveOfObtaining[2] = 1;
		else if (id == "scrag")
			YG2.saves.massiveOfObtaining[3] = 1;
		else if (id == "lord")
			YG2.saves.massiveOfObtaining[4] = 1;
		YG2.SaveProgress();
    YG2.ConsumePurchaseByID(id);
		HorizontalLayout3D.Instance?.UpdateForChosen();
		UpdatePanelOfValute();
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
		YG2.SaveProgress();
		UpdatePanelOfValute();
		UpdateTriggers();
	}

	private void FailedPurchased(string id)
	{
	}
	public void ApplyNick(string wroteName)
	{
		SaveNick(wroteName);
		if (nameInput != null)
			nameInput.text = wroteName;
		if (DnameInput != null)
			DnameInput.text = wroteName;
	}

	void SaveNick(string wroteName)
	{
		if (string.IsNullOrWhiteSpace(wroteName))
			wroteName = GameTexts.LegendNick;
		YG2.saves.isNickGiven = true;
		YG2.saves.nickName = wroteName;
		YG2.SaveProgress();
	}

	public void UpdateMainMenu()
	{
		UpdateMapOnBackground(YG2.saves.selectedMapID);
		YG2.saves.levelOfProgress = (int)(YG2.saves.exp / 100f);
		levelText.text = $"{(int)(YG2.saves.exp / 100f)}";
		pointText.text = $"{Tool.ConvertText(YG2.saves.goldCoins)}";
		levelImage.fillAmount = YG2.saves.exp % 100 / 100f;
		DlevelText.text = $"{(int)(YG2.saves.exp / 100f)}";
		DpointText.text = $"{Tool.ConvertText(YG2.saves.goldCoins)}";
		DlevelImage.fillAmount = YG2.saves.exp % 100 / 100f;
		if (YG2.saves.isNickGiven || !string.IsNullOrEmpty(YG2.saves.nickName))
		{
			string nick = string.IsNullOrEmpty(YG2.saves.nickName) ? GameTexts.LegendNick : YG2.saves.nickName;
			nameInput.text = nick;
			DnameInput.text = nick;
		}
		else if (YG2.saves.tutorialMenuSeen)
		{
			nameInput.text = GameTexts.LegendNick;
			DnameInput.text = GameTexts.LegendNick;
		}
		OnOpenLeaderboard();
		UpdateUI();
		UpdatePanelOfValute();
		UpdateTriggers();
		if (GameController.Instance != null)
			GameController.Instance.RefreshModeSelectionUI();
	}

	private void onUpdateLB(LBData lbData)
	{
		rank.text = "";
		Drank.text = "";
		if (lbData.technoName == "BestPlayers")
		{
			rank.text = $"{lbData.currentPlayer.rank}";
			Drank.text = $"{lbData.currentPlayer.rank}";
		}
	}

	public void UpdateMapOnBackground(int id)
	{
		mapField.sprite = maps[id];
		YG2.saves.selectedMapID = id;
		GameController.NormalizeChosenMode();
		YG2.SaveProgress();
		if (GameController.Instance != null)
			GameController.Instance.RefreshModeSelectionUI();
	}

	public bool CheckForDaimonds() => YG2.saves.diamonds != 0;

	public void UpdatePanelOfValute()
	{
		cntOfDiamonds.text = $"{YG2.saves.diamonds}";
		DcntOfDiamonds.text = $"{YG2.saves.diamonds}";
		Tcouple.text = YG2.saves.langRu ? $"1 рекл" : $"1 ad";
		Thand.text = YG2.saves.langRu ? $"{CntHand} рекл" : $"{CntHand} ad";
		Tbag.text = YG2.saves.langRu ? $"{CntBag} рекл" : $"{CntBag} ad";
		Tbox.text = YG2.saves.langRu ? $"{CntBox} рекл" : $"{CntBox} ad";
		DTcouple.text = YG2.saves.langRu ? $"1 рекл" : $"1 ad";
		DThand.text = YG2.saves.langRu ? $"{CntHand} рекл" : $"{CntHand} ad";
		DTbag.text = YG2.saves.langRu ? $"{CntBag} рекл" : $"{CntBag} ad";
		DTbox.text = YG2.saves.langRu ? $"{CntBox} рекл" : $"{CntBox} ad";
	}

	public void OnOpenLeaderboard()
	{
		YG2.GetLeaderboard("BestPlayers");
	}

	public void UpdateUI()
	{
		bool ru = YG2.saves.langRu;

		SetText(scoreText, YG2.saves.exp.ToString());
		SetTexts(MainMenu,
			GameTexts.Title,
			GameTexts.EnterNick,
			GameTexts.Level,
			ru ? "Магазин\nСкинов" : "Skin\nStore",
			ru ? "КАРТЫ" : "MAPS",
			ru ? "РЕЖИМЫ" : "MODES",
			ru ? "ИГРАТЬ" : "PLAY",
			ru ? "ЛИДЕРЫ" : "LEADERS",
			ru ? "НАСТРОЙКИ" : "SETTINGS");

		SetTexts(PanelOfSkins,
			ru ? "Описание" : "Description",
			ru ? "Белый\nДруг" : "White\nFriend",
			ru ? "Золотой\nунитаз" : "Golden\nbowl",
			ru ? "Трон\nКощея" : "Scrag's\nThrone",
			ru ? "Туалет\nБога" : "God's\nToilet",
			ru ? "Красный\nТазик" : "Red\nbasin",
			ru ? "Особенности" : "Features");

		SetText(PanelOfLeaders, ru ? "Легенды" : "Legends");
		SetTexts(MobilePanelOfSettings,
			GameTexts.Settings,
			GameTexts.Language,
			GameTexts.Sounds,
			GameTexts.Music);
		SetTexts(PanelOfMaps,
			ru ? "Доступные Локации" : "Available Locations",
			ru ? "Городской Вайб" : "City Vibe",
			ru ? "Садовый Парк" : "Garden Park",
			ru ? "Новые карты скоро..." : "New maps are coming soon...");

		SetModePanelTexts(PanelOfModes, ru);

		SetTexts(PanelOfProgress,
			ru ? "Прогресс" : "Progress",
			ru ? "Красный\nТазик" : "Red\nbasin",
			ru ? "Белый\nДруг" : "White\nFriend",
			ru ? "Золотой\nунитаз" : "Golden\nbowl",
			ru ? "Трон\nКощея" : "Scrag's\nThrone",
			ru ? "Туалет\nБога" : "God's\nToilet");
		SetTexts(PanelOfValute,
			ru ? "Магазин Валюты" : "Currency Store",
			ru ? "Баланс:" : "Balance:",
			ru ? "пара\nалмазов" : "couple\nof diamonds",
			ru ? "Горсть\nалмазов" : "Handful of\ndiamonds",
			ru ? "Мешок\nалмазов" : "Bag of\ndiamonds",
			ru ? "Бочка\nалмазов" : "Barrel of\ndiamonds",
			ru ? "Сундук\nалмазов" : "Chest of\ndiamonds",
			ru ? "Обменять" : "Exchange");

		SetText(DscoreText, YG2.saves.exp.ToString());
		SetTexts(DMainMenu,
			GameTexts.Title,
			GameTexts.EnterNick,
			GameTexts.Level,
			ru ? "Магазин\nСкинов" : "Skin\nStore",
			ru ? "КАРТЫ" : "MAPS",
			ru ? "РЕЖИМЫ" : "MODES",
			ru ? "ЛИДЕРЫ" : "LEADERS",
			ru ? "НАСТРОЙКИ" : "SETTINGS",
			ru ? "ИГРАТЬ" : "PLAY");
		SetTexts(DPanelOfSkins,
			ru ? "Описание" : "Description",
			ru ? "Белый\nДруг" : "White\nFriend",
			ru ? "Золотой\nунитаз" : "Golden\nbowl",
			ru ? "Трон\nКощея" : "Scrag's\nThrone",
			ru ? "Туалет\nБога" : "God's\nToilet",
			ru ? "Красный\nТазик" : "Red\nbasin",
			ru ? "Особенности" : "Features");
		SetText(DPanelOfLeaders, ru ? "Легенды" : "Legends");
		SetTexts(DesktopPanelOfSettings,
			GameTexts.Settings,
			GameTexts.Language,
			GameTexts.Sounds,
			GameTexts.Music);
		SetTexts(DPanelOfMaps,
			ru ? "Доступные Локации" : "Available Locations",
			ru ? "Городской Вайб" : "City Vibe",
			ru ? "Садовый Парк" : "Garden Park",
			ru ? "Новые карты скоро..." : "New maps are coming soon...");
		SetModePanelTexts(DPanelOfModes, ru);
		SetTexts(DPanelOfProgress,
			ru ? "Прогресс" : "Progress",
			ru ? "Красный\nТазик" : "Red\nbasin",
			ru ? "Белый\nДруг" : "White\nFriend",
			ru ? "Золотой\nунитаз" : "Golden\nbowl",
			ru ? "Трон\nКощея" : "Scrag's\nThrone",
			ru ? "Туалет\nБога" : "God's\nToilet");
		SetTexts(DPanelOfValute,
			ru ? "Магазин Валюты" : "Currency Store",
			ru ? "Баланс:" : "Balance:",
			ru ? "пара\nалмазов" : "couple\nof diamonds",
			ru ? "Горсть\nалмазов" : "Handful of\ndiamonds",
			ru ? "Мешок\nалмазов" : "Bag of\ndiamonds",
			ru ? "Бочка\nалмазов" : "Barrel of\ndiamonds",
			ru ? "Сундук\nалмазов" : "Chest of\ndiamonds",
			ru ? "Обменять" : "Exchange");
	}

	private static void SetModePanelTexts(Text[] panel, bool ru)
	{
		SetTexts(panel,
			ru ? "Доступные Режимы" : "Available Modes",
			ru ? "Тотальная Зачистка" : "Total Cleaning",
			ru ? "Съешь крупные объекты за 3 минуты. Мелочь можно есть, но в 100% она не входит." :
				"Eat the large objects in 3 minutes. Small props are optional and do not count toward 100%.",
			ru ? "Босс Туалетов" : "The Toilet Boss",
			ru ? "Задача перегнать Босса по уровню и победить поглотив его" :
				"The task is to overtake the Boss by level and defeat him by absorbing him",
			ru ? "Охота" : "Hunting",
			ru ? "Город — 5 врагов, сад — 4. Мелкие убегают, крупные идут в тебя. Награда за каждого, джекпот за всех." :
				"City has 5 enemies, garden has 4. Small ones flee, big ones chase you. Reward per kill, jackpot for all.",
			ru ? "Командный" : "Teamwork",
			ru ? "Победа по сумме поглощённого. На HUD — процент очков синих и красных." :
				"Win by absorbed score. The HUD shows each team's point percent.");
	}

	private static void SetText(Text target, string value)
	{
		if (target != null)
			target.text = value;
	}

	private static void SetTexts(Text[] panel, params string[] values)
	{
		if (panel == null || values == null)
			return;

		int count = Mathf.Min(panel.Length, values.Length);
		for (int i = 0; i < count; i++)
			SetText(panel[i], values[i]);
	}


}
