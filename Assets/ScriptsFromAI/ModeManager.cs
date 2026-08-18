using UnityEngine;
using YG;

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

	public static void ResetModeState() => ActiveBoss = null;
	public static void ClearActiveBoss() => ActiveBoss = null;

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

		if (GamingManager.Instance == null)
			return false;

		float minX = GamingManager.Instance.minX;
		float maxX = GamingManager.Instance.maxX;
		float minZ = GamingManager.Instance.minZ;
		float maxZ = GamingManager.Instance.maxZ;

		if (minX >= maxX || minZ >= maxZ)
			return false;

		float centerX = (minX + maxX) * 0.5f;
		float playerZ = minZ + playerEdgeInset;
		float bossZ = maxZ - bossEdgeInset;
		float spawnHeight = IsGardenMap() ? gardenSpawnHeight : citySpawnHeight;

		playerPos = new Vector3(centerX, spawnHeight, playerZ);
		bossPos = new Vector3(centerX, spawnHeight, bossZ);
		return true;
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

	public void StartHuntingMode() { }

	public void StartTeamModeMode() { }
}
