using UnityEngine;

[CreateAssetMenu(fileName = "MapConfig", menuName = "VODOVOROT/Map Config")]
public class MapConfig : ScriptableObject
{
	public int huntingEnemyCount = 5;
	public float spawnHeight = 0.2f;
	public float playerEdgeInset = 12f;
	public float bossEdgeInset = 12f;
	public float huntingSpawnInset = 18f;
	public float huntingMinPlayerDistance = 22f;
	public float teamSideInset = 18f;
}
