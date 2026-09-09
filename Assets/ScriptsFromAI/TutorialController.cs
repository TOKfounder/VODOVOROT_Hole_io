using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class TutorialController : MonoBehaviour
{
	private const int MenuCardCount = 4;
	private const float ReplayLeft = 18f;
	private const float ReplayTop = -18f;

	public static TutorialController Instance { get; private set; }

	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private Text fingerText;
	private Text titleText;
	private Text bodyText;
	private Text hintText;
	private Button nextButton;
	private Button skipButton;
	private Text nextLabel;
	private Text skipLabel;
	private Text replayLabel;
	private GameObject replayButtonGo;
	private bool isReplay;
	private bool showIntroAdOnClose;
	private bool menuFlow;
	private bool waitingForEat;
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
			StartCoroutine(OpenMenuNextFrame(false));
	}

	private void BindMatch(Canvas canvas)
	{
		targetCanvas = canvas;
		Instance = this;
		EnsureOverlay();
		SetReplayVisible(false);
		if (YG2.saves.tutorialMenuSeen && !YG2.saves.tutorialMatchSeen)
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

	private IEnumerator OpenMenuNextFrame(bool replay)
	{
		yield return null;
		OpenMenu(replay);
	}

	public void OpenMenu(bool replay)
	{
		menuFlow = true;
		isReplay = replay;
		showIntroAdOnClose = !replay && !YG2.saves.tutorialMenuSeen;
		waitingForEat = false;
		step = 0;
		if (!replay)
			ClearNickFieldsForFirstRun();
		SetReplayVisible(false);
		ShowOverlay(true);
		ApplyCurrentCard();
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
		Image dim = rootGo.GetComponent<Image>();
		dim.color = new Color(0f, 0f, 0f, 0.62f);
		dim.raycastTarget = true;

		GameObject cardGo = new GameObject("TutorialCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		RectTransform card = cardGo.GetComponent<RectTransform>();
		card.anchorMin = new Vector2(0.5f, 0.5f);
		card.anchorMax = new Vector2(0.5f, 0.5f);
		card.pivot = new Vector2(0.5f, 0.5f);
		card.sizeDelta = new Vector2(620f, 360f);
		card.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.96f);
		card.GetComponent<Image>().raycastTarget = true;

		fingerText = CreateLabel(card, "Finger", new Vector2(0f, 132f), new Vector2(80f, 64f), 42);
		titleText = CreateLabel(card, "Title", new Vector2(0f, 72f), new Vector2(560f, 56f), 34);
		bodyText = CreateLabel(card, "Body", new Vector2(0f, -8f), new Vector2(560f, 140f), 22);
		if (titleText != null)
			titleText.fontStyle = FontStyle.Bold;
		if (bodyText != null)
			bodyText.alignment = TextAnchor.UpperCenter;

		nextButton = CreateButton(card, "NextButton", new Vector2(120f, -130f), new Vector2(200f, 56f), new Color(0.28f, 0.72f, 0.32f, 1f), out nextLabel);
		skipButton = CreateButton(card, "SkipButton", new Vector2(-120f, -130f), new Vector2(200f, 56f), new Color(0.35f, 0.35f, 0.4f, 1f), out skipLabel);
		nextButton.onClick.AddListener(OnNext);
		skipButton.onClick.AddListener(OnSkip);

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
			button.onClick.AddListener(() => OpenMenu(true));
			buttonGo = button.gameObject;
			if (canvasGo == targetCanvas.gameObject)
				replayLabel = label;
			else
				label.text = GameTexts.Tutorial;
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

	private void ShowOverlay(bool visible)
	{
		if (overlayRoot != null)
		{
			overlayRoot.gameObject.SetActive(visible);
			if (visible)
				overlayRoot.SetAsLastSibling();
		}
		if (!visible)
			HideEatHint();
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
			nextLabel.text = GameTexts.TutorialNext;
		if (skipLabel != null)
			skipLabel.text = GameTexts.TutorialSkip;
		if (fingerText != null)
			fingerText.text = GameTexts.TutorialFinger;

		SetNickFieldOverOverlay(menuFlow && step == 0);

		if (menuFlow)
		{
			switch (step)
			{
				case 0:
					SetCard(GameTexts.TutorialNickTitle, GameTexts.TutorialNickBody);
					break;
				case 1:
					SetCard(GameTexts.TutorialMoveTitle, GameTexts.TutorialMoveBody);
					break;
				case 2:
					SetCard(GameTexts.TutorialBoostTitle, GameTexts.TutorialBoostBody);
					break;
				default:
					SetCard(GameTexts.TutorialGrowTitle, GameTexts.TutorialGrowBody);
					break;
			}
			return;
		}

		if (waitingForEat)
		{
			SetCard(GameTexts.TutorialEatDoneTitle, GameTexts.TutorialEatDoneBody);
			if (nextLabel != null)
				nextLabel.text = GameTexts.TutorialOk;
			return;
		}

		if (step == 0)
			SetCard(GameTexts.TutorialEatTitle, GameTexts.TutorialEatBody);
		else
			SetCard(GameTexts.TutorialArrowsTitle, GameTexts.TutorialArrowsBody);
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
			if (step == 0 && !TryCommitNick(false))
			{
				if (bodyText != null)
					bodyText.text = GameTexts.TutorialNickNeed;
				return;
			}

			step++;
			if (step >= MenuCardCount)
			{
				FinishMenu(false);
				return;
			}
			ApplyCurrentCard();
			return;
		}

		if (waitingForEat)
		{
			CloseMatchTutorial();
			return;
		}

		if (step == 0 && NeedsArrowsCard())
		{
			step = 1;
			ApplyCurrentCard();
			return;
		}

		BeginEatTask();
	}

	private void OnSkip()
	{
		if (menuFlow)
		{
			if (!isReplay)
				TryCommitNick(true);
			FinishMenu(true);
			return;
		}

		CloseMatchTutorial();
	}

	private void FinishMenu(bool skipped)
	{
		YG2.saves.tutorialMenuSeen = true;
		if (skipped && !isReplay)
			YG2.saves.tutorialMatchSeen = true;
		YG2.SaveProgress();
		RestoreNickField();
		ShowOverlay(false);
		SetReplayVisible(true);
		if (showIntroAdOnClose && !YG2.nowAdsShow)
			YG2.InterstitialAdvShow();
		showIntroAdOnClose = false;
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

	private static string ReadNickFields()
	{
		if (MainMenuController.Instance == null)
			return YG2.saves.nickName;

		if (YG2.envir.isMobile && MainMenuController.Instance.nameInput != null)
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
		if (YG2.envir.isMobile && MainMenuController.Instance.nameInput != null)
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
		step = 0;
		isReplay = false;
		MatchPause.Pause();
		ShowOverlay(true);
		ApplyCurrentCard();
	}

	private static bool NeedsArrowsCard()
	{
		return ModeManager.currentMode == ModeManager.Mode.Boss
			|| ModeManager.currentMode == ModeManager.Mode.Hunting
			|| ModeManager.currentMode == ModeManager.Mode.TeamMode;
	}

	private void BeginEatTask()
	{
		waitingForEat = true;
		ShowOverlay(false);
		MatchPause.Resume();
		if (hintText != null)
		{
			hintText.text = GameTexts.TutorialEatHint;
			hintText.gameObject.SetActive(true);
			hintText.transform.SetAsLastSibling();
		}
	}

	private void OnPlayerScored()
	{
		if (!waitingForEat || overlayRoot == null || overlayRoot.gameObject.activeSelf)
			return;
		if (GamingManager.Instance != null && GamingManager.Instance.HasEnded)
		{
			CloseMatchTutorial();
			return;
		}

		HideEatHint();
		MatchPause.Pause();
		ShowOverlay(true);
		ApplyCurrentCard();
	}

	private void CloseMatchTutorial()
	{
		waitingForEat = false;
		YG2.saves.tutorialMatchSeen = true;
		YG2.SaveProgress();
		ShowOverlay(false);
		MatchPause.Resume();
	}

	private void HideEatHint()
	{
		if (hintText != null)
			hintText.gameObject.SetActive(false);
	}
}
