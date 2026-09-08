using UnityEngine;
using YG;

public static class MatchPause
{
	public static bool IsPaused { get; private set; }

	public static void ForceReset()
	{
		IsPaused = false;
		Time.timeScale = 1f;
	}

	public static void Pause()
	{
		if (!YG2.saves.isGaming || GamingManager.Instance == null || GamingManager.Instance.HasEnded)
			return;

		IsPaused = true;
		Time.timeScale = 0f;
		if (!YG2.nowAdsShow)
			YG2.GameplayStop();
	}

	public static void Resume()
	{
		if (!YG2.saves.isGaming || GamingManager.Instance == null || GamingManager.Instance.HasEnded)
			return;

		IsPaused = false;
		Time.timeScale = 1f;
		if (!YG2.nowAdsShow)
			YG2.GameplayStart();
	}
}

public class SettingsPauseHook : MonoBehaviour
{
	public static bool IgnoreEnable;

	void OnEnable()
	{
		if (IgnoreEnable)
			return;
		MatchPause.Pause();
	}

	void OnDisable()
	{
		MatchPause.Resume();
	}
}