using UnityEngine;
using UnityEngine.UI;
using YG;

public class LanguageManager : MonoBehaviour
{
	public static bool done = false;
	public Sprite isRus;
	public Sprite isEng;
	public Button Mflag;
	public Button Dflag;
	public bool Adecvat = true;
	public Image Mimage;
	public Image Dimage;

	private void Awake()
	{
		if (VodovorotGameManager.Instance != null)
    		VodovorotGameManager.Instance.LanguageManager = this;
		
		Mflag.onClick.AddListener(Onclick);
		Dflag.onClick.AddListener(Onclick);
    	// DontDestroyOnLoad(gameObject);
	}

    void Start()
    {
        bool firstLanguageSelection = !YG2.saves.done;
        if (firstLanguageSelection)
        {
            YG2.saves.langRu = YG2.envir.language == "ru";
            YG2.saves.done = true;
        }

        // A scene change must restore the saved choice, not click the toggle twice
        // and silently return to the browser language.
        Adecvat = YG2.saves.langRu == (YG2.envir.language == "ru");
        VodovorotGameManager.Instance.GameController.UpdateAllUI();
        if (firstLanguageSelection) YG2.SaveProgress();
    }
	public void Onclick()
	{
		Adecvat = !Adecvat;
		VodovorotGameManager.Instance.GameController.UpdateAllUI();
		YG2.SaveProgress();
	}
}
