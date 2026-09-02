using UnityEngine;

public class ModePanelRefreshTrigger : MonoBehaviour
{
	void OnEnable()
	{
		if (GameController.Instance != null)
			GameController.Instance.RefreshModeSelectionUI();
	}
}
