using UnityEngine;
using UnityEngine.UI;
using YG;

public class LanguageManager : MonoBehaviour
{
	public static LanguageManager Instance;
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
		Instance = this;
		Mflag.onClick.AddListener(Onclick);
		Dflag.onClick.AddListener(Onclick);
    DontDestroyOnLoad(gameObject);
	}

	void Start()
	{
		if (!YG2.saves.done)
		{
			YG2.SwitchLanguage(YG2.envir.language);
			YG2.saves.langRu = YG2.envir.language == "ru"? true : false;
			YG2.saves.done = true;
			YG2.SaveProgress();
		}
		Onclick();
		Onclick();
		// if (YG2.saves.langRu)
		// 	Onclick();
		// flag.onClick.Invoke();
	}
	public void Onclick()
	{
		Adecvat = !Adecvat;
		GameController.Instance.UpdateAllUI();
		YG2.SaveProgress();
	}
}
