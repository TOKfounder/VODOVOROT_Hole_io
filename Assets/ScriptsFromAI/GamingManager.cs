using UnityEngine;
using UnityEngine.UI;
using YG;

public class GamingManager : MonoBehaviour
{
	public static GamingManager Instance;
	public GameObject MobpanelOfEnd;
	public GameObject DeskpanelOfEnd;
	public float perc = 0f;
	public float minX;
	public float maxX;
	public float minZ;
	public float maxZ;
	public GameObject[] walls;

	public float timer;
	public int AllValues;
	public Image Mflazhok;
	public Image Dflazhok;
	public Text Mpercent;
	public Text Dpercent;
	public int score;

[Header("Mobile UI")]
	public Text BoostText;
	public Text[] MobilePanelOfSettings;
	public Text[] PanelOfEnd;
[Header("Desktop UI")]
	public Text DBoostText;
	public Text[] DesktopPanelOfSettings;
	public Text[] DPanelOfEnd;

	private bool timerGo;
	private bool once;

	void Awake()
	{
		Instance = this;
		maxX = walls[0].GetComponent<Collider>().bounds.min.x;
		minX = walls[1].GetComponent<Collider>().bounds.max.x;
		minZ = walls[2].GetComponent<Collider>().bounds.max.z;
		maxZ = walls[3].GetComponent<Collider>().bounds.min.z;
	}

	void Start()
	{
		foreach (var renderer in FindObjectsOfType<MeshRenderer>()) {
			renderer.receiveShadows = false;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}
		once = true;
		YG2.saves.isGaming = true;
		Time.timeScale = 1f;
		YG2.saves.score = 0;
		YG2.SaveProgress();
		timer = 0f;
		timerGo = true;
		// UpdateUI();
		// if (!YG2.saves.langRu)
		// LanguageManager.Instance.Onclick();
	}

	public void HandleTimer(bool b) => timerGo = b;

	void FixedUpdate()
	{
		score = YG2.saves.score;
		perc = (float)YG2.saves.score / (AllValues - 20);
		if (once && (int)(perc * 100) >= 100)
		{
			if (YG2.envir.isMobile)
				MobpanelOfEnd.SetActive(true);
			else
				DeskpanelOfEnd.SetActive(true);
		}
		if (YG2.envir.isMobile)
		{
			Mflazhok.fillAmount = perc;
			Mpercent.text = $"{(int)(perc * 100)}%";
		} else {
			Dflazhok.fillAmount = perc;
			Dpercent.text = $"{(int)(perc * 100)}%";
		}
		if (timerGo)
			timer += Time.fixedDeltaTime;
	}

	public void EndOfGame()
	{
		timerGo = false;
		once = false;
		Time.timeScale = 0;
		Invoke(nameof(timeScalingBitLater), 7f);
	}

	void timeScalingBitLater()
	{
		Time.timeScale = 0f;
	} 

	public void UpdateUI()
	{
		BoostText.text = YG2.saves.langRu ? "Буст Скорости" : "Speed Boost";
		MobilePanelOfSettings[0].text = YG2.saves.langRu ? "Настройки" : "Settings";
		MobilePanelOfSettings[1].text = YG2.saves.langRu ? "Язык" : "Language";
		MobilePanelOfSettings[2].text = YG2.saves.langRu ? "Звуки" : "Sounds";
		MobilePanelOfSettings[3].text = YG2.saves.langRu ? "Музыка" : "Music";
		MobilePanelOfSettings[4].text = YG2.saves.langRu ? "Завершить игру" : "End the game";

		DBoostText.text = YG2.saves.langRu ? "Буст Скорости" : "Speed Boost";
		PanelOfEnd[0].text = YG2.saves.langRu ? "Опыт:" : "Experience:";
		PanelOfEnd[1].text = YG2.saves.langRu ? "Итог" : "Result";
		PanelOfEnd[2].text = YG2.saves.langRu ? "Монеты:" : "Coins:";
		PanelOfEnd[3].text = YG2.saves.langRu ? "Бриллианты:" : "Brilliants:";
		PanelOfEnd[4].text = YG2.saves.langRu ? "Продолжить" : "Continue";
		PanelOfEnd[5].text = YG2.saves.langRu ? "x3 Монеты\n(короткая реклама)" : "x3 Coins\n(short ad)";


		//----------------------------------------------------------------------------------------------------------------------------

		DesktopPanelOfSettings[0].text = YG2.saves.langRu ? "Настройки" : "Settings";
		DesktopPanelOfSettings[1].text = YG2.saves.langRu ? "Язык" : "Language";
		DesktopPanelOfSettings[2].text = YG2.saves.langRu ? "Звуки" : "Sounds";
		DesktopPanelOfSettings[3].text = YG2.saves.langRu ? "Музыка" : "Music";
		DesktopPanelOfSettings[4].text = YG2.saves.langRu ? "Завершить игру" : "End the game";

		DPanelOfEnd[0].text = YG2.saves.langRu ? "Опыт:" : "Experience:";
		DPanelOfEnd[1].text = YG2.saves.langRu ? "Итог" : "Result";
		DPanelOfEnd[2].text = YG2.saves.langRu ? "Монеты:" : "Coins:";
		DPanelOfEnd[3].text = YG2.saves.langRu ? "Бриллианты:" : "Brilliants:";
		DPanelOfEnd[4].text = YG2.saves.langRu ? "Продолжить" : "Continue";
		DPanelOfEnd[5].text = YG2.saves.langRu ? "x3 Монеты\n(короткая реклама)" : "x3 Coins\n(short ad)";
	}
}
