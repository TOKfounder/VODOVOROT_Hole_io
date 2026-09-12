using UnityEngine;
using YG;
using System.Collections.Generic;

[DefaultExecutionOrder(-150)]
public class ModeManager : MonoBehaviour
{
	public static Mode currentMode = Mode.Boss;

	public enum Mode
	{
		Boss, TotalCleaning, Hunting, TeamMode
	}

	private const int CityMapId = 0;
	private const int GardenMapId = 1;
	private const int CastleMapId = 2;

	public static bool IsGardenMap() => YG2.saves.selectedMapID == GardenMapId;
	public static bool IsCityMap() => YG2.saves.selectedMapID == CityMapId;
	public static bool IsCastleMap() => YG2.saves.selectedMapID == CastleMapId;

	public static EnemyController ActiveBoss { get; private set; }
	public static readonly List<EnemyController> HuntingEnemies = new List<EnemyController>();
	public static readonly List<EnemyController> TeamAllies = new List<EnemyController>();
	public static readonly List<EnemyController> TeamEnemies = new List<EnemyController>();
	public static int HuntingSpawned { get; private set; }
	public static int TeamEnemySpawned { get; private set; }

	public const int TeamBlue = 0;
	public const int TeamRed = 1;

	public static int RemainingHunters => CountAlive(HuntingEnemies);
	public static int RemainingTeamEnemies => CountAlive(TeamEnemies);
	public static int RemainingTeamAllies => CountAlive(TeamAllies);

	private static int CountAlive(List<EnemyController> list)
	{
		int count = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && !list[i].IsConsumed)
				count++;
		}
		return count;
	}

	public static int GetTeamScore(int teamId)
	{
		int sum = 0;
		for (int i = 0; i < HoleParent.holeList.Count; i++)
		{
			HoleParent hole = HoleParent.holeList[i];
			if (hole != null && !hole.IsConsumed && hole.TeamId == teamId)
				sum += hole.score;
		}
		return sum;
	}

	public static void ResetModeState()
	{
		ActiveBoss = null;
		HuntingEnemies.Clear();
		HuntingSpawned = 0;
		TeamAllies.Clear();
		TeamEnemies.Clear();
		TeamEnemySpawned = 0;
	}

	public static void ClearActiveBoss() => ActiveBoss = null;

	public static void NotifyEnemyAbsorbed(EnemyController enemy)
	{
		if (enemy == null)
			return;

		if (ActiveBoss == enemy)
		{
			ActiveBoss = null;
			GamingManager.Instance?.OnBossDefeated();
		}

		HuntingEnemies.Remove(enemy);
		TeamAllies.Remove(enemy);
		TeamEnemies.Remove(enemy);
		if (currentMode == Mode.Hunting && RemainingHunters == 0)
			GamingManager.Instance?.OnHuntingComplete();
		if (currentMode == Mode.TeamMode && RemainingTeamEnemies == 0)
			GamingManager.Instance?.OnTeamVictory();
	}

	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private GameObject mainPlayer;
	[SerializeField] private Transform bossSpawnPoint;
	[SerializeField] private Transform playerSpawnPoint;
	[SerializeField] private Vector3 playerSpawnWorld;
	[SerializeField] private float playerSpawnYaw;
	[SerializeField] private Vector3 bossSpawnWorld;
	[SerializeField] private float bossSpawnYaw = 180f;
	[SerializeField] private Transform[] huntingSpawnPoints;
	[SerializeField] private Transform[] teamSpawnPoints;

	[Header("Bounds Spawn")]
	[SerializeField] private float playerEdgeInset = 12f;
	[SerializeField] private float bossEdgeInset = 12f;
	[SerializeField] private float citySpawnHeight = 0.2f;
	[SerializeField] private float gardenSpawnHeight = 0.164f;
	[SerializeField] private float spawnYaw = 0f;

	[Header("Hunting")]
	[SerializeField] private int cityHuntingEnemyCount = 5;
	[SerializeField] private int gardenHuntingEnemyCount = 4;
	[SerializeField] private float huntingSpawnInset = 18f;
	[SerializeField] private float huntingMinPlayerDistance = 22f;

	[Header("Team Mode")]
	[SerializeField] private float teamSideInset = 18f;
	[SerializeField] private Color allyTeamColor = new Color(0.2f, 0.85f, 0.95f, 1f);
	[SerializeField] private Color enemyTeamColor = new Color(0.95f, 0.3f, 0.22f, 1f);

	void Awake()
	{
		GameController.NormalizeChosenMode();
		currentMode = (Mode)YG2.saves.chosenMode;
		ApplyMapConfig();

		if (mainPlayer == null)
			mainPlayer = GameObject.FindGameObjectWithTag("Player");
	}

	private void ApplyMapConfig()
	{
		MapConfig map = GameBalance.Map(YG2.saves.selectedMapID);
		if (map == null)
			return;

		if (map.playerEdgeInset > 0f)
			playerEdgeInset = map.playerEdgeInset;
		if (map.bossEdgeInset > 0f)
			bossEdgeInset = map.bossEdgeInset;
		if (map.huntingSpawnInset > 0f)
			huntingSpawnInset = map.huntingSpawnInset;
		if (map.huntingMinPlayerDistance > 0f)
			huntingMinPlayerDistance = map.huntingMinPlayerDistance;
		if (map.teamSideInset > 0f)
			teamSideInset = map.teamSideInset;
	}

	void Start()
	{
		if (currentMode == Mode.Boss)
			StartBossMode();
		else if (currentMode == Mode.TotalCleaning)
			StartCleaningMode();
		else if (currentMode == Mode.Hunting)
			StartHuntingMode();
		else if (currentMode == Mode.TeamMode)
			StartTeamMode();
		else
			Debug.LogWarning($"Not valid mode: {currentMode}");
	}

	public void StartBossMode()
	{
		if (enemyPrefab == null || mainPlayer == null)
		{
			Debug.LogWarning("ModeManager: enemyPrefab or mainPlayer is not assigned");
			return;
		}

		PlacePlayer();
		SpawnBoss();
	}

	public void StartCleaningMode()
	{
		if (mainPlayer == null)
			return;

		PlacePlayer();
	}

	public void StartHuntingMode()
	{
		if (enemyPrefab == null || mainPlayer == null)
		{
			Debug.LogWarning("ModeManager: enemyPrefab or mainPlayer is not assigned");
			return;
		}

		PlacePlayer();
		HuntingEnemies.Clear();
		HuntingSpawned = 0;

		int count = GetHuntingCount();
		for (int i = 0; i < count; i++)
		{
			if (!TryResolveHuntingSpawn(i, count, out Vector3 pos, out float yaw))
				continue;

			if (TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ))
				yaw = YawTowardMapCenter(pos, minX, maxX, minZ, maxZ);

			Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
			GameObject enemyObject = Instantiate(enemyPrefab, pos, rot, transform);
			EnemyController enemy = enemyObject != null ? enemyObject.GetComponent<EnemyController>() : null;
			if (enemy != null)
			{
				bool ru = YG2.saves.langRu;
				int index = HuntingSpawned + 1;
				enemy.SetNickname(ru ? $"Враг {index}" : $"Enemy {index}");
				HuntingEnemies.Add(enemy);
				HuntingSpawned++;
			}
		}
	}

	private int GetHuntingCount()
	{
		int assigned = CountAssigned(huntingSpawnPoints);
		if (assigned > 0)
			return assigned;
		MapConfig map = GameBalance.Map(YG2.saves.selectedMapID);
		if (map != null && map.huntingEnemyCount > 0)
			return map.huntingEnemyCount;
		return IsGardenMap() ? gardenHuntingEnemyCount : cityHuntingEnemyCount;
	}

	private bool TryResolveHuntingSpawn(int index, int count, out Vector3 pos, out float yaw)
	{
		yaw = spawnYaw;
		if (TryGetAssignedPoint(huntingSpawnPoints, index, out pos, out yaw))
			return true;

		if (!TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ))
		{
			pos = Vector3.zero;
			return false;
		}

		float height = GetSpawnHeight();
		Vector3 playerPos = mainPlayer.transform.position;
		float t = (index + 0.5f) / count * Mathf.PI * 2f;
		float nx = 0.5f + Mathf.Cos(t) * 0.32f;
		float nz = 0.5f + Mathf.Sin(t) * 0.32f;
		pos = ResolveHuntingSpawn(playerPos, height, minX, maxX, minZ, maxZ, nx, nz, t);
		yaw = YawTowardMapCenter(pos, minX, maxX, minZ, maxZ);
		return true;
	}

	private static float YawTowardMapCenter(Vector3 pos, float minX, float maxX, float minZ, float maxZ)
	{
		Vector3 toCenter = new Vector3((minX + maxX) * 0.5f - pos.x, 0f, (minZ + maxZ) * 0.5f - pos.z);
		if (toCenter.sqrMagnitude < 0.0001f)
			return 0f;
		return Quaternion.LookRotation(toCenter.normalized).eulerAngles.y;
	}

	private Vector3 ResolveHuntingSpawn(
		Vector3 playerPos,
		float height,
		float minX,
		float maxX,
		float minZ,
		float maxZ,
		float nx,
		float nz,
		float angle)
	{
		float x = Mathf.Lerp(minX + huntingSpawnInset, maxX - huntingSpawnInset, nx);
		float z = Mathf.Lerp(minZ + huntingSpawnInset, maxZ - huntingSpawnInset, nz);
		Vector3 pos = new Vector3(x, height, z);
		pos = PushAwayFromPlayer(pos, playerPos, height, minX, maxX, minZ, maxZ);

		for (int k = 0; k < 8; k++)
		{
			Vector3 delta = pos - playerPos;
			delta.y = 0f;
			if (delta.magnitude >= huntingMinPlayerDistance * 0.95f)
				break;

			float extra = angle + (k + 1) * 45f * Mathf.Deg2Rad;
			Vector3 dir = new Vector3(Mathf.Cos(extra), 0f, Mathf.Sin(extra));
			pos = playerPos + dir * huntingMinPlayerDistance;
			pos.y = height;
			pos.x = Mathf.Clamp(pos.x, minX + huntingSpawnInset, maxX - huntingSpawnInset);
			pos.z = Mathf.Clamp(pos.z, minZ + huntingSpawnInset, maxZ - huntingSpawnInset);
		}

		return pos;
	}

	private Vector3 PushAwayFromPlayer(Vector3 pos, Vector3 playerPos, float height, float minX, float maxX, float minZ, float maxZ)
	{
		Vector3 fromPlayer = pos - playerPos;
		fromPlayer.y = 0f;
		if (fromPlayer.sqrMagnitude < 0.0001f)
			fromPlayer = Vector3.forward;
		if (fromPlayer.magnitude < huntingMinPlayerDistance)
			pos = playerPos + fromPlayer.normalized * huntingMinPlayerDistance;

		pos.y = height;
		pos.x = Mathf.Clamp(pos.x, minX + huntingSpawnInset, maxX - huntingSpawnInset);
		pos.z = Mathf.Clamp(pos.z, minZ + huntingSpawnInset, maxZ - huntingSpawnInset);
		return pos;
	}

	public void StartTeamMode()
	{
		if (enemyPrefab == null || mainPlayer == null)
		{
			Debug.LogWarning("ModeManager: enemyPrefab or mainPlayer is not assigned");
			return;
		}

		PlacePlayer();
		TeamAllies.Clear();
		TeamEnemies.Clear();
		TeamEnemySpawned = 0;

		bool ru = YG2.saves.langRu;
		HoleParent playerHole = mainPlayer.GetComponent<HoleParent>();
		if (playerHole != null)
			playerHole.ApplyTeamVisuals(TeamBlue, allyTeamColor, null);

		if (CountAssigned(teamSpawnPoints) >= 5)
		{
			TryGetAssignedPoint(teamSpawnPoints, 0, out Vector3 a1, out float a1y);
			TryGetAssignedPoint(teamSpawnPoints, 1, out Vector3 a2, out float a2y);
			TryGetAssignedPoint(teamSpawnPoints, 2, out Vector3 e1, out float e1y);
			TryGetAssignedPoint(teamSpawnPoints, 3, out Vector3 e2, out float e2y);
			TryGetAssignedPoint(teamSpawnPoints, 4, out Vector3 e3, out float e3y);
			SpawnTeamHole(a1, a1y, TeamBlue, allyTeamColor, ru ? "Союзник 1" : "Ally 1", TeamAllies);
			SpawnTeamHole(a2, a2y, TeamBlue, allyTeamColor, ru ? "Союзник 2" : "Ally 2", TeamAllies);
			SpawnTeamHole(e1, e1y, TeamRed, enemyTeamColor, ru ? "Враг 1" : "Enemy 1", TeamEnemies);
			SpawnTeamHole(e2, e2y, TeamRed, enemyTeamColor, ru ? "Враг 2" : "Enemy 2", TeamEnemies);
			SpawnTeamHole(e3, e3y, TeamRed, enemyTeamColor, ru ? "Враг 3" : "Enemy 3", TeamEnemies);
			TeamEnemySpawned = RemainingTeamEnemies;
			return;
		}

		if (!TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ))
			return;

		float height = GetSpawnHeight();
		float centerX = (minX + maxX) * 0.5f;
		float leftX = minX + teamSideInset;
		float rightX = maxX - teamSideInset;
		float southZ = minZ + playerEdgeInset;
		float northZ = maxZ - bossEdgeInset;
		float southYaw = spawnYaw;
		float northYaw = spawnYaw + 180f;

		SpawnTeamHole(new Vector3(leftX, height, southZ), southYaw, TeamBlue, allyTeamColor, ru ? "Союзник 1" : "Ally 1", TeamAllies);
		SpawnTeamHole(new Vector3(rightX, height, southZ), southYaw, TeamBlue, allyTeamColor, ru ? "Союзник 2" : "Ally 2", TeamAllies);
		SpawnTeamHole(new Vector3(leftX, height, northZ), northYaw, TeamRed, enemyTeamColor, ru ? "Враг 1" : "Enemy 1", TeamEnemies);
		SpawnTeamHole(new Vector3(centerX, height, northZ), northYaw, TeamRed, enemyTeamColor, ru ? "Враг 2" : "Enemy 2", TeamEnemies);
		SpawnTeamHole(new Vector3(rightX, height, northZ), northYaw, TeamRed, enemyTeamColor, ru ? "Враг 3" : "Enemy 3", TeamEnemies);
		TeamEnemySpawned = RemainingTeamEnemies;
	}

	private void SpawnTeamHole(
		Vector3 position,
		float yaw,
		int teamId,
		Color color,
		string nick,
		List<EnemyController> list)
	{
		Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
		GameObject enemyObject = Instantiate(enemyPrefab, position, rot, transform);
		EnemyController enemy = enemyObject != null ? enemyObject.GetComponent<EnemyController>() : null;
		if (enemy == null)
			return;

		ApplySpawnTransform(enemy.transform, position, yaw);
		enemy.ApplyTeamVisuals(teamId, color, nick);
		list.Add(enemy);
	}

	private void PlacePlayer()
	{
		if (TryGetPlayerSpawn(out Vector3 playerPos, out float yaw))
		{
			ApplySpawnTransform(mainPlayer.transform, playerPos, yaw);
			return;
		}

		Debug.LogWarning("ModeManager: could not resolve player spawn position");
	}

	private void SpawnBoss()
	{
		if (!TryGetBossSpawn(out Vector3 bossPos, out float yaw))
		{
			Debug.LogWarning("ModeManager: could not resolve boss spawn position");
			return;
		}

		Quaternion bossRot = Quaternion.Euler(0f, yaw, 0f);
		RegisterBoss(Instantiate(enemyPrefab, bossPos, bossRot, transform));
		if (ActiveBoss != null)
		{
			bool ru = YG2.saves.langRu;
			ActiveBoss.SetNickname(ru ? "Босс" : "Boss");
		}
	}

	private bool TryGetPlayerSpawn(out Vector3 pos, out float yaw)
	{
		if (TryGetAssignedPoint(new[] { playerSpawnPoint }, 0, out pos, out yaw))
			return true;
		if (playerSpawnWorld.sqrMagnitude > 0.01f)
		{
			pos = playerSpawnWorld;
			yaw = playerSpawnYaw;
			return true;
		}
		if (TryGetBoundsSpawn(out pos, out _))
		{
			yaw = spawnYaw;
			return true;
		}
		yaw = spawnYaw;
		return false;
	}

	private bool TryGetBossSpawn(out Vector3 pos, out float yaw)
	{
		if (TryGetAssignedPoint(new[] { bossSpawnPoint }, 0, out pos, out yaw))
			return true;
		if (bossSpawnWorld.sqrMagnitude > 0.01f)
		{
			pos = bossSpawnWorld;
			yaw = bossSpawnYaw;
			return true;
		}
		if (TryGetBoundsSpawn(out _, out pos))
		{
			yaw = spawnYaw;
			return true;
		}
		yaw = spawnYaw;
		return false;
	}

	private static int CountAssigned(Transform[] points)
	{
		if (points == null)
			return 0;
		int count = 0;
		for (int i = 0; i < points.Length; i++)
		{
			if (points[i] != null)
				count++;
		}
		return count;
	}

	private static bool TryGetAssignedPoint(Transform[] points, int index, out Vector3 pos, out float yaw)
	{
		pos = Vector3.zero;
		yaw = 0f;
		if (points == null || index < 0 || index >= points.Length || points[index] == null)
			return false;
		pos = points[index].position;
		yaw = points[index].eulerAngles.y;
		return true;
	}

	private bool TryGetBoundsSpawn(out Vector3 playerPos, out Vector3 bossPos)
	{
		playerPos = Vector3.zero;
		bossPos = Vector3.zero;

		if (!TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ))
			return false;

		float centerX = (minX + maxX) * 0.5f;
		float playerZ = minZ + playerEdgeInset;
		float bossZ = maxZ - bossEdgeInset;
		float spawnHeight = GetSpawnHeight();

		playerPos = new Vector3(centerX, spawnHeight, playerZ);
		bossPos = new Vector3(centerX, spawnHeight, bossZ);
		return true;
	}

	private bool TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ)
	{
		minX = maxX = minZ = maxZ = 0f;
		if (GamingManager.Instance == null)
			return false;

		minX = GamingManager.Instance.minX;
		maxX = GamingManager.Instance.maxX;
		minZ = GamingManager.Instance.minZ;
		maxZ = GamingManager.Instance.maxZ;
		return minX < maxX && minZ < maxZ;
	}

	private float GetSpawnHeight()
	{
		MapConfig map = GameBalance.Map(YG2.saves.selectedMapID);
		if (map != null)
			return map.spawnHeight;
		return IsGardenMap() ? gardenSpawnHeight : citySpawnHeight;
	}

	private static void ApplySpawnTransform(Transform target, Vector3 position, float yaw)
	{
		if (target == null)
			return;

		target.position = position;
		target.rotation = Quaternion.Euler(0f, yaw, 0f);

		HoleParent hole = target.GetComponent<HoleParent>();
		if (hole != null && hole.WithoutCamera != null)
			hole.WithoutCamera.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
	}

	private void RegisterBoss(GameObject bossObject)
	{
		ActiveBoss = bossObject != null ? bossObject.GetComponent<EnemyController>() : null;
	}
}
