using UnityEngine;
using UnityEngine.UI;
using YG;

public class LanguageManager : MonoBehaviour
{
	public static LanguageManager Instance;
	public Sprite isRus;
	public Sprite isEng;
	public Button Mflag;
	public Button Dflag;
	public Image Mimage;
	public Image Dimage;

	private void Awake()
	{
		Instance = this;
		Mflag.onClick.AddListener(Onclick);
		Dflag.onClick.AddListener(Onclick);
	}

	void Start()
	{
		if (!YG2.saves.languageChosenByPlayer)
			ApplyEnvironmentLanguage();
		else
			YG2.SwitchLanguage(YG2.saves.langRu ? "ru" : "en");
		RefreshUI();
	}

	public static void ApplyEnvironmentLanguage()
	{
		YG2.SwitchLanguage(YG2.envir.language);
		YG2.saves.langRu = YG2.envir.language == "ru";
		YG2.saves.done = true;
		YG2.SaveProgress();
	}

	public void Onclick()
	{
		YG2.saves.langRu = !YG2.saves.langRu;
		YG2.saves.languageChosenByPlayer = true;
		YG2.SwitchLanguage(YG2.saves.langRu ? "ru" : "en");
		YG2.SaveProgress();
		RefreshUI();
	}

	public void RefreshUI()
	{
		if (Mimage != null)
			Mimage.sprite = YG2.saves.langRu ? isRus : isEng;
		if (Dimage != null)
			Dimage.sprite = YG2.saves.langRu ? isRus : isEng;
		if (GameController.Instance != null)
			GameController.Instance.UpdateAllUI();
	}
}