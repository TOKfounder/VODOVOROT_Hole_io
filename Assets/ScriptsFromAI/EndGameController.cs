using UnityEngine;
using UnityEngine.UI;
using YG;

public class EndGameController : MonoBehaviour
{
	public static EndGameController Instance;
	public Image resultImage;
[Header("Mobile UI")]
	public Image MresultImage;
	public Text MexpText;
	public Text McoinText;
	public Text MbrillText;
[Header("Desktop UI")]
	public Image DresultImage;
	public Text expText;
	public Text coinText;
	public Text brillText;

	public Sprite[] spritesOfResult;

	private int currentCoinIncome;
	private int brillCount;
	private bool x3Claimed;

	void Awake()
	{
		Instance = this;
	}

	public void ShowRewardedAdv(string rewardID)
	{
		if (x3Claimed)
			return;
		YG2.RewardedAdvShow(rewardID);
	}

	void Start()
	{
		GamingManager gamingManager = GamingManager.Instance;
		if (gamingManager == null)
			return;

		gamingManager.EndOfGame();

		GamingManager.MatchRewardData reward = gamingManager.GetCurrentClassicReward();
		currentCoinIncome = reward.coins;
		brillCount = reward.diamonds;
		x3Claimed = false;

		ApplyResultSprite(reward.resultSpriteIndex);
		SetResultTexts(reward.exp, reward.coins, reward.diamonds);

		if (!gamingManager.HasRewardBeenApplied)
			gamingManager.ApplyMatchReward(reward);

		gamingManager.UpdateUI();
		ApplyVerdict(gamingManager);
	}

	private void ApplyVerdict(GamingManager gamingManager)
	{
		gamingManager.GetEndVerdict(out string title, out string reason, out Color color);
		ApplyVerdictToPanel(gamingManager.PanelOfEnd, title, color);
		ApplyVerdictToPanel(gamingManager.DPanelOfEnd, title, color);
		EnsureReasonText(gamingManager.MobpanelOfEnd, title, reason, color);
		EnsureReasonText(gamingManager.DeskpanelOfEnd, title, reason, color);
	}

	private static void ApplyVerdictToPanel(Text[] panel, string title, Color color)
	{
		if (panel == null || panel.Length < 2 || panel[1] == null)
			return;
		panel[1].text = title;
		panel[1].color = color;
		panel[1].fontSize = Mathf.Max(panel[1].fontSize, 44);
		panel[1].fontStyle = FontStyle.Bold;
	}

	private void EnsureReasonText(GameObject panel, string title, string reason, Color color)
	{
		if (panel == null)
			return;

		Transform titleTransform = panel.transform.Find("VerdictTitle");
		Text titleText = titleTransform != null ? titleTransform.GetComponent<Text>() : null;
		if (titleText == null)
		{
			titleText = ActiveCanvas.CreateText(panel.transform, "VerdictTitle", new Vector2(0f, 210f), new Vector2(720f, 72f));
			if (titleText != null)
			{
				titleText.fontSize = 52;
				titleText.fontStyle = FontStyle.Bold;
			}
		}
		if (titleText != null)
		{
			titleText.text = title;
			titleText.color = color;
		}

		Transform reasonTransform = panel.transform.Find("VerdictReason");
		Text reasonText = reasonTransform != null ? reasonTransform.GetComponent<Text>() : null;
		if (reasonText == null)
		{
			reasonText = ActiveCanvas.CreateText(panel.transform, "VerdictReason", new Vector2(0f, 150f), new Vector2(760f, 56f));
			if (reasonText != null)
				reasonText.fontSize = 28;
		}
		if (reasonText != null)
		{
			reasonText.text = reason;
			reasonText.color = Color.white;
		}
	}

	private void ApplyResultSprite(int spriteIndex)
	{
		ApplyResultSpriteTo(resultImage, spriteIndex);
		ApplyResultSpriteTo(MresultImage, spriteIndex);
		ApplyResultSpriteTo(DresultImage, spriteIndex);

		GamingManager gamingManager = GamingManager.Instance;
		ApplyResultSpriteToPanel(gamingManager != null ? gamingManager.MobpanelOfEnd : null, spriteIndex);
		ApplyResultSpriteToPanel(gamingManager != null ? gamingManager.DeskpanelOfEnd : null, spriteIndex);
	}

	private void ApplyResultSpriteToPanel(GameObject panel, int spriteIndex)
	{
		if (panel == null)
			return;

		Image[] images = panel.GetComponentsInChildren<Image>(true);
		for (int i = 0; i < images.Length; i++)
		{
			if (images[i] != null && images[i].name.Contains("Result"))
				ApplyResultSpriteTo(images[i], spriteIndex);
		}
	}

	private void ApplyResultSpriteTo(Image image, int spriteIndex)
	{
		if (image == null)
			return;

		if (spriteIndex < 0 || spritesOfResult == null || spritesOfResult.Length == 0)
		{
			image.sprite = null;
			image.color = new Color(1, 1, 1, 0);
			return;
		}

		int safeIndex = Mathf.Clamp(spriteIndex, 0, spritesOfResult.Length - 1);
		image.sprite = spritesOfResult[safeIndex];
		image.color = Color.white;
	}

	private void SetResultTexts(int exp, int coins, int diamonds)
	{
		string expStr = $"+{exp}";
		string coinStr = $"+{coins}";
		string diaStr = $"+{diamonds}";

		if (MexpText != null) MexpText.text = expStr;
		if (expText != null) expText.text = expStr;
		if (McoinText != null) McoinText.text = coinStr;
		if (coinText != null) coinText.text = coinStr;
		if (MbrillText != null) MbrillText.text = diaStr;
		if (brillText != null) brillText.text = diaStr;
	}

	private void OnEnable()
	{
		YG2.onRewardAdv += X3ToCoinsForRewarded;
	}

	private void OnDisable()
	{
		YG2.onRewardAdv -= X3ToCoinsForRewarded;
	}

	public void X3ToCoinsForRewarded(string id)
	{
		if (id != "1" || x3Claimed)
			return;

		x3Claimed = true;
		YG2.saves.goldCoins += 2 * currentCoinIncome;
		string tripleStr = $"+{3 * currentCoinIncome}";
		if (McoinText != null) McoinText.text = tripleStr;
		if (coinText != null) coinText.text = tripleStr;
		YG2.SaveProgress();
	}
}
