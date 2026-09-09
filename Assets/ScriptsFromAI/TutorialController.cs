using System.Collections;
using PinePie.SimpleJoystick;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class TutorialController : MonoBehaviour
{
	private const int MatchCardCount = 4;
	private const float ReplayLeft = 18f;
	private const float ReplayTop = -18f;
	private const float PointerOffset = 92f;
	private static readonly Color PointerTint = new Color(1f, 0.85f, 0.2f, 1f);

	public static TutorialController Instance { get; private set; }
	public static bool ReplayMatchTutorial { get; set; }

	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private RectTransform cardRect;
	private Image dimImage;
	private RectTransform pointerRect;
	private RectTransform pointerTarget;
	private Text titleText;
	private Text bodyText;
	private Text hintText;
	private Button nextButton;
	private Button skipButton;
	private Text nextLabel;
	private Text skipLabel;
	private Text replayLabel;
	private GameObject replayButtonGo;
	private Vector2 skipCardPos;
	private bool menuFlow;
	private bool waitingForEat;
	private bool showingDone;
	private int step;
	private Coroutine matchRoutine;
	private InputField promotedNick;
	private Transform nickFieldHomeParent;
	private int nickFieldHomeIndex = -1;

	public static void NotifyPlayerScored()
	{
		if (Instance != null)
			Instance.OnPlayerScored();
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

	public void RefreshTexts()
	{
		if (replayLabel != null)
			replayLabel.text = GameTexts.Tutorial;
		if (overlayRoot == null || !overlayRoot.gameObject.activeSelf)
			return;
		ApplyCurrentCard();
	}

	private void BindMenu(Canvas canvas)
	{
		targetCanvas = canvas;
		Instance = this;
		EnsureOverlay();
		EnsureReplayButton(GameController.Instance != null ? GameController.Instance.CanvasForMobile : null);
		EnsureReplayButton(GameController.Instance != null ? GameController.Instance.CanvasForDesktop : null);
		EnsureReplayButton(canvas.gameObject);
		SetReplayVisible(YG2.saves.tutorialMenuSeen);
		if (!YG2.saves.tutorialMenuSeen)
			StartCoroutine(OpenMenuNextFrame());
	}

	private void BindMatch(Canvas canvas)
	{
		targetCanvas = canvas;
		Instance = this;
		EnsureOverlay();
		SetReplayVisible(false);
		bool replay = ReplayMatchTutorial;
		ReplayMatchTutorial = false;
		if (replay || (YG2.saves.tutorialMenuSeen && !YG2.saves.tutorialMatchSeen))
		{
			if (matchRoutine != null)
				StopCoroutine(matchRoutine);
			matchRoutine = StartCoroutine(RunMatchTutorial());
		}
	}

	void OnDestroy()
	{
		RestoreNickField();
		if (Instance == this)
			Instance = null;
	}

	private void ReleaseForCanvasSwitch()
	{
		RestoreNickField();
		HideEatHint();
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
		if (Instance == this)
			Instance = null;
	}

	void LateUpdate()
	{
		RefreshLivePointerTarget();
		if (pointerRect != null && pointerRect.gameObject.activeSelf)
			UpdatePointer();
	}

	private void RefreshLivePointerTarget()
	{
		if (menuFlow || waitingForEat || showingDone || overlayRoot == null || !overlayRoot.gameObject.activeSelf)
			return;
		if (step != 3 || cardRect == null || !cardRect.gameObject.activeSelf)
			return;

		MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
		if (hud == null)
			return;
		RectTransform target = IsCleaningMode() ? hud.FirstActiveArrow() : hud.MinimapRect;
		if (target != pointerTarget)
			PointAt(target);
	}

	private IEnumerator OpenMenuNextFrame()
	{
		yield return null;
		OpenNickCard();
	}

	private void OpenNickCard()
	{
		menuFlow = true;
		waitingForEat = false;
		showingDone = false;
		step = 0;
		ClearNickFieldsForFirstRun();
		SetReplayVisible(false);
		ShowCardOverlay();
		ApplyCurrentCard();
	}

	private void OnReplayClicked()
	{
		ReplayMatchTutorial = true;
		if (GameController.Instance != null)
			GameController.Instance.StartGame();
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
		dimImage = rootGo.GetComponent<Image>();
		dimImage.color = new Color(0f, 0f, 0f, 0.62f);
		dimImage.raycastTarget = true;

		GameObject cardGo = new GameObject("TutorialCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		cardRect = cardGo.GetComponent<RectTransform>();
		cardRect.anchorMin = new Vector2(0.5f, 0.5f);
		cardRect.anchorMax = new Vector2(0.5f, 0.5f);
		cardRect.pivot = new Vector2(0.5f, 0.5f);
		cardRect.sizeDelta = new Vector2(620f, 320f);
		cardGo.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.96f);
		cardGo.GetComponent<Image>().raycastTarget = true;

		titleText = CreateLabel(cardRect, "Title", new Vector2(0f, 92f), new Vector2(560f, 56f), 34);
		bodyText = CreateLabel(cardRect, "Body", new Vector2(0f, 8f), new Vector2(560f, 140f), 22);
		if (titleText != null)
			titleText.fontStyle = FontStyle.Bold;
		if (bodyText != null)
			bodyText.alignment = TextAnchor.UpperCenter;

		nextButton = CreateButton(cardRect, "NextButton", new Vector2(130f, -110f), new Vector2(220f, 56f), new Color(0.28f, 0.72f, 0.32f, 1f), out nextLabel);
		skipButton = CreateButton(cardRect, "SkipButton", new Vector2(-130f, -110f), new Vector2(220f, 56f), new Color(0.35f, 0.35f, 0.4f, 1f), out skipLabel);
		skipCardPos = new Vector2(-130f, -110f);
		nextButton.onClick.AddListener(OnNext);
		skipButton.onClick.AddListener(OnSkip);

		pointerRect = CreatePointer(overlayRoot);
		hintText = ActiveCanvas.CreateText(targetCanvas.transform, "TutorialEatHint", new Vector2(0f, -150f), new Vector2(640f, 48f));
		if (hintText != null)
		{
			hintText.fontSize = 26;
			hintText.gameObject.SetActive(false);
		}

		overlayRoot.gameObject.SetActive(false);
		if (font != null)
			ActiveCanvas.ApplyUiFontEverywhere();
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

	private void EnsureReplayButton(GameObject canvasGo)
	{
		if (canvasGo == null)
			return;

		Transform existing = canvasGo.transform.Find("TutorialReplayButton");
		GameObject buttonGo = existing != null ? existing.gameObject : null;
		if (buttonGo == null)
		{
			Text label;
			Button button = CreateButton(
				canvasGo.transform,
				"TutorialReplayButton",
				new Vector2(ReplayLeft, ReplayTop),
				new Vector2(200f, 52f),
				new Color(0.2f, 0.22f, 0.28f, 0.92f),
				out label);
			RectTransform rect = button.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(ReplayLeft, ReplayTop);
			buttonGo = button.gameObject;
			if (canvasGo == targetCanvas.gameObject)
				replayLabel = label;
			else
				label.text = GameTexts.Tutorial;
		}

		Button replayButton = buttonGo.GetComponent<Button>();
		if (replayButton != null)
		{
			replayButton.onClick.RemoveAllListeners();
			replayButton.onClick.AddListener(OnReplayClicked);
		}

		if (canvasGo == targetCanvas.gameObject)
		{
			replayButtonGo = buttonGo;
			if (replayLabel == null)
				replayLabel = buttonGo.GetComponentInChildren<Text>();
			if (replayLabel != null)
				replayLabel.text = GameTexts.Tutorial;
		}
	}

	private static Text CreateLabel(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize)
	{
		Text text = ActiveCanvas.CreateText(parent, name, pos, size);
		if (text == null)
			return null;
		RectTransform rect = text.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
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
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
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

	private void ShowCardOverlay()
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		overlayRoot.SetAsLastSibling();
		if (dimImage != null)
		{
			dimImage.color = new Color(0f, 0f, 0f, 0.62f);
			dimImage.raycastTarget = true;
		}
		if (cardRect != null)
			cardRect.gameObject.SetActive(true);
		if (nextButton != null)
			nextButton.gameObject.SetActive(true);
		PlaceSkipOnCard();
		HideEatHint();
	}

	private void ShowPracticeHud()
	{
		if (overlayRoot == null)
			return;
		overlayRoot.gameObject.SetActive(true);
		overlayRoot.SetAsLastSibling();
		if (dimImage != null)
		{
			dimImage.color = new Color(0f, 0f, 0f, 0f);
			dimImage.raycastTarget = false;
		}
		if (cardRect != null)
			cardRect.gameObject.SetActive(false);
		if (nextButton != null)
			nextButton.gameObject.SetActive(false);
		PlaceSkipForPractice();
		HidePointer();
		if (hintText != null)
		{
			hintText.text = GameTexts.TutorialEatHint;
			hintText.gameObject.SetActive(true);
			hintText.transform.SetAsLastSibling();
		}
	}

	private void HideOverlay()
	{
		RestoreNickField();
		HidePointer();
		HideEatHint();
		PlaceSkipOnCard();
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
	}

	private void PlaceSkipOnCard()
	{
		if (skipButton == null || cardRect == null)
			return;
		RectTransform rect = skipButton.GetComponent<RectTransform>();
		rect.SetParent(cardRect, false);
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = skipCardPos;
		skipButton.gameObject.SetActive(true);
	}

	private void PlaceSkipForPractice()
	{
		if (skipButton == null || overlayRoot == null)
			return;
		RectTransform rect = skipButton.GetComponent<RectTransform>();
		rect.SetParent(overlayRoot, false);
		rect.anchorMin = new Vector2(1f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(1f, 1f);
		rect.anchoredPosition = new Vector2(-18f, -18f);
		skipButton.gameObject.SetActive(true);
	}

	private void SetReplayVisible(bool visible)
	{
		if (GameController.Instance == null)
		{
			if (replayButtonGo != null)
				replayButtonGo.SetActive(visible);
			return;
		}

		SetReplayOnCanvas(GameController.Instance.CanvasForMobile, visible);
		SetReplayOnCanvas(GameController.Instance.CanvasForDesktop, visible);
	}

	private static void SetReplayOnCanvas(GameObject canvasGo, bool visible)
	{
		if (canvasGo == null)
			return;
		Transform replay = canvasGo.transform.Find("TutorialReplayButton");
		if (replay != null)
			replay.gameObject.SetActive(visible);
	}

	private void ApplyCurrentCard()
	{
		if (nextLabel != null)
			nextLabel.text = showingDone ? GameTexts.TutorialOk : GameTexts.TutorialNext;
		if (skipLabel != null)
			skipLabel.text = GameTexts.TutorialSkip;

		SetNickFieldOverOverlay(menuFlow);
		if (promotedNick != null)
			promotedNick.transform.SetAsLastSibling();

		if (menuFlow)
		{
			SetCard(GameTexts.TutorialNickTitle, GameTexts.TutorialNickBody);
			PointAt(ResolveNickField() != null ? ResolveNickField().GetComponent<RectTransform>() : null);
			if (promotedNick != null)
				promotedNick.transform.SetAsLastSibling();
			return;
		}

		if (showingDone)
		{
			SetCard(GameTexts.TutorialEatDoneTitle, GameTexts.TutorialEatDoneBody);
			HidePointer();
			return;
		}

		switch (step)
		{
			case 0:
				SetCard(GameTexts.TutorialMoveTitle, GameTexts.TutorialMoveBody);
				PointAt(FindJoystick());
				break;
			case 1:
				SetCard(GameTexts.TutorialBoostTitle, GameTexts.TutorialBoostBody);
				PointAt(FindBoost());
				break;
			case 2:
				SetCard(GameTexts.TutorialEatTitle, GameTexts.TutorialEatBody);
				HidePointer();
				break;
			default:
				if (IsCleaningMode())
				{
					SetCard(GameTexts.TutorialArrowsTitle, GameTexts.TutorialArrowsBody);
					MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
					PointAt(hud != null ? hud.FirstActiveArrow() : null);
				}
				else
				{
					SetCard(GameTexts.TutorialMapTitle, GameTexts.TutorialMapBody);
					MatchHud hud = Object.FindAnyObjectByType<MatchHud>();
					PointAt(hud != null ? hud.MinimapRect : null);
				}
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

	private void OnNext()
	{
		if (menuFlow)
		{
			if (!TryCommitNick(false))
			{
				if (bodyText != null)
					bodyText.text = GameTexts.TutorialNickNeed;
				return;
			}
			FinishMenu(false);
			return;
		}

		if (showingDone)
		{
			CloseMatchTutorial();
			return;
		}

		step++;
		if (step >= MatchCardCount)
		{
			BeginEatTask();
			return;
		}
		ApplyCurrentCard();
	}

	private void OnSkip()
	{
		if (menuFlow)
		{
			TryCommitNick(true);
			FinishMenu(true);
			return;
		}

		CloseMatchTutorial();
	}

	private void FinishMenu(bool skipped)
	{
		YG2.saves.tutorialMenuSeen = true;
		if (skipped)
			YG2.saves.tutorialMatchSeen = true;
		YG2.SaveProgress();
		HideOverlay();
		SetReplayVisible(true);
	}

	private bool TryCommitNick(bool allowLegend)
	{
		string nick = ReadNickFields();
		if (string.IsNullOrWhiteSpace(nick))
		{
			if (!allowLegend)
				return false;
			nick = GameTexts.LegendNick;
		}

		if (MainMenuController.Instance != null)
			MainMenuController.Instance.ApplyNick(nick);
		else
		{
			YG2.saves.nickName = nick;
			YG2.saves.isNickGiven = true;
			YG2.SaveProgress();
		}
		return true;
	}

	private static bool UseMobileNickField()
	{
		if (GameController.Instance != null)
			return GameController.Instance.IsMobileCanvasActive();
		return YG2.envir.isMobile;
	}

	private static string ReadNickFields()
	{
		if (MainMenuController.Instance == null)
			return YG2.saves.nickName;

		if (UseMobileNickField() && MainMenuController.Instance.nameInput != null)
			return MainMenuController.Instance.nameInput.text;
		if (MainMenuController.Instance.DnameInput != null)
			return MainMenuController.Instance.DnameInput.text;
		if (MainMenuController.Instance.nameInput != null)
			return MainMenuController.Instance.nameInput.text;
		return YG2.saves.nickName;
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

	private void SetNickFieldOverOverlay(bool over)
	{
		if (!over)
		{
			RestoreNickField();
			return;
		}

		InputField field = ResolveNickField();
		if (field == null || overlayRoot == null)
			return;
		if (promotedNick == field)
		{
			field.transform.SetAsLastSibling();
			return;
		}

		RestoreNickField();
		promotedNick = field;
		nickFieldHomeParent = field.transform.parent;
		nickFieldHomeIndex = field.transform.GetSiblingIndex();
		field.transform.SetParent(overlayRoot, true);
		field.transform.SetAsLastSibling();
	}

	private void RestoreNickField()
	{
		if (promotedNick == null)
			return;
		if (nickFieldHomeParent != null)
		{
			promotedNick.transform.SetParent(nickFieldHomeParent, true);
			int maxIndex = nickFieldHomeParent.childCount - 1;
			if (nickFieldHomeIndex >= 0 && maxIndex >= 0)
				promotedNick.transform.SetSiblingIndex(Mathf.Clamp(nickFieldHomeIndex, 0, maxIndex));
		}
		promotedNick = null;
		nickFieldHomeParent = null;
		nickFieldHomeIndex = -1;
	}

	private static void ClearNickFieldsForFirstRun()
	{
		if (YG2.saves.tutorialMenuSeen || MainMenuController.Instance == null)
			return;
		if (MainMenuController.Instance.nameInput != null)
			MainMenuController.Instance.nameInput.text = "";
		if (MainMenuController.Instance.DnameInput != null)
			MainMenuController.Instance.DnameInput.text = "";
	}

	private IEnumerator RunMatchTutorial()
	{
		while (BlackHoleController.Player == null)
			yield return null;
		while (BlackHoleController.Player.IsBirthIntro)
			yield return null;
		if (GamingManager.Instance != null && GamingManager.Instance.HasEnded)
			yield break;

		menuFlow = false;
		waitingForEat = false;
		showingDone = false;
		step = 0;
		SetMatchClockFrozen(true);
		MatchPause.Pause();
		ShowCardOverlay();
		ApplyCurrentCard();
	}

	private static bool IsCleaningMode()
	{
		return ModeManager.currentMode == ModeManager.Mode.TotalCleaning;
	}

	private void BeginEatTask()
	{
		waitingForEat = true;
		showingDone = false;
		MatchPause.Resume();
		SetMatchClockFrozen(true);
		ShowPracticeHud();
	}

	private void OnPlayerScored()
	{
		if (!waitingForEat || showingDone)
			return;
		if (GamingManager.Instance != null && GamingManager.Instance.HasEnded)
		{
			CloseMatchTutorial();
			return;
		}

		waitingForEat = false;
		showingDone = true;
		HideEatHint();
		SetMatchClockFrozen(true);
		MatchPause.Pause();
		ShowCardOverlay();
		ApplyCurrentCard();
	}

	private void CloseMatchTutorial()
	{
		waitingForEat = false;
		showingDone = false;
		YG2.saves.tutorialMatchSeen = true;
		YG2.SaveProgress();
		SetMatchClockFrozen(false);
		HideOverlay();
		MatchPause.Resume();
	}

	private static void SetMatchClockFrozen(bool frozen)
	{
		if (GamingManager.Instance != null)
			GamingManager.Instance.SetMatchClockFrozen(frozen);
	}

	private void HideEatHint()
	{
		if (hintText != null)
			hintText.gameObject.SetActive(false);
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
		JoystickController[] sticks = Object.FindObjectsByType<JoystickController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
