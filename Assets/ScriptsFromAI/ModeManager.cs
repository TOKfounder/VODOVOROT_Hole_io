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

	public static bool IsGardenMap() => YG2.saves.selectedMapID == GardenMapId;
	public static bool IsCityMap() => YG2.saves.selectedMapID == CityMapId;

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

	[Header("Bounds Spawn")]
	[SerializeField] private float playerEdgeInset = 12f;
	[SerializeField] private float bossEdgeInset = 12f;
	[SerializeField] private float citySpawnHeight = 0.2f;
	[SerializeField] private float gardenSpawnHeight = 0.164f;
	[SerializeField] private float spawnYaw = 0f;

	[Header("Hunting")]
	[SerializeField] private int huntingEnemyCount = 6;
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

		if (mainPlayer == null)
			mainPlayer = GameObject.FindGameObjectWithTag("Player");
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

		if (!TryGetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ))
			return;

		float height = GetSpawnHeight();
		Vector3 playerPos = mainPlayer.transform.position;
		int count = Mathf.Max(1, huntingEnemyCount);
		for (int i = 0; i < count; i++)
		{
			float t = (i + 0.5f) / count * Mathf.PI * 2f;
			float nx = 0.5f + Mathf.Cos(t) * 0.32f;
			float nz = 0.5f + Mathf.Sin(t) * 0.32f;
			Vector3 pos = ResolveHuntingSpawn(playerPos, height, minX, maxX, minZ, maxZ, nx, nz, t);
			Quaternion rot = Quaternion.Euler(0f, spawnYaw, 0f);
			GameObject enemyObject = Instantiate(enemyPrefab, pos, rot, transform);
			EnemyController enemy = enemyObject != null ? enemyObject.GetComponent<EnemyController>() : null;
			if (enemy != null)
			{
				HuntingEnemies.Add(enemy);
				HuntingSpawned++;
			}
		}
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

		ApplySpawnTransform(mainPlayer.transform, new Vector3(centerX, height, southZ), southYaw);

		HoleParent playerHole = mainPlayer.GetComponent<HoleParent>();
		if (playerHole != null)
			playerHole.ApplyTeamVisuals(TeamBlue, allyTeamColor, null);

		SpawnTeamHole(new Vector3(leftX, height, southZ), southYaw, TeamBlue, allyTeamColor, "Ally", TeamAllies);
		SpawnTeamHole(new Vector3(rightX, height, southZ), southYaw, TeamBlue, allyTeamColor, "Ally", TeamAllies);
		SpawnTeamHole(new Vector3(leftX, height, northZ), northYaw, TeamRed, enemyTeamColor, "Enemy", TeamEnemies);
		SpawnTeamHole(new Vector3(centerX, height, northZ), northYaw, TeamRed, enemyTeamColor, "Enemy", TeamEnemies);
		SpawnTeamHole(new Vector3(rightX, height, northZ), northYaw, TeamRed, enemyTeamColor, "Enemy", TeamEnemies);
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
		if (playerSpawnPoint != null)
		{
			ApplySpawnTransform(mainPlayer.transform, playerSpawnPoint.position, playerSpawnPoint.eulerAngles.y);
			return;
		}

		if (TryGetBoundsSpawn(out Vector3 playerPos, out Vector3 bossPos))
		{
			ApplySpawnTransform(mainPlayer.transform, playerPos, spawnYaw);
			return;
		}

		Debug.LogWarning("ModeManager: could not resolve player spawn position");
	}

	private void SpawnBoss()
	{
		Vector3 bossPos;
		float bossYaw;

		if (bossSpawnPoint != null)
		{
			bossPos = bossSpawnPoint.position;
			bossYaw = bossSpawnPoint.eulerAngles.y;
		}
		else if (TryGetBoundsSpawn(out Vector3 playerPos, out bossPos))
		{
			bossYaw = spawnYaw;
		}
		else
		{
			Debug.LogWarning("ModeManager: could not resolve boss spawn position");
			return;
		}

		Quaternion bossRot = Quaternion.Euler(0f, bossYaw, 0f);
		RegisterBoss(Instantiate(enemyPrefab, bossPos, bossRot, transform));
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

	private float GetSpawnHeight() => IsGardenMap() ? gardenSpawnHeight : citySpawnHeight;

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
