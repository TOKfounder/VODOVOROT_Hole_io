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
		new SkinDef { speedMul = 1.08f, startMul = 1.05f, necessaryLevel = 4, costCoins = 270, costDonate = 10 },
		new SkinDef { speedMul = 1.12f, startMul = 1.08f, necessaryLevel = 7, costCoins = 800, costDonate = 40 },
		new SkinDef { speedMul = 1.15f, startMul = 1.1f, necessaryLevel = 10, costCoins = 2400, costDonate = 100 }
	};

	public SkinDef Get(int index)
	{
		if (skins == null || skins.Length == 0)
			return null;
		index = Mathf.Clamp(index, 0, skins.Length - 1);
		return skins[index];
	}
}
