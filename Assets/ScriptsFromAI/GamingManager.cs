using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using YG;

[DefaultExecutionOrder(-100)]
public class GamingManager : MonoBehaviour
{
	public static GamingManager Instance;
	public static List<Collider> allPlatforms = new List<Collider>();

	public struct MatchRewardData
	{
		public int exp;
		public int coins;
		public int diamonds;
		public int resultSpriteIndex; // -1 = без спрайта (неполное прохождение)
	}

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

	[Header("Total Cleaning")]
	public float totalCleaningDuration = 180f;

	[Header("Boss Mode")]
	[SerializeField] private float bossModeDuration = 300f;

	public Text totalCleaningTimerText;

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
	private bool rewardApplied;
	private bool endSequenceStarted;
	private bool isTotalCleaningMode;
	private bool bossDefeated;

	public bool HasRewardBeenApplied => rewardApplied;
	public bool BossDefeated => bossDefeated;
	public bool IsTotalCleaningMode => isTotalCleaningMode;
	public float RemainingTime => Mathf.Max(0f, totalCleaningDuration - timer);
	public float RemainingBossTime => Mathf.Max(0f, bossModeDuration - timer);

	private bool IsBossMode => ModeManager.currentMode == ModeManager.Mode.Boss;

	void Awake()
	{
		Instance = this;
		ResetMatchState();

		if (walls != null && walls.Length >= 4)
		{
			maxX = walls[0].GetComponent<Collider>().bounds.min.x;
			minX = walls[1].GetComponent<Collider>().bounds.max.x;
			minZ = walls[2].GetComponent<Collider>().bounds.max.z;
			maxZ = walls[3].GetComponent<Collider>().bounds.min.z;
		}
	}

	public static void ResetMatchState()
	{
		allPlatforms.Clear();
		HoleParent.ResetStaticMatchState();
		EnemyController.count = 0;
		ModeManager.ResetModeState();
	}

	void Start()
	{
		// WebGL: отключаем тени глобально вместо обхода всех MeshRenderer на огромной карте
		QualitySettings.shadows = ShadowQuality.Disable;

		once = true;
		rewardApplied = false;
		endSequenceStarted = false;
		bossDefeated = false;
		isTotalCleaningMode = ModeManager.currentMode == ModeManager.Mode.TotalCleaning;

		ResolveModeTimerText();
		bool showModeTimer = isTotalCleaningMode || IsBossMode;
		if (totalCleaningTimerText != null)
		{
			totalCleaningTimerText.gameObject.SetActive(showModeTimer);
			if (showModeTimer)
				totalCleaningTimerText.text = FormatTime(GetModeRemainingTime());
		}

		YG2.saves.isGaming = true;
		Time.timeScale = 1f;
		YG2.SaveProgress();
		timer = 0f;
		timerGo = true;
		StartCoroutine(UpdateFlag());
	}

	public void HandleTimer(bool b) => timerGo = b;

	IEnumerator UpdateFlag()
	{
		while (true)
		{
			YG2.saves.score = GetPlayerScore();
			perc = GetCapturePercent();
			yield return new WaitForSeconds(0.25f);
		}
	}

	void FixedUpdate()
	{
		if (timerGo)
			timer += Time.fixedDeltaTime;

		bool shouldEnd = once && (
			(isTotalCleaningMode && (
				GetCapturePercent() >= 1f
				|| timer >= totalCleaningDuration - 0.01f))
			|| (IsBossMode && timer >= bossModeDuration - 0.01f)
		);

		if (shouldEnd)
		{
			once = false;
			ShowEndPanel();
		}

		float fill = Mathf.Clamp01(perc);
		string percentText = $"{(int)(fill * 100)}%";
		if (YG2.envir.isMobile)
		{
			if (Mflazhok != null) Mflazhok.fillAmount = fill;
			if (Mpercent != null) Mpercent.text = percentText;
		}
		else
		{
			if (Dflazhok != null) Dflazhok.fillAmount = fill;
			if (Dpercent != null) Dpercent.text = percentText;
		}

		if (isTotalCleaningMode || IsBossMode)
		{
			ResolveModeTimerText();
			if (totalCleaningTimerText != null)
				totalCleaningTimerText.text = FormatTime(GetModeRemainingTime());
		}
	}

	private float GetModeRemainingTime()
	{
		if (isTotalCleaningMode)
			return RemainingTime;
		if (IsBossMode)
			return RemainingBossTime;
		return 0f;
	}

	private const string ModeTimerName = "TimerText";

	private void ResolveModeTimerText()
	{
		if (totalCleaningTimerText != null)
			return;

		Canvas canvas = GameController.Instance != null
			? GameController.Instance.currentCanvas
			: FindAnyObjectByType<Canvas>();
		if (canvas == null)
			return;

		Text[] texts = canvas.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < texts.Length; i++)
		{
			if (texts[i] != null && texts[i].name == ModeTimerName)
			{
				totalCleaningTimerText = texts[i];
				break;
			}
		}

		if (totalCleaningTimerText != null || (!isTotalCleaningMode && !IsBossMode))
			return;

		GameObject go = new GameObject(ModeTimerName, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(canvas.transform, false);
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.5f, 1f);
		rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, -40f);
		rt.sizeDelta = new Vector2(240f, 60f);
		totalCleaningTimerText = go.GetComponent<Text>();
		totalCleaningTimerText.alignment = TextAnchor.MiddleCenter;
		totalCleaningTimerText.fontSize = 36;
		totalCleaningTimerText.color = Color.white;
		totalCleaningTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (totalCleaningTimerText.font == null)
			totalCleaningTimerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
	}

	public void OnBossDefeated()
	{
		if (bossDefeated || ModeManager.currentMode != ModeManager.Mode.Boss)
			return;

		bossDefeated = true;
		once = false;
		ShowEndPanel();
	}

	private void ShowEndPanel()
	{
		if (YG2.envir.isMobile)
			MobpanelOfEnd?.SetActive(true);
		else
			DeskpanelOfEnd?.SetActive(true);
	}

	private static int GetPlayerScore()
	{
		return BlackHoleController.Player != null
			? BlackHoleController.Player.score
			: HoleParent.totalScore;
	}

	public float GetCapturePercent()
	{
		// Балансный запас: ~15 очков «недостижимого» хвоста, чтобы 100% было достижимо раньше last object
		const int ProgressSlack = 15;
		if (AllValues <= ProgressSlack)
			return 0f;

		int score = GetPlayerScore();
		if (score <= 0)
			score = YG2.saves.score;
		return Mathf.Clamp01((float)score / (AllValues - ProgressSlack));
	}

	public MatchRewardData GetClassicReward(float progress)
	{
		progress = Mathf.Clamp01(progress);

		if (isTotalCleaningMode)
			return GetTotalCleaningReward(progress);

		if (progress < 1f)
		{
			return new MatchRewardData
			{
				exp = (int)(50 * progress),
				coins = (int)(13 * progress),
				diamonds = (int)(4 * progress),
				resultSpriteIndex = -1
			};
		}

		if (timer <= 360f)
		{
			return new MatchRewardData
			{
				exp = 50,
				coins = 30,
				diamonds = 5,
				resultSpriteIndex = 0
			};
		}

		if (timer <= 600f)
		{
			return new MatchRewardData
			{
				exp = 50,
				coins = 20,
				diamonds = 4,
				resultSpriteIndex = 1
			};
		}

		return new MatchRewardData
		{
			exp = 50,
			coins = 15,
			diamonds = 3,
			resultSpriteIndex = 2
		};
	}

	public MatchRewardData GetTotalCleaningReward(float progress)
	{
		progress = Mathf.Clamp01(progress);

		if (progress < 0.5f)
		{
			int coins = Mathf.RoundToInt(15f * (progress / 0.5f));
			int exp = Mathf.RoundToInt(25f * (progress / 0.5f));
			return new MatchRewardData
			{
				exp = exp,
				coins = coins,
				diamonds = coins / 5,
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

	public MatchRewardData GetCurrentClassicReward()
	{
		float progress = GetCapturePercent();
		if (ModeManager.currentMode == ModeManager.Mode.Boss && bossDefeated)
			progress = 1f;
		return GetClassicReward(progress);
	}

	public void ApplyMatchReward(MatchRewardData reward)
	{
		if (rewardApplied)
			return;

		YG2.saves.score = GetPlayerScore();
		YG2.saves.exp += reward.exp;
		YG2.saves.goldCoins += reward.coins;
		YG2.saves.diamonds += reward.diamonds;
		YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
		YG2.SaveProgress();
		rewardApplied = true;
	}

	public void EndOfGame()
	{
		if (endSequenceStarted)
			return;

		endSequenceStarted = true;
		timerGo = false;
		once = false;
		Time.timeScale = 0f;
	}

	private string FormatTime(float seconds)
	{
		int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
		int minutes = totalSeconds / 60;
		int secs = totalSeconds % 60;
		return $"{minutes:00}:{secs:00}";
	}

	public void UpdateUI()
	{
		if (BoostText != null)
			BoostText.text = YG2.saves.langRu ? "Буст Скорости" : "Speed Boost";
		if (DBoostText != null)
			DBoostText.text = YG2.saves.langRu ? "Буст Скорости" : "Speed Boost";

		SetSettingsTexts(MobilePanelOfSettings);
		SetSettingsTexts(DesktopPanelOfSettings);
		SetEndPanelTexts(PanelOfEnd);
		SetEndPanelTexts(DPanelOfEnd);
	}

	private void SetSettingsTexts(Text[] panel)
	{
		if (panel == null || panel.Length < 5) return;
		bool ru = YG2.saves.langRu;
		if (panel[0] != null) panel[0].text = ru ? "Настройки" : "Settings";
		if (panel[1] != null) panel[1].text = ru ? "Язык" : "Language";
		if (panel[2] != null) panel[2].text = ru ? "Звуки" : "Sounds";
		if (panel[3] != null) panel[3].text = ru ? "Музыка" : "Music";
		if (panel[4] != null) panel[4].text = ru ? "Завершить игру" : "End the game";
	}

	private void SetEndPanelTexts(Text[] panel)
	{
		if (panel == null || panel.Length < 6) return;
		bool ru = YG2.saves.langRu;
		if (panel[0] != null) panel[0].text = ru ? "Опыт:" : "Experience:";
		if (panel[1] != null) panel[1].text = ru ? "Итог" : "Result";
		if (panel[2] != null) panel[2].text = ru ? "Монеты:" : "Coins:";
		if (panel[3] != null) panel[3].text = ru ? "Бриллианты:" : "Brilliants:";
		if (panel[4] != null) panel[4].text = ru ? "Продолжить" : "Continue";
		if (panel[5] != null) panel[5].text = ru ? "x3 Монеты\n(короткая реклама)" : "x3 Coins\n(short ad)";
	}
}
