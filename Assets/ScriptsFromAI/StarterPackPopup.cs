using UnityEngine;
using UnityEngine.UI;
using YG;

public class StarterPackPopup : MonoBehaviour
{
	private static bool dismissedThisSession;

	private Canvas targetCanvas;
	private RectTransform overlayRoot;
	private Text titleText;
	private Text bodyText;
	private Text wasText;
	private Text nowText;
	private Text buyLabel;
	private Text laterLabel;

	public static void TryShow()
	{
		if (YG2.saves.adsRemoved || dismissedThisSession || !TutorialController.IsDone)
			return;

		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;

		StarterPackPopup popup = canvas.GetComponent<StarterPackPopup>();
		if (popup == null)
			popup = canvas.gameObject.AddComponent<StarterPackPopup>();
		popup.Bind(canvas);
		popup.Show();
	}

	public static void RefreshIfOpen()
	{
		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return;
		StarterPackPopup popup = canvas.GetComponent<StarterPackPopup>();
		if (popup != null)
			popup.RefreshTexts();
	}

	private void Bind(Canvas canvas)
	{
		targetCanvas = canvas;
		EnsureOverlay();
	}

	private void OnEnable()
	{
		YG2.onPurchaseSuccess += OnPurchased;
	}

	private void OnDisable()
	{
		YG2.onPurchaseSuccess -= OnPurchased;
	}

	private void OnPurchased(string id)
	{
		if (id != MatchRules.StarterPackId)
			return;
		Hide();
	}

	private void EnsureOverlay()
	{
		if (overlayRoot != null || targetCanvas == null)
			return;

		GameObject rootGo = new GameObject("StarterPackOverlay", typeof(RectTransform), typeof(Image));
		rootGo.transform.SetParent(targetCanvas.transform, false);
		overlayRoot = rootGo.GetComponent<RectTransform>();
		overlayRoot.anchorMin = Vector2.zero;
		overlayRoot.anchorMax = Vector2.one;
		overlayRoot.offsetMin = Vector2.zero;
		overlayRoot.offsetMax = Vector2.zero;
		Image dim = rootGo.GetComponent<Image>();
		dim.color = new Color(0f, 0f, 0f, 0.62f);
		dim.raycastTarget = true;

		GameObject cardGo = new GameObject("StarterPackCard", typeof(RectTransform), typeof(Image));
		cardGo.transform.SetParent(overlayRoot, false);
		RectTransform card = cardGo.GetComponent<RectTransform>();
		card.anchorMin = new Vector2(0.5f, 0.5f);
		card.anchorMax = new Vector2(0.5f, 0.5f);
		card.pivot = new Vector2(0.5f, 0.5f);
		card.sizeDelta = new Vector2(620f, 420f);
		ActiveCanvas.ApplyRoundedPanel(cardGo.GetComponent<Image>());

		titleText = ActiveCanvas.CreateText(card, "Title", new Vector2(0f, -28f), new Vector2(560f, 48f));
		if (titleText != null)
		{
			titleText.fontSize = 34;
			titleText.fontStyle = FontStyle.Bold;
		}

		bodyText = ActiveCanvas.CreateText(card, "Body", new Vector2(0f, -92f), new Vector2(560f, 110f));
		if (bodyText != null)
		{
			bodyText.fontSize = 22;
			bodyText.alignment = TextAnchor.UpperCenter;
		}

		wasText = ActiveCanvas.CreateText(card, "WasPrice", new Vector2(0f, -220f), new Vector2(260f, 36f));
		if (wasText != null)
			wasText.fontSize = 24;

		nowText = ActiveCanvas.CreateText(card, "NowPrice", new Vector2(0f, -258f), new Vector2(260f, 44f));
		if (nowText != null)
		{
			nowText.fontSize = 36;
			nowText.fontStyle = FontStyle.Bold;
			nowText.color = new Color(0.45f, 0.95f, 0.4f, 1f);
		}

		Button buy = ActiveCanvas.CreateLabeledButton(
			card, "BuyButton", new Vector2(-130f, -340f), new Vector2(240f, 64f),
			new Color(0.28f, 0.72f, 0.32f, 1f), GameTexts.StarterPackBuy);
		if (buy != null)
		{
			PinToCardTop(buy, new Vector2(-130f, -340f));
			buy.onClick.AddListener(OnBuy);
			buyLabel = buy.GetComponentInChildren<Text>();
		}

		Button later = ActiveCanvas.CreateLabeledButton(
			card, "LaterButton", new Vector2(130f, -340f), new Vector2(240f, 64f),
			new Color(0.35f, 0.35f, 0.38f, 1f), GameTexts.StarterPackLater);
		if (later != null)
		{
			PinToCardTop(later, new Vector2(130f, -340f));
			later.onClick.AddListener(OnLater);
			laterLabel = later.GetComponentInChildren<Text>();
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

	private void Show()
	{
		if (overlayRoot == null)
			return;
		RefreshTexts();
		overlayRoot.SetAsLastSibling();
		overlayRoot.gameObject.SetActive(true);
	}

	private void Hide()
	{
		if (overlayRoot != null)
			overlayRoot.gameObject.SetActive(false);
	}

	private void RefreshTexts()
	{
		if (titleText != null)
			titleText.text = GameTexts.StarterPackTitle;
		if (bodyText != null)
			bodyText.text = GameTexts.StarterPackBody;
		if (wasText != null)
			wasText.text = GameTexts.StarterPackWas;
		if (nowText != null)
			nowText.text = GameTexts.StarterPackNow;
		if (buyLabel != null)
			buyLabel.text = GameTexts.StarterPackBuy;
		if (laterLabel != null)
			laterLabel.text = GameTexts.StarterPackLater;
	}

	private void OnBuy()
	{
		YG2.BuyPayments(MatchRules.StarterPackId);
	}

	private void OnLater()
	{
		dismissedThisSession = true;
		Hide();
	}
}
