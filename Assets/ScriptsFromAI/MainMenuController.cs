using UnityEngine;
using UnityEngine.UI;
using YG;

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
	public Button couple;
	public Button hand;
	public Button bag;
	public Button box;

	public Text Tcouple;
	public Text Thand;
	public Text Tbag;
	public Text Tbox;

	public Text[] MainMenu;
	public Text[] PanelOfSkins;
	public Text PanelOfLeaders;
	public Text[] MobilePanelOfSettings;
	public Text[] PanelOfMaps;
	public Text[] PanelOfModes;
	public Text[] PanelOfProgress;
	public Text[] PanelOfValute;
[Header("Desktop UI")]
	public Button Dcouple;
	public Button Dhand;
	public Button Dbag;
	public Button Dbox;

	public Text DTcouple;
	public Text DThand;
	public Text DTbag;
	public Text DTbox;
	public Text[] DMainMenu;
	public Text[] DPanelOfSkins;
	public Text DPanelOfLeaders;
	public Text[] DesktopPanelOfSettings;
	public Text[] DPanelOfMaps;
	public Text[] DPanelOfModes;
	public Text[] DPanelOfProgress;
	public Text[] DPanelOfValute;


	private int CntHand = 2;
	private int CntBag = 5;
	private int CntBox = 10;

	bool subscribed = false;



	void Awake()
	{
		Instance = this;
		YG2.saves.isGaming = false;
	}

	void OnEnable()
	{
		if (!subscribed) {
			YG2.onPurchaseSuccess += SuccessPurchased;
			subscribed = true;
		}
		YG2.onPurchaseFailed += FailedPurchased;
		YG2.onRewardAdv += UpgradeForAdv;
	}
	private void OnDisable()
	{
		 if (subscribed) {
			YG2.onPurchaseSuccess -= SuccessPurchased;
			subscribed = false;
    }
		YG2.onPurchaseFailed -= FailedPurchased;
		YG2.onRewardAdv -= UpgradeForAdv;
	}
	void Start()
	{
		// UpdateMainMenu();
		// LanguageManager.Instance.Onclick();
		// LanguageManager.Instance.Onclick();
		// AudioManager.Instance.StartMusic();
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
			CntHand -= 1;
			if (CntHand <= 0)
			{
				YG2.saves.diamonds += 20;
				CntHand = 2;
			}
		}
		else if (id == "bag")
		{
			CntBag -= 1;
			if (CntBag <= 0)
			{
				YG2.saves.diamonds += 100;
				CntBag = 5;
			}
		}
		else if (id == "box")
		{
			CntBox -= 1;
			if (CntBox <= 0)
			{
				YG2.saves.diamonds += 300;
				CntBox = 10;
			}
		}
		YG2.SaveProgress();
		UpdatePanelOfValute();
	}

	private void SuccessPurchased(string id)
	{
		if (id == "hand")
			YG2.saves.diamonds += 20/2;
		else if (id == "bag")
			YG2.saves.diamonds += 100/2;
		else if (id == "box")
			YG2.saves.diamonds += 300/2;
		else if (id == "chest")
			YG2.saves.diamonds += 600/2;
		else if (id == "gold")
			YG2.saves.massiveOfObtaining[1] = 1;
		else if (id == "scrag")
			YG2.saves.massiveOfObtaining[2] = 1;
		else if (id == "lord")
			YG2.saves.massiveOfObtaining[3] = 1;
		YG2.SaveProgress();
    YG2.ConsumePurchaseByID(id);
		Debug.Log($"Покупка {id} сохранена и обработана");
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
	}

	private void FailedPurchased(string id)
	{
		Debug.Log($"Покупка {id} была совершена");
	}
	void SaveNick(string wroteName)
	{
		YG2.saves.isNickGiven = true;
		YG2.saves.nickName = wroteName;
		YG2.SaveProgress();
	}

	public void UpdateMainMenu()
	{
		UpdateMapOnBackground(YG2.saves.selectedMapID);
		YG2.saves.levelOfProgress = (int)(YG2.saves.exp / 100f) + 1;
		levelText.text = $"{(int)(YG2.saves.exp / 100f)}";
		pointText.text = $"{Tool.ConvertText(YG2.saves.goldCoins)}";
		levelImage.fillAmount = YG2.saves.exp % 100 / 100f;
		DlevelText.text = $"{(int)(YG2.saves.exp / 100f)}";
		DpointText.text = $"{Tool.ConvertText(YG2.saves.goldCoins)}";
		DlevelImage.fillAmount = YG2.saves.exp % 100 / 100f;
		if (YG2.saves.isNickGiven)
		{
			nameInput.text = YG2.saves.nickName;
			DnameInput.text = YG2.saves.nickName;
		}
		UpdateUI();
		UpdatePanelOfValute();
	}

	public void UpdateMapOnBackground(int id)
	{
		mapField.sprite = maps[id];
		YG2.saves.selectedMapID = id;
		// YG2.SaveProgress();
	}

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
		MainMenu[0].text = YG2.saves.langRu ? "Туалетная Дыра.ио" : "Toilet Hole";
		MainMenu[1].text = YG2.saves.langRu ? "Введите ваш ник" : "Enter your nickname";
		MainMenu[2].text = YG2.saves.langRu ? "Уровень" : "Level";
		MainMenu[3].text = YG2.saves.langRu ? "Магазин\nСкинов" : "Skin\nStore";
		MainMenu[4].text = YG2.saves.langRu ? "КАРТЫ" : "MAPS";
		MainMenu[5].text = YG2.saves.langRu ? "РЕЖИМЫ" : "MODES";
		MainMenu[6].text = YG2.saves.langRu ? "ИГРАТЬ" : "PLAY";
		MainMenu[7].text = YG2.saves.langRu ? "ЛИДЕРЫ" : "LEADERS";
		MainMenu[8].text = YG2.saves.langRu ? "НАСТРОЙКИ" : "SETTINGS";

		PanelOfSkins[0].text = YG2.saves.langRu ? "Описание" : "Description";
		PanelOfSkins[1].text = YG2.saves.langRu ? "Белый\nДруг" : "White\nFriend";
		PanelOfSkins[2].text = YG2.saves.langRu ? "Золотой\nунитаз" : "Golden\nbowl";
		PanelOfSkins[3].text = YG2.saves.langRu ? "Трон\nКощея" : "Scrag's\nThrone";
		PanelOfSkins[4].text = YG2.saves.langRu ? "Туалет\nБога" : "God's\nToilet";
		PanelOfSkins[5].text = YG2.saves.langRu ? "Особенности" : "Features";

		PanelOfLeaders.text = YG2.saves.langRu ? "Легенды" : "Legends";

		MobilePanelOfSettings[0].text = YG2.saves.langRu ? "Настройки" : "Settings";
		MobilePanelOfSettings[1].text = YG2.saves.langRu ? "Язык" : "Language";
		MobilePanelOfSettings[2].text = YG2.saves.langRu ? "Звуки" : "Sounds";
		MobilePanelOfSettings[3].text = YG2.saves.langRu ? "Музыка" : "Music";

		PanelOfMaps[1].text = YG2.saves.langRu ? "Городской Вайб" : "City Vibe";
		PanelOfMaps[0].text = YG2.saves.langRu ? "Доступные Локации" : "Available Locations";
		PanelOfMaps[2].text = YG2.saves.langRu ? "Садовый Парк" : "Garden Park";
		PanelOfMaps[3].text = YG2.saves.langRu ? "Новые карты скоро..." : "New maps are coming soon...";

		PanelOfModes[0].text = YG2.saves.langRu ? "Доступные Режимы" : "Available Modes";
		PanelOfModes[1].text = YG2.saves.langRu ? "Тотальная Зачистка" : "Total Cleaning";
		PanelOfModes[2].text = YG2.saves.langRu ? "Задача поглотить абсолютно все объекты на карте на 100%" : 
		"The task is to absorb absolutely all objects on the map by 100%";
		PanelOfModes[3].text = YG2.saves.langRu ? "Босс Туалетов" : "The Toilet Boss";
		PanelOfModes[4].text = YG2.saves.langRu ? "Задача перегнать Босса по уровню и победить поглотив его" : 
		"The task is to overtake the Boss by level and defeat him by absorbing him";
		PanelOfModes[5].text = YG2.saves.langRu ? "Охота" : "Hunting";
		PanelOfModes[6].text = YG2.saves.langRu ? "Появляется 6 врагов-туалетов. Твоя задача - поглотить всех" : 
		"6 toilet enemies appear. Your task is to consume everyone";
		PanelOfModes[7].text = YG2.saves.langRu ? "Командный" : "Teamwork";
		PanelOfModes[8].text = YG2.saves.langRu ? "3 Красных Vs 3 Синих. Задача задавить вражескую команду" : 
		"3 Red Vs 3 Blue. The task is to crush the enemy team";
		PanelOfModes[9].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";
		PanelOfModes[10].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";
		PanelOfModes[11].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";

		PanelOfProgress[0].text = YG2.saves.langRu ? "Прогресс" : "Progress";
		PanelOfProgress[1].text = YG2.saves.langRu ? "Белый\nДруг" : "White\nFriend";
		PanelOfProgress[2].text = YG2.saves.langRu ? "Золотой\nунитаз" : "Golden\nbowl";
		PanelOfProgress[3].text = YG2.saves.langRu ? "Трон\nКощея" : "Scrag's\nThrone";
		PanelOfProgress[4].text = YG2.saves.langRu ? "Туалет\nБога" : "God's\nToilet";

		PanelOfValute[0].text = YG2.saves.langRu ? "Магазин Валюты" : "Currency Store";
		PanelOfValute[1].text = YG2.saves.langRu ? "Баланс:" : "Balance:";
		PanelOfValute[2].text = YG2.saves.langRu ? "пара\nбриллиантов" : "couple\ndiamonds";
		PanelOfValute[3].text = YG2.saves.langRu ? "Горсть\nбриллиантов" : "Bunch\ndiamonds";
		PanelOfValute[4].text = YG2.saves.langRu ? "Мешок\nбриллиантов" : "Bag\ndiamonds";
		PanelOfValute[5].text = YG2.saves.langRu ? "Бочка\nбриллиантов" : "Barrel\ndiamonds";
		PanelOfValute[6].text = YG2.saves.langRu ? "Сундук\nбриллиантов" : "Chest\ndiamonds";
		PanelOfValute[7].text = YG2.saves.langRu ? "Обменять" : "Exchange";


		//--------------------------------------------------------------------------------------------------------------------------


		DMainMenu[0].text = YG2.saves.langRu ? "Туалетная Дыра.ио" : "Toilet Hole";
		DMainMenu[1].text = YG2.saves.langRu ? "Введите ваш ник" : "Enter your nickname";
		DMainMenu[2].text = YG2.saves.langRu ? "Уровень" : "Level";
		DMainMenu[3].text = YG2.saves.langRu ? "Магазин\nСкинов" : "Skin\nStore";
		DMainMenu[4].text = YG2.saves.langRu ? "КАРТЫ" : "MAPS";
		DMainMenu[5].text = YG2.saves.langRu ? "РЕЖИМЫ" : "MODES";
		DMainMenu[6].text = YG2.saves.langRu ? "ЛИДЕРЫ" : "LEADERS";
		DMainMenu[7].text = YG2.saves.langRu ? "НАСТРОЙКИ" : "SETTINGS";
		DMainMenu[8].text = YG2.saves.langRu ? "ИГРАТЬ" : "PLAY";

		DPanelOfSkins[0].text = YG2.saves.langRu ? "Описание" : "Description";
		DPanelOfSkins[1].text = YG2.saves.langRu ? "Белый\nДруг" : "White\nFriend";
		DPanelOfSkins[2].text = YG2.saves.langRu ? "Золотой\nунитаз" : "Golden\nbowl";
		DPanelOfSkins[3].text = YG2.saves.langRu ? "Трон\nКощея" : "Scrag's\nThrone";
		DPanelOfSkins[4].text = YG2.saves.langRu ? "Туалет\nБога" : "God's\nToilet";
		DPanelOfSkins[5].text = YG2.saves.langRu ? "Особенности" : "Features";

		DPanelOfLeaders.text = YG2.saves.langRu ? "Легенды" : "Legends";

		DesktopPanelOfSettings[0].text = YG2.saves.langRu ? "Настройки" : "Settings";
		DesktopPanelOfSettings[1].text = YG2.saves.langRu ? "Язык" : "Language";
		DesktopPanelOfSettings[2].text = YG2.saves.langRu ? "Звуки" : "Sounds";
		DesktopPanelOfSettings[3].text = YG2.saves.langRu ? "Музыка" : "Music";

		DPanelOfMaps[0].text = YG2.saves.langRu ? "Доступные Локации" : "Available Locations";
		DPanelOfMaps[1].text = YG2.saves.langRu ? "Городской Вайб" : "City Vibe";
		DPanelOfMaps[2].text = YG2.saves.langRu ? "Садовый Парк" : "Garden Park";
		DPanelOfMaps[3].text = YG2.saves.langRu ? "Новые карты скоро..." : "New maps are coming soon...";

		DPanelOfModes[0].text = YG2.saves.langRu ? "Доступные Режимы" : "Available Modes";
		DPanelOfModes[1].text = YG2.saves.langRu ? "Тотальная Зачистка" : "Total Cleaning";
		DPanelOfModes[2].text = YG2.saves.langRu ? "Задача поглотить абсолютно все объекты на карте на 100%" : 
		"The task is to absorb absolutely all objects on the map by 100%";
		DPanelOfModes[3].text = YG2.saves.langRu ? "Босс Туалетов" : "The Toilet Boss";
		DPanelOfModes[4].text = YG2.saves.langRu ? "Задача перегнать Босса по уровню и победить поглотив его" : 
		"The task is to overtake the Boss by level and defeat him by absorbing him";
		DPanelOfModes[5].text = YG2.saves.langRu ? "Охота" : "Hunting";
		DPanelOfModes[6].text = YG2.saves.langRu ? "Появляется 6 врагов-туалетов. Твоя задача - поглотить всех" : 
		"6 toilet enemies appear. Your task is to consume everyone";
		DPanelOfModes[7].text = YG2.saves.langRu ? "Командный" : "Teamwork";
		DPanelOfModes[8].text = YG2.saves.langRu ? "3 Красных Vs 3 Синих. Задача задавить вражескую команду" : 
		"3 Red Vs 3 Blue. The task is to crush the enemy team";
		DPanelOfModes[9].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";
		DPanelOfModes[10].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";
		DPanelOfModes[11].text = YG2.saves.langRu ? "Скоро в игре" : "Coming soon in the game";

		DPanelOfProgress[0].text = YG2.saves.langRu ? "Прогресс" : "Progress";
		DPanelOfProgress[1].text = YG2.saves.langRu ? "Белый\nДруг" : "White\nFriend";
		DPanelOfProgress[2].text = YG2.saves.langRu ? "Золотой\nунитаз" : "Golden\nbowl";
		DPanelOfProgress[3].text = YG2.saves.langRu ? "Трон\nКощея" : "Scrag's\nThrone";
		DPanelOfProgress[4].text = YG2.saves.langRu ? "Туалет\nБога" : "God's\nToilet";

		DPanelOfValute[0].text = YG2.saves.langRu ? "Магазин Валюты" : "Currency Store";
		DPanelOfValute[1].text = YG2.saves.langRu ? "Баланс:" : "Balance:";
		DPanelOfValute[2].text = YG2.saves.langRu ? "пара\nбриллиантов" : "couple\ndiamonds";
		DPanelOfValute[3].text = YG2.saves.langRu ? "Горсть\nбриллиантов" : "Bunch\ndiamonds";
		DPanelOfValute[4].text = YG2.saves.langRu ? "Мешок\nбриллиантов" : "Bag\ndiamonds";
		DPanelOfValute[5].text = YG2.saves.langRu ? "Бочка\nбриллиантов" : "Barrel\ndiamonds";
		DPanelOfValute[6].text = YG2.saves.langRu ? "Сундук\nбриллиантов" : "Chest\ndiamonds";
		DPanelOfValute[7].text = YG2.saves.langRu ? "Обменять" : "Exchange";

	}

}
