using UnityEngine;
using UnityEngine.UI;
using YG;

public class EndGameController : MonoBehaviour
{
    [Header("Result Image")]
    public Image resultImage;

    [Header("Mobile UI")]
    public Text MexpText;
    public Text McoinText;
    public Text MbrillText;

    [Header("Desktop UI")]
    public Text expText;
    public Text coinText;
    public Text brillText;

    [Header("Result Sprites")]
    public Sprite[] spritesOfResult;

    // Данные для награды (используется для рекламы x3)
    private int currentCoinIncome;
    private int brillCount;

    private void Awake()
    {
        // Если в будущем понадобится доступ из VodovorotGameManager — раскомментируй
        // if (VodovorotGameManager.Instance != null)
        //     VodovorotGameManager.Instance.EndGameController = this;
    }

    public void ShowRewardedAdv(string rewardID) => YG2.RewardedAdvShow(rewardID);

    private void Start()
    {
        VodovorotGameManager.Instance.GamingManager.EndOfGame();

        int score = YG2.saves.score;
        int allValues = VodovorotGameManager.Instance.GamingManager.AllValues;
        float progress = allValues > 20 ? (float)score / (allValues - 20) : 0f;

        if ((int)(progress * 100) < 100)
        {
            HandleIncompleteGame(progress);
        }
        else
        {
            HandleCompleteGame();
        }
    }

    private void HandleIncompleteGame(float progress)
    {
        resultImage.sprite = null;
        resultImage.color = new Color(1, 1, 1, 0);

        int exp = (int)(50 * progress);
        currentCoinIncome = (int)(13 * progress);
        brillCount = (int)(4 * progress);

        SetResultTexts(exp, currentCoinIncome, brillCount);

        YG2.saves.exp += exp;
        YG2.saves.goldCoins += currentCoinIncome;
        YG2.saves.diamonds += brillCount;

        YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
        VodovorotGameManager.Instance.SaveProgress();
        VodovorotGameManager.Instance.GamingManager.UpdateUI();
    }

    private void HandleCompleteGame()
    {
        float timer = VodovorotGameManager.Instance.GamingManager.timer;

        if (timer <= 360f)
        {
            resultImage.sprite = spritesOfResult[0];
            currentCoinIncome = 30;
            brillCount = 5;
        }
        else if (timer <= 600f)
        {
            resultImage.sprite = spritesOfResult[1];
            currentCoinIncome = 20;
            brillCount = 4;
        }
        else
        {
            resultImage.sprite = spritesOfResult[2];
            currentCoinIncome = 15;
            brillCount = 3;
        }

        YG2.saves.goldCoins += currentCoinIncome;
        YG2.saves.diamonds += brillCount;
        YG2.saves.exp += 50;

        SetResultTexts(50, currentCoinIncome, brillCount);

        YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
        VodovorotGameManager.Instance.SaveProgress();
        VodovorotGameManager.Instance.GamingManager.UpdateUI();
    }

    private void SetResultTexts(int exp, int coins, int diamonds)
    {
        string expStr = $"+{exp}";
        string coinStr = $"+{coins}";
        string diaStr = $"+{diamonds}";

        MexpText.text = expText.text = expStr;
        McoinText.text = coinText.text = coinStr;
        MbrillText.text = brillText.text = diaStr;
    }

    private void OnEnable() => YG2.onRewardAdv += X3ToCoinsForRewarded;
    private void OnDisable() => YG2.onRewardAdv -= X3ToCoinsForRewarded;

    public void X3ToCoinsForRewarded(string id)
    {
        if (id == "1")
        {
            YG2.saves.goldCoins += 2 * currentCoinIncome;
            string tripleStr = $"+{3 * currentCoinIncome}";

            McoinText.text = coinText.text = tripleStr;

            VodovorotGameManager.Instance.SaveProgress();
        }
    }
}