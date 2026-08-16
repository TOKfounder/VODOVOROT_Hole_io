using UnityEngine;
using UnityEngine.UI;
using YG;

public class EndGameController : MonoBehaviour
{
	public static EndGameController Instance;
	public Image resultImage;
[Header("Mobile UI")]
	public Text MexpText;
	public Text McoinText;
	public Text MbrillText;
[Header("Desktop UI")]
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
	}

	private void ApplyResultSprite(int spriteIndex)
	{
		if (resultImage == null)
			return;

		if (spriteIndex < 0 || spritesOfResult == null || spritesOfResult.Length == 0)
		{
			resultImage.sprite = null;
			resultImage.color = new Color(1, 1, 1, 0);
			return;
		}

		int safeIndex = Mathf.Clamp(spriteIndex, 0, spritesOfResult.Length - 1);
		resultImage.sprite = spritesOfResult[safeIndex];
		resultImage.color = Color.white;
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
