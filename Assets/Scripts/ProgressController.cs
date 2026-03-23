using UnityEngine;
using UnityEngine.UI;
using YG;

public class ProgressController : MonoBehaviour
{
	public Image progressFill;
	private int maxOfExp = 1000;

	void OnEnable()
	{
		progressFill.fillAmount = (float)YG2.saves.exp / maxOfExp;
	}
}
