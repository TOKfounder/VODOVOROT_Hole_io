using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class ModeManager : MonoBehaviour
{
	public static Mode currentMode = Mode.Boss;
	public enum Mode
	{
		Boss, TotalCleaning, Hunting, TeamMode
	}
	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private GameObject playerPrefab;

	void Awake()
	{
		currentMode = (Mode)YG2.saves.chosenMode;
		if (currentMode == Mode.Boss)
		{
			StartBossMode();
		} else if (currentMode == Mode.TotalCleaning)
		{
			StartCleaningMode();
		} else if (currentMode == Mode.Hunting)
		{
			StartHuntingMode();
		} else if (currentMode == Mode.TeamMode)
		{
			StartTeamModeMode();
		} else
		{
			print("Not Valid Mode number");
		}
	}

	public void StartBossMode()
	{
		GameObject bossEnemy = (GameObject) Instantiate(enemyPrefab);
		bossEnemy.transform.position = new Vector3(-2.23f, 0.164f, 92.22f);
		bossEnemy.transform.Rotate(new Vector3(0, 180, 0), Space.Self);
		HoleParent bossHole = bossEnemy.GetComponent<HoleParent>();
		bossHole.GetComponent<Renderer>().material = GameController.Instance.materials[2];
		
		GameObject player = (GameObject) Instantiate(playerPrefab);
		player.transform.position = new Vector3(-23.7f, 0.164f, -80.2f);
	}
	public void StartCleaningMode()
	{
		return;	
	}
	public void StartHuntingMode()
	{
		return;	
	}
	public void StartTeamModeMode()
	{
		return;	
	}
}
