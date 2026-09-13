using UnityEngine;

[System.Serializable]
public class SkinDef
{
	public float speedMul = 1f;
	public float startMul = 1f;
	public int necessaryLevel;
	public int costCoins;
	public int costDonate;
}

[CreateAssetMenu(fileName = "SkinCatalog", menuName = "VODOVOROT/Skin Catalog")]
public class SkinCatalog : ScriptableObject
{
	public SkinDef[] skins =
	{
		new SkinDef { speedMul = 1f, startMul = 1f, necessaryLevel = 0, costCoins = 0, costDonate = 0 },
		new SkinDef { speedMul = 1.04f, startMul = 1.02f, necessaryLevel = 1, costCoins = 20, costDonate = 10000 },
		new SkinDef { speedMul = 1.08f, startMul = 1.05f, necessaryLevel = 2, costCoins = 150, costDonate = 10 },
		new SkinDef { speedMul = 1.12f, startMul = 1.08f, necessaryLevel = 4, costCoins = 400, costDonate = 40 },
		new SkinDef { speedMul = 1.15f, startMul = 1.1f, necessaryLevel = 6, costCoins = 900, costDonate = 100 }
	};

	public SkinDef Get(int index)
	{
		if (skins == null || skins.Length == 0)
			return null;
		index = Mathf.Clamp(index, 0, skins.Length - 1);
		return skins[index];
	}

	public int GetNextLockedIndex(int[] obtained)
	{
		if (skins == null)
			return -1;
		int count = skins.Length;
		if (obtained != null)
			count = Mathf.Min(count, obtained.Length);
		for (int i = 0; i < count; i++)
		{
			if (obtained == null || obtained[i] == 0)
				return i;
		}
		return -1;
	}

	public bool CanBuy(int index, int level, int coins)
	{
		SkinDef def = Get(index);
		if (def == null)
			return false;
		return level >= def.necessaryLevel && coins >= def.costCoins;
	}
}
