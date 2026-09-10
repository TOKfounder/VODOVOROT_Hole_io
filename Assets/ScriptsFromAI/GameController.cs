using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using YG;

public class GameController : MonoBehaviour
{
	public static GameController Instance;
	public Material[] materials;

	public GameObject mainToilet;
	public GameObject mainObodok;

	public GameObject CanvasForDesktop;
	public GameObject CanvasForMobile;

[HideInInspector]
	public Canvas currentCanvas;
	//Сделать его YG2
	// public Camera renderCamra;           // Камера с Render Texture
	// public RenderTexture renderTexture;   // Твой Render Texturee

	void Awake()
	{
		YG2.StickyAdActivity(true);
		Instance = this;
		if (CanvasForDesktop == null)
			CanvasForDesktop = GameObject.Find("CanvasForDesktop");
		if (CanvasForMobile == null)
			CanvasForMobile = GameObject.Find("CanvasForMobile");
		if (YG2.envir.isMobile)
		{
			CanvasForDesktop?.SetActive(false);
			if (CanvasForMobile != null)
			{
				CanvasForMobile.SetActive(true);
				currentCanvas = CanvasForMobile.GetComponent<Canvas>();
			}
		}
		else
		{
			if (CanvasForDesktop != null)
			{
				CanvasForDesktop.SetActive(true);
				currentCanvas = CanvasForDesktop.GetComponent<Canvas>();
			}
			CanvasForMobile?.SetActive(false);
		}

		if (SceneManager.GetActiveScene().buildIndex == 0)
			EnsureModeSelectionUI();
		ApplyMenuCanvasSorting();
	}

	private void ApplyMenuCanvasSorting()
	{
		ApplyUiCanvasOrder(CanvasForDesktop, 20);
		ApplyUiCanvasOrder(CanvasForMobile, 20);
		if (SceneManager.GetActiveScene().buildIndex != 0)
			return;

		GameObject mapGo = GameObject.Find("CanvasOfMap");
		if (mapGo == null)
			return;

		Canvas mapCanvas = mapGo.GetComponent<Canvas>();
		if (mapCanvas == null)
			return;

		mapCanvas.overrideSorting = true;
		mapCanvas.sortingOrder = -20;
	}

	private static void ApplyUiCanvasOrder(GameObject canvasGo, int order)
	{
		if (canvasGo == null)
			return;
		Canvas canvas = canvasGo.GetComponent<Canvas>();
		if (canvas == null)
			return;
		canvas.overrideSorting = true;
		canvas.sortingOrder = order;
	}

	private void EnsureModeSelectionUI()
	{
		if (GetComponent<ModeSelectionUI>() == null)
			gameObject.AddComponent<ModeSelectionUI>();
	}
	

	void Start()
	{
		Time.timeScale = 1f;
		if (YG2.saves.isFirst)
		{
			YG2.saves.soundValue = 0.5f;
			YG2.saves.musicValue = 0.5f;
			YG2.saves.score = 0;
			YG2.saves.equipedMaterial = 0;
			YG2.saves.massiveOfObtaining = new int[] { 1, 0, 0, 0, 0 };
			YG2.saves.exp = 0;
			YG2.saves.isFirst = false;
			YG2.saves.levelOfProgress = 0;
			YG2.saves.goldCoins = 0;
			YG2.saves.chosenMode = 0;
			YG2.saves.diamonds = 3;
			YG2.saves.selectedMapID = 0;
			YG2.saves.rewardedHandLeft = 2;
			YG2.saves.rewardedBagLeft = 5;
			YG2.saves.rewardedBoxLeft = 10;
			YG2.GetLeaderboard("BestPlayers");
			YG2.SetLeaderboard("BestPlayers", YG2.saves.exp);
		}
		if( YG2.saves.massiveOfObtaining.Length < 5)	
		{
			int[] newArray = new int[5];
			for(int i = 0; i < YG2.saves.massiveOfObtaining.Length; i++)
			{
				newArray[i] = YG2.saves.massiveOfObtaining[i];
			}
			YG2.saves.massiveOfObtaining = newArray;
		}
		NormalizeChosenMode();
		TutorialController.MigrateSaves();
		YG2.SaveProgress();
		ChangeMain(YG2.saves.equipedMaterial);
		UiClickFeedback.EnsureOnScene();
		if (!YG2.saves.isGaming)
			RefreshModeSelectionUI();
		else
			ScorePopupZone.EnsureZone(ActiveCanvas.Get());
		// SaveScreenshot();
	}
	//---------------------------------------------------------------------------------------------------

	// public void SaveScreenshot(string path = "Assets/ScreenShot.png")
	// {
	//     // Назначить Render Texture камере
	//     renderCamera.targetTexture = renderTexture;

	//     // Включить Render Texture как активный
	//     RenderTexture currentRT = RenderTexture.active;
	//     RenderTexture.active = renderTexture;

	//     // Снять фото внутри Render Texture
	//     renderCamera.Render();
	//     Texture2D image = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
	//     image.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
	//     image.Apply();

	//     // Вернуть targetTexture и RenderTexture.active обратно
	//     renderCamera.targetTexture = null;
	//     RenderTexture.active = currentRT;

	//     // Сохранить файл
	//     System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

	//     // Не забудь очистить!
	//     DestroyImmediate(image);

	//     UnityEngine.Debug.Log("Скриншот сохранился по адресу " + path);
	// }
	//---------------------------------------------------------------------------------------------------
	public void StartGame()
	{
		if (!TutorialController.IsDone)
		{
			YG2.saves.chosenMode = (int)ModeManager.Mode.TotalCleaning;
			YG2.saves.selectedMapID = 0;
			TutorialController.NotifyPlayStarted();
		}
		NormalizeChosenMode();
		ModeManager.currentMode = (ModeManager.Mode)YG2.saves.chosenMode;
		YG2.saves.isGaming = true;
		YG2.SaveProgress();
		int sceneIndex = YG2.saves.selectedMapID + 1;
		int sceneCount = SceneManager.sceneCountInBuildSettings;
		if (sceneIndex <= 0 || sceneIndex >= sceneCount)
			sceneIndex = 1;
		SceneManager.LoadScene(sceneIndex);
	}

	public void ReturnToMenu()
	{
		bool skipAds = !TutorialController.IsDone;
		TutorialController.NotifyReturningToMenu();
		YG2.saves.isGaming = false;
		YG2.SaveProgress();
		if (!skipAds && !YG2.nowAdsShow)
			YG2.InterstitialAdvShow();
		SceneManager.LoadScene(0);
	}

	public void ChangeMain(int chosenObj)
	{
		if (chosenObj == 0)
		{
			mainToilet.SetActive(false);
			mainObodok.SetActive(true);
			mainObodok.GetComponent<Renderer>().material = materials[chosenObj];
		}
		else
		{
			mainToilet.SetActive(true);
			mainObodok.SetActive(false);
			mainToilet.GetComponent<Renderer>().material = materials[chosenObj];
		}
		YG2.saves.equipedMaterial = chosenObj;
		YG2.SaveProgress();
	}

	public void ChangeMode(int id)
	{
		YG2.saves.chosenMode = id;
		ModeManager.currentMode = (ModeManager.Mode)id;
		YG2.SaveProgress();
		RefreshModeSelectionUI();
	}

	public static void NormalizeChosenMode()
	{
		int mode = YG2.saves.chosenMode;
		if (mode < 0 || mode > (int)ModeManager.Mode.TeamMode)
			mode = (int)ModeManager.Mode.Boss;

		YG2.saves.chosenMode = mode;
		ModeManager.currentMode = (ModeManager.Mode)mode;
	}

	public void RefreshModeSelectionUI()
	{
		ModeSelectionUI ui = GetComponent<ModeSelectionUI>();
		if (ui == null)
			ui = gameObject.AddComponent<ModeSelectionUI>();
		ui.FindButtons();
		ui.WireButtons();
		ui.Refresh();
	}

	public void UpdateAllUI()
	{
		if (!YG2.saves.isGaming)
		{
			if (MainMenuController.Instance != null)
				MainMenuController.Instance.UpdateMainMenu();
		}
		else
		{
			if (GamingManager.Instance != null)
				GamingManager.Instance.UpdateUI();
		}
	}

	public bool IsMobileCanvasActive()
	{
		return CanvasForMobile != null && CanvasForMobile.activeInHierarchy;
	}

#if UNITY_EDITOR
	public void EditorResetTutorial()
	{
		YG2.saves.tutorialMenuSeen = false;
		YG2.saves.tutorialMatchSeen = false;
		YG2.saves.tutorialStage = 0;
		YG2.saves.isNickGiven = false;
		YG2.saves.nickName = "";
		YG2.saves.isGaming = false;
		YG2.saves.equipedMaterial = 0;
		YG2.saves.massiveOfObtaining = new int[] { 1, 0, 0, 0, 0 };
		YG2.saves.goldCoins = 0;
		YG2.saves.diamonds = 3;
		YG2.SaveProgress();
		Time.timeScale = 1f;
		SceneManager.LoadScene(0);
	}

	public void EditorShowMobileCanvas()
	{
		ApplyEditorCanvas(true);
	}

	public void EditorShowDesktopCanvas()
	{
		ApplyEditorCanvas(false);
	}

	private void ApplyEditorCanvas(bool mobile)
	{
		if (CanvasForMobile != null)
			CanvasForMobile.SetActive(mobile);
		if (CanvasForDesktop != null)
			CanvasForDesktop.SetActive(!mobile);

		GameObject active = mobile ? CanvasForMobile : CanvasForDesktop;
		currentCanvas = active != null ? active.GetComponent<Canvas>() : null;

		if (SceneManager.GetActiveScene().buildIndex == 0)
			TutorialController.EnsureMenu();
		else
			TutorialController.EnsureMatch();

		UpdateAllUI();
		LanguageManager language = FindAnyObjectByType<LanguageManager>();
		if (language != null)
			language.RefreshUI();
	}
#endif
}
