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

	[Header("Hunting")]
	[SerializeField] private float huntingModeDuration = 180f;

	[Header("Team Mode")]
	[SerializeField] private float teamModeDuration = 180f;

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
	private bool isHuntingMode;
	private bool isTeamMode;
	private bool bossDefeated;
	private bool huntingComplete;
	private bool teamVictory;
	private bool playerEliminated;

	public bool HasRewardBeenApplied => rewardApplied;
	public bool BossDefeated => bossDefeated;
	public bool HuntingComplete => huntingComplete;
	public bool TeamVictory => teamVictory;
	public bool PlayerEliminated => playerEliminated;
	public bool IsTotalCleaningMode => isTotalCleaningMode;
	public float RemainingTime => Mathf.Max(0f, totalCleaningDuration - timer);
	public float RemainingBossTime => Mathf.Max(0f, bossModeDuration - timer);
	public float RemainingHuntingTime => Mathf.Max(0f, huntingModeDuration - timer);
	public float RemainingTeamTime => Mathf.Max(0f, teamModeDuration - timer);

	private bool IsBossMode => ModeManager.currentMode == ModeManager.Mode.Boss;
	private bool IsHuntingMode => ModeManager.currentMode == ModeManager.Mode.Hunting;
	private bool UsesModeTimer => isTotalCleaningMode || IsBossMode || isHuntingMode || isTeamMode;
	private Vector3 timerBaseScale = Vector3.one;
	private bool timerScaleCached;
	private bool timerTextResolved;

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
		huntingComplete = false;
		teamVictory = false;
		playerEliminated = false;
		isTotalCleaningMode = ModeManager.currentMode == ModeManager.Mode.TotalCleaning;
		isHuntingMode = ModeManager.currentMode == ModeManager.Mode.Hunting;
		isTeamMode = ModeManager.currentMode == ModeManager.Mode.TeamMode;

		ScorePopupZone.EnsureZone(ActiveCanvas.Get());
		if (IsBossMode || isHuntingMode || isTeamMode)
			MatchHud.Ensure();

		ResolveModeTimerText();
		bool showModeTimer = UsesModeTimer;
		if (totalCleaningTimerText != null)
		{
			totalCleaningTimerText.gameObject.SetActive(showModeTimer);
			if (showModeTimer)
			{
				float remaining = GetModeRemainingTime();
				totalCleaningTimerText.text = FormatTime(remaining);
				ApplyTimerUrgency(totalCleaningTimerText, remaining);
			}
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
			perc = GetMatchProgress();
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
			|| (isHuntingMode && timer >= huntingModeDuration - 0.01f)
			|| (isTeamMode && timer >= teamModeDuration - 0.01f)
		);

		if (shouldEnd)
		{
			if (isTeamMode)
				ResolveTeamTimeout();
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

		if (UsesModeTimer && totalCleaningTimerText != null)
		{
			float remaining = GetModeRemainingTime();
			totalCleaningTimerText.text = FormatTime(remaining);
			ApplyTimerUrgency(totalCleaningTimerText, remaining);
		}
	}

	private float GetModeRemainingTime()
	{
		if (isTotalCleaningMode)
			return RemainingTime;
		if (IsBossMode)
			return RemainingBossTime;
		if (isHuntingMode)
			return RemainingHuntingTime;
		if (isTeamMode)
			return RemainingTeamTime;
		return 0f;
	}

	private const string ModeTimerName = "TimerText";

	private void ResolveModeTimerText()
	{
		if (timerTextResolved && totalCleaningTimerText != null)
			return;

		MatchHud hud = FindAnyObjectByType<MatchHud>();
		if (hud != null && hud.TimerText != null)
		{
			if (totalCleaningTimerText != null && totalCleaningTimerText != hud.TimerText)
				totalCleaningTimerText.gameObject.SetActive(false);
			totalCleaningTimerText = hud.TimerText;
			HideDuplicateTimerTexts(totalCleaningTimerText);
			timerTextResolved = true;
			return;
		}

		if (totalCleaningTimerText != null)
		{
			timerTextResolved = true;
			return;
		}

		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		Transform parent = hud != null ? hud.transform : canvas.transform;
		Transform existing = parent.Find(ModeTimerName);
		if (existing != null)
		{
			totalCleaningTimerText = existing.GetComponent<Text>();
			if (totalCleaningTimerText != null)
			{
				timerTextResolved = true;
				return;
			}
		}

		if (!UsesModeTimer)
			return;

		totalCleaningTimerText = ActiveCanvas.CreateText(parent, ModeTimerName, new Vector2(0f, -36f), new Vector2(240f, 52f));
		if (totalCleaningTimerText != null)
		{
			totalCleaningTimerText.fontSize = 36;
			timerTextResolved = true;
		}
	}

	private static void HideDuplicateTimerTexts(Text keep)
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null || keep == null)
			return;

		Text[] texts = canvas.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < texts.Length; i++)
		{
			if (texts[i] == null || texts[i] == keep || texts[i].name != ModeTimerName)
				continue;
			texts[i].gameObject.SetActive(false);
		}
	}

	public void OnHuntingComplete()
	{
		if (huntingComplete || ModeManager.currentMode != ModeManager.Mode.Hunting)
			return;

		huntingComplete = true;
		once = false;
		ShowEndPanel();
	}

	public void OnTeamVictory()
	{
		if (teamVictory || playerEliminated || ModeManager.currentMode != ModeManager.Mode.TeamMode)
			return;

		teamVictory = true;
		once = false;
		ShowEndPanel();
	}

	public void OnPlayerEliminated()
	{
		if (playerEliminated || teamVictory || ModeManager.currentMode != ModeManager.Mode.TeamMode)
			return;

		playerEliminated = true;
		once = false;
		ShowEndPanel();
	}

	private void ResolveTeamTimeout()
	{
		if (teamVictory || playerEliminated)
			return;

		teamVictory = ModeManager.GetTeamScore(ModeManager.TeamBlue) > ModeManager.GetTeamScore(ModeManager.TeamRed);
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
		HoleFeedback.ForPlayer?.SetMatchActive(false);
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

		if (isHuntingMode)
			return GetTotalCleaningReward(progress);

		if (isTeamMode)
		{
			progress = GetMatchProgress();
			if (!teamVictory)
				return GetPartialDefeatReward(progress);
			return GetTotalCleaningReward(progress);
		}

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

	private MatchRewardData GetPartialDefeatReward(float progress)
	{
		progress = Mathf.Clamp01(progress);
		return new MatchRewardData
		{
			exp = Mathf.Max(5, (int)(25f * progress)),
			coins = Mathf.Max(3, (int)(12f * progress)),
			diamonds = Mathf.Max(0, (int)(3f * progress)),
			resultSpriteIndex = -1
		};
	}

	public MatchRewardData GetCurrentClassicReward()
	{
		return GetClassicReward(GetMatchProgress());
	}

	public float GetMatchProgress()
	{
		float progress = GetCapturePercent();
		if (ModeManager.currentMode == ModeManager.Mode.Boss && bossDefeated)
			progress = 1f;
		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
		{
			if (huntingComplete)
				progress = 1f;
			else if (ModeManager.HuntingSpawned > 0)
				progress = 1f - ModeManager.RemainingHunters / (float)ModeManager.HuntingSpawned;
		}
		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			if (teamVictory)
				progress = 1f;
			else if (playerEliminated || ModeManager.TeamEnemySpawned > 0)
				progress = 1f - ModeManager.RemainingTeamEnemies / (float)Mathf.Max(1, ModeManager.TeamEnemySpawned);
		}
		return Mathf.Clamp01(progress);
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
		HoleFeedback.ForPlayer?.SetMatchActive(false);
		Time.timeScale = 0f;
	}

	private string FormatTime(float seconds)
	{
		int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
		int minutes = totalSeconds / 60;
		int secs = totalSeconds % 60;
		return $"{minutes:00}:{secs:00}";
	}

	private void ApplyTimerUrgency(Text timer, float remaining)
	{
		if (timer == null)
			return;

		Color color = Color.white;
		float pulse = 1f;
		if (remaining <= 10f)
		{
			color = new Color(1f, 0.25f, 0.2f, 1f);
			pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
		}
		else if (remaining <= 30f)
		{
			color = new Color(1f, 0.55f, 0.15f, 1f);
		}

		timer.color = color;
		if (!timerScaleCached)
		{
			timerBaseScale = timer.transform.localScale;
			timerScaleCached = true;
		}
		timer.transform.localScale = timerBaseScale * pulse;
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
