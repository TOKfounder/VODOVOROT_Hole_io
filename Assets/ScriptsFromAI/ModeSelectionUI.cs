using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class ModeSelectionUI : MonoBehaviour
{
	[SerializeField] private Color selectedColor = new Color(0.35f, 0.85f, 0.45f, 1f);
	[SerializeField] private Color normalColor = new Color(0.223f, 0.223f, 0.223f, 1f);
	[SerializeField] private Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);

	private Button mobileBossButton;
	private Button mobileTotalButton;
	private Button mobileHuntingButton;
	private Button mobileTeamButton;
	private Button mobileCityButton;
	private Button mobileGardenButton;

	private Button desktopBossButton;
	private Button desktopTotalButton;
	private Button desktopHuntingButton;
	private Button desktopTeamButton;
	private Button desktopCityButton;
	private Button desktopGardenButton;

	void Awake()
	{
		FindButtons();
		WireButtons();
	}

	void OnEnable()
	{
		Refresh();
	}

	void Start()
	{
		Refresh();
	}

	public void RefreshNextFrame()
	{
		StopAllCoroutines();
		StartCoroutine(RefreshAtEndOfFrame());
	}

	private IEnumerator RefreshAtEndOfFrame()
	{
		yield return new WaitForEndOfFrame();
		Refresh();
	}

	public void FindButtons()
	{
		mobileBossButton = null;
		mobileTotalButton = null;
		mobileHuntingButton = null;
		mobileTeamButton = null;
		mobileCityButton = null;
		mobileGardenButton = null;
		desktopBossButton = null;
		desktopTotalButton = null;
		desktopHuntingButton = null;
		desktopTeamButton = null;
		desktopCityButton = null;
		desktopGardenButton = null;

		Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < allButtons.Length; i++)
		{
			Button button = allButtons[i];
			string name = button.gameObject.name;
			bool isMobile = IsMobileButton(button);

			switch (name)
			{
				case "BossOfToilet":
					Assign(ref mobileBossButton, ref desktopBossButton, button, isMobile);
					break;
				case "TotalCleaning":
					Assign(ref mobileTotalButton, ref desktopTotalButton, button, isMobile);
					break;
				case "Hunting":
					Assign(ref mobileHuntingButton, ref desktopHuntingButton, button, isMobile);
					break;
				case "TeamPlay":
					Assign(ref mobileTeamButton, ref desktopTeamButton, button, isMobile);
					break;
				case "City":
					Assign(ref mobileCityButton, ref desktopCityButton, button, isMobile);
					break;
				case "Garden":
					Assign(ref mobileGardenButton, ref desktopGardenButton, button, isMobile);
					break;
			}
		}
	}

	private static bool IsMobileButton(Button button)
	{
		Canvas parentCanvas = button.GetComponentInParent<Canvas>(true);
		return parentCanvas != null && parentCanvas.name.Contains("Mobile");
	}

	private static void Assign(ref Button mobile, ref Button desktop, Button button, bool isMobile)
	{
		if (isMobile)
			mobile = button;
		else
			desktop = button;
	}

	public void WireButtons()
	{
		BindModeButton(mobileBossButton, ModeManager.Mode.Boss);
		BindModeButton(desktopBossButton, ModeManager.Mode.Boss);
		BindModeButton(mobileTotalButton, ModeManager.Mode.TotalCleaning);
		BindModeButton(desktopTotalButton, ModeManager.Mode.TotalCleaning);
		BindModeButton(mobileHuntingButton, ModeManager.Mode.Hunting);
		BindModeButton(desktopHuntingButton, ModeManager.Mode.Hunting);
		BindModeButton(mobileTeamButton, ModeManager.Mode.TeamMode);
		BindModeButton(desktopTeamButton, ModeManager.Mode.TeamMode);
		HideComingSoonOverlay(mobileHuntingButton);
		HideComingSoonOverlay(desktopHuntingButton);
		HideComingSoonOverlay(mobileTeamButton);
		HideComingSoonOverlay(desktopTeamButton);
		BindMapButton(mobileCityButton, 0);
		BindMapButton(desktopCityButton, 0);
		BindMapButton(mobileGardenButton, 1);
		BindMapButton(desktopGardenButton, 1);
		HookModePanel(mobileBossButton);
		HookModePanel(desktopBossButton);
		HookModePanel(mobileTotalButton);
		HookModePanel(desktopTotalButton);
	}

	private void BindModeButton(Button button, ModeManager.Mode mode)
	{
		if (button == null)
			return;

		button.enabled = true;
		button.interactable = true;
		button.onClick = new Button.ButtonClickedEvent();
		button.onClick.AddListener(() =>
		{
			PlayMenuClick();
			OnModeButtonClicked(mode);
			CloseNamedPanel(button, "PanelOfModes");
		});
		DisableChildRaycasts(button);
	}

	private void BindMapButton(Button button, int mapId)
	{
		if (button == null)
			return;

		button.enabled = true;
		button.interactable = true;
		button.onClick = new Button.ButtonClickedEvent();
		button.onClick.AddListener(() =>
		{
			PlayMenuClick();
			if (MainMenuController.Instance != null)
				MainMenuController.Instance.UpdateMapOnBackground(mapId);
			CloseNamedPanel(button, "PanelOfMaps");
		});
		DisableChildRaycasts(button);
	}

	private static void HideComingSoonOverlay(Button button)
	{
		if (button == null)
			return;

		Text[] texts = button.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < texts.Length; i++)
		{
			if (texts[i] == null)
				continue;

			string value = texts[i].text ?? "";
			if (value.IndexOf("Скоро", System.StringComparison.OrdinalIgnoreCase) >= 0
				|| value.IndexOf("Coming soon", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				texts[i].gameObject.SetActive(false);
			}
		}

		Image[] images = button.GetComponentsInChildren<Image>(true);
		Graphic targetGraphic = button.targetGraphic;
		for (int i = 0; i < images.Length; i++)
		{
			Image image = images[i];
			if (image == null || image == targetGraphic)
				continue;

			Color color = image.color;
			if (color.a >= 0.4f && color.r <= 0.2f && color.g <= 0.2f && color.b <= 0.2f)
				image.gameObject.SetActive(false);
		}
	}

	private static void CloseNamedPanel(Button button, string panelName)
	{
		Transform current = button.transform;
		while (current != null)
		{
			if (current.name == panelName)
			{
				current.gameObject.SetActive(false);
				return;
			}
			current = current.parent;
		}
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
		GameController.NormalizeChosenMode();
		int chosenMode = YG2.saves.chosenMode;

		ApplyModeCard(mobileBossButton, chosenMode == (int)ModeManager.Mode.Boss);
		ApplyModeCard(desktopBossButton, chosenMode == (int)ModeManager.Mode.Boss);
		ApplyModeCard(mobileTotalButton, chosenMode == (int)ModeManager.Mode.TotalCleaning);
		ApplyModeCard(desktopTotalButton, chosenMode == (int)ModeManager.Mode.TotalCleaning);
		ApplyModeCard(mobileHuntingButton, chosenMode == (int)ModeManager.Mode.Hunting);
		ApplyModeCard(desktopHuntingButton, chosenMode == (int)ModeManager.Mode.Hunting);
		ApplyModeCard(mobileTeamButton, chosenMode == (int)ModeManager.Mode.TeamMode);
		ApplyModeCard(desktopTeamButton, chosenMode == (int)ModeManager.Mode.TeamMode);

		ClearMapCardTint(mobileCityButton);
		ClearMapCardTint(desktopCityButton);
		ClearMapCardTint(mobileGardenButton);
		ClearMapCardTint(desktopGardenButton);
	}

	private void ApplyModeCard(Button button, bool selected)
	{
		if (button == null)
			return;

		button.interactable = true;
		button.enabled = true;
		button.transition = Selectable.Transition.None;
		Color face = selected ? selectedColor : normalColor;
		Image image = button.targetGraphic as Image;
		if (image == null)
			image = button.GetComponent<Image>();
		if (image != null)
			image.color = face;

		ColorBlock colors = button.colors;
		colors.normalColor = face;
		colors.highlightedColor = face;
		colors.pressedColor = face;
		colors.selectedColor = face;
		colors.disabledColor = normalColor;
		button.colors = colors;
	}

	private static void ClearMapCardTint(Button button)
	{
		if (button == null)
			return;

		Image image = button.targetGraphic as Image;
		if (image == null)
			image = button.GetComponent<Image>();
		if (image != null)
			image.color = Color.white;

		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = Color.white;
		colors.pressedColor = Color.white;
		colors.selectedColor = Color.white;
		colors.disabledColor = Color.white;
		button.colors = colors;
	}

	private void HookModePanel(Button button)
	{
		Transform current = button != null ? button.transform : null;
		while (current != null)
		{
			if (current.name == "PanelOfModes")
			{
				ModePanelRefreshHook hook = current.GetComponent<ModePanelRefreshHook>();
				if (hook == null)
					hook = current.gameObject.AddComponent<ModePanelRefreshHook>();
				hook.owner = this;
				RefreshNextFrame();
				return;
			}
			current = current.parent;
		}
	}

	private static void PlayMenuClick()
	{
		if (MainMenuController.Instance != null && MainMenuController.Instance.dzyn != null)
			MainMenuController.Instance.dzyn.Play();
	}
}

public class ModePanelRefreshHook : MonoBehaviour
{
	public ModeSelectionUI owner;

	void OnEnable()
	{
		if (owner != null)
			owner.RefreshNextFrame();
	}
}
