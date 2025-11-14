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
[Header("Desktop UI")]
	public Text expText;
	public Text coinText;

	public Sprite[] spritesOfResult;

	private int currentCoinIncome;
	void Awake()
	{
		Instance = this;
	}

	public void ShowRewardedAdv(string rewardID) => YG2.RewardedAdvShow(rewardID);

	void Start()
	{
		GamingManager.Instance.EndOfGame();
		if ((int)((float)YG2.saves.score / (GamingManager.Instance.AllValues - 20) * 100) < 100)
		{
			resultImage.sprite = null;
			resultImage.color = new Color(1, 1, 1, 0);
			float koef = (float)YG2.saves.score / (GamingManager.Instance.AllValues - 20);
			int exp = (int)(50 * koef);
			currentCoinIncome = (int)(13 * koef);
			MexpText.text = $"+{exp}";
			expText.text = $"+{exp}";
			YG2.saves.exp += exp;
			YG2.saves.goldCoins += currentCoinIncome;
			McoinText.text = $"+{currentCoinIncome}";
			coinText.text = $"+{currentCoinIncome}";
			YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
			YG2.SaveProgress();
			GamingManager.Instance.UpdateUI();
			return;
		}
		if (GamingManager.Instance.timer <= 360f)
		{
			resultImage.sprite = spritesOfResult[0];
			currentCoinIncome = 27;
		}
		else if (GamingManager.Instance.timer <= 600f)
		{
			resultImage.sprite = spritesOfResult[1];
			currentCoinIncome = 20;
		}
		else
		{
			resultImage.sprite = spritesOfResult[2];
			currentCoinIncome = 13;
		}
		YG2.saves.goldCoins += currentCoinIncome;
		MexpText.text = "+50";
		expText.text = "+50";
		McoinText.text = $"+{currentCoinIncome}";
		coinText.text = $"+{currentCoinIncome}";
		YG2.saves.exp += 50;
		YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
		GamingManager.Instance.UpdateUI();
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
		//Показать рекламу
		if (id == "1")
		{
			YG2.saves.goldCoins += 2 * currentCoinIncome;
			McoinText.text = $"+{3 * currentCoinIncome}";
			coinText.text = $"+{3 * currentCoinIncome}";
			YG2.SaveProgress();
		}
	}

}
