using UnityEngine;
using YG;

public static class SkinStats
{
	private static readonly float[] SpeedMul = { 1f, 1.04f, 1.08f, 1.12f, 1.15f };
	private static readonly float[] StartMul = { 1f, 1.02f, 1.05f, 1.08f, 1.1f };

	public static int EquippedIndex => Mathf.Clamp(YG2.saves.equipedMaterial, 0, SpeedMul.Length - 1);

	public static float SpeedMultiplier => SpeedMul[EquippedIndex];

	public static float StartScaleMultiplier => StartMul[EquippedIndex];

	public static float VfxMultiplier => SpeedMul[EquippedIndex];
}