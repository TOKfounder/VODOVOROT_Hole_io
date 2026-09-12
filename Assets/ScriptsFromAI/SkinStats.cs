using UnityEngine;
using YG;

public static class SkinStats
{
	private static readonly float[] FallbackSpeedMul = { 1f, 1.04f, 1.08f, 1.12f, 1.15f };
	private static readonly float[] FallbackStartMul = { 1f, 1.02f, 1.05f, 1.08f, 1.1f };

	private static SkinDef[] Catalog
	{
		get
		{
			SkinCatalog catalog = GameBalance.Skins;
			if (catalog != null && catalog.skins != null && catalog.skins.Length > 0)
				return catalog.skins;
			return null;
		}
	}

	private static float[] SpeedMul
	{
		get
		{
			GameBalanceConfig config = GameBalance.Current;
			if (config != null && config.skinSpeedMul != null && config.skinSpeedMul.Length > 0)
				return config.skinSpeedMul;
			return FallbackSpeedMul;
		}
	}

	private static float[] StartMul
	{
		get
		{
			GameBalanceConfig config = GameBalance.Current;
			if (config != null && config.skinStartMul != null && config.skinStartMul.Length > 0)
				return config.skinStartMul;
			return FallbackStartMul;
		}
	}

	public static int Count
	{
		get
		{
			SkinDef[] defs = Catalog;
			if (defs != null)
				return defs.Length;
			return SpeedMul.Length;
		}
	}

	public static int EquippedIndex => Mathf.Clamp(YG2.saves.equipedMaterial, 0, Mathf.Max(0, Count - 1));

	public static float SpeedMultiplier => SpeedAt(EquippedIndex);

	public static float StartScaleMultiplier => StartAt(EquippedIndex);

	public static float VfxMultiplier => SpeedMultiplier;

	public static int SpeedBonusPercent(int index)
	{
		return Mathf.RoundToInt((SpeedAt(index) - 1f) * 100f);
	}

	public static int StartBonusPercent(int index)
	{
		return Mathf.RoundToInt((StartAt(index) - 1f) * 100f);
	}

	private static float SpeedAt(int index)
	{
		SkinDef[] defs = Catalog;
		if (defs != null)
		{
			index = Mathf.Clamp(index, 0, defs.Length - 1);
			return defs[index].speedMul;
		}

		float[] mul = SpeedMul;
		index = Mathf.Clamp(index, 0, mul.Length - 1);
		return mul[index];
	}

	private static float StartAt(int index)
	{
		SkinDef[] defs = Catalog;
		if (defs != null)
		{
			index = Mathf.Clamp(index, 0, defs.Length - 1);
			return defs[index].startMul;
		}

		float[] mul = StartMul;
		index = Mathf.Clamp(index, 0, mul.Length - 1);
		return mul[index];
	}
}
