using UnityEngine;
using UnityEngine.UI;
using YG;

public class ReviveOffer : MonoBehaviour
{
	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private Text titleText;
	private Text bodyText;
	private Text watchLabel;
	private Text giveUpLabel;
	private bool waitingReward;

	public static ReviveOffer Ensure()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return null;

		ReviveOffer offer = canvas.GetComponent<ReviveOffer>();
		if (offer == null)
			offer = canvas.gameObject.AddComponent<ReviveOffer>();
		offer.Bind(canvas);
		return offer;
	}

	private void Bind(Canvas canvas)
	{
		targetCanvas = canvas;
		EnsureOverlay();
	}

	private void OnEnable()
	{
		YG2.onRewardAdv += OnReward;
	}

	private void OnDisable()
	{
		YG2.onRewardAdv -= OnReward;
	}

	public void Show()
	{
		if (overlayRoot == null)
			return;
		waitingReward = false;
		RefreshTexts();
		overlayRoot.SetAsLastSibling();
		overlayRoot.gameObject.SetActive(true);
	}

	public void Hide()
	{
		waitingReward = false;
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
	}

	private void EnsureOverlay()
	{
		if (overlayRoot != null || targetCanvas == null)
			return;

		GameObject rootGo = new GameObject("ReviveOverlay", typeof(RectTransform), typeof(Image));
		rootGo.transform.SetParent(targetCanvas.transform, false);
		overlayRoot = rootGo.GetComponent<RectTransform>();
		overlayRoot.anchorMin = Vector2.zero;
		overlayRoot.anchorMax = Vector2.one;
		overlayRoot.offsetMin = Vector2.zero;
		overlayRoot.offsetMax = Vector2.zero;
		Image dim = rootGo.GetComponent<Image>();
		dim.color = new Color(0f, 0f, 0f, 0.7f);
		dim.raycastTarget = true;

		GameObject cardGo = new GameObject("ReviveCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		RectTransform card = cardGo.GetComponent<RectTransform>();
		card.anchorMin = new Vector2(0.5f, 0.5f);
		card.anchorMax = new Vector2(0.5f, 0.5f);
		card.pivot = new Vector2(0.5f, 0.5f);
		card.sizeDelta = new Vector2(580f, 320f);
		ActiveCanvas.ApplyRoundedPanel(cardGo.GetComponent<Image>());

		titleText = ActiveCanvas.CreateText(card, "Title", new Vector2(0f, -28f), new Vector2(520f, 52f));
		if (titleText != null)
		{
			titleText.fontSize = 36;
			titleText.fontStyle = FontStyle.Bold;
			titleText.color = new Color(1f, 0.45f, 0.3f, 1f);
		}

		bodyText = ActiveCanvas.CreateText(card, "Body", new Vector2(0f, -96f), new Vector2(520f, 80f));
		if (bodyText != null)
			bodyText.fontSize = 22;

		Button watch = ActiveCanvas.CreateLabeledButton(
			card, "WatchButton", new Vector2(-130f, -250f), new Vector2(240f, 64f),
			new Color(0.28f, 0.72f, 0.32f, 1f), GameTexts.ReviveWatch);
		if (watch != null)
		{
			PinToCardTop(watch, new Vector2(-130f, -250f));
			watch.onClick.AddListener(OnWatch);
			watchLabel = watch.GetComponentInChildren<Text>();
		}

		Button giveUp = ActiveCanvas.CreateLabeledButton(
			card, "GiveUpButton", new Vector2(130f, -250f), new Vector2(240f, 64f),
			new Color(0.55f, 0.25f, 0.22f, 1f), GameTexts.ReviveGiveUp);
		if (giveUp != null)
		{
			PinToCardTop(giveUp, new Vector2(130f, -250f));
			giveUp.onClick.AddListener(OnGiveUp);
			giveUpLabel = giveUp.GetComponentInChildren<Text>();
		}

		overlayRoot.gameObject.SetActive(false);
	}

	private static void PinToCardTop(Button button, Vector2 pos)
	{
		if (button == null)
			return;
		RectTransform rect = button.GetComponent<RectTransform>();
		if (rect == null)
			return;
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = pos;
	}

	private void RefreshTexts()
	{
		if (titleText != null)
			titleText.text = GameTexts.ReviveTitle;
		if (bodyText != null)
			bodyText.text = GameTexts.ReviveBody;
		if (watchLabel != null)
			watchLabel.text = GameTexts.ReviveWatch;
		if (giveUpLabel != null)
			giveUpLabel.text = GameTexts.ReviveGiveUp;
	}

	private void OnWatch()
	{
		if (waitingReward)
			return;
		waitingReward = true;
		YG2.RewardedAdvShow(MatchRules.ReviveRewardId);
	}

	private void OnGiveUp()
	{
		Hide();
		GamingManager.Instance?.OnReviveDeclined();
	}

	private void OnReward(string id)
	{
		if (id != MatchRules.ReviveRewardId || !waitingReward)
			return;
		waitingReward = false;
		Hide();
		GamingManager.Instance?.OnReviveAccepted();
	}
}
