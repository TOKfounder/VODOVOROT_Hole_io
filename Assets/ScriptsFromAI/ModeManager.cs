using UnityEngine;
using YG;

public class ModeManager : MonoBehaviour
{
	public static Mode currentMode = Mode.Boss;
	public enum Mode
	{
		Boss, TotalCleaning, Hunting, TeamMode
	}

	private const int GardenMapId = 1;

	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private GameObject mainPlayer;
	[SerializeField] private Transform bossSpawnPoint;
	[SerializeField] private Transform playerSpawnPoint;
	[SerializeField] private float nonGardenBossOffset = 40f;

	void Awake()
	{
		currentMode = (Mode)YG2.saves.chosenMode;

		// Hunting / Team пока не реализованы — откат на Boss
		if (currentMode == Mode.Hunting || currentMode == Mode.TeamMode)
		{
			currentMode = Mode.Boss;
			YG2.saves.chosenMode = (int)Mode.Boss;
		}

		if (currentMode == Mode.Boss)
			StartBossMode();
		else if (currentMode == Mode.TotalCleaning)
			StartCleaningMode();
		else
			print("Not Valid Mode number");
	}

	public void StartBossMode()
	{
		if (enemyPrefab == null || mainPlayer == null)
			return;

		if (IsGardenMap())
		{
			Vector3 enemyPos = bossSpawnPoint != null
				? bossSpawnPoint.position
				: new Vector3(-2.23f, 0.164f, 92.22f);
			Quaternion enemyRot = bossSpawnPoint != null
				? bossSpawnPoint.rotation
				: Quaternion.Euler(0, 180, 0);
			Instantiate(enemyPrefab, enemyPos, enemyRot, transform);

			Vector3 playerPos = playerSpawnPoint != null
				? playerSpawnPoint.position
				: new Vector3(-23.7f, 0.164f, -80.2f);
			mainPlayer.transform.position = playerPos;
			return;
		}

		Vector3 origin = mainPlayer.transform.position;
		Vector3 forward = mainPlayer.transform.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < 0.001f)
			forward = Vector3.forward;
		forward.Normalize();

		Vector3 spawnPos = origin + forward * nonGardenBossOffset;
		spawnPos.y = origin.y;
		Instantiate(enemyPrefab, spawnPos, Quaternion.LookRotation(origin - spawnPos), transform);
	}

	public void StartCleaningMode()
	{
		if (mainPlayer == null)
			return;

		if (!IsGardenMap())
			return;

		Vector3 playerPos = playerSpawnPoint != null
			? playerSpawnPoint.position
			: new Vector3(-23.7f, 0.164f, -80.2f);
		mainPlayer.transform.position = playerPos;
	}

	public void StartHuntingMode() { }

	public void StartTeamModeMode() { }

	private static bool IsGardenMap() => YG2.saves.selectedMapID == GardenMapId;
}
