using UnityEngine;
using UnityEngine.UI;
using YG;

public class HorizontalLayout3D : MonoBehaviour
{
	public static HorizontalLayout3D Instance;
	public float radius = 120f;
	[SerializeField] private float startAngle = 50f;
	[SerializeField] private Camera targetCamera;
	public GameObject[] captions;
	[SerializeField] private Text feature;
	[SerializeField] private GameObject buttonOfBuying;
	[SerializeField] private GameObject buttonOfEquiping;
	[SerializeField] private Text necessaryLevel;
	[SerializeField] private Text costForCoins;
	[SerializeField] private Text costForDonate;
	[SerializeField] private Button donateButton;
	[SerializeField] private Image currencyImage;

	private string[] toiletIDs = {"obodok", "white", "gold", "scrag", "lord" };
	private float initialAngle;
	private float targetAngle;
	private float timeElapsed;
	private bool isRotating = false;

	private int chosenObj = 0;

	public int ChosenIndex => chosenObj;

	private int[] necessaryLevels = { 0, 1, 2, 4, 6 };
	private int[] costsForCoins = { 0, 20, 150, 400, 900 };
	private int[] costsForDonate = { 0, 10000, 10, 40, 100 };

	void Awake()
	{
		Instance = this;
		ApplySkinCatalog();
	}

	void ApplySkinCatalog()
	{
		SkinCatalog catalog = GameBalance.Skins;
		if (catalog == null || catalog.skins == null || catalog.skins.Length == 0)
			return;

		int n = catalog.skins.Length;
		necessaryLevels = new int[n];
		costsForCoins = new int[n];
		costsForDonate = new int[n];
		for (int i = 0; i < n; i++)
		{
			SkinDef def = catalog.skins[i];
			if (def == null)
				continue;
			necessaryLevels[i] = def.necessaryLevel;
			costsForCoins[i] = def.costCoins;
			costsForDonate[i] = def.costDonate;
		}
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
		if (!IsSkinUnlocked(chosenObj))
			return;
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
		int count = Mathf.Max(1, transform.childCount);
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
		if (necessaryLevels != null && necessaryLevels.Length > 0)
			chosenObj = Mathf.Clamp(chosenObj, 0, necessaryLevels.Length - 1);
		if (YG2.saves.massiveOfObtaining == null || chosenObj >= YG2.saves.massiveOfObtaining.Length)
			return;
		if (YG2.saves.massiveOfObtaining[chosenObj] == 0)
		{
			buttonOfBuying.SetActive(true);
			buttonOfEquiping.SetActive(false);
			bool unlocked = IsSkinUnlocked(chosenObj);
			necessaryLevel.text = YG2.saves.langRu
				? (unlocked ? $"{necessaryLevels[chosenObj]} уровень" : $"Нужен {necessaryLevels[chosenObj]} уровень")
				: (unlocked ? $"{necessaryLevels[chosenObj]} level" : $"Need level {necessaryLevels[chosenObj]}");
			costForCoins.text = $"{costsForCoins[chosenObj]}";
			SetBuyInteractable(unlocked);
			if (chosenObj == 1)
			{
				costForDonate.text = "";
				donateButton.interactable = false;
				currencyImage.color = new Color(1, 1, 1, 0);
			}
			else
			{
				costForDonate.text = $"{costsForDonate[chosenObj]}";
				donateButton.interactable = unlocked;
				currencyImage.color = unlocked ? Color.white : new Color(1, 1, 1, 0.35f);
			}
		}
		else
		{
			buttonOfBuying.SetActive(false);
			buttonOfEquiping.SetActive(true);
			if (chosenObj == YG2.saves.equipedMaterial)
			{
				buttonOfEquiping.GetComponent<Image>().color = new Color32(50, 101, 182, 255);
				buttonOfEquiping.GetComponentInChildren<Text>().text = GameTexts.Equipped;
			}
			else
			{
				buttonOfEquiping.GetComponent<Image>().color = new Color32(120, 182, 50, 255);
				buttonOfEquiping.GetComponentInChildren<Text>().text = GameTexts.Equip;
			}
		}
		if (feature != null)
		{
			feature.horizontalOverflow = HorizontalWrapMode.Wrap;
			feature.verticalOverflow = VerticalWrapMode.Overflow;
			feature.resizeTextForBestFit = true;
			feature.resizeTextMinSize = 12;
			int maxSize = feature.fontSize > 0 ? feature.fontSize : 24;
			feature.resizeTextMaxSize = maxSize;
			feature.text = GameTexts.SkinFeature(chosenObj);
		}
	}

	private bool IsSkinUnlocked(int index)
	{
		if (index < 0 || index >= necessaryLevels.Length)
			return false;
		if (index == 1 && TutorialController.AllowsWhiteFriendBuy)
			return true;
		return YG2.saves.levelOfProgress >= necessaryLevels[index];
	}

	private void SetBuyInteractable(bool unlocked)
	{
		if (buttonOfBuying == null)
			return;
		Button[] buttons = buttonOfBuying.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			Button btn = buttons[i];
			if (btn == null || btn == donateButton)
				continue;
			if (btn.gameObject.name == "Achivements")
			{
				btn.interactable = false;
				continue;
			}
			btn.interactable = unlocked;
		}
	}

	public void BuyForSomething(int id)
	{
		if (id != 2)
		{
			if (MainMenuController.Instance != null)
				MainMenuController.Instance.fart.Play();
			return;
		}

		BuyCurrentForCoins();
	}

	public void BuyCurrentForCoins()
	{
		if (!IsSkinUnlocked(chosenObj))
		{
			if (MainMenuController.Instance != null)
				MainMenuController.Instance.fart.Play();
			return;
		}

		int cost = chosenObj >= 0 && chosenObj < costsForCoins.Length ? costsForCoins[chosenObj] : 0;
		if (YG2.saves.goldCoins < cost)
		{
			if (MainMenuController.Instance != null)
				MainMenuController.Instance.fart.Play();
			return;
		}

		YG2.saves.goldCoins -= cost;
		if (YG2.saves.massiveOfObtaining != null && chosenObj < YG2.saves.massiveOfObtaining.Length)
			YG2.saves.massiveOfObtaining[chosenObj] = 1;
		YG2.SaveProgress();
		UpdateForChosen();
		MainMenuController.Instance?.UpdateTriggers();
	}
	
	public void EquipMaterial()
	{
		GameController.Instance.ChangeMain(chosenObj);
		UpdateForChosen();
	}
}
