using UnityEngine;

[CreateAssetMenu(fileName = "ModeConfig", menuName = "VODOVOROT/Mode Config")]
public class ModeConfig : ScriptableObject
{
	[Header("Timer")]
	public float duration = 75f;
	public float overtimeDuration;

	[Header("Boss")]
	public int bossStartLevel = 2;
	public float bossFarmSeconds = 30f;
	public float bossProbeSeconds = 120f;
	public float bossSpeedMul = 0.56f;
	public float leftoverSecondsPerBonus = 20f;
	public int leftoverSpriteHigh = 8;
	public int leftoverSpriteMid = 3;

	[Header("Hunting AI")]
	public float huntSightRadius = 45f;
	public float huntPlayerSeconds = 10f;
	public float farmAfterHuntSeconds = 18f;
	public float enemySpeedMul = 0.25f;

	[Header("Win reward")]
	public int winExp = 50;
	public int winCoins = 25;
	public int winDiamonds = 5;

	[Header("Hunting rewards")]
	public int jackpotExp = 60;
	public int jackpotCoins = 40;
	public int jackpotDiamonds = 8;
	public float partialExp = 50f;
	public float partialCoins = 30f;
	public float partialDiamonds = 6f;
	public int partialExpMin = 8;
	public int partialCoinsMin = 5;
	public int partialDiamondsMin = 1;
	public float goodSpriteProgress = 0.7f;

	[Header("Cleaning rewards")]
	public float lowThreshold = 0.5f;
	public float midThreshold = 0.7f;
	public float lowCoins = 15f;
	public float lowExp = 25f;
	public int midExp = 35;
	public int midCoins = 20;
	public int midDiamonds = 4;
	public int highExp = 50;
	public int highCoins = 25;
	public int highDiamonds = 5;
}
