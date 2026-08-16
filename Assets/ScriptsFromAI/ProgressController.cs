using UnityEngine;
using UnityEngine.UI;
using YG;

public class ProgressController : MonoBehaviour
{
	public Image progressFill;
	private const int ExpPerLevel = 100;
	private const int MaxDisplayLevel = 10;

	void OnEnable()
	{
		if (progressFill == null)
			return;

		float levelProgress = (YG2.saves.exp % ExpPerLevel) / (float)ExpPerLevel;
		// Бар общего прогресса до max уровня (0..10)
		float overall = Mathf.Clamp01(YG2.saves.exp / (float)(ExpPerLevel * MaxDisplayLevel));
		progressFill.fillAmount = overall > 0f ? overall : levelProgress;
	}
}
