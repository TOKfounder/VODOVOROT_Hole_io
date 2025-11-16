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
			CanvasForMobile.SetActive(true);
			currentCanvas = CanvasForMobile.GetComponent<Canvas>();
		}
		else
		{
			CanvasForDesktop.SetActive(true);
			CanvasForMobile?.SetActive(false);
			currentCanvas = CanvasForDesktop.GetComponent<Canvas>();
		}
	}
	

	void Start()
	{
		// YG2.saves.isFirst = true;
		Time.timeScale = 1f;
		// Для обнуления
		if (YG2.saves.isFirst)
		{
			YG2.saves.soundValue = 0.5f;
			YG2.saves.musicValue = 0.5f;
			YG2.saves.langRu = true;
			YG2.saves.score = 0;
			YG2.saves.equipedMaterial = 0;
			YG2.saves.massiveOfObtaining = new int[] { 1, 0, 0, 0, 0 };
			YG2.saves.exp = 0;
			YG2.saves.isFirst = false;
			YG2.saves.levelOfProgress = 1;
			YG2.saves.goldCoins = 0;
			YG2.saves.chosenMode = 0;
			YG2.saves.diamonds = 3;
			YG2.saves.selectedMapID = 0;
		}
		YG2.SaveProgress();
		ChangeMain(YG2.saves.equipedMaterial);
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
		YG2.saves.isGaming = true;
		YG2.SaveProgress();
		SceneManager.LoadScene(YG2.saves.selectedMapID + 1);
	}

	public void ReturnToMenu()
	{
		YG2.saves.isGaming = false;
		YG2.SaveProgress();
		YG2.InterstitialAdvShow();
		SceneManager.LoadScene(0);
	}

	public void ChangeMain(int chosenObj)
	{
		// if (YG2.saves.equipedMaterial == chosenObj) return;
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
		YG2.SaveProgress();
	}

	public void UpdateAllUI()
	{
		Debug.Log("UpdateUI");
		if (LanguageManager.Instance.Adecvat)
		{
			YG2.SwitchLanguage(YG2.envir.language);
			YG2.saves.langRu = YG2.envir.language == "ru" ? true : false;
		}
		else
		{
			YG2.SwitchLanguage(YG2.envir.language == "ru" ? "en" : "ru");
			YG2.saves.langRu = YG2.envir.language == "ru" ? false : true;
		}


		if (!YG2.saves.isGaming)
		{
			MainMenuController.Instance.UpdateMainMenu();
		}
		else
		{
			GamingManager.Instance.UpdateUI();
		}
			// Переключение иконки языка
			LanguageManager.Instance.Mimage.sprite = YG2.saves.langRu ?
			LanguageManager.Instance.isRus : LanguageManager.Instance.isEng;
			LanguageManager.Instance.Dimage.sprite = YG2.saves.langRu ?
			LanguageManager.Instance.isRus : LanguageManager.Instance.isEng;
	}
}
