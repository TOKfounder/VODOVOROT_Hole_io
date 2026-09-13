using System.Collections;
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
	public const int StageValuteReturn = 17;
	public const int StageSkinsReturn = 18;
	public const int StageDone = 19;

	private const int MatchCardCount = 4;
	private const int WhiteFriendIndex = 1;
	private static int WhiteFriendCoinCost
	{
		get
		{
			SkinCatalog catalog = GameBalance.Skins;
			if (catalog != null)
			{
				SkinDef def = catalog.Get(WhiteFriendIndex);
				if (def != null && def.costCoins > 0)
					return def.costCoins;
			}
			return 20;
		}
	}
	private const float PointerOffset = 92f;
	private const float PlaqueGap = 16f;
	private const float OverlayLocalZ = -200f;
	private const float OverlaySkinZPad = 80f;
	private const float GuideDimAlpha = 0.62f;
	private const float GuidePlaquePadX = 24f;
	private const float GuidePlaquePadY = 18f;
	private const float GuidePlaqueMinWidth = 200f;
	private const float GuidePlaqueMaxWidth = 560f;
	private const float GuidePlaqueScreenMargin = 20f;
	private const float GuideTextGap = 8f;
	private const float GuidePlaqueWidth = 480f;
	private const float GuidePlaqueHeight = 160f;
	private const float MatchCardWidth = 620f;
	private const float MatchCardHeight = 320f;
	private static readonly Color PointerTint = new Color(1f, 0.85f, 0.2f, 1f);

	public static TutorialController Instance { get; private set; }

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

	public static bool AllowsWhiteFriendBuy
	{
		get
		{
			int stage = YG2.saves.tutorialStage;
			return stage == StageRotateWhite || stage == StageBuyWhite;
		}
	}

	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private RectTransform cardRect;
	private Image dimImage;
	private GameObject matchDimRoot;
	private Image matchDimImage;
	private RectTransform pointerRect;
	private RectTransform pointerTarget;
	private Text titleText;
	private Text bodyText;
	private Button nextButton;
	private Text nextLabel;
	private int matchCard;
	private Coroutine matchRoutine;
	private bool visualsDirty;
	private bool plaqueFollowsPointer;
	private bool modesPanelWasOpen;
	private bool mapsPanelWasOpen;
	private InputField nickField;
	private bool nickHooked;

	public static void MigrateSaves()
	{
		if (YG2.saves.tutorialMenuSeen && YG2.saves.tutorialMatchSeen)
		{
			YG2.saves.tutorialStage = StageDone;
			return;
		}

		if (YG2.saves.tutorialStage == 16)
		{
			YG2.saves.tutorialStage = StageDone;
			YG2.saves.tutorialMenuSeen = true;
			YG2.saves.tutorialMatchSeen = true;
			return;
		}

		if (YG2.saves.tutorialStage >= StageDone)
		{
			YG2.saves.tutorialMenuSeen = true;
			YG2.saves.tutorialMatchSeen = true;
		}
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
		SetStage(StageValuteReturn);
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
		MatchPause.ForceReset();
		UiClickFeedback.EnsureOnScene();
		HideReplayButtons();
		EnsureOverlay();
		MigrateSaves();
		RecoverMenuStage();
		if (IsDone)
		{
			UnlockAll();
			HideOverlay();
			RefreshSelectionCards();
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
			Instance = null;
	}

	private void ReleaseForCanvasSwitch()
	{
		UnhookNickField();
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
		if (YG2.saves.tutorialStage == StageMatchCards)
		{
			HidePointer();
		}
		else
		{
			RectTransform live = ResolveTargetRect();
			if (live != pointerTarget)
				PointAt(live);
			if (pointerRect != null && pointerRect.gameObject.activeSelf)
				UpdatePointer();
		}
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
		else if (stage == StageValuteReturn && !IsPanelActive("PanelOfValute"))
			SetStage(StageSkins);
		else if (stage == StageRotateWhite || stage == StageBuyWhite || stage == StageEquipWhite)
			SetStage(StageSkins);
		else if (stage == StageSkinsReturn && !IsPanelActive("PanelOfSkins"))
			CompleteTutorial();
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
			case StageValuteReturn:
				if (!IsPanelActive("PanelOfValute"))
				{
					SetStage(StageSkins);
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
				{
					SetStage(StageSkinsReturn);
					ApplyStageVisuals();
				}
				break;
			case StageSkinsReturn:
				if (!IsPanelActive("PanelOfSkins"))
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
		RefreshSelectionCards();
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
		dimImage.enabled = false;

		GameObject matchDimGo = new GameObject("MatchDim", typeof(RectTransform), typeof(Image));
		matchDimGo.transform.SetParent(overlayRoot, false);
		matchDimRoot = matchDimGo;
		RectTransform matchDimRect = matchDimGo.GetComponent<RectTransform>();
		matchDimRect.anchorMin = Vector2.zero;
		matchDimRect.anchorMax = Vector2.one;
		matchDimRect.offsetMin = Vector2.zero;
		matchDimRect.offsetMax = Vector2.zero;
		matchDimImage = matchDimGo.GetComponent<Image>();
		matchDimImage.color = new Color(0f, 0f, 0f, GuideDimAlpha);
		matchDimImage.raycastTarget = true;
		matchDimGo.SetActive(false);

		GameObject cardGo = new GameObject("TutorialCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		cardRect = cardGo.GetComponent<RectTransform>();
		cardRect.anchorMin = new Vector2(0.5f, 0.5f);
		cardRect.anchorMax = new Vector2(0.5f, 0.5f);
		cardRect.pivot = new Vector2(0.5f, 0.5f);
		cardRect.anchoredPosition = Vector2.zero;
		cardRect.sizeDelta = new Vector2(GuidePlaqueWidth, GuidePlaqueHeight);
		Image cardImage = cardGo.GetComponent<Image>();
		ActiveCanvas.ApplyRoundedPanel(cardImage);
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
		pos.z = ResolveOverlayLocalZ();
		overlayRoot.localPosition = pos;
		ApplyGuidePerspectiveScale();
	}

	private float GuidePerspectiveScale()
	{
		if (overlayRoot == null || targetCanvas == null)
			return 1f;
		Camera cam = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
		if (cam == null)
			return 1f;

		Transform canvasTf = targetCanvas.transform;
		Vector3 close = overlayRoot.position;
		Vector3 baseline = canvasTf.TransformPoint(new Vector3(overlayRoot.localPosition.x, overlayRoot.localPosition.y, OverlayLocalZ));
		float dClose = Vector3.Distance(cam.transform.position, close);
		float dBase = Vector3.Distance(cam.transform.position, baseline);
		if (dBase < 0.001f)
			return 1f;
		return Mathf.Clamp(dClose / dBase, 0.25f, 1f);
	}

	private void ApplyGuidePerspectiveScale()
	{
		float scale = GuidePerspectiveScale();
		if (cardRect != null)
			cardRect.localScale = new Vector3(scale, scale, 1f);
		if (pointerRect != null)
			pointerRect.localScale = new Vector3(scale, scale, 1f);
	}

	private float ResolveOverlayLocalZ()
	{
		float z = OverlayLocalZ;
		if (targetCanvas == null)
			return z;

		HorizontalLayout3D[] layouts = targetCanvas.GetComponentsInChildren<HorizontalLayout3D>(false);
		for (int i = 0; i < layouts.Length; i++)
		{
			HorizontalLayout3D layout = layouts[i];
			if (layout == null || !layout.gameObject.activeInHierarchy)
				continue;
			float worldRadius = layout.radius * Mathf.Max(
				Mathf.Abs(layout.transform.lossyScale.x),
				Mathf.Abs(layout.transform.lossyScale.z));
			Vector3 worldFront = layout.transform.position - targetCanvas.transform.forward * worldRadius;
			float canvasZ = targetCanvas.transform.InverseTransformPoint(worldFront).z;
			z = Mathf.Min(z, canvasZ - OverlaySkinZPad);
		}
		return z;
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
			RefreshSelectionCards();
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

		ShowGuidePlaque(GuideTitle(stage), GuideBody(stage));
		RefreshSelectionCards();
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
			case StageValuteReturn: return GameTexts.TutorialValuteReturnTitle;
			case StageSkins: return GameTexts.TutorialSkinsTitle;
			case StageRotateWhite: return GameTexts.TutorialRotateTitle;
			case StageBuyWhite: return GameTexts.TutorialBuyTitle;
			case StageEquipWhite: return GameTexts.TutorialEquipTitle;
			case StageSkinsReturn: return GameTexts.TutorialSkinsReturnTitle;
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
			case StageValuteReturn: return GameTexts.TutorialValuteReturnBody;
			case StageSkins: return GameTexts.TutorialSkinsBody;
			case StageRotateWhite: return GameTexts.TutorialRotateBody;
			case StageBuyWhite: return GameTexts.TutorialBuyBody;
			case StageEquipWhite: return GameTexts.TutorialEquipBody;
			case StageSkinsReturn: return GameTexts.TutorialSkinsReturnBody;
			default: return "";
		}
	}

	private void ShowGuidePlaque(string title, string body)
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		BringOverlayForward();
		DisableRootOverlayImage();
		HideMatchDim();
		plaqueFollowsPointer = true;
		if (cardRect != null)
		{
			cardRect.gameObject.SetActive(true);
			Image cardImage = cardRect.GetComponent<Image>();
			if (cardImage != null)
				cardImage.raycastTarget = true;
		}
		if (nextButton != null)
			nextButton.gameObject.SetActive(false);
		if (titleText != null)
			titleText.text = title;
		if (bodyText != null)
			bodyText.text = body;
		ApplyGuidePlaqueTextLayout();
		PointAt(ResolveTargetRect());
	}

	private void ShowMatchCard(int card)
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		BringOverlayForward();
		DisableRootOverlayImage();
		ShowMatchDim();
		plaqueFollowsPointer = false;
		if (cardRect != null)
		{
			cardRect.gameObject.SetActive(true);
			cardRect.anchorMin = new Vector2(0.5f, 0.5f);
			cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);
			cardRect.anchoredPosition = Vector2.zero;
			cardRect.sizeDelta = new Vector2(MatchCardWidth, MatchCardHeight);
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
		HidePointer();
	}

	private void DisableRootOverlayImage()
	{
		if (dimImage == null)
			return;
		dimImage.enabled = false;
		dimImage.raycastTarget = false;
		dimImage.color = new Color(0f, 0f, 0f, 0f);
	}

	private void ShowMatchDim()
	{
		if (matchDimRoot != null)
		{
			matchDimRoot.SetActive(true);
			matchDimRoot.transform.SetAsFirstSibling();
		}
		if (matchDimImage != null)
		{
			matchDimImage.color = new Color(0f, 0f, 0f, GuideDimAlpha);
			matchDimImage.raycastTarget = true;
		}
	}

	private void HideMatchDim()
	{
		if (matchDimRoot != null)
			matchDimRoot.SetActive(false);
	}

	private Vector2 FitGuidePlaqueSize()
	{
		float maxWidth = GuidePlaqueMaxWidth;
		if (overlayRoot != null)
			maxWidth = Mathf.Min(maxWidth, overlayRoot.rect.width - GuidePlaqueScreenMargin * 2f);
		if (TryGetMenuSafeRect(out Rect safe))
			maxWidth = Mathf.Min(maxWidth, Mathf.Max(GuidePlaqueMinWidth, safe.width - GuidePlaqueScreenMargin * 2f));
		maxWidth = Mathf.Max(maxWidth, GuidePlaqueMinWidth);
		float innerMax = Mathf.Max(80f, maxWidth - GuidePlaquePadX * 2f);

		float titleW = MeasurePreferredWidth(titleText, innerMax);
		float bodyW = MeasurePreferredWidth(bodyText, innerMax);
		float inner = Mathf.Clamp(Mathf.Max(titleW, bodyW, 160f), 160f, innerMax);

		float titleH = ApplyWrappedSize(titleText, inner);
		float bodyH = ApplyWrappedSize(bodyText, inner);
		float gap = titleH > 0f && bodyH > 0f ? GuideTextGap : 0f;

		if (titleText != null)
			titleText.rectTransform.anchoredPosition = new Vector2(0f, -GuidePlaquePadY);
		if (bodyText != null)
			bodyText.rectTransform.anchoredPosition = new Vector2(0f, -(GuidePlaquePadY + titleH + gap));

		return new Vector2(inner + GuidePlaquePadX * 2f, GuidePlaquePadY + titleH + gap + bodyH + GuidePlaquePadY);
	}

	private static float MeasurePreferredWidth(Text text, float cap)
	{
		if (text == null || string.IsNullOrEmpty(text.text))
			return 0f;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return Mathf.Min(text.preferredWidth, cap);
	}

	private static float ApplyWrappedSize(Text text, float inner)
	{
		if (text == null)
			return 0f;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.rectTransform.sizeDelta = new Vector2(inner, 8f);
		float height = Mathf.Max(text.preferredHeight, text.fontSize);
		text.rectTransform.sizeDelta = new Vector2(inner, height);
		return height;
	}

	private void ApplyGuidePlaqueTextLayout()
	{
		if (cardRect == null)
			return;
		cardRect.sizeDelta = FitGuidePlaqueSize();
	}

	private void LayoutPlaqueAtPointerTail(Vector2 tipDirLocal)
	{
		if (cardRect == null || pointerRect == null)
			return;

		cardRect.anchorMin = new Vector2(0.5f, 0.5f);
		cardRect.anchorMax = new Vector2(0.5f, 0.5f);
		cardRect.pivot = new Vector2(0.5f, 0.5f);
		Vector2 size = FitGuidePlaqueSize();
		cardRect.sizeDelta = size;
		float scale = GuidePerspectiveScale();
		ApplyGuidePerspectiveScale();
		float along = (pointerRect.sizeDelta.y * 0.5f + PlaqueGap + size.y * 0.5f) * scale;
		Vector2 pos = new Vector2(0f, pointerRect.anchoredPosition.y - tipDirLocal.y * along);
		cardRect.anchoredPosition = ClampPlaquePos(pos, size * scale);
		cardRect.SetAsLastSibling();
		if (pointerRect != null)
			pointerRect.SetAsLastSibling();
	}

	private Vector2 ClampPlaquePos(Vector2 pos, Vector2 size)
	{
		float halfW = size.x * 0.5f;
		float halfH = size.y * 0.5f;
		if (TryGetMenuSafeRect(out Rect safe))
		{
			float minX = safe.xMin + halfW;
			float maxX = safe.xMax - halfW;
			float minY = safe.yMin + halfH;
			float maxY = safe.yMax - halfH;
			pos.x = minX > maxX ? safe.center.x : Mathf.Clamp(pos.x, minX, maxX);
			pos.y = minY > maxY ? safe.center.y : Mathf.Clamp(pos.y, minY, maxY);
			return pos;
		}

		if (overlayRoot == null)
			return pos;
		float halfScreenW = overlayRoot.rect.width * 0.5f;
		float halfScreenH = overlayRoot.rect.height * 0.5f;
		float minOx = -halfScreenW + halfW + GuidePlaqueScreenMargin;
		float maxOx = halfScreenW - halfW - GuidePlaqueScreenMargin;
		float minOy = -halfScreenH + halfH + GuidePlaqueScreenMargin;
		float maxOy = halfScreenH - halfH - GuidePlaqueScreenMargin;
		pos.x = minOx > maxOx ? 0f : Mathf.Clamp(pos.x, minOx, maxOx);
		pos.y = minOy > maxOy ? 0f : Mathf.Clamp(pos.y, minOy, maxOy);
		return pos;
	}

	private bool TryGetMenuSafeRect(out Rect safe)
	{
		safe = default;
		RectTransform title = FindMenuTitle();
		Button play = FindButton("PlayButton");
		Button maps = FindButton("Maps");
		Button modes = FindButton("Modes");
		if (title == null || play == null || maps == null || modes == null)
			return false;

		if (!TryGetOverlayAabb(title, out Vector2 titleMin, out _)
			|| !TryGetOverlayAabb(play.GetComponent<RectTransform>(), out _, out Vector2 playMax)
			|| !TryGetOverlayAabb(maps.GetComponent<RectTransform>(), out Vector2 mapsMin, out _)
			|| !TryGetOverlayAabb(modes.GetComponent<RectTransform>(), out _, out Vector2 modesMax))
			return false;

		float xMin = mapsMin.x + GuidePlaqueScreenMargin;
		float xMax = modesMax.x - GuidePlaqueScreenMargin;
		float yMin = playMax.y + GuidePlaqueScreenMargin;
		float yMax = titleMin.y - GuidePlaqueScreenMargin;
		if (xMax <= xMin || yMax <= yMin)
			return false;
		safe = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
		return true;
	}

	private bool TryGetOverlayAabb(RectTransform target, out Vector2 min, out Vector2 max)
	{
		min = Vector2.zero;
		max = Vector2.zero;
		if (target == null || overlayRoot == null)
			return false;

		Vector3[] corners = new Vector3[4];
		target.GetWorldCorners(corners);
		min = max = WorldToOverlayLocal(corners[0]);
		for (int i = 1; i < 4; i++)
		{
			Vector2 point = WorldToOverlayLocal(corners[i]);
			min = Vector2.Min(min, point);
			max = Vector2.Max(max, point);
		}
		return true;
	}

	private RectTransform FindMenuTitle()
	{
		MainMenuController menu = MainMenuController.Instance;
		if (menu == null)
			return null;
		RectTransform mobile = TitleFromArray(menu.MainMenu);
		RectTransform desktop = TitleFromArray(menu.DMainMenu);
		if (IsUnderTargetCanvas(mobile))
			return mobile;
		if (IsUnderTargetCanvas(desktop))
			return desktop;
		return mobile != null ? mobile : desktop;
	}

	private static RectTransform TitleFromArray(Text[] texts)
	{
		if (texts == null || texts.Length == 0 || texts[0] == null)
			return null;
		return texts[0].rectTransform;
	}

	private bool IsUnderTargetCanvas(RectTransform rect)
	{
		if (rect == null || targetCanvas == null)
			return false;
		return rect.GetComponentInParent<Canvas>() == targetCanvas;
	}

	private Vector2 WorldToOverlayLocal(Vector3 world)
	{
		if (overlayRoot == null)
			return Vector2.zero;
		Camera cam = targetCanvas != null ? targetCanvas.worldCamera : null;
		Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
		Vector2 local;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screen, cam, out local))
			return Vector2.zero;
		return local;
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
		HideMatchDim();
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
		ShowGuidePlaque(GameTexts.TutorialContinueTitle, GameTexts.TutorialContinueBody);
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
		if (lockUi && allowed == null && ShouldHaveMenuButtonTarget())
		{
			allowed = FindFallbackMenuButton();
			if (allowed == null)
				lockUi = false;
		}
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
			case StageValuteReturn: return FindButton("Return", "PanelOfValute");
			case StageSkins: return FindActivator("PanelOfSkins") ?? FindButton("SkinsShop");
			case StageRotateWhite: return FindRotateNextButton();
			case StageBuyWhite: return FindButton("InnerValute");
			case StageEquipWhite: return FindButton("Equiping");
			case StageSkinsReturn: return FindButton("Return", "PanelOfSkins");
			default: return null;
		}
	}

	private static bool ShouldHaveMenuButtonTarget()
	{
		int stage = YG2.saves.tutorialStage;
		return stage != StageNick
			&& stage != StageMatchCards
			&& stage != StageMatchPlay
			&& stage < StageDone;
	}

	private Button FindFallbackMenuButton()
	{
		return FindButton("Points") ?? FindButton("Modes") ?? FindButton("PlayButton");
	}

	private static void RefreshSelectionCards()
	{
		ModeSelectionUI ui = Object.FindAnyObjectByType<ModeSelectionUI>();
		if (ui != null)
			ui.Refresh();
	}

	private RectTransform ResolveTargetRect()
	{
		if (YG2.saves.tutorialStage == StageNick)
		{
			InputField field = ResolveNickField();
			return field != null ? field.GetComponent<RectTransform>() : null;
		}
		if (YG2.saves.tutorialStage == StageMatchCards)
			return null;
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
		else if (plaqueFollowsPointer && cardRect != null)
		{
			cardRect.anchorMin = new Vector2(0.5f, 0.5f);
			cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);
			Vector2 size = FitGuidePlaqueSize();
			cardRect.sizeDelta = size;
			float scale = GuidePerspectiveScale();
			ApplyGuidePerspectiveScale();
			cardRect.anchoredPosition = ClampPlaquePos(Vector2.zero, size * scale);
		}
	}

	private void UpdatePointer()
	{
		if (pointerRect == null || pointerTarget == null || overlayRoot == null)
			return;

		Vector3[] corners = new Vector3[4];
		pointerTarget.GetWorldCorners(corners);
		Vector3 targetCenter = (corners[0] + corners[2]) * 0.5f;
		Vector2 targetLocal = WorldToOverlayLocal(targetCenter);
		Camera cam = targetCanvas != null ? targetCanvas.worldCamera : null;
		Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, targetCenter);
		bool targetHigh = screen.y > Screen.height * 0.5f;
		Vector2 tipDir = targetHigh ? Vector2.up : Vector2.down;
		pointerRect.anchorMin = new Vector2(0.5f, 0.5f);
		pointerRect.anchorMax = new Vector2(0.5f, 0.5f);
		pointerRect.pivot = new Vector2(0.5f, 0.5f);
		pointerRect.anchoredPosition = targetLocal - tipDir * (PointerOffset * GuidePerspectiveScale());
		float angle = Mathf.Atan2(tipDir.y, tipDir.x) * Mathf.Rad2Deg - 90f;
		pointerRect.localEulerAngles = new Vector3(0f, 0f, angle);
		pointerRect.SetAsLastSibling();
		if (plaqueFollowsPointer)
			LayoutPlaqueAtPointerTail(tipDir);
	}
}
