using UnityEngine;

[CreateAssetMenu(fileName = "GameBalance", menuName = "VODOVOROT/Game Balance")]
public class GameBalanceConfig : ScriptableObject
{
	[Header("Growth")]
	public float[] scoreRequired =
	{
		0, 44, 128, 293, 624, 1209, 2243, 3998, 6825, 10725, 15990
	};

	public float[] levelScales =
	{
		0.41f, 0.45f, 0.62f, 1.12f, 2f, 3.34f, 5.11f, 7.57f, 11.48f, 17.06f, 18.7f
	};

	public float[] levelSpeeds =
	{
		3.6f, 4.134f, 4.668f, 5.202f, 5.736f, 6.264f, 8.298f, 9.132f, 12f, 15f, 16.8f
	};

	public float scaleLerpSpeed = 1.15f;
	public float birthLerpSpeed = 5f;

	[Header("Skins fallback")]
	public float[] skinSpeedMul = { 1f, 1.04f, 1.08f, 1.12f, 1.15f };
	public float[] skinStartMul = { 1f, 1.02f, 1.05f, 1.08f, 1.1f };

	[Header("Catalog")]
	public SkinCatalog skins;
	public ModeConfig boss;
	public ModeConfig cleaning;
	public ModeConfig hunting;
	public ModeConfig team;
	public MapConfig city;
	public MapConfig garden;
	public MapConfig castle;
	public MapConfig industrial;

	[Header("Suction")]
	public float suctionMassMin = 0.4f;
	public float suctionMassMax = 3f;
	public float suctionDrag = 0.8f;
	public float suctionPull = 13f;
	public float suctionDownForce = 17f;
	public float suctionOrbit = 10f;
	public float suctionOrbitInward = 4f;
	public float suctionOrbitDown = 6f;
	[Range(0.2f, 1f)] public float suctionRimFactor = 0.55f;
	public float suctionSqueezeSpeed = 1.2f;
	[Min(0.05f)]
	[Tooltip("Множитель радиуса дыры, с которого объекты начинают всасываться")]
	public float suctionAttractRadius = 1.1f;
	[Min(0.05f)]
	[Tooltip("Высота подъёма при захвате в долях диаметра дыры")]
	public float suctionLiftDiameterFactor = 0.3f;

	[Header("Hole absorb")]
	public float absorbSizeMul = 1.15f;
	public float absorbCenterFactor = 0.55f;

	[Header("Defeat reward")]
	public float defeatExp = 25f;
	public float defeatCoins = 12f;
	public float defeatDiamonds = 3f;
	public int defeatExpMin = 5;
	public int defeatCoinsMin = 3;
	public int defeatDiamondsMin = 0;

	public ModeConfig Mode(ModeManager.Mode mode)
	{
		switch (mode)
		{
			case ModeManager.Mode.Boss:
				return boss;
			case ModeManager.Mode.TotalCleaning:
				return cleaning;
			case ModeManager.Mode.Hunting:
				return hunting;
			case ModeManager.Mode.TeamMode:
				return team;
			default:
				return null;
		}
	}

	public MapConfig Map(int mapId)
	{
		if (mapId == 1)
			return garden != null ? garden : city;
		if (mapId == 2)
			return castle != null ? castle : city;
		if (mapId == 3)
			return industrial != null ? industrial : city;
		return city;
	}
}

public static class GameBalance
{
	private static GameBalanceConfig cached;

	public static GameBalanceConfig Current
	{
		get
		{
			if (cached == null)
				cached = Resources.Load<GameBalanceConfig>("GameBalance");
			return cached;
		}
	}

	public static ModeConfig Mode(ModeManager.Mode mode)
	{
		return Current != null ? Current.Mode(mode) : null;
	}

	public static MapConfig Map(int mapId)
	{
		return Current != null ? Current.Map(mapId) : null;
	}

	public static SkinCatalog Skins => Current != null ? Current.skins : null;

	public static float[] CopyOr(float[] source, float[] fallback)
	{
		if (source == null || source.Length == 0)
			return fallback;
		return (float[])source.Clone();
	}

	public static int[] CopyOr(int[] source, int[] fallback)
	{
		if (source == null || source.Length == 0)
			return fallback;
		return (int[])source.Clone();
	}
}
