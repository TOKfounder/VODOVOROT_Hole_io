using UnityEngine;

[CreateAssetMenu(fileName = "ProgressConfig", menuName = "VODOVOROT/Progress Config")]
public class ProgressConfig : ScriptableObject
{
	public const int MaxLevel = 30;
	public const int CityMapId = 0;
	public const int GardenMapId = 1;
	public const int CastleMapId = 2;
	public const int IndustrialMapId = 3;
	public const int FreeMapsMask = (1 << CityMapId) | (1 << GardenMapId);
	public const int FreeModesMask = (1 << (int)ModeManager.Mode.Boss) | (1 << (int)ModeManager.Mode.TotalCleaning);
	public const int AllContentMask = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3);
	public const int ContentLocksVersion = 1;

	[Min(1)] public int maxLevel = MaxLevel;
	[Tooltip("Опыт с уровня i+1 на i+2. Длина maxLevel-1")]
	public int[] expToNext =
	{
		60, 68, 77, 87, 98, 110, 123, 137, 152,
		168, 185, 203, 222, 242, 263, 285, 308, 332,
		357, 383, 410, 438, 467, 497, 528, 560, 593, 627, 662
	};
	[Tooltip("Монеты за достижение уровня. Индекс = уровень")]
	public int[] coinsAtLevel =
	{
		0, 0, 12, 13, 14, 15, 16, 17, 18, 19,
		20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
		30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40
	};
	[Tooltip("Алмазы за достижение уровня. Индекс = уровень")]
	public int[] diamondsAtLevel =
	{
		0, 0, 0, 0, 0, 3, 0, 0, 0, 0,
		4, 0, 0, 0, 0, 5, 0, 0, 0, 0,
		6, 0, 0, 0, 0, 8, 0, 0, 0, 0, 12
	};
	[Min(1)] public int overflowExpPerCoin = 25;
	[Min(1)] public int huntingUnlockLevel = 10;
	[Min(1)] public int castleUnlockLevel = 15;
	[Min(1)] public int teamUnlockLevel = 22;
	[Min(1)] public int industrialUnlockLevel = 30;
}

public class ProgressRuntime
{
	public int maxLevel;
	public int[] expToNext;
	public int[] coinsAtLevel;
	public int[] diamondsAtLevel;
	public int overflowExpPerCoin;
	public int huntingUnlockLevel;
	public int castleUnlockLevel;
	public int teamUnlockLevel;
	public int industrialUnlockLevel;

	public static ProgressRuntime From(ProgressConfig config)
	{
		ProgressRuntime runtime = new ProgressRuntime();
		if (config == null)
		{
			config = ScriptableObject.CreateInstance<ProgressConfig>();
			runtime.CopyFrom(config);
			Object.Destroy(config);
			return runtime;
		}

		runtime.CopyFrom(config);
		return runtime;
	}

	public void CopyFrom(ProgressConfig config)
	{
		maxLevel = Mathf.Max(1, config.maxLevel);
		expToNext = GameBalance.CopyOr(config.expToNext, FallbackExpToNext());
		coinsAtLevel = GameBalance.CopyOr(config.coinsAtLevel, FallbackCoins());
		diamondsAtLevel = GameBalance.CopyOr(config.diamondsAtLevel, FallbackDiamonds());
		overflowExpPerCoin = Mathf.Max(1, config.overflowExpPerCoin);
		huntingUnlockLevel = Mathf.Max(1, config.huntingUnlockLevel);
		castleUnlockLevel = Mathf.Max(1, config.castleUnlockLevel);
		teamUnlockLevel = Mathf.Max(1, config.teamUnlockLevel);
		industrialUnlockLevel = Mathf.Max(1, config.industrialUnlockLevel);
	}

	public int ExpToReach(int level)
	{
		if (level <= 1)
			return 0;
		int cap = Mathf.Min(level - 1, expToNext.Length);
		int sum = 0;
		for (int i = 0; i < cap; i++)
			sum += Mathf.Max(1, expToNext[i]);
		return sum;
	}

	public int GetLevel(int exp)
	{
		exp = Mathf.Max(0, exp);
		int level = 1;
		int spent = 0;
		for (int i = 0; i < expToNext.Length && level < maxLevel; i++)
		{
			int need = Mathf.Max(1, expToNext[i]);
			if (exp < spent + need)
				break;
			spent += need;
			level++;
		}
		return Mathf.Clamp(level, 1, maxLevel);
	}

	public float LevelFill(int exp)
	{
		int level = GetLevel(exp);
		if (level >= maxLevel)
			return 1f;
		int start = ExpToReach(level);
		int need = level - 1 < expToNext.Length ? Mathf.Max(1, expToNext[level - 1]) : 1;
		return Mathf.Clamp01((exp - start) / (float)need);
	}

	public float OverallFill(int exp)
	{
		int top = ExpToReach(maxLevel);
		if (top <= 0)
			return 1f;
		return Mathf.Clamp01(exp / (float)top);
	}

	public int CoinsAt(int level)
	{
		if (coinsAtLevel == null || level < 0 || level >= coinsAtLevel.Length)
			return 0;
		return Mathf.Max(0, coinsAtLevel[level]);
	}

	public int DiamondsAt(int level)
	{
		if (diamondsAtLevel == null || level < 0 || level >= diamondsAtLevel.Length)
			return 0;
		return Mathf.Max(0, diamondsAtLevel[level]);
	}

	private static int[] FallbackExpToNext()
	{
		int[] values = new int[ProgressConfig.MaxLevel - 1];
		for (int i = 0; i < values.Length; i++)
			values[i] = 60 + 8 * i + Mathf.RoundToInt(0.25f * i * i);
		return values;
	}

	private static int[] FallbackCoins()
	{
		int[] values = new int[ProgressConfig.MaxLevel + 1];
		for (int i = 2; i < values.Length; i++)
			values[i] = 10 + i;
		return values;
	}

	private static int[] FallbackDiamonds()
	{
		int[] values = new int[ProgressConfig.MaxLevel + 1];
		values[5] = 3;
		values[10] = 4;
		values[15] = 5;
		values[20] = 6;
		values[25] = 8;
		values[30] = 12;
		return values;
	}
}
