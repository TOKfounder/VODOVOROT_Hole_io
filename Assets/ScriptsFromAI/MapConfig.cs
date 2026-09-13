using UnityEngine;

[CreateAssetMenu(fileName = "MapConfig", menuName = "VODOVOROT/Map Config")]
public class MapConfig : ScriptableObject
{
	public int huntingEnemyCount = 3;
	public int teamAllyCount = 1;
	public int teamEnemyCount = 2;
	public float spawnHeight = 0.2f;
	public float playerEdgeInset = 8f;
	public float bossEdgeInset = 8f;
	public float huntingSpawnInset = 12f;
	public float huntingMinPlayerDistance = 14f;
	public float teamSideInset = 12f;
}
