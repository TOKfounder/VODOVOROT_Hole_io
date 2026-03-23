using UnityEngine;
using UnityEngine.UI;
using YG;

public class HorizontalLayout3D : MonoBehaviour
{
	public static HorizontalLayout3D Instance;
	public float radius = 120f; // радиус круга
	public float startAngle = 50f;
	public Camera targetCamera;
	public GameObject[] captions;
	public Text feature;
	public GameObject buttonOfBuying;
	public GameObject buttonOfEquiping;
	public Text necessaryLevel;
	public Text costForCoins;
	public Text costForDonate;
	public Button donateButton;
	public Image currencyImage;

	private string[] toiletIDs = {"obodok", "white", "gold", "scrag", "lord" };
	private float initialAngle;
	private float targetAngle;
	private float timeElapsed;
	private bool isRotating = false;

	private int chosenObj = 0;
	private string[] features = { "Этот красный тазик – для тех, кто любит жить на скорости! Бросай вызов привычному, залетай в тазик!",
	 "Этот унитаз готов поддержать тебя в любой трудной и странной ситуации!",
	 "Блеск роскоши для истинных чемпионов! Стань королём туалетных побед.",
	 "Сиди с комфортом и властвуй! Злые силы не пройдут через эту дыру...",
	 "На этом троне даже проблемы исчезают! Почувствуй себя властелином стока." };
	private string[] featuresEn = { "This red basin is for those who like to live at speed! Challenge the familiar, fly into the basin!",
	 "This toilet bowl is ready to support you in any difficult and strange situation!",
	 "The splendor of luxury for true champions! Become the king of toilet victories.",
	 "Sit comfortably and rule! Evil forces will not pass through this hole...",
	 "On this throne, even problems disappear! Feel like the lord of the drain." };

	private int[] necessaryLevels = {0, 1, 4, 7, 10 };
	private int[] costsForCoins = { 0, 20, 270, 800, 2400 };
	private int[] costsForDonate = { 0, 10000, 10, 40, 100 };

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		targetCamera = Camera.main;
		ArrangeChildren();
		UpdateForChosen();
		donateButton.onClick.AddListener(BuyCurrentItem);
	}

	void OnEnable()
	{
		UpdateForChosen();
	}
	
	void BuyCurrentItem()
	{
		YG2.BuyPayments(toiletIDs[chosenObj]);
	}

	public void ArrangeChildren()
	{
		int count = transform.childCount;
		float angleStep = 360f / count; // угол между объектами

		for (int i = 0; i < count; i++)
		{
			float angle = startAngle + i * angleStep;
			float rad = angle * Mathf.Deg2Rad;
			Vector3 pos = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
			Transform child = transform.GetChild(i);
			child.localPosition = pos;
			// Повернуть child лицом к камере
			Vector3 lookPos = targetCamera.transform.position;
			lookPos.y = child.position.y; // чтобы не крутились по высоте
			lookPos.y = 37.7f;
			child.LookAt(lookPos);
		}
		foreach (var caption in captions)
		{
			Vector3 direction = caption.transform.position - targetCamera.transform.position;
			caption.transform.rotation = Quaternion.LookRotation(direction);

			// Vector3 euler = caption.transform.eulerAngles;
			// euler.z = 0f;
			// caption.transform.eulerAngles = euler;
		}
	}

	public void RotateOnDeg(bool right)
	{
		if (isRotating) return;
		initialAngle = startAngle;
		// int count = transform.childCount;
		int count = 5;
		float val = 360f / count;
		float angle = right ? -val : val;
		chosenObj = right ? chosenObj + 1 : chosenObj + count - 1;
		chosenObj %= count;
		targetAngle = startAngle + angle;
		timeElapsed = 0f;
		isRotating = true;
	}

	void Update()
	{
		if (isRotating)
		{
			timeElapsed += Time.deltaTime;
			float t = Mathf.Clamp01(timeElapsed / 1f);
			startAngle = Mathf.LerpAngle(initialAngle, targetAngle, t);
			ArrangeChildren();

			if (t >= 1f)
			{
				isRotating = false;
				startAngle = (targetAngle + 360) % 360;
				UpdateForChosen();
			}
		}
	}
	public void UpdateForChosen()
	{
		Debug.Log(chosenObj);
		if (YG2.saves.massiveOfObtaining[chosenObj] == 0)
		{
			buttonOfBuying.SetActive(true);
			buttonOfEquiping.SetActive(false);
			necessaryLevel.text = YG2.saves.langRu ? $"{necessaryLevels[chosenObj]} уровень" : $"{necessaryLevels[chosenObj]} level";
			costForCoins.text = $"{costsForCoins[chosenObj]}";
			if (chosenObj == 1)
			{
				costForDonate.text = "";
				donateButton.interactable = false;
				currencyImage.color = new Color(1, 1, 1, 0);
			}
			else
			{
				costForDonate.text = $"{costsForDonate[chosenObj]}";
				donateButton.interactable = true;
				currencyImage.color = new Color(1, 1, 1, 1);
			}
		}
		else
		{
			buttonOfBuying.SetActive(false);
			buttonOfEquiping.SetActive(true);
			if (chosenObj == YG2.saves.equipedMaterial)
			{
				buttonOfEquiping.GetComponent<Image>().color = new Color32(50, 101, 182, 255);
				buttonOfEquiping.GetComponentInChildren<Text>().text = YG2.saves.langRu ? "Надето" : "equipped";
			}
			else
			{
				buttonOfEquiping.GetComponent<Image>().color = new Color32(120, 182, 50, 255);
				buttonOfEquiping.GetComponentInChildren<Text>().text = YG2.saves.langRu ? "Одеть" : "equip";
			}
		}
		feature.text = YG2.saves.langRu ? features[chosenObj] : featuresEn[chosenObj];
	}

	public void BuyForSomething(int id)
	{
		bool isBought = false;
		if (id == 1)
		{
			//проверка уровня
			if (YG2.saves.levelOfProgress >= necessaryLevels[chosenObj])
			{
				isBought = true;
				MainMenuController.Instance.dzyn.Play();
			}
			else
				MainMenuController.Instance.fart.Play();
		}
		else if (id == 2)
		{
			//проверка ресурсов
			if (YG2.saves.goldCoins >= costsForCoins[chosenObj])
			{
				isBought = true;
				YG2.saves.goldCoins -= costsForCoins[chosenObj];
				MainMenuController.Instance.dzyn.Play();
			}
			else
				MainMenuController.Instance.fart.Play();
		}
		else if (id == 3)
		{
			//покупка за яны
			Debug.Log("Сработала обработка Инапа");
			isBought = true;
		}
		if (isBought)
			YG2.saves.massiveOfObtaining[chosenObj] = 1;
		YG2.SaveProgress();
		UpdateForChosen();
		MainMenuController.Instance.UpdateTriggers();
	}
	
	public void EquipMaterial()
	{
		GameController.Instance.ChangeMain(chosenObj);
		UpdateForChosen();
	}
}
