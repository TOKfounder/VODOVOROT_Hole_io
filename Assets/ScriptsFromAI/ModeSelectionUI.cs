using UnityEngine;
using UnityEngine.UI;
using YG;

public class ModeSelectionUI : MonoBehaviour
{
	[SerializeField] private Color selectedColor = new Color(0.35f, 0.85f, 0.45f, 1f);
	[SerializeField] private Color normalColor = Color.white;

	private Button mobileBossButton;
	private Button mobileTotalButton;
	private Button desktopBossButton;
	private Button desktopTotalButton;

	void Awake()
	{
		FindButtons();
		WireButtons();
	}

	void Start()
	{
		Refresh();
	}

	public void FindButtons()
	{
		mobileBossButton = null;
		mobileTotalButton = null;
		desktopBossButton = null;
		desktopTotalButton = null;

		Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < allButtons.Length; i++)
		{
			Button button = allButtons[i];
			string name = button.gameObject.name;
			if (name != "BossOfToilet" && name != "TotalCleaning")
				continue;

			Canvas parentCanvas = button.GetComponentInParent<Canvas>();
			bool isMobile = parentCanvas != null && parentCanvas.name.Contains("Mobile");

			if (name == "BossOfToilet")
			{
				if (isMobile)
					mobileBossButton = button;
				else
					desktopBossButton = button;
			}
			else
			{
				if (isMobile)
					mobileTotalButton = button;
				else
					desktopTotalButton = button;
			}
		}
	}

	public void WireButtons()
	{
		BindModeButton(mobileBossButton, ModeManager.Mode.Boss);
		BindModeButton(desktopBossButton, ModeManager.Mode.Boss);
		BindModeButton(mobileTotalButton, ModeManager.Mode.TotalCleaning);
		BindModeButton(desktopTotalButton, ModeManager.Mode.TotalCleaning);
	}

	private void BindModeButton(Button button, ModeManager.Mode mode)
	{
		if (button == null)
			return;

		button.enabled = true;
		button.interactable = true;
		button.onClick.RemoveAllListeners();
		button.onClick.AddListener(() => OnModeButtonClicked(mode));
		DisableChildRaycasts(button);
	}

	private static void DisableChildRaycasts(Button button)
	{
		Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
		Graphic targetGraphic = button.targetGraphic;
		for (int i = 0; i < graphics.Length; i++)
		{
			Graphic graphic = graphics[i];
			if (graphic == null || graphic == targetGraphic)
				continue;

			graphic.raycastTarget = false;
		}
	}

	private static void OnModeButtonClicked(ModeManager.Mode mode)
	{
		if (GameController.Instance != null)
			GameController.Instance.ChangeMode((int)mode);
	}

	public void Refresh()
	{
		bool isBoss = YG2.saves.chosenMode == (int)ModeManager.Mode.Boss;

		ApplyButtonState(mobileBossButton, isBoss);
		ApplyButtonState(desktopBossButton, isBoss);
		ApplyButtonState(mobileTotalButton, !isBoss);
		ApplyButtonState(desktopTotalButton, !isBoss);
	}

	private void ApplyButtonState(Button button, bool selected)
	{
		if (button == null)
			return;

		button.interactable = true;
		button.enabled = true;
		Image image = button.GetComponent<Image>();
		if (image != null)
			image.color = selected ? selectedColor : normalColor;
	}
}
