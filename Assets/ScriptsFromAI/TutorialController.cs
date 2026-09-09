using System.Collections;
using PinePie.SimpleJoystick;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class TutorialController : MonoBehaviour
{
	public const int StageNick = 0;
	public const int StageModes = 1;
	public const int StageCleaning = 2;
	public const int StageModesReturn = 3;
	public const int StageMaps = 4;
	public const int StageCity = 5;
	public const int StagePlay = 6;
	public const int StageMatchCards = 7;
	public const int StageMatchPlay = 8;
	public const int StageEndContinue = 9;
	public const int StageCurrency = 10;
	public const int StageExchange = 11;
	public const int StageSkins = 12;
	public const int StageRotateWhite = 13;
	public const int StageBuyWhite = 14;
	public const int StageEquipWhite = 15;
	public const int StageDone = 16;

	private const int MatchCardCount = 4;
	private const int WhiteFriendIndex = 1;
	private const int WhiteFriendCoinCost = 20;
	private const float PointerOffset = 92f;
	private const float OverlayLocalZ = -110f;
	private static readonly Color PointerTint = new Color(1f, 0.85f, 0.2f, 1f);

	public static TutorialController Instance { get; private set; }
	public static bool ShowCleaningLandmarks { get; private set; }

	public static bool IsDone => YG2.saves.tutorialStage >= StageDone;

	public static bool IsLockingUi
	{
		get
		{
			int stage = YG2.saves.tutorialStage;
			return stage < StageDone && stage != StageMatchPlay;
		}
	}

	public static bool KeepModesPanelOpen
	{
		get
		{
			int stage = YG2.saves.tutorialStage;
			return stage == StageCleaning || stage == StageModesReturn;
		}
	}

	public static bool BlocksAutoLegendNick => YG2.saves.tutorialStage == StageNick;

	public static bool IsExchangeStep => YG2.saves.tutorialStage == StageExchange;

	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private RectTransform cardRect;
	private Image dimImage;
	private RectTransform pointerRect;
	private RectTransform pointerTarget;
	private Text titleText;
	private Text bodyText;
	private Button nextButton;
	private Text nextLabel;
	private int matchCard;
	private Coroutine matchRoutine;
	private bool visualsDirty;
	private bool modesPanelWasOpen;
	private bool mapsPanelWasOpen;
	private InputField nickField;
	private bool nickHooked;

	public static void NotifyPlayerScored()
	{
	}

	public static void MigrateSaves()
	{
		if (YG2.saves.tutorialStage >= StageDone)
		{
			YG2.saves.tutorialMenuSeen = true;
			YG2.saves.tutorialMatchSeen = true;
			return;
		}

		if (YG2.saves.tutorialMenuSeen && YG2.saves.tutorialMatchSeen)
			YG2.saves.tutorialStage = StageDone;
	}

	public static void EnsureMenu()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		if (Instance != null && Instance.targetCanvas != canvas)
			Instance.ReleaseForCanvasSwitch();

		TutorialController controller = canvas.GetComponent<TutorialController>();
		if (controller == null)
			controller = canvas.gameObject.AddComponent<TutorialController>();
		controller.BindMenu(canvas);
	}

	public static void EnsureMatch()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		if (Instance != null && Instance.targetCanvas != canvas)
			Instance.ReleaseForCanvasSwitch();

		TutorialController controller = canvas.GetComponent<TutorialController>();
		if (controller == null)
			controller = canvas.gameObject.AddComponent<TutorialController>();
		controller.BindMatch(canvas);
	}

	public static void NotifyMatchEnded()
	{
		if (IsDone)
			return;
		int stage = YG2.saves.tutorialStage;
		if (stage == StageMatchCards || stage == StageMatchPlay)
			SetStage(StageEndContinue);
		if (Instance != null)
			Instance.ShowEndContinueGuide();
	}

	public static void NotifyReturningToMenu()
	{
		if (IsDone)
			return;
		int stage = YG2.saves.tutorialStage;
		if (stage == StageEndContinue || stage == StageMatchPlay || stage == StageMatchCards)
			SetStage(StageCurrency);
	}

	public static void NotifyPlayStarted()
	{
		if (IsDone)
			return;
		if (YG2.saves.tutorialStage <= StagePlay)
			SetStage(StageMatchCards);
	}

	public static void NotifyExchanged()
	{
		if (!IsExchangeStep)
			return;
		EnsureWhiteFriendCoins();
		SetStage(StageSkins);
	}

	public static void EnsureWhiteFriendCoins()
	{
		if (YG2.saves.goldCoins < WhiteFriendCoinCost)
			YG2.saves.goldCoins = WhiteFriendCoinCost;
	}

	public void RefreshTexts()
	{
		if (overlayRoot == null || !overlayRoot.gameObject.activeSelf)
			return;
		ApplyStageVisuals();
	}

	public void RefreshLock()
	{
		ApplyLock();
	}

	private void BindMenu(Canvas canvas)
	{
		targetCanvas = canvas;
		Instance = this;
		ShowCleaningLandmarks = false;
		UiClickFeedback.EnsureOnScene();
		HideReplayButtons();
		EnsureOverlay();
		MigrateSaves();
		RecoverMenuStage();
		if (IsDone)
		{
			UnlockAll();
			HideOverlay();
			return;
		}

		if (YG2.saves.tutorialStage == StageNick)
			ClearNickFieldsForFirstRun();
		HookNickField();
		ApplyStageVisuals();
		ApplyLock();
	}

	private void BindMatch(Canvas canvas)
	{
		targetCanvas = canvas;
		Instance = this;
		ShowCleaningLandmarks = false;
		UiClickFeedback.EnsureOnScene();
		HideReplayButtons();
		EnsureOverlay();
		MigrateSaves();
		if (IsDone)
		{
			UnlockAll();
			HideOverlay();
			return;
		}

		if (YG2.saves.tutorialStage < StageMatchCards)
			SetStage(StageMatchCards);

		if (YG2.saves.tutorialStage == StageMatchCards)
		{
			if (matchRoutine != null)
				StopCoroutine(matchRoutine);
			matchRoutine = StartCoroutine(RunMatchCards());
		}
		else if (YG2.saves.tutorialStage == StageEndContinue)
			ShowEndContinueGuide();
		else
		{
			HideOverlay();
			UnlockAll();
		}
	}

	void OnDestroy()
	{
		UnhookNickField();
		if (Instance == this)
		{
			ShowCleaningLandmarks = false;
			Instance = null;
		}
	}

	private void ReleaseForCanvasSwitch()
	{
		UnhookNickField();
		ShowCleaningLandmarks = false;
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
		if (Instance == this)
			Instance = null;
	}

	void LateUpdate()
	{
		if (IsDone || targetCanvas == null)
			return;

		PollAdvances();
		if (visualsDirty)
		{
			visualsDirty = false;
			ApplyStageVisuals();
		}
		ApplyLock();
		RectTransform live = ResolveTargetRect();
		if (live != pointerTarget)
			PointAt(live);
		if (pointerRect != null && pointerRect.gameObject.activeSelf)
			UpdatePointer();
		if (overlayRoot != null && overlayRoot.gameObject.activeSelf)
			BringOverlayForward();
	}

	private void RecoverMenuStage()
	{
		int stage = YG2.saves.tutorialStage;
		if (stage == StageMatchCards || stage == StageMatchPlay || stage == StageEndContinue)
			SetStage(StageCurrency);
		else if (stage == StageCleaning || stage == StageModesReturn)
			SetStage(StageModes);
		else if (stage == StageCity && !IsPanelActive("PanelOfMaps"))
			SetStage(StageMaps);
		else if (stage == StageExchange)
			SetStage(StageCurrency);
		else if (stage == StageRotateWhite || stage == StageBuyWhite || stage == StageEquipWhite)
			SetStage(StageSkins);
	}

	private void PollAdvances()
	{
		switch (YG2.saves.tutorialStage)
		{
			case StageModes:
				if (IsPanelActive("PanelOfModes"))
				{
					modesPanelWasOpen = true;
					SetStage(StageCleaning);
					ApplyStageVisuals();
				}
				break;
			case StageCleaning:
				if (YG2.saves.chosenMode == (int)ModeManager.Mode.TotalCleaning)
				{
					SetStage(StageModesReturn);
					ApplyStageVisuals();
				}
				break;
			case StageModesReturn:
				if (modesPanelWasOpen && !IsPanelActive("PanelOfModes"))
				{
					SetStage(StageMaps);
					ApplyStageVisuals();
				}
				break;
			case StageMaps:
				if (IsPanelActive("PanelOfMaps"))
				{
					mapsPanelWasOpen = true;
					SetStage(StageCity);
					ApplyStageVisuals();
				}
				break;
			case StageCity:
				if (mapsPanelWasOpen && !IsPanelActive("PanelOfMaps"))
				{
					SetStage(StagePlay);
					ApplyStageVisuals();
				}
				break;
			case StageCurrency:
				if (IsPanelActive("PanelOfValute"))
				{
					SetStage(StageExchange);
					ApplyStageVisuals();
				}
				break;
			case StageSkins:
				if (IsPanelActive("PanelOfSkins"))
				{
					SetStage(StageRotateWhite);
					ApplyStageVisuals();
				}
				break;
			case StageRotateWhite:
				if (ReadChosenSkin() == WhiteFriendIndex)
				{
					SetStage(StageBuyWhite);
					ApplyStageVisuals();
				}
				break;
			case StageBuyWhite:
				if (YG2.saves.massiveOfObtaining != null
					&& YG2.saves.massiveOfObtaining.Length > WhiteFriendIndex
					&& YG2.saves.massiveOfObtaining[WhiteFriendIndex] == 1)
				{
					SetStage(StageEquipWhite);
					ApplyStageVisuals();
				}
				break;
			case StageEquipWhite:
				if (YG2.saves.equipedMaterial == WhiteFriendIndex)
					CompleteTutorial();
				break;
		}
	}

	private void CompleteTutorial()
	{
		SetStage(StageDone);
		YG2.saves.tutorialMenuSeen = true;
		YG2.saves.tutorialMatchSeen = true;
		YG2.SaveProgress();
		UnlockAll();
		HideOverlay();
	}

	private static void SetStage(int stage)
	{
		if (YG2.saves.tutorialStage == stage)
			return;
		YG2.saves.tutorialStage = stage;
		YG2.SaveProgress();
		if (Instance != null)
			Instance.visualsDirty = true;
	}

	private void EnsureOverlay()
	{
		if (overlayRoot != null || targetCanvas == null)
			return;

		Font font = ActiveCanvas.GetUiFont();
		GameObject rootGo = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Image));
		rootGo.transform.SetParent(targetCanvas.transform, false);
		overlayRoot = rootGo.GetComponent<RectTransform>();
		overlayRoot.anchorMin = Vector2.zero;
		overlayRoot.anchorMax = Vector2.one;
		overlayRoot.offsetMin = Vector2.zero;
		overlayRoot.offsetMax = Vector2.zero;
		BringOverlayForward();
		dimImage = rootGo.GetComponent<Image>();
		dimImage.color = new Color(0f, 0f, 0f, 0f);
		dimImage.raycastTarget = false;

		GameObject cardGo = new GameObject("TutorialCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		cardRect = cardGo.GetComponent<RectTransform>();
		cardRect.anchorMin = new Vector2(0.5f, 1f);
		cardRect.anchorMax = new Vector2(0.5f, 1f);
		cardRect.pivot = new Vector2(0.5f, 1f);
		cardRect.anchoredPosition = new Vector2(0f, -18f);
		cardRect.sizeDelta = new Vector2(560f, 120f);
		Image cardImage = cardGo.GetComponent<Image>();
		cardImage.color = new Color(0.12f, 0.1f, 0.14f, 0.96f);
		cardImage.raycastTarget = false;

		titleText = CreateLabel(cardRect, "Title", new Vector2(0f, -28f), new Vector2(520f, 36f), 26);
		bodyText = CreateLabel(cardRect, "Body", new Vector2(0f, -78f), new Vector2(520f, 64f), 20);
		if (titleText != null)
			titleText.fontStyle = FontStyle.Bold;
		if (bodyText != null)
			bodyText.alignment = TextAnchor.UpperCenter;

		nextButton = CreateButton(cardRect, "NextButton", new Vector2(0f, -240f), new Vector2(220f, 56f), new Color(0.28f, 0.72f, 0.32f, 1f), out nextLabel);
		nextButton.onClick.AddListener(OnMatchNext);
		UiClickFeedback.EnsureOn(nextButton);
		nextButton.gameObject.SetActive(false);

		pointerRect = CreatePointer(overlayRoot);
		overlayRoot.gameObject.SetActive(false);
		if (font != null)
			ActiveCanvas.ApplyUiFontEverywhere();
	}

	private void BringOverlayForward()
	{
		if (overlayRoot == null)
			return;
		overlayRoot.SetAsLastSibling();
		Vector3 pos = overlayRoot.localPosition;
		pos.z = OverlayLocalZ;
		overlayRoot.localPosition = pos;
	}

	private static RectTransform CreatePointer(Transform parent)
	{
		GameObject go = new GameObject("TutorialPointer", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(64f, 96f);
		Image image = go.GetComponent<Image>();
		image.sprite = ActiveCanvas.GetHudPointerSprite();
		image.preserveAspect = true;
		image.color = PointerTint;
		image.raycastTarget = false;
		go.SetActive(false);
		return rect;
	}

	private static Text CreateLabel(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize)
	{
		Text text = ActiveCanvas.CreateText(parent, name, pos, size);
		if (text == null)
			return null;
		RectTransform rect = text.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = pos;
		text.fontSize = fontSize;
		text.raycastTarget = false;
		return text;
	}

	private static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, out Text label)
	{
		Transform existing = parent.Find(name);
		if (existing != null)
		{
			label = existing.GetComponentInChildren<Text>();
			return existing.GetComponent<Button>();
		}

		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = pos;
		rect.sizeDelta = size;
		Image image = go.GetComponent<Image>();
		image.color = color;
		image.raycastTarget = true;
		Button button = go.GetComponent<Button>();

		label = ActiveCanvas.CreateText(go.transform, "Label", Vector2.zero, size);
		if (label != null)
		{
			RectTransform labelRect = label.rectTransform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = Vector2.zero;
			labelRect.offsetMax = Vector2.zero;
			label.fontSize = 24;
			label.raycastTarget = false;
		}
		return button;
	}

	private void ApplyStageVisuals()
	{
		int stage = YG2.saves.tutorialStage;
		if (stage >= StageDone)
		{
			HideOverlay();
			return;
		}

		if (stage == StageMatchPlay)
		{
			HideOverlay();
			return;
		}

		if (stage == StageMatchCards)
		{
			ShowMatchCard(matchCard);
			return;
		}

		ShowGuidePlaque(GuideTitle(stage), GuideBody(stage), false);
		PointAt(ResolveTargetRect());
	}

	private static string GuideTitle(int stage)
	{
		switch (stage)
		{
			case StageNick: return GameTexts.TutorialLegendNickTitle;
			case StageModes: return GameTexts.TutorialModesTitle;
			case StageCleaning: return GameTexts.TutorialCleaningPickTitle;
			case StageModesReturn: return GameTexts.TutorialReturnTitle;
			case StageMaps: return GameTexts.TutorialMapsTitle;
			case StageCity: return GameTexts.TutorialCityTitle;
			case StagePlay: return GameTexts.TutorialPlayTitle;
			case StageEndContinue: return GameTexts.TutorialContinueTitle;
			case StageCurrency: return GameTexts.TutorialCurrencyTitle;
			case StageExchange: return GameTexts.TutorialExchangeTitle;
			case StageSkins: return GameTexts.TutorialSkinsTitle;
			case StageRotateWhite: return GameTexts.TutorialRotateTitle;
			case StageBuyWhite: return GameTexts.TutorialBuyTitle;
			case StageEquipWhite: return GameTexts.TutorialEquipTitle;
			default: return "";
		}
	}

	private static string GuideBody(int stage)
	{
		switch (stage)
		{
			case StageNick: return GameTexts.TutorialLegendNickBody;
			case StageModes: return GameTexts.TutorialModesBody;
			case StageCleaning: return GameTexts.TutorialCleaningPickBody;
			case StageModesReturn: return GameTexts.TutorialReturnBody;
			case StageMaps: return GameTexts.TutorialMapsBody;
			case StageCity: return GameTexts.TutorialCityBody;
			case StagePlay: return GameTexts.TutorialPlayBody;
			case StageEndContinue: return GameTexts.TutorialContinueBody;
			case StageCurrency: return GameTexts.TutorialCurrencyBody;
			case StageExchange: return GameTexts.TutorialExchangeBody;
			case StageSkins: return GameTexts.TutorialSkinsBody;
			case StageRotateWhite: return GameTexts.TutorialRotateBody;
			case StageBuyWhite: return GameTexts.TutorialBuyBody;
			case StageEquipWhite: return GameTexts.TutorialEquipBody;
			default: return "";
		}
	}

	private void ShowGuidePlaque(string title, string body, bool dim)
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		BringOverlayForward();
		if (dimImage != null)
		{
			dimImage.color = dim ? new Color(0f, 0f, 0f, 0.62f) : new Color(0f, 0f, 0f, 0f);
			dimImage.raycastTarget = dim;
		}
		if (cardRect != null)
		{
			cardRect.gameObject.SetActive(true);
			LayoutPlaqueAwayFrom(ResolveTargetRect());
			Image cardImage = cardRect.GetComponent<Image>();
			if (cardImage != null)
				cardImage.raycastTarget = dim;
		}
		if (nextButton != null)
			nextButton.gameObject.SetActive(false);
		if (titleText != null)
			titleText.text = title;
		if (bodyText != null)
			bodyText.text = body;
	}

	private void ShowMatchCard(int card)
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		BringOverlayForward();
		if (dimImage != null)
		{
			dimImage.color = new Color(0f, 0f, 0f, 0.62f);
			dimImage.raycastTarget = true;
		}
		if (cardRect != null)
		{
			cardRect.gameObject.SetActive(true);
			cardRect.anchorMin = new Vector2(0.5f, 0.5f);
			cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);
			cardRect.anchoredPosition = Vector2.zero;
			cardRect.sizeDelta = new Vector2(620f, 320f);
			Image cardImage = cardRect.GetComponent<Image>();
			if (cardImage != null)
				cardImage.raycastTarget = true;
		}
		if (titleText != null)
		{
			titleText.rectTransform.anchoredPosition = new Vector2(0f, -36f);
			titleText.rectTransform.sizeDelta = new Vector2(560f, 56f);
		}
		if (bodyText != null)
		{
			bodyText.rectTransform.anchoredPosition = new Vector2(0f, -120f);
			bodyText.rectTransform.sizeDelta = new Vector2(560f, 140f);
		}
		if (nextButton != null)
		{
			nextButton.gameObject.SetActive(true);
			RectTransform nextRect = nextButton.GetComponent<RectTransform>();
			nextRect.anchorMin = new Vector2(0.5f, 1f);
			nextRect.anchorMax = new Vector2(0.5f, 1f);
			nextRect.pivot = new Vector2(0.5f, 0.5f);
			nextRect.anchoredPosition = new Vector2(0f, -270f);
			nextButton.transform.SetAsLastSibling();
		}
		if (nextLabel != null)
			nextLabel.text = GameTexts.TutorialNext;
		ApplyMatchCardTexts(card);
		PointAt(MatchCardPointer(card));
	}

	private void LayoutPlaqueAwayFrom(RectTransform target)
	{
		if (cardRect == null)
			return;

		bool targetHigh = false;
		if (target != null && targetCanvas != null)
		{
			Camera cam = targetCanvas.worldCamera;
			Vector3[] corners = new Vector3[4];
			target.GetWorldCorners(corners);
			Vector3 mid = (corners[0] + corners[2]) * 0.5f;
			Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, mid);
			targetHigh = screen.y > Screen.height * 0.62f;
		}

		cardRect.anchorMin = new Vector2(0.5f, targetHigh ? 0f : 1f);
		cardRect.anchorMax = cardRect.anchorMin;
		cardRect.pivot = new Vector2(0.5f, targetHigh ? 0f : 1f);
		cardRect.anchoredPosition = new Vector2(0f, targetHigh ? 24f : -18f);
		cardRect.sizeDelta = new Vector2(560f, 120f);
		if (titleText != null)
		{
			titleText.rectTransform.anchoredPosition = new Vector2(0f, -28f);
			titleText.rectTransform.sizeDelta = new Vector2(520f, 36f);
		}
		if (bodyText != null)
		{
			bodyText.rectTransform.anchoredPosition = new Vector2(0f, -78f);
			bodyText.rectTransform.sizeDelta = new Vector2(520f, 64f);
		}
	}

	private void ApplyMatchCardTexts(int card)
	{
		switch (card)
		{
			case 0:
				SetCard(GameTexts.TutorialMoveTitle, GameTexts.TutorialMoveBody);
				break;
			case 1:
				SetCard(GameTexts.TutorialBoostTitle, GameTexts.TutorialBoostBody);
				break;
			case 2:
				SetCard(GameTexts.TutorialEatTitle, GameTexts.TutorialEatBody);
				break;
			default:
				SetCard(GameTexts.TutorialCleaningRulesTitle, GameTexts.TutorialCleaningRulesBody);
				break;
		}
	}

	private RectTransform MatchCardPointer(int card)
	{
		if (card == 0)
			return FindJoystick();
		if (card == 1)
			return FindBoost();
		return null;
	}

	private void SetCard(string title, string body)
	{
		if (titleText != null)
			titleText.text = title;
		if (bodyText != null)
			bodyText.text = body;
	}

	private void HideOverlay()
	{
		HidePointer();
		ShowCleaningLandmarks = false;
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
	}

	private IEnumerator RunMatchCards()
	{
		while (BlackHoleController.Player == null)
			yield return null;
		while (BlackHoleController.Player.IsBirthIntro)
			yield return null;
		if (GamingManager.Instance != null && GamingManager.Instance.HasEnded)
			yield break;

		matchCard = 0;
		SetMatchClockFrozen(true);
		MatchPause.Pause();
		ShowMatchCard(matchCard);
		ApplyLock();
	}

	private void OnMatchNext()
	{
		if (YG2.saves.tutorialStage != StageMatchCards)
			return;

		matchCard++;
		if (matchCard >= MatchCardCount)
		{
			BeginFreeMatch();
			return;
		}
		ShowMatchCard(matchCard);
	}

	private void BeginFreeMatch()
	{
		SetStage(StageMatchPlay);
		SetMatchClockFrozen(false);
		HideOverlay();
		UnlockAll();
		MatchPause.Resume();
	}

	private void ShowEndContinueGuide()
	{
		if (IsDone || YG2.saves.tutorialStage != StageEndContinue)
			return;
		ShowGuidePlaque(GameTexts.TutorialContinueTitle, GameTexts.TutorialContinueBody, false);
		PointAt(ResolveTargetRect());
		ApplyLock();
	}

	private static void SetMatchClockFrozen(bool frozen)
	{
		if (GamingManager.Instance != null)
			GamingManager.Instance.SetMatchClockFrozen(frozen);
	}

	private void ApplyLock()
	{
		if (targetCanvas == null)
			return;

		bool lockUi = IsLockingUi;
		Button allowed = ResolveTargetButton();
		bool allowNext = YG2.saves.tutorialStage == StageMatchCards;
		Button[] buttons = targetCanvas.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			Button button = buttons[i];
			if (button == null)
				continue;
			if (!lockUi)
			{
				button.interactable = true;
				BoostButton boostOn = button.GetComponent<BoostButton>();
				if (boostOn != null)
					boostOn.enabled = true;
				continue;
			}
			button.interactable = button == allowed || (allowNext && button == nextButton);
			BoostButton boostOff = button.GetComponent<BoostButton>();
			if (boostOff != null)
				boostOff.enabled = false;
		}

		InputField allowedField = YG2.saves.tutorialStage == StageNick ? ResolveNickField() : null;
		InputField[] fields = targetCanvas.GetComponentsInChildren<InputField>(true);
		for (int i = 0; i < fields.Length; i++)
		{
			if (fields[i] == null)
				continue;
			fields[i].interactable = !lockUi || fields[i] == allowedField;
		}
	}

	private void UnlockAll()
	{
		if (targetCanvas == null)
			return;
		Button[] buttons = targetCanvas.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] == null)
				continue;
			buttons[i].interactable = true;
			BoostButton boost = buttons[i].GetComponent<BoostButton>();
			if (boost != null)
				boost.enabled = true;
		}
		InputField[] fields = targetCanvas.GetComponentsInChildren<InputField>(true);
		for (int i = 0; i < fields.Length; i++)
		{
			if (fields[i] != null)
				fields[i].interactable = true;
		}
		if (HorizontalLayout3D.Instance != null)
			HorizontalLayout3D.Instance.UpdateForChosen();
	}

	private Button ResolveTargetButton()
	{
		switch (YG2.saves.tutorialStage)
		{
			case StageModes: return FindButton("Modes");
			case StageCleaning: return FindButton("TotalCleaning");
			case StageModesReturn: return FindButton("Return", "PanelOfModes");
			case StageMaps: return FindButton("Maps");
			case StageCity: return FindButton("City") ?? FindButton("City1");
			case StagePlay: return FindButton("PlayButton");
			case StageMatchCards: return nextButton;
			case StageEndContinue: return FindButton("ReturningToHome");
			case StageCurrency: return FindActivator("PanelOfValute") ?? FindButton("Points");
			case StageExchange: return FindButton("Exchange");
			case StageSkins: return FindActivator("PanelOfSkins") ?? FindButton("SkinsShop");
			case StageRotateWhite: return FindRotateNextButton();
			case StageBuyWhite: return FindButton("InnerValute");
			case StageEquipWhite: return FindButton("Equiping");
			default: return null;
		}
	}

	private RectTransform ResolveTargetRect()
	{
		if (YG2.saves.tutorialStage == StageNick)
		{
			InputField field = ResolveNickField();
			return field != null ? field.GetComponent<RectTransform>() : null;
		}
		if (YG2.saves.tutorialStage == StageMatchCards)
			return MatchCardPointer(matchCard);
		Button button = ResolveTargetButton();
		return button != null ? button.GetComponent<RectTransform>() : null;
	}

	private Button FindButton(string name, string ancestor = null)
	{
		if (targetCanvas == null)
			return null;
		Button[] buttons = targetCanvas.GetComponentsInChildren<Button>(true);
		Button fallback = null;
		for (int i = 0; i < buttons.Length; i++)
		{
			Button button = buttons[i];
			if (button == null || button.gameObject.name != name)
				continue;
			if (ancestor != null && !HasAncestor(button.transform, ancestor))
				continue;
			if (button.gameObject.activeInHierarchy)
				return button;
			fallback = button;
		}
		return fallback;
	}

	private Button FindActivator(string targetName)
	{
		if (targetCanvas == null)
			return null;
		Button[] buttons = targetCanvas.GetComponentsInChildren<Button>(true);
		Button fallback = null;
		for (int i = 0; i < buttons.Length; i++)
		{
			Button button = buttons[i];
			if (button == null)
				continue;
			int count = button.onClick.GetPersistentEventCount();
			bool match = false;
			for (int p = 0; p < count; p++)
			{
				if (button.onClick.GetPersistentMethodName(p) != "SetActive")
					continue;
				UnityEngine.Object persistentTarget = button.onClick.GetPersistentTarget(p);
				if (persistentTarget != null && persistentTarget.name == targetName)
				{
					match = true;
					break;
				}
			}
			if (!match)
				continue;
			if (HasAncestor(button.transform, targetName))
				continue;
			if (button.gameObject.activeInHierarchy)
				return button;
			fallback = button;
		}
		return fallback;
	}

	private Button FindRotateNextButton()
	{
		if (targetCanvas == null)
			return null;
		Button[] buttons = targetCanvas.GetComponentsInChildren<Button>(true);
		Button best = null;
		float bestX = float.NegativeInfinity;
		for (int i = 0; i < buttons.Length; i++)
		{
			Button button = buttons[i];
			if (button == null || !HasPersistentMethod(button, "RotateOnDeg"))
				continue;
			if (!HasAncestor(button.transform, "PanelOfSkins"))
				continue;
			float x = button.transform.position.x;
			if (x > bestX)
			{
				bestX = x;
				best = button;
			}
		}
		return best;
	}

	private static bool HasPersistentMethod(Button button, string methodName)
	{
		int count = button.onClick.GetPersistentEventCount();
		for (int i = 0; i < count; i++)
		{
			if (button.onClick.GetPersistentMethodName(i) == methodName)
				return true;
		}
		return false;
	}

	private static bool HasAncestor(Transform transform, string name)
	{
		Transform current = transform;
		while (current != null)
		{
			if (current.name == name)
				return true;
			current = current.parent;
		}
		return false;
	}

	private bool IsPanelActive(string name)
	{
		GameObject panel = FindNamed(name);
		return panel != null && panel.activeSelf;
	}

	private GameObject FindNamed(string name)
	{
		if (targetCanvas == null)
			return null;
		Transform[] transforms = targetCanvas.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			if (transforms[i] != null && transforms[i].name == name)
				return transforms[i].gameObject;
		}
		return null;
	}

	private int ReadChosenSkin()
	{
		HorizontalLayout3D shop = ShopForActiveCanvas();
		return shop != null ? shop.ChosenIndex : 0;
	}

	private HorizontalLayout3D ShopForActiveCanvas()
	{
		Button rotate = FindRotateNextButton();
		if (rotate != null)
		{
			int count = rotate.onClick.GetPersistentEventCount();
			for (int i = 0; i < count; i++)
			{
				if (rotate.onClick.GetPersistentMethodName(i) != "RotateOnDeg")
					continue;
				HorizontalLayout3D shop = rotate.onClick.GetPersistentTarget(i) as HorizontalLayout3D;
				if (shop != null)
					return shop;
			}
		}
		return HorizontalLayout3D.Instance;
	}

	private void HookNickField()
	{
		UnhookNickField();
		nickField = ResolveNickField();
		if (nickField == null)
			return;
		nickField.onEndEdit.AddListener(OnNickEndEdit);
		nickHooked = true;
	}

	private void UnhookNickField()
	{
		if (nickHooked && nickField != null)
			nickField.onEndEdit.RemoveListener(OnNickEndEdit);
		nickField = null;
		nickHooked = false;
	}

	private void OnNickEndEdit(string value)
	{
		if (YG2.saves.tutorialStage != StageNick)
			return;
		if (string.IsNullOrWhiteSpace(value))
		{
			if (bodyText != null)
				bodyText.text = GameTexts.TutorialNickNeed;
			return;
		}

		string nick = value.Trim();
		if (MainMenuController.Instance != null)
			MainMenuController.Instance.ApplyNick(nick);
		else
		{
			YG2.saves.nickName = nick;
			YG2.saves.isNickGiven = true;
			YG2.SaveProgress();
		}
		SetStage(StageModes);
		ApplyStageVisuals();
		ApplyLock();
	}

	private static bool UseMobileNickField()
	{
		if (GameController.Instance != null)
			return GameController.Instance.IsMobileCanvasActive();
		return YG2.envir.isMobile;
	}

	private static InputField ResolveNickField()
	{
		if (MainMenuController.Instance == null)
			return null;
		if (UseMobileNickField() && MainMenuController.Instance.nameInput != null)
			return MainMenuController.Instance.nameInput;
		if (MainMenuController.Instance.DnameInput != null)
			return MainMenuController.Instance.DnameInput;
		return MainMenuController.Instance.nameInput;
	}

	private static void ClearNickFieldsForFirstRun()
	{
		if (YG2.saves.isNickGiven || MainMenuController.Instance == null)
			return;
		if (MainMenuController.Instance.nameInput != null)
			MainMenuController.Instance.nameInput.text = "";
		if (MainMenuController.Instance.DnameInput != null)
			MainMenuController.Instance.DnameInput.text = "";
	}

	private static void HideReplayButtons()
	{
		if (GameController.Instance == null)
			return;
		HideReplayOn(GameController.Instance.CanvasForMobile);
		HideReplayOn(GameController.Instance.CanvasForDesktop);
	}

	private static void HideReplayOn(GameObject canvasGo)
	{
		if (canvasGo == null)
			return;
		Transform replay = canvasGo.transform.Find("TutorialReplayButton");
		if (replay != null)
			replay.gameObject.SetActive(false);
	}

	private void HidePointer()
	{
		pointerTarget = null;
		if (pointerRect != null)
			pointerRect.gameObject.SetActive(false);
	}

	private void PointAt(RectTransform target)
	{
		pointerTarget = target;
		if (pointerRect == null)
			return;
		bool show = target != null;
		pointerRect.gameObject.SetActive(show);
		if (show)
		{
			pointerRect.SetAsLastSibling();
			UpdatePointer();
		}
	}

	private void UpdatePointer()
	{
		if (pointerRect == null || pointerTarget == null)
			return;

		Vector3[] corners = new Vector3[4];
		pointerTarget.GetWorldCorners(corners);
		Vector3 targetCenter = (corners[0] + corners[2]) * 0.5f;
		Vector3 from = cardRect != null && cardRect.gameObject.activeSelf
			? cardRect.position
			: pointerRect.position;
		Vector3 away = targetCenter - from;
		away.z = 0f;
		if (away.sqrMagnitude < 0.01f)
			away = Vector3.up;
		away.Normalize();
		pointerRect.position = targetCenter - away * PointerOffset;
		float angle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg - 90f;
		pointerRect.localEulerAngles = new Vector3(0f, 0f, angle);
	}

	private static RectTransform FindJoystick()
	{
		JoystickController[] sticks = UnityEngine.Object.FindObjectsByType<JoystickController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		for (int i = 0; i < sticks.Length; i++)
		{
			if (sticks[i] != null && sticks[i].gameObject.activeInHierarchy)
				return sticks[i].GetComponent<RectTransform>();
		}
		return null;
	}

	private RectTransform FindBoost()
	{
		Canvas canvas = targetCanvas != null ? targetCanvas : ActiveCanvas.Get();
		if (canvas == null)
			return null;
		BoostButton[] buttons = canvas.GetComponentsInChildren<BoostButton>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
				return buttons[i].GetComponent<RectTransform>();
		}
		if (buttons.Length > 0 && buttons[0] != null)
			return buttons[0].GetComponent<RectTransform>();
		return null;
	}
}
