using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class ModeManager : MonoBehaviour
{
	public Mode currentMode = Mode.Boss;
	public enum Mode
	{
		Boss, TotalCleaning, Hunting, TeamMode
	}
	[SerializeField] private GameObject enemyPrefab;
	[SerializeField] private GameObject mainPlayer;

	void Awake()
	{
		if (VodovorotGameManager.Instance != null)
    		VodovorotGameManager.Instance.ModeManager = this;
			
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
		if (enemyPrefab == null) return;
		
		Instantiate(enemyPrefab, new Vector3(-2.23f, 0.164f, 92.22f), 
		Quaternion.Euler(0, 180, 0), transform);
		mainPlayer.transform.position = new Vector3(-23.7f, 0.164f, -80.2f);

		// Можно добавить красивую анимацию с передвижением камеры
		// Только после этого вновь запустить воспроизведение
	}
	public void StartCleaningMode()
	{
		mainPlayer.transform.position = new Vector3(-23.7f, 0.164f, -80.2f);
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
