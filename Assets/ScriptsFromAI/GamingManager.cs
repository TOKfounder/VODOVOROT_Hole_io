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
	[SerializeField] private float endPanelYBoost = 120f;
	private bool endPanelRaised;
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
	[SerializeField] private float bossOvertimeDuration = 20f;

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

	[SerializeField] private float boostLeftShift = 170f;

	private bool timerGo;
	private bool matchClockFrozen;
	private bool once;
	private bool rewardApplied;
	private bool endSequenceStarted;
	private bool isTotalCleaningMode;
	private bool isHuntingMode;
	private bool isTeamMode;
	private bool bossDefeated;
	private bool bossTimeoutWin;
	private bool huntingComplete;
	private bool teamVictory;
	private bool playerEliminated;
	private bool bossOvertimeArmed;
	private float bossEndTime;
	private int progressScore;

	public bool TeamDraw { get; private set; }
	public bool BossDraw { get; private set; }
	public bool HasEnded => endSequenceStarted || endPanelRaised;
	public bool BossTimeoutWin => bossTimeoutWin;
	public int ProgressScore => progressScore;

	public bool HasRewardBeenApplied => rewardApplied;
	public bool BossDefeated => bossDefeated;
	public bool HuntingComplete => huntingComplete;
	public bool TeamVictory => teamVictory;
	public bool PlayerEliminated => playerEliminated;
	public bool IsTotalCleaningMode => isTotalCleaningMode;
	public float RemainingTime => Mathf.Max(0f, totalCleaningDuration - timer);
	public float RemainingBossTime => Mathf.Max(0f, bossEndTime - timer);
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
		AllValues = 0;
		progressScore = 0;
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
		endPanelRaised = false;
		bossDefeated = false;
		bossTimeoutWin = false;
		BossDraw = false;
		huntingComplete = false;
		teamVictory = false;
		playerEliminated = false;
		TeamDraw = false;
		bossOvertimeArmed = false;
		bossEndTime = bossModeDuration;
		progressScore = 0;
		AllValues = 0;
		isTotalCleaningMode = ModeManager.currentMode == ModeManager.Mode.TotalCleaning;
		isHuntingMode = ModeManager.currentMode == ModeManager.Mode.Hunting;
		isTeamMode = ModeManager.currentMode == ModeManager.Mode.TeamMode;

		MatchPause.ForceReset();
		SetMatchClockFrozen(false);
		ActiveCanvas.ApplyUiFontEverywhere();
		ScorePopupZone.EnsureZone(ActiveCanvas.Get());
		MatchHud.Ensure();
		SettingsPauseHook.IgnoreEnable = true;
		HookSettingsPause();
		SettingsPauseHook.IgnoreEnable = false;

		bool showCaptureBar = isTotalCleaningMode;
		if (Mflazhok != null)
			Mflazhok.gameObject.SetActive(showCaptureBar);
		if (Dflazhok != null)
			Dflazhok.gameObject.SetActive(showCaptureBar);
		if (Mpercent != null)
			Mpercent.gameObject.SetActive(showCaptureBar);
		if (Dpercent != null)
			Dpercent.gameObject.SetActive(showCaptureBar);

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
		if (!YG2.nowAdsShow)
			YG2.GameplayStart();
		StartCoroutine(UpdateFlag());
		NudgeBoostButton();
		TutorialController.EnsureMatch();
	}

	private void NudgeBoostButton()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		BoostButton[] buttons = canvas.GetComponentsInChildren<BoostButton>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] == null || !buttons[i].gameObject.activeInHierarchy)
				continue;
			RectTransform rect = buttons[i].GetComponent<RectTransform>();
			if (rect == null)
				continue;
			Vector2 pos = rect.anchoredPosition;
			pos.x -= boostLeftShift;
			rect.anchoredPosition = pos;
			return;
		}
	}

	private void HookSettingsPause()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform t = transforms[i];
			if (t == null || t.name != "Settings")
				continue;
			if (t.GetComponent<SettingsPauseHook>() == null)
				t.gameObject.AddComponent<SettingsPauseHook>();
		}
	}

	public void AddProgressScore(int amount)
	{
		if (amount <= 0)
			return;
		progressScore += amount;
	}

	public void HandleTimer(bool b) => timerGo = b;

	public void SetMatchClockFrozen(bool frozen)
	{
		matchClockFrozen = frozen;
	}

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
		if (timerGo && !matchClockFrozen)
			timer += Time.fixedDeltaTime;

		UpdateBossClock();

		bool shouldEnd = once && (
			(isTotalCleaningMode && (
				GetCapturePercent() >= 1f
				|| timer >= totalCleaningDuration - 0.01f))
			|| (IsBossMode && timer >= bossEndTime - 0.01f)
			|| (isHuntingMode && timer >= huntingModeDuration - 0.01f)
			|| (isTeamMode && timer >= teamModeDuration - 0.01f)
		);

		if (shouldEnd)
		{
			if (isTeamMode)
				ResolveTeamTimeout();
			if (IsBossMode && !bossDefeated)
				ResolveBossTimeout();
			once = false;
			ShowEndPanel();
		}

		float fill = Mathf.Clamp01(perc);
		if (isTotalCleaningMode)
		{
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
		if (playerEliminated)
			return;

		playerEliminated = true;
		once = false;
		ShowEndPanel();
	}

	private void ResolveTeamTimeout()
	{
		if (teamVictory || playerEliminated)
			return;

		int blue = ModeManager.GetTeamScore(ModeManager.TeamBlue);
		int red = ModeManager.GetTeamScore(ModeManager.TeamRed);
		if (blue > red)
			teamVictory = true;
		else if (blue == red)
			TeamDraw = true;
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
		MatchPause.ForceReset();
		SetMatchClockFrozen(false);
		HoleFeedback.ForPlayer?.SetMatchActive(false);
		MatchHud hud = FindAnyObjectByType<MatchHud>();
		if (hud != null)
			hud.SetVisible(false);

		RaiseEndPanel(MobpanelOfEnd);
		RaiseEndPanel(DeskpanelOfEnd);
		endPanelRaised = true;

		if (YG2.envir.isMobile)
			MobpanelOfEnd?.SetActive(true);
		else
			DeskpanelOfEnd?.SetActive(true);
	}

	private void RaiseEndPanel(GameObject panel)
	{
		if (endPanelRaised || panel == null)
			return;

		RectTransform rect = panel.GetComponent<RectTransform>();
		if (rect == null)
			return;

		Vector2 pos = rect.anchoredPosition;
		pos.y += endPanelYBoost;
		rect.anchoredPosition = pos;
	}

	private static int GetPlayerScore()
	{
		return BlackHoleController.Player != null
			? BlackHoleController.Player.score
			: HoleParent.totalScore;
	}

	private void UpdateBossClock()
	{
		if (!IsBossMode || bossDefeated || bossOvertimeArmed || timer < bossModeDuration - 0.01f)
			return;

		bossOvertimeArmed = true;
		if (CanStartBossOvertime())
			bossEndTime = bossModeDuration + bossOvertimeDuration;
		else
			bossEndTime = timer;
	}

	private bool CanStartBossOvertime()
	{
		HoleParent player = BlackHoleController.Player;
		EnemyController boss = ModeManager.ActiveBoss;
		if (player == null || player.IsConsumed || boss == null || boss.IsConsumed)
			return false;
		return player.currentLevel >= boss.currentLevel;
	}

	private void ResolveBossTimeout()
	{
		if (bossDefeated || bossTimeoutWin || BossDraw)
			return;

		int playerScore = GetPlayerScore();
		int bossScore = ModeManager.ActiveBoss != null && !ModeManager.ActiveBoss.IsConsumed
			? ModeManager.ActiveBoss.score
			: 0;
		if (playerScore > bossScore)
			bossTimeoutWin = true;
		else if (playerScore == bossScore)
			BossDraw = true;
	}

	public float GetCapturePercent()
	{
		if (AllValues <= 0)
			return 0f;
		if (progressScore <= 0)
			return 0f;
		return Mathf.Clamp01((float)progressScore / AllValues);
	}

	public MatchRewardData GetClassicReward(float progress)
	{
		progress = Mathf.Clamp01(progress);

		if (isTotalCleaningMode)
			return GetTotalCleaningReward(progress);

		if (isHuntingMode)
			return GetHuntingReward();

		if (isTeamMode)
		{
			if (playerEliminated)
				return GetPartialDefeatReward(0f);
			if (teamVictory)
				return GetTotalCleaningReward(1f);
			if (TeamDraw)
				return GetPartialDefeatReward(0.5f);
			return GetPartialDefeatReward(GetMatchProgress());
		}

		if (IsBossMode)
		{
			if (playerEliminated)
				return GetPartialDefeatReward(0f);
			if (bossDefeated || bossTimeoutWin)
				return GetBossWinReward();
			if (BossDraw)
				return GetPartialDefeatReward(0.5f);
			return GetPartialDefeatReward(0f);
		}

		return GetPartialDefeatReward(progress);
	}

	private MatchRewardData GetBossWinReward()
	{
		int leftover = Mathf.RoundToInt(RemainingBossTime / 20f);
		return new MatchRewardData
		{
			exp = 50,
			coins = 25 + leftover,
			diamonds = 5,
			resultSpriteIndex = leftover >= 8 ? 0 : leftover >= 3 ? 1 : 2
		};
	}

	private MatchRewardData GetHuntingReward()
	{
		if (playerEliminated)
			return GetPartialDefeatReward(0f);

		int spawned = Mathf.Max(1, ModeManager.HuntingSpawned);
		int killed = Mathf.Clamp(spawned - ModeManager.RemainingHunters, 0, spawned);
		if (huntingComplete || killed >= spawned)
		{
			return new MatchRewardData
			{
				exp = 60,
				coins = 40,
				diamonds = 8,
				resultSpriteIndex = 0
			};
		}

		float progress = killed / (float)spawned;
		return new MatchRewardData
		{
			exp = Mathf.Max(8, Mathf.RoundToInt(50f * progress)),
			coins = Mathf.Max(5, Mathf.RoundToInt(30f * progress)),
			diamonds = Mathf.Max(1, Mathf.RoundToInt(6f * progress)),
			resultSpriteIndex = progress >= 0.7f ? 1 : -1
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
		float progress = 0f;
		if (isTotalCleaningMode)
			progress = GetCapturePercent();
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			if (bossDefeated || bossTimeoutWin)
				progress = 1f;
			else if (BossDraw)
				progress = 0.5f;
		}
		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
		{
			if (huntingComplete)
				progress = 1f;
			else if (ModeManager.HuntingSpawned > 0)
				progress = 1f - ModeManager.RemainingHunters / (float)ModeManager.HuntingSpawned;
		}
		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			if (playerEliminated)
				progress = 0f;
			else if (teamVictory)
				progress = 1f;
			else
			{
				int blue = ModeManager.GetTeamScore(ModeManager.TeamBlue);
				int red = ModeManager.GetTeamScore(ModeManager.TeamRed);
				int total = blue + red;
				progress = total > 0 ? blue / (float)total : 0.5f;
			}
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
		if (!YG2.nowAdsShow)
			YG2.GameplayStop();
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
			BoostText.text = GameTexts.SpeedBoost;
		if (DBoostText != null)
			DBoostText.text = GameTexts.SpeedBoost;

		SetSettingsTexts(MobilePanelOfSettings);
		SetSettingsTexts(DesktopPanelOfSettings);
		SetEndPanelTexts(PanelOfEnd);
		SetEndPanelTexts(DPanelOfEnd);
	}

	private void SetSettingsTexts(Text[] panel)
	{
		if (panel == null || panel.Length < 5) return;
		if (panel[0] != null) panel[0].text = GameTexts.Settings;
		if (panel[1] != null) panel[1].text = GameTexts.Language;
		if (panel[2] != null) panel[2].text = GameTexts.Sounds;
		if (panel[3] != null) panel[3].text = GameTexts.Music;
		if (panel[4] != null) panel[4].text = GameTexts.EndTheGame;
	}

	private void SetEndPanelTexts(Text[] panel)
	{
		if (panel == null || panel.Length < 6) return;
		if (panel[0] != null) panel[0].text = GameTexts.Experience;
		if (panel[1] != null) panel[1].text = GameTexts.Result;
		if (panel[2] != null) panel[2].text = GameTexts.Coins;
		if (panel[3] != null) panel[3].text = GameTexts.Diamonds;
		if (panel[4] != null) panel[4].text = GameTexts.Continue;
		if (panel[5] != null) panel[5].text = GameTexts.X3Coins;
	}

	public void GetEndVerdict(out string title, out string reason, out Color color)
	{
		title = GameTexts.Defeat;
		reason = "";
		color = new Color(1f, 0.35f, 0.3f, 1f);

		if (playerEliminated)
		{
			reason = GameTexts.ReasonEaten;
			return;
		}

		if (isTotalCleaningMode)
		{
			int percent = Mathf.RoundToInt(GetCapturePercent() * 100f);
			if (percent >= 100)
			{
				title = GameTexts.Victory;
				reason = GameTexts.ReasonCleaningDone;
				color = new Color(0.35f, 0.9f, 0.4f, 1f);
			}
			else
			{
				reason = GameTexts.ReasonCleaningTimeout(percent);
			}
			return;
		}

		if (IsBossMode)
		{
			if (bossDefeated)
			{
				title = GameTexts.Victory;
				reason = GameTexts.ReasonBossAbsorbed;
				color = new Color(0.35f, 0.9f, 0.4f, 1f);
			}
			else if (bossTimeoutWin)
			{
				title = GameTexts.Victory;
				reason = GameTexts.ReasonBiggerWin;
				color = new Color(0.35f, 0.9f, 0.4f, 1f);
			}
			else if (BossDraw)
			{
				title = GameTexts.Draw;
				reason = GameTexts.ReasonBossDraw;
				color = new Color(1f, 0.85f, 0.3f, 1f);
			}
			else
			{
				reason = GameTexts.ReasonBiggerLoss;
			}
			return;
		}

		if (isHuntingMode)
		{
			int spawned = Mathf.Max(0, ModeManager.HuntingSpawned);
			int killed = Mathf.Clamp(spawned - ModeManager.RemainingHunters, 0, spawned);
			if (huntingComplete || (spawned > 0 && killed >= spawned))
			{
				title = GameTexts.Victory;
				reason = GameTexts.ReasonHuntingJackpot;
				color = new Color(0.35f, 0.9f, 0.4f, 1f);
			}
			else if (killed > 0)
			{
				title = GameTexts.Result;
				reason = GameTexts.ReasonHuntingKills(killed, spawned);
				color = new Color(1f, 0.85f, 0.3f, 1f);
			}
			else
			{
				reason = GameTexts.ReasonHuntingKills(killed, spawned);
			}
			return;
		}

		if (isTeamMode)
		{
			if (teamVictory)
			{
				title = GameTexts.Victory;
				reason = ModeManager.RemainingTeamEnemies == 0
					? GameTexts.ReasonEnemiesCleared
					: GameTexts.ReasonTeamWin;
				color = new Color(0.35f, 0.9f, 0.4f, 1f);
			}
			else if (TeamDraw)
			{
				title = GameTexts.Draw;
				reason = GameTexts.ReasonTeamDraw;
				color = new Color(1f, 0.85f, 0.3f, 1f);
			}
			else
			{
				reason = GameTexts.ReasonTeamLose;
			}
		}
	}
}
